<#
.SYNOPSIS
  Copies backups (and the configuration files) to a second disk, network share or removable drive and proves each copy
  is identical by SHA-256.

.DESCRIPTION
  For every backup file of the database in -BackupFolder that is not yet in a destination, copies it (to a temporary
  name, then renames), compares the SHA-256 of source and copy, and deletes a bad copy. Files already present with the same
  hash are skipped; a file present with a different hash is reported as an error and never overwritten.
  -ConfigFiles (for example appsettings.Production.json and the Data Protection key folder) are zipped into
  <Database>_config_<stamp>.zip in the backup folder once a day and copied like any backup. The zip is NOT encrypted:
  appsettings.Production.json holds no passwords by design and the key files are DPAPI-protected when
  DataProtection:ProtectWithDpapi is on. Backups themselves are encrypted by Backup-LimsDatabase.ps1 -EncryptionCertificate.
  Retention at the destination is the destination's job: run Remove-OldBackups.ps1 there too.
  Copies are NOT deleted from the source by this script.

.EXAMPLE
  .\Copy-LimsBackupOffsite.ps1 -Database LIMS -BackupFolder D:\LimsBackups -Destination E:\LimsOffsite, \\backup-pc\LimsBackups `
      -ConfigFiles C:\inetpub\lims\appsettings.Production.json, D:\LimsData\keys
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BackupFolder,
    [Parameter(Mandatory)][string[]]$Destination,
    [string]$Database = "LIMS",
    [string[]]$ConfigFiles = @(),
    [string]$OnFailure
)
$ErrorActionPreference = "Stop"
. "$PSScriptRoot\LimsBackup.Common.ps1"

function Get-Sha256([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }

$errors = New-Object System.Collections.Generic.List[string]
$copied = 0
try {
    Assert-DbName $Database
    if (-not (Test-Path -LiteralPath $BackupFolder)) { throw "Backup folder '$BackupFolder' does not exist." }
    foreach ($d in $Destination) {
        $resolved = Resolve-Path -LiteralPath $d -ErrorAction SilentlyContinue
        if ($resolved -and $resolved.Path -eq (Resolve-Path -LiteralPath $BackupFolder).Path) { throw "Destination '$d' is the backup folder itself." }
    }

    # Daily zip of configuration (once per calendar day).
    $present = @($ConfigFiles | Where-Object { Test-Path -LiteralPath $_ })
    if ($present.Count -gt 0 -and -not (Get-ChildItem -LiteralPath $BackupFolder -Filter "$($Database)_config_$(Get-Date -Format yyyyMMdd)_*.zip" -ErrorAction SilentlyContinue)) {
        $zip = Join-Path $BackupFolder "$($Database)_config_$(Get-Date -Format 'yyyyMMdd_HHmmss').zip"
        Compress-Archive -LiteralPath $present -DestinationPath $zip
        Write-Host "Configuration archive: $zip"
    }
    foreach ($missing in ($ConfigFiles | Where-Object { -not (Test-Path -LiteralPath $_) })) { $errors.Add("Configuration path not found: $missing") }

    # Always arrays: with a single backup file a bare pipeline result is one object and += would fail.
    $files = @(@(Get-BackupFiles $Database $BackupFolder) | ForEach-Object { Get-Item -LiteralPath $_.Path })
    $files += @(Get-ChildItem -LiteralPath $BackupFolder -File -Filter "$($Database)_config_*.zip")
    foreach ($dest in $Destination) {
        try {
            New-Item -ItemType Directory -Force -Path $dest | Out-Null
            foreach ($f in $files) {
                $target = Join-Path $dest $f.Name
                $srcHash = $null
                if (Test-Path -LiteralPath $target) {
                    if ((Get-Item -LiteralPath $target).Length -eq $f.Length) {
                        $srcHash = Get-Sha256 $f.FullName
                        if ((Get-Sha256 $target) -eq $srcHash) { continue }
                    }
                    $errors.Add("$target exists but differs from the source: not overwritten"); continue
                }
                $tmp = "$target.copying"
                Copy-Item -LiteralPath $f.FullName -Destination $tmp -Force
                if (-not $srcHash) { $srcHash = Get-Sha256 $f.FullName }
                if ((Get-Sha256 $tmp) -ne $srcHash) { Remove-Item -LiteralPath $tmp -Force; $errors.Add("Copy of $($f.Name) to $dest did not match the source (removed)"); continue }
                Move-Item -LiteralPath $tmp -Destination $target
                $copied++
                Write-Host "  copied and verified: $($f.Name) -> $dest"
            }
        } catch { $errors.Add("Destination ${dest}: $($_.Exception.Message)") }
    }

    if ($errors.Count -gt 0) { throw ($errors -join "; ") }
    Set-BackupStatus $BackupFolder "Offsite" $true "$copied file(s) copied to $($Destination -join ', ')"
    if ($copied -gt 0) { Write-LimsEvent "Off-machine copy of [$Database]: $copied file(s) copied and verified to $($Destination -join ', ')" "Information" 1003 }
    Write-Host "OK: $copied file(s) copied; all destinations up to date."
}
catch {
    $msg = "Off-machine copy of [$Database] FAILED: $($_.Exception.Message)"
    Write-Host $msg -ForegroundColor Red
    try { Set-BackupStatus $BackupFolder "Offsite" $false $_.Exception.Message } catch { }
    Write-LimsEvent $msg "Error" 1903
    Invoke-FailureHook $OnFailure $msg
    exit 1
}
