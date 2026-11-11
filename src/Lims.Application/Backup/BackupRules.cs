using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lims.Application.Backup;

public enum BackupKind { Full, Differential, Log }

/// <summary>A backup file found in the backup folder (name only; the folder is never sent to the browser as a path per file).</summary>
public sealed record BackupFile(string Name, BackupKind Kind, DateTime StampLocal, long SizeBytes);

/// <summary>What one backup job last reported in backup-status.json. Times are the server's local time, as the scripts write them.</summary>
public sealed record JobStatus(string Name, DateTime? LastSuccessLocal, string? LastDetail, DateTime? LastFailureLocal, string? LastFailureDetail);

public sealed record BackupThresholds(int MaxFullAgeHours = 26, int MaxLogAgeMinutes = 45, int MaxOffsiteAgeHours = 26, bool ExpectLogBackups = true, bool ExpectOffsite = true);

/// <summary>A problem the page shows. <c>Code</c> is translated by the page; <c>Detail</c> is the job's own message when there is one.</summary>
public sealed record BackupProblem(string Code, string? Detail = null, string? When = null);

/// <summary>
/// Reads the files and status file written by the backup scripts (db/scripts) and decides whether backups are healthy.
/// The rules mirror Test-LimsBackupHealth.ps1 so the page and the scheduled health check agree.
/// </summary>
public static class BackupRules
{
    public const string StatusFileName = "backup-status.json";

    /// <summary>Parses &lt;db&gt;_&lt;yyyyMMdd_HHmmss&gt;[_FULL|_DIFF|_LOG].(bak|trn); returns null for any other file name.</summary>
    public static BackupFile? ParseFileName(string database, string fileName, long size)
    {
        var m = Regex.Match(fileName, $"^{Regex.Escape(database)}_(\\d{{8}})_(\\d{{6}})(?:_(FULL|DIFF|LOG))?\\.(bak|trn)$", RegexOptions.CultureInvariant);
        if (!m.Success) return null;
        var kind = !m.Groups[3].Success ? BackupKind.Full : m.Groups[3].Value switch { "DIFF" => BackupKind.Differential, "LOG" => BackupKind.Log, _ => BackupKind.Full };
        if ((kind == BackupKind.Log) != (m.Groups[4].Value == "trn")) return null;
        if (!DateTime.TryParseExact(m.Groups[1].Value + m.Groups[2].Value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp)) return null;
        return new BackupFile(fileName, kind, stamp, size);
    }

    /// <summary>Parses backup-status.json. Returns an empty list for blank or damaged content rather than failing the page.</summary>
    public static List<JobStatus> ParseStatus(string? json)
    {
        var list = new List<JobStatus>();
        if (string.IsNullOrWhiteSpace(json)) return list;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return list;
            foreach (var job in doc.RootElement.EnumerateObject())
            {
                if (job.Value.ValueKind != JsonValueKind.Object) continue;
                string? Str(string p) => job.Value.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                DateTime? Time(string p) => DateTime.TryParse(Str(p), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
                list.Add(new JobStatus(job.Name, Time("lastSuccess"), Str("lastDetail"), Time("lastFailure"), Str("lastFailureDetail")));
            }
        }
        catch (JsonException) { /* a half-written file: show what we can */ }
        return list;
    }

    /// <summary>The newest backup file that can be restored up to: the last log backup, or failing that the last full/differential.</summary>
    public static DateTime? RecoverableToLocal(IReadOnlyCollection<BackupFile> files) => files.Count == 0 ? null : files.Max(f => f.StampLocal);

    public static List<BackupProblem> Evaluate(DateTime nowLocal, IReadOnlyCollection<BackupFile> files, IReadOnlyCollection<JobStatus> status, bool statusFileFound, BackupThresholds t)
    {
        var problems = new List<BackupProblem>();

        var lastFull = files.Where(f => f.Kind == BackupKind.Full).DefaultIfEmpty().MaxBy(f => f?.StampLocal);
        if (lastFull is null) problems.Add(new BackupProblem("noFull"));
        else if (lastFull.StampLocal < nowLocal.AddHours(-t.MaxFullAgeHours)) problems.Add(new BackupProblem("fullOld", When: Iso(lastFull.StampLocal)));

        if (t.ExpectLogBackups)
        {
            var lastLog = files.Where(f => f.Kind == BackupKind.Log).DefaultIfEmpty().MaxBy(f => f?.StampLocal);
            if (lastLog is null) problems.Add(new BackupProblem("noLog"));
            else if (lastLog.StampLocal < nowLocal.AddMinutes(-t.MaxLogAgeMinutes)) problems.Add(new BackupProblem("logOld", When: Iso(lastLog.StampLocal)));
        }

        if (!statusFileFound) problems.Add(new BackupProblem("noStatusFile"));
        foreach (var job in status)
        {
            if (job.LastFailureLocal is { } failed && (job.LastSuccessLocal is null || failed > job.LastSuccessLocal))
                problems.Add(new BackupProblem("jobFailed:" + job.Name, job.LastFailureDetail, Iso(failed)));
        }

        if (t.ExpectOffsite)
        {
            var off = status.FirstOrDefault(s => s.Name == "Offsite");
            if (off is null) problems.Add(new BackupProblem("offsiteNever"));
            else if (off.LastSuccessLocal is null || off.LastSuccessLocal < nowLocal.AddHours(-t.MaxOffsiteAgeHours)) problems.Add(new BackupProblem("offsiteOld", When: off.LastSuccessLocal is { } d ? Iso(d) : null));
        }
        return problems;
    }

    private static string Iso(DateTime local) => local.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
}
