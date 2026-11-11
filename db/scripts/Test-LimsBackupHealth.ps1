<#
.SYNOPSIS
  Checks that backups are recent and records an ERROR in the Windows event log (and runs -OnFailure) when they are not.
  Meant to run every 30 minutes from Task Scheduler, so a backup that silently stopped is noticed.

.DESCRIPTION
  Reads <BackupFolder>\backup-status.json and the newest files on disk. Problems reported:
    - no full backup in the last -MaxFullAgeHours (default 26)
    - no log backup in the last -MaxLogAgeMinutes (default 45, i.e. three missed 15-minute runs; 0 turns the check off)
    - the last attempt of any kind failed after its last success
    - nothing copied off the machine in the last -MaxOffsiteAgeHours (default 26; 0 turns the check off)
  Exit code 0 = healthy, 1 = problem. Prints one line per problem.

.EXAMPLE
  .\Test-LimsBackupHealth.ps1 -BackupFolder D:\LimsBackups -Database LIMS
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BackupFolder,
    [string]$Database = "LIMS",
    [int]$MaxFullAgeHours = 26,
    [int]$MaxLogAgeMinutes = 45,
    [int]$MaxOffsiteAgeHours = 26,
    [string]$OnFailure
)
$ErrorActionPreference = "Stop"
. "$PSScriptRoot\LimsBackup.Common.ps1"

$problems = New-Object System.Collections.Generic.List[string]
try {
    Assert-DbName $Database
    if (-not (Test-Path -LiteralPath $BackupFolder)) { throw "Backup folder '$BackupFolder' does not exist." }
    $files = @(Get-BackupFiles $Database $BackupFolder)
    $now = Get-Date

    $lastFull = $files | Where-Object Type -eq "FULL" | Select-Object -Last 1
    if (-not $lastFull) { $problems.Add("No full backup of [$Database] found in $BackupFolder.") }
    elseif ($lastFull.Stamp -lt $now.AddHours(-$MaxFullAgeHours)) { $problems.Add("The last full backup is from $($lastFull.Stamp) (older than $MaxFullAgeHours hours).") }

    if ($MaxLogAgeMinutes -gt 0) {
        $lastLog = $files | Where-Object Type -eq "LOG" | Select-Object -Last 1
        if (-not $lastLog) { $problems.Add("No log backup found: point-in-time recovery is not protected.") }
        elseif ($lastLog.Stamp -lt $now.AddMinutes(-$MaxLogAgeMinutes)) { $problems.Add("The last log backup is from $($lastLog.Stamp) (older than $MaxLogAgeMinutes minutes).") }
    }

    $statusPath = Join-Path $BackupFolder "backup-status.json"
    if (Test-Path $statusPath) {
        $status = Get-Content $statusPath -Raw | ConvertFrom-Json
        foreach ($kind in $status.PSObject.Properties.Name) {
            $e = $status.$kind
            $failed = $e.PSObject.Properties.Name -contains "lastFailure"
            $okd = $e.PSObject.Properties.Name -contains "lastSuccess"
            if ($failed -and (-not $okd -or [datetime]$e.lastFailure -gt [datetime]$e.lastSuccess)) {
                $problems.Add("$kind failed at $($e.lastFailure): $($e.lastFailureDetail)")
            }
        }
        if ($MaxOffsiteAgeHours -gt 0) {
            $off = $status.PSObject.Properties.Name -contains "Offsite"
            if (-not $off) { $problems.Add("No off-machine copy has ever been recorded.") }
            elseif (-not ($status.Offsite.PSObject.Properties.Name -contains "lastSuccess") -or [datetime]$status.Offsite.lastSuccess -lt $now.AddHours(-$MaxOffsiteAgeHours)) {
                $problems.Add("The last successful off-machine copy is older than $MaxOffsiteAgeHours hours.")
            }
        }
    }
    elseif ($MaxOffsiteAgeHours -gt 0) { $problems.Add("No backup-status.json: no backup script has reported yet.") }
}
catch { $problems.Add("Health check could not run: $($_.Exception.Message)") }

if ($problems.Count -eq 0) { Write-Host "BACKUPS OK"; exit 0 }
$msg = "LIMS backup problem(s) for [$Database]: " + ($problems -join " | ")
$problems | ForEach-Object { Write-Host "PROBLEM: $_" -ForegroundColor Red }
Write-LimsEvent $msg "Error" 1900
Invoke-FailureHook $OnFailure $msg
exit 1
