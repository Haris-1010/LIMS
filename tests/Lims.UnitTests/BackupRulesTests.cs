using Lims.Application.Backup;

namespace Lims.UnitTests;

public class BackupRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 12, 0, 0);
    private static BackupFile F(BackupKind k, DateTime stamp) => new($"LIMS_{stamp:yyyyMMdd_HHmmss}", k, stamp, 1000);
    private static JobStatus Ok(string job, DateTime at) => new(job, at, "ok", null, null);

    [Theory]
    [InlineData("LIMS_20261112_100000_FULL.bak", BackupKind.Full)]
    [InlineData("LIMS_20261112_130000_DIFF.bak", BackupKind.Differential)]
    [InlineData("LIMS_20261112_103000_LOG.trn", BackupKind.Log)]
    [InlineData("LIMS_20261109_120000.bak", BackupKind.Full)]          // first-generation name, a full backup
    public void File_names_written_by_the_scripts_are_recognised(string name, BackupKind kind)
    {
        var f = BackupRules.ParseFileName("LIMS", name, 42);
        Assert.NotNull(f);
        Assert.Equal(kind, f!.Kind);
        Assert.Equal(42, f.SizeBytes);
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("OTHER_20261112_100000_FULL.bak")]
    [InlineData("LIMS_20261112_100000_LOG.bak")]                         // a log backup must be .trn
    [InlineData("LIMS_20261112_100000_FULL.trn")]
    [InlineData("LIMS_20269999_100000_FULL.bak")]                        // impossible date
    [InlineData("LIMS_config_20261112_100000.zip")]
    [InlineData("backup-status.json")]
    public void Other_files_are_ignored(string name) => Assert.Null(BackupRules.ParseFileName("LIMS", name, 1));

    [Fact]
    public void Database_name_in_a_file_name_is_matched_literally() =>
        Assert.Null(BackupRules.ParseFileName("LIMS.", "LIMSX_20261112_100000_FULL.bak", 1));

    [Fact]
    public void Status_file_is_read_and_a_damaged_one_gives_what_it_can()
    {
        var json = """{"Full":{"lastSuccess":"2026-11-12T10:00:01.123","lastDetail":"a.bak (1 MB)"},"Log":{"lastSuccess":"2026-11-12T11:45:00.000","lastFailure":"2026-11-12T11:50:00.000","lastFailureDetail":"disk full"}}""";
        var jobs = BackupRules.ParseStatus(json);
        Assert.Equal(2, jobs.Count);
        var log = jobs.Single(j => j.Name == "Log");
        Assert.Equal("disk full", log.LastFailureDetail);
        Assert.True(log.LastFailureLocal > log.LastSuccessLocal);

        Assert.Empty(BackupRules.ParseStatus(null));
        Assert.Empty(BackupRules.ParseStatus("   "));
        Assert.Empty(BackupRules.ParseStatus("{ not json"));
        Assert.Empty(BackupRules.ParseStatus("[1,2]"));
    }

    [Fact]
    public void Healthy_when_everything_is_recent()
    {
        var files = new[] { F(BackupKind.Full, Now.AddHours(-14)), F(BackupKind.Log, Now.AddMinutes(-10)) };
        var status = new[] { Ok("Full", Now.AddHours(-14)), Ok("Log", Now.AddMinutes(-10)), Ok("Offsite", Now.AddMinutes(-12)) };
        Assert.Empty(BackupRules.Evaluate(Now, files, status, true, new BackupThresholds()));
    }

    [Fact]
    public void Missing_and_old_backups_are_reported()
    {
        var none = BackupRules.Evaluate(Now, [], [], true, new BackupThresholds(ExpectOffsite: false));
        Assert.Contains(none, p => p.Code == "noFull");
        Assert.Contains(none, p => p.Code == "noLog");

        var old = BackupRules.Evaluate(Now, [F(BackupKind.Full, Now.AddHours(-27)), F(BackupKind.Log, Now.AddMinutes(-46))], [Ok("Offsite", Now)], true, new BackupThresholds());
        Assert.Contains(old, p => p.Code == "fullOld");
        Assert.Contains(old, p => p.Code == "logOld");
    }

    [Fact]
    public void Limits_are_inclusive_at_the_edge()
    {
        var files = new[] { F(BackupKind.Full, Now.AddHours(-26)), F(BackupKind.Log, Now.AddMinutes(-45)) };
        Assert.Empty(BackupRules.Evaluate(Now, files, [Ok("Offsite", Now.AddHours(-26))], true, new BackupThresholds()));
    }

    [Fact]
    public void A_job_that_failed_after_its_last_success_is_a_problem_but_not_if_it_recovered()
    {
        var files = new[] { F(BackupKind.Full, Now.AddHours(-1)), F(BackupKind.Log, Now.AddMinutes(-5)) };
        var failing = new JobStatus("Log", Now.AddMinutes(-20), "x", Now.AddMinutes(-5), "disk full");
        var recovered = new JobStatus("Log", Now.AddMinutes(-2), "x", Now.AddMinutes(-20), "disk full");
        var t = new BackupThresholds(ExpectOffsite: false);

        var p = Assert.Single(BackupRules.Evaluate(Now, files, [failing], true, t));
        Assert.Equal("jobFailed:Log", p.Code);
        Assert.Equal("disk full", p.Detail);
        Assert.Empty(BackupRules.Evaluate(Now, files, [recovered], true, t));
    }

    [Fact]
    public void Offsite_is_checked_only_when_expected()
    {
        var files = new[] { F(BackupKind.Full, Now.AddHours(-1)), F(BackupKind.Log, Now.AddMinutes(-5)) };
        Assert.Contains(BackupRules.Evaluate(Now, files, [], true, new BackupThresholds()), p => p.Code == "offsiteNever");
        Assert.Contains(BackupRules.Evaluate(Now, files, [Ok("Offsite", Now.AddHours(-30))], true, new BackupThresholds()), p => p.Code == "offsiteOld");
        Assert.Empty(BackupRules.Evaluate(Now, files, [], true, new BackupThresholds(ExpectOffsite: false)));
    }

    [Fact]
    public void Missing_status_file_and_log_expectation_are_reported_or_skipped()
    {
        var files = new[] { F(BackupKind.Full, Now.AddHours(-1)) };
        var t = new BackupThresholds(ExpectLogBackups: false, ExpectOffsite: false);
        var p = Assert.Single(BackupRules.Evaluate(Now, files, [], false, t));
        Assert.Equal("noStatusFile", p.Code);
    }

    [Fact]
    public void Recoverable_to_is_the_newest_backup_of_any_kind()
    {
        Assert.Null(BackupRules.RecoverableToLocal([]));
        var files = new[] { F(BackupKind.Full, Now.AddHours(-5)), F(BackupKind.Differential, Now.AddHours(-2)), F(BackupKind.Log, Now.AddMinutes(-7)) };
        Assert.Equal(Now.AddMinutes(-7), BackupRules.RecoverableToLocal(files));
    }
}
