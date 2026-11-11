using Lims.Application.Backup;
using Lims.Application.Common;

namespace Lims.UnitTests;

public sealed class BackupScheduleTests
{
    private static readonly BackupSettings Daily = new(true, @"D:\LimsData\backups", null);   // 22:00 daily, log every 15 min, 14 days
    private static BackupState State(DateTime? full = null, DateTime? log = null, DateTime? retention = null, DateTime? offsite = null,
        Dictionary<BackupJob, DateTime>? failures = null) => new(full, log, retention, offsite, failures ?? []);

    [Fact]
    public void A_new_installation_takes_a_full_backup_straight_away()
    {
        var due = BackupSchedule.DueJobs(new DateTime(2026, 11, 11, 10, 0, 0), Daily, State());
        Assert.Equal([BackupJob.Full], due);
    }

    [Fact]
    public void Full_backup_runs_at_the_set_time_and_a_missed_one_is_caught_up()
    {
        var lastNight = new DateTime(2026, 11, 10, 22, 0, 5);
        Assert.DoesNotContain(BackupJob.Full, BackupSchedule.DueJobs(new DateTime(2026, 11, 11, 21, 59, 0), Daily, State(lastNight, lastNight)));
        Assert.Contains(BackupJob.Full, BackupSchedule.DueJobs(new DateTime(2026, 11, 11, 22, 0, 0), Daily, State(lastNight, lastNight)));
        // Server was off at 22:00; it is caught up the next morning.
        Assert.Contains(BackupJob.Full, BackupSchedule.DueJobs(new DateTime(2026, 11, 12, 8, 0, 0), Daily, State(lastNight, lastNight)));
    }

    [Fact]
    public void Weekly_full_backups_run_on_monday()
    {
        var weekly = Daily with { FullFrequency = "Weekly", FullTime = "23:00" };
        var thursday = new DateTime(2026, 11, 12, 12, 0, 0);
        Assert.Equal(new DateTime(2026, 11, 9, 23, 0, 0), BackupSchedule.LastFullSlot(thursday, weekly));   // Monday 9 November
        Assert.Equal(new DateTime(2026, 11, 16, 23, 0, 0), BackupSchedule.NextFull(thursday, weekly, new DateTime(2026, 11, 9, 23, 0, 30)));
        Assert.Equal(new DateTime(2026, 11, 2, 23, 0, 0), BackupSchedule.LastFullSlot(new DateTime(2026, 11, 9, 22, 0, 0), weekly));
    }

    [Fact]
    public void Log_backups_follow_the_interval_and_need_a_full_backup()
    {
        var now = new DateTime(2026, 11, 11, 10, 0, 0);
        var full = new DateTime(2026, 11, 10, 22, 0, 0);
        Assert.Contains(BackupJob.Log, BackupSchedule.DueJobs(now, Daily, State(full, now.AddMinutes(-15))));
        Assert.DoesNotContain(BackupJob.Log, BackupSchedule.DueJobs(now, Daily, State(full, now.AddMinutes(-14))));
        Assert.DoesNotContain(BackupJob.Log, BackupSchedule.DueJobs(now, Daily with { LogMinutes = 0 }, State(full)));
        Assert.DoesNotContain(BackupJob.Log, BackupSchedule.DueJobs(now, Daily, State()));
    }

    [Fact]
    public void A_failed_job_waits_before_it_is_tried_again()
    {
        var now = new DateTime(2026, 11, 11, 10, 0, 0);
        Assert.Empty(BackupSchedule.DueJobs(now, Daily, State(failures: new() { [BackupJob.Full] = now.AddMinutes(-3) })));
        Assert.Contains(BackupJob.Full, BackupSchedule.DueJobs(now, Daily, State(failures: new() { [BackupJob.Full] = now.AddMinutes(-11) })));
    }

    [Fact]
    public void Nothing_runs_when_automatic_backups_are_off()
    {
        Assert.Empty(BackupSchedule.DueJobs(DateTime.Now, Daily with { Enabled = false }, State()));
    }

    [Fact]
    public void Retention_and_offsite_run_once_there_is_a_full_backup()
    {
        var now = new DateTime(2026, 11, 11, 10, 0, 0);
        var withOffsite = Daily with { OffsiteFolder = @"E:\Offsite" };
        var full = new DateTime(2026, 11, 10, 22, 0, 0);
        var due = BackupSchedule.DueJobs(now, withOffsite, State(full, now.AddMinutes(-1), retention: now.AddDays(-1), offsite: now.AddMinutes(-20)));
        Assert.Contains(BackupJob.Retention, due);
        Assert.Contains(BackupJob.Offsite, due);
        due = BackupSchedule.DueJobs(now, withOffsite, State(full, now.AddMinutes(-1), retention: now.AddHours(-1), offsite: now.AddMinutes(-5)));
        Assert.DoesNotContain(BackupJob.Retention, due);
        Assert.DoesNotContain(BackupJob.Offsite, due);
    }

    [Fact]
    public void Retention_keeps_everything_needed_to_restore_any_moment_in_the_window()
    {
        var now = new DateTime(2026, 11, 30, 12, 0, 0);
        var files = new List<BackupFile>();
        for (var d = 1; d <= 29; d++)
        {
            files.Add(new BackupFile($"F{d}", BackupKind.Full, new DateTime(2026, 11, d, 22, 0, 0), 1));
            files.Add(new BackupFile($"L{d}", BackupKind.Log, new DateTime(2026, 11, d, 23, 0, 0), 1));
        }
        var gone = BackupSchedule.ToDelete(now, files, 14);
        // Window starts 16 Nov 12:00: the full of 15 Nov 22:00 is the base for it, so it and everything after stays.
        Assert.All(gone, f => Assert.True(f.StampLocal < new DateTime(2026, 11, 15, 22, 0, 0)));
        Assert.Contains(gone, f => f.Name == "L14");
        Assert.DoesNotContain(gone, f => f.Name == "F15");
        Assert.Equal(28, gone.Count);
    }

    [Fact]
    public void Retention_removes_nothing_until_a_full_backup_is_old_enough()
    {
        var now = new DateTime(2026, 11, 30, 12, 0, 0);
        var files = new List<BackupFile> { new("F", BackupKind.Full, now.AddDays(-3), 1), new("L", BackupKind.Log, now.AddDays(-20), 1) };
        Assert.Empty(BackupSchedule.ToDelete(now, files, 14));
    }

    [Theory]
    [InlineData("relative", "22:00", 15, 14, "backup_folder_invalid")]
    [InlineData(@"D:\B", "7pm", 15, 14, "backup_time_invalid")]
    [InlineData(@"D:\B", "22:00", 4, 14, "backup_log_interval_invalid")]
    [InlineData(@"D:\B", "22:00", 61, 14, "backup_log_interval_invalid")]
    [InlineData(@"D:\B", "22:00", 15, 0, "backup_retention_invalid")]
    public void Invalid_settings_are_refused(string folder, string time, int log, int days, string code)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => BackupSchedule.Validate(new BackupSettingsRequest(true, folder, null, "Daily", time, log, days)));
        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public void Log_interval_zero_turns_log_backups_off()
    {
        BackupSchedule.Validate(new BackupSettingsRequest(true, @"D:\B", null, "Daily", "22:00", 0, 14));
    }
}
