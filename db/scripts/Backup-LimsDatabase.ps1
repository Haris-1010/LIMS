<#
.SYNOPSIS
  Full backup of the LIMS database with checksum, followed by RESTORE VERIFYONLY.

.EXAMPLE
  .\Backup-LimsDatabase.ps1                                    # LIMS_Dev on localhost, default backup folder
  .\Backup-LimsDatabase.ps1 -Database LIMS -BackupFolder D:\LimsBackups

.NOTES
  The SQL Server service account must have write access to the backup folder.
  Copy backups to a second disk or machine; a backup on the same disk as the database is not enough.
#>
param(
    [string]$Server = "localhost",
    [string]$Database = "LIMS_Dev",
    [string]$BackupFolder
)
$ErrorActionPreference = "Stop"

function Invoke-Sql([string]$Query) {
    $out = & sqlcmd -S $Server -E -b -h -1 -W -Q "SET NOCOUNT ON; $Query"
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed: $out" }
    return $out
}

if ($Database -notmatch '^[A-Za-z0-9_]+$') { throw "Invalid database name '$Database'." }

if (-not $BackupFolder) {
    $BackupFolder = (Invoke-Sql "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))" | Select-Object -First 1).Trim()
}

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$file = Join-Path $BackupFolder "$($Database)_$stamp.bak"
$fileSql = $file.Replace("'", "''")

Write-Host "Backing up [$Database] to $file"
Invoke-Sql "BACKUP DATABASE [$Database] TO DISK = N'$fileSql' WITH INIT, CHECKSUM, STATS = 25, NAME = N'$Database full backup $stamp';" | Out-Null

Write-Host "Verifying backup"
Invoke-Sql "RESTORE VERIFYONLY FROM DISK = N'$fileSql' WITH CHECKSUM;" | Out-Null

Write-Host "OK: $file"
$file
