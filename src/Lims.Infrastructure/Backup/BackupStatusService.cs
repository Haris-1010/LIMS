using Lims.Application.Backup;
using Microsoft.Extensions.Configuration;

namespace Lims.Infrastructure.Backup;

/// <summary>
/// Read-only view of the backups made by the scripts in db/scripts. It reads the backup folder named in configuration
/// (<c>Backup:Folder</c>) and the backup-status.json the scripts keep there; it never writes, runs, or restores anything.
/// The application pool's identity needs Read access to that folder (docs/BACKUP_RESTORE.md).
/// </summary>
public sealed class BackupStatusService(IConfiguration config)
{
    private const int MaxFilesScanned = 20_000;
    private const int FilesShown = 60;

    private int Int(string key, int fallback) => int.TryParse(config[key], out var v) && v > 0 ? v : fallback;
    private bool Bool(string key, bool fallback) => bool.TryParse(config[key], out var v) ? v : fallback;

    public Task<BackupStatusDto> GetAsync(CancellationToken ct) => Task.Run(() => Build(DateTime.Now), ct);

    // Backup scripts write the server's local time; compare in local time, send UTC.
    private static DateTime Utc(DateTime local) => DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
    private static DateTime? Utc(DateTime? local) => local is null ? null : Utc(local.Value);

    internal BackupStatusDto Build(DateTime nowLocal)
    {
        var folder = config["Backup:Folder"];
        var database = config["Backup:Database"] is { Length: > 0 } d ? d : "LIMS";
        var thresholds = new BackupThresholds(
            Int("Backup:MaxFullAgeHours", 26), Int("Backup:MaxLogAgeMinutes", 45), Int("Backup:MaxOffsiteAgeHours", 26),
            Bool("Backup:ExpectLogBackups", true), Bool("Backup:ExpectOffsite", true));
        var emptySummary = new BackupSummaryDto(0, 0, null, null, null, null, thresholds.MaxFullAgeHours, thresholds.MaxLogAgeMinutes, thresholds.ExpectLogBackups, thresholds.ExpectOffsite);
        var checkedAt = Utc(nowLocal);

        if (string.IsNullOrWhiteSpace(folder))
            return new BackupStatusDto(false, false, null, database, checkedAt, false, [], [], [], emptySummary);

        List<BackupFile> files;
        string? statusJson = null;
        var statusFound = false;
        try
        {
            var dir = new DirectoryInfo(folder);
            if (!dir.Exists) throw new DirectoryNotFoundException();
            files = dir.EnumerateFiles().Take(MaxFilesScanned)
                .Select(f => BackupRules.ParseFileName(database, f.Name, f.Length)).Where(f => f is not null).Select(f => f!).ToList();
            var statusPath = Path.Combine(folder, BackupRules.StatusFileName);
            if (File.Exists(statusPath))
            {
                // The scripts may be rewriting the file at this moment: open it shared and read what is there.
                using var fs = new FileStream(statusPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(fs);
                statusJson = reader.ReadToEnd();
                statusFound = true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Folder missing or not readable by the application: say so instead of reporting an empty (and alarming) backup set.
            return new BackupStatusDto(true, false, folder, database, checkedAt, false, [new BackupProblemDto("folderUnreadable", null, null)], [], [], emptySummary);
        }

        var status = BackupRules.ParseStatus(statusJson);
        var problems = BackupRules.Evaluate(nowLocal, files, status, statusFound, thresholds);

        var fulls = files.Where(f => f.Kind == BackupKind.Full).ToList();
        var logs = files.Where(f => f.Kind == BackupKind.Log).ToList();
        var summary = new BackupSummaryDto(
            files.Count, files.Sum(f => f.SizeBytes),
            Utc(fulls.Count == 0 ? null : fulls.Min(f => f.StampLocal)), Utc(fulls.Count == 0 ? null : fulls.Max(f => f.StampLocal)),
            Utc(logs.Count == 0 ? null : logs.Max(f => f.StampLocal)), Utc(BackupRules.RecoverableToLocal(files)),
            thresholds.MaxFullAgeHours, thresholds.MaxLogAgeMinutes, thresholds.ExpectLogBackups, thresholds.ExpectOffsite);

        return new BackupStatusDto(
            true, true, folder, database, checkedAt, problems.Count == 0,
            problems.Select(p => new BackupProblemDto(p.Code, p.Detail, p.When is null ? null : Utc(DateTime.Parse(p.When, System.Globalization.CultureInfo.InvariantCulture)))).ToList(),
            status.Select(s => new BackupJobDto(s.Name, Utc(s.LastSuccessLocal), s.LastDetail, Utc(s.LastFailureLocal), s.LastFailureDetail)).OrderBy(j => j.Name).ToList(),
            files.OrderByDescending(f => f.StampLocal).Take(FilesShown).Select(f => new BackupFileDto(f.Name, f.Kind.ToString(), Utc(f.StampLocal), f.SizeBytes)).ToList(),
            summary);
    }
}
