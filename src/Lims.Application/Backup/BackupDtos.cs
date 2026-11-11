namespace Lims.Application.Backup;

/// <summary>Everything the Backup page shows. Times are UTC. When <see cref="Configured"/> is false nothing else is filled in.</summary>
public sealed record BackupStatusDto(
    bool Configured, bool FolderReadable, string? Folder, string Database, DateTime CheckedAtUtc, bool Healthy,
    IReadOnlyList<BackupProblemDto> Problems, IReadOnlyList<BackupJobDto> Jobs, IReadOnlyList<BackupFileDto> Files,
    BackupSummaryDto Summary);

public sealed record BackupProblemDto(string Code, string? Detail, DateTime? WhenUtc);

/// <summary>One job (Full, Differential, Log, Offsite, Retention) as last reported to backup-status.json.</summary>
public sealed record BackupJobDto(string Name, DateTime? LastSuccessUtc, string? LastDetail, DateTime? LastFailureUtc, string? LastFailureDetail);

public sealed record BackupFileDto(string Name, string Kind, DateTime StampUtc, long SizeBytes);

public sealed record BackupSummaryDto(
    int FileCount, long TotalBytes, DateTime? OldestFullUtc, DateTime? NewestFullUtc, DateTime? NewestLogUtc, DateTime? RecoverableToUtc,
    int FullMaxAgeHours, int LogMaxAgeMinutes, bool ExpectLogBackups, bool ExpectOffsite);
