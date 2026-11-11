<#
.SYNOPSIS
  Backs up the LIMS database: full, differential or transaction log, with checksum, verification, status file and event log.

.DESCRIPTION
  -Type Full          (default) whole database                      -> <db>_<yyyyMMdd_HHmmss>_FULL.bak
  -Type Differential  changes since the last full backup            -> <db>_<stamp>_DIFF.bak
  -Type Log           transaction log (point-in-time recovery)       -> <db>_<stamp>_LOG.trn
  Every backup uses CHECKSUM and is checked with RESTORE VERIFYONLY. On failure the script writes an Error to the
  Windows Application event log (source "LIMS Backup"), records it in <BackupFolder>\backup-status.json, runs
  -OnFailure (a script that receives the message, e.g. to send an e-mail) and exits with code 1.

  Log and differential backups need the FULL recovery model and an earlier full backup; the script says so if not.
  Compression is used unless the edition is Express (not supported there).
  -EncryptionCertificate <name> encrypts the file (AES-256) with a certificate in master. BACK UP THAT CERTIFICATE AND ITS
  PRIVATE KEY SEPARATELY: without it these backups cannot be restored anywhere.

.EXAMPLE
  .\Backup-LimsDatabase.ps1 -Database LIMS -BackupFolder D:\LimsBackups                      # full
  .\Backup-LimsDatabase.ps1 -Database LIMS -BackupFolder D:\LimsBackups -Type Log
  .\Backup-LimsDatabase.ps1 -Database LIMS -BackupFolder D:\LimsBackups -EncryptionCertificate LimsBackupCert

.NOTES
  The SQL Server service account must have write access to the backup folder.
  A backup on the same disk as the database is not enough: see Copy-LimsBackupOffsite.ps1.
#>
[CmdletBinding()]
param(
    [string]$Server = "localhost",
    [string]$Database = "LIMS_Dev",
    [string]$BackupFolder,
    [ValidateSet("Full", "Differential", "Log")][string]$Type = "Full",
    [string]$EncryptionCertificate,
    [string]$OnFailure
)
$ErrorActionPreference = "Stop"
. "$PSScriptRoot\LimsBackup.Common.ps1"

try {
    Assert-DbName $Database
    if ($EncryptionCertificate) { Assert-DbName $EncryptionCertificate }
    if (-not $BackupFolder) { $BackupFolder = [string](Invoke-SqlScalar $Server "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))") }
    if (-not (Test-Path -LiteralPath $BackupFolder)) { throw "Backup folder '$BackupFolder' does not exist." }

    $recovery = Invoke-SqlScalar $Server "SELECT recovery_model_desc FROM sys.databases WHERE name = $(SqlLiteral $Database)"
    if (-not $recovery) { throw "Database [$Database] was not found on $Server." }
    if ($Type -ne "Full") {
        if ($recovery -ne "FULL") { throw "[$Database] uses the $recovery recovery model; $Type backups need FULL. Run Register-LimsBackupTasks.ps1 (it sets it) or: ALTER DATABASE [$Database] SET RECOVERY FULL, then take a full backup." }
        $hasFull = Invoke-SqlScalar $Server "SELECT COUNT(*) FROM msdb.dbo.backupset WHERE database_name = $(SqlLiteral $Database) AND type = 'D'"
        if ([int]$hasFull -eq 0) { throw "No full backup of [$Database] is recorded yet. Take a full backup first." }
    }

    $edition = [string](Invoke-SqlScalar $Server "SELECT CAST(SERVERPROPERTY('Edition') AS nvarchar(200))")
    $options = @("CHECKSUM", "STATS = 25")
    if ($edition -notmatch "Express") { $options += "COMPRESSION" }
    if ($Type -eq "Differential") { $options += "DIFFERENTIAL" }
    if ($Type -ne "Log") { $options += "INIT" }
    if ($EncryptionCertificate) { $options += "ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = [$EncryptionCertificate])" }

    $stamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $suffix = @{ Full = "FULL.bak"; Differential = "DIFF.bak"; Log = "LOG.trn" }[$Type]
    $file = Join-Path $BackupFolder "$($Database)_$($stamp)_$suffix"
    $verb = if ($Type -eq "Log") { "LOG" } else { "DATABASE" }

    Write-Host "Backing up [$Database] ($Type) to $file"
    Invoke-SqlNonQuery $Server "BACKUP $verb [$Database] TO DISK = $(SqlLiteral $file) WITH $($options -join ', '), NAME = $(SqlLiteral "$Database $Type backup $stamp");"

    Write-Host "Verifying backup"
    Invoke-SqlNonQuery $Server "RESTORE VERIFYONLY FROM DISK = $(SqlLiteral $file) WITH CHECKSUM;"

    $size = try { [math]::Round((Get-Item -LiteralPath $file).Length / 1MB, 1) } catch { "?" }
    Set-BackupStatus $BackupFolder $Type $true "$([IO.Path]::GetFileName($file)) ($size MB)"
    Write-LimsEvent "$Type backup of [$Database] succeeded: $file ($size MB)" "Information" 1001
    Write-Host "OK: $file"
    $file
}
catch {
    $reason = if ($_.Exception.InnerException) { $_.Exception.InnerException.Message } else { $_.Exception.Message }
    $msg = "$Type backup of [$Database] FAILED: $reason"
    Write-Host $msg -ForegroundColor Red
    try { if ($BackupFolder -and (Test-Path -LiteralPath $BackupFolder)) { Set-BackupStatus $BackupFolder $Type $false $reason } } catch { }
    Write-LimsEvent $msg "Error" 1901
    Invoke-FailureHook $OnFailure $msg
    exit 1
}
