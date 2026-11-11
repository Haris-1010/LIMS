using Lims.Application.Backup;
using Lims.Application.Common;
using Lims.Infrastructure.Backup;

namespace Lims.Api.Infrastructure;

/// <summary>
/// Runs the automatic backups inside the application: checks once a minute what is due (BackupSchedule) and runs it.
/// Nothing has to be registered in Windows. A slot missed while the site was stopped is caught up when it starts again,
/// so the first full backup is taken shortly after the first start. Turned off with Backup:Automatic=false.
/// </summary>
public sealed class BackupScheduler(BackupSettingsStore store, BackupEngine engine, ILogger<BackupScheduler> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!store.AutomaticAllowed)
        {
            log.LogInformation("Automatic backups are turned off (Backup:Automatic=false)");
            return;
        }
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);   // let the site finish starting first
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            do
            {
                await TickAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        BackupSettings settings;
        try { settings = store.Load(); }
        catch (Exception ex) { log.LogError(ex, "Backup settings could not be read"); return; }
        if (!settings.Enabled) return;

        var due = BackupSchedule.DueJobs(DateTime.Now, settings, engine.ReadState(settings.Folder));
        foreach (var job in due)
        {
            try
            {
                await engine.RunAsync(job, settings, ct);
            }
            catch (BusinessRuleException ex) when (ex.Code == "backup_busy")
            {
                return;   // a manual or deployment backup is running; try next minute
            }
            catch (BusinessRuleException)
            {
                // Already logged and written to backup-status.json; the status page shows it.
                if (job == BackupJob.Full) return;   // nothing else makes sense without a full backup
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                log.LogError(ex, "Backup job {Job} failed unexpectedly", job);
            }
        }
    }
}
