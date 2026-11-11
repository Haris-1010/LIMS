<#
.SYNOPSIS
  Restores the database as it was at a chosen moment, from the full, differential and log backups in a folder.
  Always restores into a NEW database name; never overwrites a database.

.DESCRIPTION
  1. Refuses if the target database already exists.
  2. Reads the headers of the backup files (works on a copy of the folder on another machine; msdb history is not needed)
     and chooses: the newest FULL that finished before -StopAt, the newest DIFFERENTIAL of that full that finished before
     -StopAt, then the log backups in order, checking that no log backup is missing (the LSNs must join up).
  3. Restores them (NORECOVERY), the last log with STOPAT, then brings the database online.
  4. Runs DBCC CHECKDB and prints the row counts of key tables, plus -SourceDatabase's if it exists, to compare.
  5. Prints the chosen restore plan first. -PlanOnly stops after that.
  -StopAt is in the SERVER's local time (the time shown in the file names). Without -StopAt the latest possible moment is used.
  If -StopAt is later than the last log backup the database is restored up to that log backup and the script says so.
  The database is kept (this is a recovery, not a test). To make it the live database follow docs/BACKUP_RESTORE.md.
  Encrypted backups need their certificate installed in master on this server, or the restore fails.

.EXAMPLE
  .\Restore-LimsPointInTime.ps1 -BackupFolder D:\LimsBackups -SourceDatabase LIMS -TargetDatabase LIMS_Recovered -StopAt "2026-11-12 10:41"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BackupFolder,
    [Parameter(Mandatory)][string]$TargetDatabase,
    [string]$SourceDatabase = "LIMS",
    [string]$Server = "localhost",
    [datetime]$StopAt,
    [switch]$PlanOnly
)
$ErrorActionPreference = "Stop"
. "$PSScriptRoot\LimsBackup.Common.ps1"

Assert-DbName $TargetDatabase; Assert-DbName $SourceDatabase
if ($TargetDatabase -eq $SourceDatabase) { throw "Target must differ from the source database name." }
if (-not (Test-Path -LiteralPath $BackupFolder)) { throw "Backup folder '$BackupFolder' does not exist." }
if ([int](Invoke-SqlScalar $Server "SELECT COUNT(*) FROM sys.databases WHERE name = $(SqlLiteral $TargetDatabase)") -gt 0) {
    throw "Database [$TargetDatabase] already exists. Choose another -TargetDatabase; this script never overwrites."
}
$limit = if ($PSBoundParameters.ContainsKey("StopAt")) { $StopAt } else { [datetime]::MaxValue }

function Get-Header([string]$Path) {
    $h = @(Invoke-SqlRows $Server "RESTORE HEADERONLY FROM DISK = $(SqlLiteral $Path)")[0]
    [pscustomobject]@{
        BackupType = [int]$h.BackupType; Start = [datetime]$h.BackupStartDate; Finish = [datetime]$h.BackupFinishDate
        FirstLSN = [decimal]$h.FirstLSN; LastLSN = [decimal]$h.LastLSN; CheckpointLSN = [decimal]$h.CheckpointLSN
        DatabaseBackupLSN = [decimal]$h.DatabaseBackupLSN; DifferentialBaseLSN = if ($null -ne $h.DifferentialBaseLSN) { [decimal]$h.DifferentialBaseLSN } else { $null }
        DatabaseName = [string]$h.DatabaseName
    }
}

$files = @(Get-BackupFiles $SourceDatabase $BackupFolder)
if ($files.Count -eq 0) { throw "No backup files of [$SourceDatabase] in '$BackupFolder'." }

# ---- choose the full backup
$full = $null
foreach ($f in @($files | Where-Object Type -eq "FULL" | Sort-Object Stamp -Descending)) {
    $h = Get-Header $f.Path
    if ($h.DatabaseName -ne $SourceDatabase) { continue }
    if ($h.Finish -le $limit) { $full = [pscustomobject]@{ File = $f; Header = $h }; break }
}
if (-not $full) { throw "No full backup of [$SourceDatabase] finished before $limit. The earliest moment that can be restored is after the oldest full backup." }

# ---- optional differential
$diff = $null
foreach ($f in @($files | Where-Object { $_.Type -eq "DIFF" -and $_.Stamp -ge $full.File.Stamp } | Sort-Object Stamp -Descending)) {
    $h = Get-Header $f.Path
    if ($h.DifferentialBaseLSN -eq $full.Header.CheckpointLSN -and $h.Finish -le $limit) { $diff = [pscustomobject]@{ File = $f; Header = $h }; break }
}
$lastLsn = if ($diff) { $diff.Header.LastLSN } else { $full.Header.LastLSN }

# ---- log chain
$logs = New-Object System.Collections.Generic.List[object]
$reachedAt = if ($diff) { $diff.Header.Finish } else { $full.Header.Finish }
$stopLog = $null
foreach ($f in @($files | Where-Object { $_.Type -eq "LOG" -and $_.Stamp -ge $full.File.Stamp.AddDays(-1) } | Sort-Object Stamp)) {
    $h = Get-Header $f.Path
    if ($h.LastLSN -le $lastLsn) { continue }                                  # already inside the restored full/diff/log
    if ($h.FirstLSN -gt $lastLsn) { throw "A log backup is missing: the next one found ($($f.Name)) starts at LSN $($h.FirstLSN) but the restore has reached $lastLsn. Look for the missing file (offsite copy?). The database cannot be restored past this point." }
    $logs.Add([pscustomobject]@{ File = $f; Header = $h }); $lastLsn = $h.LastLSN; $reachedAt = $h.Finish
    if ($h.Finish -ge $limit) { $stopLog = $h; break }
}
$recoveredTo = if ($limit -ne [datetime]::MaxValue -and ($stopLog -or $limit -le $reachedAt)) { $limit } else { $reachedAt }
if ($limit -ne [datetime]::MaxValue -and $limit -gt $reachedAt) {
    Write-Warning "-StopAt $limit is later than the last backup available ($reachedAt). Restoring up to $reachedAt; work after that is not in these backups."
}

Write-Host "Restore plan for [$SourceDatabase] -> [$TargetDatabase], target moment: $(if ($limit -eq [datetime]::MaxValue) { 'latest available' } else { $limit })"
Write-Host "  1. FULL          $($full.File.Name)   (finished $($full.Header.Finish.ToString("yyyy-MM-dd HH:mm:ss")))"
if ($diff) { Write-Host "  2. DIFFERENTIAL  $($diff.File.Name)   (finished $($diff.Header.Finish.ToString("yyyy-MM-dd HH:mm:ss")))" }
$i = 0; foreach ($l in $logs) { $i++; Write-Host ("  L{0,-2} LOG           {1}   (finished {2})" -f $i, $l.File.Name, $l.Header.Finish.ToString("yyyy-MM-dd HH:mm:ss")) }
Write-Host "  Database will be as of: $recoveredTo"
if ($PlanOnly) { return }

# ---- file locations
$dataDir = [string](Invoke-SqlScalar $Server "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000))")
$logDir = [string](Invoke-SqlScalar $Server "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000))")
$moves = foreach ($r in @(Invoke-SqlRows $Server "RESTORE FILELISTONLY FROM DISK = $(SqlLiteral $full.File.Path)")) {
    $isLog = $r.Type -eq "L"
    $target = Join-Path $(if ($isLog) { $logDir } else { $dataDir }) "$($TargetDatabase)_$($r.LogicalName).$(if ($isLog) { 'ldf' } else { 'mdf' })"
    "MOVE $(SqlLiteral $r.LogicalName) TO $(SqlLiteral $target)"
}

$stopClause = if ($limit -ne [datetime]::MaxValue) { ", STOPAT = '" + $limit.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "'" } else { "" }

Write-Host "Restoring full backup"
Invoke-SqlNonQuery $Server "RESTORE DATABASE [$TargetDatabase] FROM DISK = $(SqlLiteral $full.File.Path) WITH $($moves -join ', '), CHECKSUM, NORECOVERY;"
if ($diff) {
    Write-Host "Restoring differential backup"
    Invoke-SqlNonQuery $Server "RESTORE DATABASE [$TargetDatabase] FROM DISK = $(SqlLiteral $diff.File.Path) WITH CHECKSUM, NORECOVERY;"
}
$n = 0
foreach ($l in $logs) {
    $n++
    Write-Host "Restoring log $n of $($logs.Count)"
    Invoke-SqlNonQuery $Server "RESTORE LOG [$TargetDatabase] FROM DISK = $(SqlLiteral $l.File.Path) WITH CHECKSUM, NORECOVERY$stopClause;"
}
Write-Host "Bringing the database online"
Invoke-SqlNonQuery $Server "RESTORE DATABASE [$TargetDatabase] WITH RECOVERY;"

Write-Host "Running DBCC CHECKDB"
Invoke-SqlNonQuery $Server "DBCC CHECKDB ([$TargetDatabase]) WITH NO_INFOMSGS, ALL_ERRORMSGS;"

$countSql = @"
SELECT 'Organizations' AS [Table], COUNT(*) AS [Rows] FROM core.Organizations UNION ALL
SELECT 'Branches', COUNT(*) FROM core.Branches UNION ALL
SELECT 'Users', COUNT(*) FROM security.Users UNION ALL
SELECT 'Patients', COUNT(*) FROM patient.Patients UNION ALL
SELECT 'Orders', COUNT(*) FROM lab.Orders UNION ALL
SELECT 'Payments', COUNT(*) FROM billing.Payments UNION ALL
SELECT 'AuditLogs', COUNT(*) FROM audit.AuditLogs UNION ALL
SELECT 'Migrations', COUNT(*) FROM core.__EFMigrationsHistory
"@
Write-Host "`nRow counts in [$TargetDatabase] (as of $recoveredTo):"
@(Invoke-SqlRows $Server $countSql $TargetDatabase) | ForEach-Object { "  {0,-14} {1}" -f $_.Table, $_.Rows }
if ([int](Invoke-SqlScalar $Server "SELECT COUNT(*) FROM sys.databases WHERE name = $(SqlLiteral $SourceDatabase)") -gt 0) {
    Write-Host "Row counts in [$SourceDatabase] now (for comparison; it may have changed since):"
    @(Invoke-SqlRows $Server $countSql $SourceDatabase) | ForEach-Object { "  {0,-14} {1}" -f $_.Table, $_.Rows }
}
Write-Host "`nPOINT-IN-TIME RESTORE COMPLETED into [$TargetDatabase]. Inspect it, then follow docs/BACKUP_RESTORE.md to put it into service."
