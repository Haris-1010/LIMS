using System.Globalization;
using Lims.Application.Common;

namespace Lims.Application.Backup;

/// <summary>Jobs the in-application backup scheduler can run.</summary>
public enum BackupJob { Full, Log, Retention, Offsite }

/// <summary>What the scheduler knows when it decides what to run. Times are the server's local time.</summary>
public sealed record BackupState(
    DateTime? LastFull, DateTime? LastLog, DateTime? LastRetention, DateTime? LastOffsite,
    IReadOnlyDictionary<BackupJob, DateTime> LastFailure);

/// <summary>
/// Pure scheduling rules for the automatic backups (no clock, no I/O), so they can be unit tested.
/// A missed slot (server off, site stopped) is caught up as soon as the application runs again.
/// </summary>
public static class BackupSchedule
{
    /// <summary>After a failure a job waits this long before it is tried again, so a broken folder does not retry every minute.</summary>
    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan OffsiteEvery = TimeSpan.FromMinutes(15);

    public static void Validate(BackupSettingsRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Folder) || !Path.IsPathFullyQualified(r.Folder) || r.Folder.Length > 240)
            throw new BusinessRuleException("backup_folder_invalid", "Enter a full backup folder path, for example D:\\LimsBackups.");
        if (!string.IsNullOrWhiteSpace(r.OffsiteFolder))
        {
            if (!Path.IsPathFullyQualified(r.OffsiteFolder) || r.OffsiteFolder.Length > 240)
                throw new BusinessRuleException("backup_offsite_invalid", "Enter a full offsite folder path.");
            if (string.Equals(Path.GetFullPath(r.OffsiteFolder).TrimEnd('\\', '/'), Path.GetFullPath(r.Folder).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("backup_offsite_invalid", "The offsite folder must be different from the backup folder.");
        }
        if (r.FullFrequency is not ("Daily" or "Weekly"))
            throw new BusinessRuleException("backup_frequency_invalid", "Choose daily or weekly full backups.");
        if (!TimeOnly.TryParseExact(r.FullTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new BusinessRuleException("backup_time_invalid", "Enter a time in HH:mm format.");
        if (r.LogMinutes != 0 && (r.LogMinutes < 5 || r.LogMinutes > 60))
            throw new BusinessRuleException("backup_log_interval_invalid", "Log interval must be 0 (off) or between 5 and 60 minutes.");
        if (r.RetentionDays < 1 || r.RetentionDays > 365)
            throw new BusinessRuleException("backup_retention_invalid", "Retention must be between 1 and 365 days.");
    }

    /// <summary>The most recent moment, at or before <paramref name="now"/>, at which a full backup was due.</summary>
    public static DateTime LastFullSlot(DateTime now, BackupSettings s)
    {
        var time = TimeOnly.ParseExact(s.FullTime, "HH:mm", CultureInfo.InvariantCulture).ToTimeSpan();
        var slot = now.Date + time;
        if (s.FullFrequency == "Weekly")
        {
            // Weekly full backups run on Monday.
            var back = ((int)slot.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            slot = slot.AddDays(-back);
            if (slot > now) slot = slot.AddDays(-7);
        }
        else if (slot > now) slot = slot.AddDays(-1);
        return slot;
    }

    public static DateTime NextFull(DateTime now, BackupSettings s, DateTime? lastFull)
    {
        var last = LastFullSlot(now, s);
        if (lastFull is null || lastFull < last) return now;   // due now
        return last.AddDays(s.FullFrequency == "Weekly" ? 7 : 1);
    }

    /// <summary>Hours after which a full backup counts as late on the status page.</summary>
    public static int MaxFullAgeHours(BackupSettings s) => s.FullFrequency == "Weekly" ? 7 * 24 + 2 : 26;

    /// <summary>Minutes after which a log backup counts as late (three missed runs, at least 45 minutes).</summary>
    public static int MaxLogAgeMinutes(BackupSettings s) => Math.Max(45, s.LogMinutes * 3);

    public static List<BackupJob> DueJobs(DateTime now, BackupSettings s, BackupState state)
    {
        var due = new List<BackupJob>();
        if (!s.Enabled) return due;
        bool Waiting(BackupJob job, DateTime? lastSuccess) =>
            state.LastFailure.TryGetValue(job, out var failed) && (lastSuccess is null || failed > lastSuccess) && now - failed < RetryAfterFailure;

        var fullDue = state.LastFull is null || state.LastFull < LastFullSlot(now, s);
        if (fullDue && !Waiting(BackupJob.Full, state.LastFull)) due.Add(BackupJob.Full);

        // Log backups need a full backup to start from; a full that is due right now goes first.
        if (s.LogMinutes > 0 && state.LastFull is not null && !due.Contains(BackupJob.Full)
            && (state.LastLog is null || now - state.LastLog >= TimeSpan.FromMinutes(s.LogMinutes))
            && !Waiting(BackupJob.Log, state.LastLog))
            due.Add(BackupJob.Log);

        if (state.LastFull is not null && (state.LastRetention is null || state.LastRetention.Value.Date < now.Date)
            && !Waiting(BackupJob.Retention, state.LastRetention))
            due.Add(BackupJob.Retention);

        if (!string.IsNullOrWhiteSpace(s.OffsiteFolder) && state.LastFull is not null
            && (due.Contains(BackupJob.Full) || due.Contains(BackupJob.Log) || state.LastOffsite is null || now - state.LastOffsite >= OffsiteEvery)
            && !Waiting(BackupJob.Offsite, state.LastOffsite))
            due.Add(BackupJob.Offsite);

        return due;
    }

    /// <summary>
    /// Backup files the retention rule removes. Everything from the newest full backup taken at or before
    /// <c>now - retentionDays</c> onwards is kept, so any moment in the retention window can still be restored.
    /// Nothing is removed while no full backup is that old, and the newest full backup is never removed.
    /// </summary>
    public static List<BackupFile> ToDelete(DateTime now, IReadOnlyCollection<BackupFile> files, int retentionDays)
    {
        var limit = now.AddDays(-retentionDays);
        var anchor = files.Where(f => f.Kind == BackupKind.Full && f.StampLocal <= limit).OrderByDescending(f => f.StampLocal).FirstOrDefault();
        if (anchor is null) return [];
        return files.Where(f => f.StampLocal < anchor.StampLocal).OrderBy(f => f.StampLocal).ToList();
    }
}
