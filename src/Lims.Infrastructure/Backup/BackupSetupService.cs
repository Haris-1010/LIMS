using Lims.Application.Backup;
using Lims.Application.Common;

namespace Lims.Infrastructure.Backup;

/// <summary>Backup page actions: read and save the schedule, browse server folders, take a full backup now.</summary>
public sealed class BackupSetupService(BackupSettingsStore store, BackupEngine engine)
{
    public BackupSettingsDto Get()
    {
        var s = store.Load();
        var state = engine.ReadState(s.Folder);
        DateTime? next = s.Enabled && store.AutomaticAllowed ? BackupSchedule.NextFull(DateTime.Now, s, state.LastFull) : null;
        return new BackupSettingsDto(s.Enabled, s.Folder, s.OffsiteFolder, s.FullFrequency, s.FullTime, s.LogMinutes, s.RetentionDays,
            store.AutomaticAllowed, store.Database, next?.ToUniversalTime(), null, null);
    }

    /// <summary>Validates and saves the schedule, and makes the folder ready (created, SQL Server may write to it).</summary>
    public async Task<BackupSettingsDto> SaveAsync(BackupSettingsRequest request, CancellationToken ct)
    {
        BackupSchedule.Validate(request);
        var folder = Path.GetFullPath(request.Folder.Trim());
        var offsite = string.IsNullOrWhiteSpace(request.OffsiteFolder) ? null : Path.GetFullPath(request.OffsiteFolder.Trim());

        var access = await engine.PrepareFolderAsync(folder, ct);
        if (!access.Ok) throw new BusinessRuleException("backup_folder_create_failed", access.Note ?? $"The folder {folder} could not be created.");

        var settings = new BackupSettings(request.Enabled, folder, offsite, request.FullFrequency, request.FullTime, request.LogMinutes, request.RetentionDays);
        store.Save(settings);
        return Get() with { SqlServiceAccount = access.SqlServiceAccount, FolderNote = access.Note };
    }

    public Task<BackupActionResult> RunFullAsync(CancellationToken ct) =>
        engine.RunAsync(BackupJob.Full, store.Load(), ct);

    public BackupFolderBrowserDto BrowseFolders(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                var roots = DriveInfo.GetDrives()
                    .Where(d => d.DriveType is DriveType.Fixed or DriveType.Network or DriveType.Removable && d.IsReady)
                    .Select(d => new BackupFolderDto(d.RootDirectory.FullName, d.Name))
                    .ToList();
                return new BackupFolderBrowserDto(null, null, roots);
            }

            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
                throw new BusinessRuleException("backup_folder_missing", "The folder does not exist on the server.");

            var parent = Directory.GetParent(fullPath)?.FullName;
            var folders = new DirectoryInfo(fullPath).EnumerateDirectories()
                .Where(folder => !folder.Attributes.HasFlag(FileAttributes.ReparsePoint) && !folder.Attributes.HasFlag(FileAttributes.Hidden))
                .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
                .Take(500)
                .Select(folder => new BackupFolderDto(folder.FullName, folder.Name))
                .ToList();
            return new BackupFolderBrowserDto(fullPath, parent, folders);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException or NotSupportedException)
        {
            throw new BusinessRuleException("backup_folder_browse_failed", ex.Message);
        }
    }
}
