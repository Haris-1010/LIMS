namespace Lims.Application.Backup;

/// <summary>
/// The backup schedule kept on the server (backup-settings.json in the data folder). Changed from the Backup page.
/// <see cref="LogMinutes"/> 0 turns transaction-log backups off.
/// </summary>
public sealed record BackupSettings(
    bool Enabled,
    string Folder,
    string? OffsiteFolder,
    string FullFrequency = "Daily",
    string FullTime = "22:00",
    int LogMinutes = 15,
    int RetentionDays = 14);

/// <summary>What the page sends when the administrator saves the schedule.</summary>
public sealed record BackupSettingsRequest(
    bool Enabled,
    string Folder,
    string? OffsiteFolder,
    string FullFrequency = "Daily",
    string FullTime = "22:00",
    int LogMinutes = 15,
    int RetentionDays = 14);

/// <summary>The saved schedule plus what the server knows about running it. Times are UTC.</summary>
public sealed record BackupSettingsDto(
    bool Enabled, string Folder, string? OffsiteFolder, string FullFrequency, string FullTime, int LogMinutes, int RetentionDays,
    bool SchedulerRunning, string Database, DateTime? NextFullUtc, string? SqlServiceAccount, string? FolderNote);

public sealed record BackupActionResult(bool Succeeded, string Message, string? File = null);

public sealed record BackupFolderDto(string Path, string Name);

public sealed record BackupFolderBrowserDto(string? CurrentPath, string? ParentPath, IReadOnlyList<BackupFolderDto> Folders);
