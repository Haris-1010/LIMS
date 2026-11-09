<#
.SYNOPSIS
  Restores a LIMS backup into a NEW, separate database and checks it. Never overwrites a database.

.DESCRIPTION
  1. Refuses to run if the target database already exists.
  2. Restores with MOVE so the data/log files get new names next to the server's default data folder.
  3. Runs DBCC CHECKDB and compares row counts of key tables with the source database (if given).
  4. Drops the test database afterwards unless -Keep is passed.

.EXAMPLE
  .\Test-LimsRestore.ps1 -BackupFile "C:\...\LIMS_Dev_20261109_120000.bak" -SourceDatabase LIMS_Dev
#>
param(
    [Parameter(Mandatory)][string]$BackupFile,
    [string]$Server = "localhost",
    [string]$TargetDatabase = "LIMS_RestoreTest",
    [string]$SourceDatabase,
    [switch]$Keep
)
$ErrorActionPreference = "Stop"

function Invoke-Sql([string]$Query, [string]$Db = "master") {
    $out = & sqlcmd -S $Server -E -b -d $Db -h -1 -W -s "|" -Q "SET NOCOUNT ON; $Query"
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed: $out" }
    return $out
}

foreach ($n in @($TargetDatabase, $SourceDatabase)) {
    if ($n -and $n -notmatch '^[A-Za-z0-9_]+$') { throw "Invalid database name '$n'." }
}
if ($SourceDatabase -and $SourceDatabase -eq $TargetDatabase) { throw "Target must differ from the source database." }

$exists = (Invoke-Sql "SELECT COUNT(*) FROM sys.databases WHERE name = N'$TargetDatabase'" | Select-Object -First 1).Trim()
if ($exists -ne "0") { throw "Database [$TargetDatabase] already exists. Choose another -TargetDatabase; this script never overwrites." }

$bak = $BackupFile.Replace("'", "''")
$dataDir = (Invoke-Sql "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000))" | Select-Object -First 1).Trim()
$logDir = (Invoke-Sql "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000))" | Select-Object -First 1).Trim()

# Logical file names inside the backup.
$files = Invoke-Sql "DECLARE @f TABLE (LogicalName nvarchar(128), PhysicalName nvarchar(260), Type char(1), FileGroupName nvarchar(128), Size numeric(20,0), MaxSize numeric(20,0), FileId bigint, CreateLSN numeric(25,0), DropLSN numeric(25,0), UniqueId uniqueidentifier, ReadOnlyLSN numeric(25,0), ReadWriteLSN numeric(25,0), BackupSizeInBytes bigint, SourceBlockSize int, FileGroupId int, LogGroupGUID uniqueidentifier, DifferentialBaseLSN numeric(25,0), DifferentialBaseGUID uniqueidentifier, IsReadOnly bit, IsPresent bit, TDEThumbprint varbinary(32), SnapshotUrl nvarchar(360)); INSERT @f EXEC('RESTORE FILELISTONLY FROM DISK = N''$($bak.Replace("'", "''"))'''); SELECT LogicalName + '|' + Type FROM @f;"

$moves = foreach ($line in $files) {
    $name, $type = $line.Trim().Split('|')
    $dir = if ($type -eq 'L') { $logDir } else { $dataDir }
    $ext = if ($type -eq 'L') { 'ldf' } else { 'mdf' }
    $target = Join-Path $dir "$($TargetDatabase)_$name.$ext"
    "MOVE N'$($name.Replace("'", "''"))' TO N'$($target.Replace("'", "''"))'"
}

Write-Host "Restoring into [$TargetDatabase]"
Invoke-Sql "RESTORE DATABASE [$TargetDatabase] FROM DISK = N'$bak' WITH $($moves -join ', '), CHECKSUM, RECOVERY;" | Out-Null

Write-Host "Running DBCC CHECKDB"
Invoke-Sql "DBCC CHECKDB ([$TargetDatabase]) WITH NO_INFOMSGS, ALL_ERRORMSGS;" | Out-Null

$countSql = @"
SELECT 'Organizations', COUNT(*) FROM core.Organizations UNION ALL
SELECT 'Branches', COUNT(*) FROM core.Branches UNION ALL
SELECT 'Users', COUNT(*) FROM security.Users UNION ALL
SELECT 'Roles', COUNT(*) FROM security.Roles UNION ALL
SELECT 'RolePermissions', COUNT(*) FROM security.RolePermissions UNION ALL
SELECT 'AuditLogs', COUNT(*) FROM audit.AuditLogs UNION ALL
SELECT 'Migrations', COUNT(*) FROM core.__EFMigrationsHistory
"@
$restored = Invoke-Sql $countSql $TargetDatabase
Write-Host "Row counts in restored database:"
$restored | ForEach-Object { "  $_" }

$ok = $true
if ($SourceDatabase) {
    $source = Invoke-Sql $countSql $SourceDatabase
    if (($source -join "`n") -ne ($restored -join "`n")) {
        Write-Warning "Row counts differ from [$SourceDatabase] (expected if it changed after the backup):"
        $source | ForEach-Object { "  source: $_" }
        $ok = $false
    } else {
        Write-Host "Row counts match [$SourceDatabase]"
    }
}

if (-not $Keep) {
    Invoke-Sql "ALTER DATABASE [$TargetDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$TargetDatabase];" | Out-Null
    Write-Host "Dropped test database [$TargetDatabase]"
}

if ($ok) { Write-Host "RESTORE TEST PASSED" } else { Write-Host "RESTORE TEST COMPLETED WITH WARNINGS" }
