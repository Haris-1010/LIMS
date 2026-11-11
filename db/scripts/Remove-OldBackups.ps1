<#
.SYNOPSIS
  Applies the backup retention rule: keep 14 daily, 8 weekly and 12 monthly full backups, and the differential and log
  backups of the last 14 days. SHOWS what it would delete; deletes only with -Delete.

.DESCRIPTION
  Only files whose names match exactly  <Database>_<yyyyMMdd_HHmmss>[_FULL|_DIFF|_LOG].(bak|trn)  in -BackupFolder
  (not sub-folders) are ever considered. Anything else in the folder is left alone.
  Full backups: the newest one of each of the last 14 days with a backup, then the newest of each of the next 8 weeks,
  then the newest of each of the next 12 months are kept. Everything older is a candidate.
  Differential and log backups older than the oldest kept DAILY full are candidates. Point-in-time recovery therefore
  reaches back 14 days; older fulls can restore only to their own moment.
  Safety rules: the script refuses to delete anything if the folder has no full backup, and never deletes the newest full
  backup or any differential/log file newer than it. It stops without deleting if the folder was not written to for
  3 days (a sign the backups themselves are not running) unless -Force is given.

.EXAMPLE
  .\Remove-OldBackups.ps1 -Database LIMS -BackupFolder D:\LimsBackups            # dry run: lists what would go
  .\Remove-OldBackups.ps1 -Database LIMS -BackupFolder D:\LimsBackups -Delete
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BackupFolder,
    [string]$Database = "LIMS",
    [int]$Daily = 14, [int]$Weekly = 8, [int]$Monthly = 12,
    [switch]$Delete,
    [switch]$Force,
    [string]$OnFailure
)
$ErrorActionPreference = "Stop"
. "$PSScriptRoot\LimsBackup.Common.ps1"

try {
    Assert-DbName $Database
    if (-not (Test-Path -LiteralPath $BackupFolder)) { throw "Backup folder '$BackupFolder' does not exist." }
    $files = @(Get-BackupFiles $Database $BackupFolder)
    $fulls = @($files | Where-Object Type -eq "FULL" | Sort-Object Stamp -Descending)
    if ($fulls.Count -eq 0) { throw "No full backup of [$Database] in '$BackupFolder': nothing will be deleted." }
    if (-not $Force -and $files[-1].Stamp -lt (Get-Date).AddDays(-3)) {
        throw "The newest backup is from $($files[-1].Stamp). Backups seem to have stopped; not deleting anything (use -Force to override)."
    }

    # Pick the newest full of each day, then each week, then each month, in that order.
    $keep = New-Object System.Collections.Generic.HashSet[string]
    $dayKeys = New-Object System.Collections.Generic.HashSet[string]
    $weekKeys = New-Object System.Collections.Generic.HashSet[string]     # weeks already represented by a kept full
    $monthKeys = New-Object System.Collections.Generic.HashSet[string]    # months already represented by a kept full
    $weeklyKept = 0; $monthlyKept = 0
    $oldestDailyKept = $fulls[0].Stamp
    foreach ($f in $fulls) {
        $day = $f.Stamp.ToString("yyyyMMdd")
        $week = $f.Stamp.Date.AddDays(-(([int]$f.Stamp.DayOfWeek + 6) % 7)).ToString("yyyyMMdd")   # the Monday of that week
        $month = $f.Stamp.ToString("yyyyMM")
        if ($dayKeys.Count -lt $Daily) {
            if ($dayKeys.Add($day)) { [void]$keep.Add($f.Path); $oldestDailyKept = $f.Stamp; [void]$weekKeys.Add($week); [void]$monthKeys.Add($month) }
            continue
        }
        if ($weeklyKept -lt $Weekly -and -not $weekKeys.Contains($week)) {
            [void]$weekKeys.Add($week); [void]$monthKeys.Add($month); [void]$keep.Add($f.Path); $weeklyKept++; continue
        }
        if ($monthlyKept -lt $Monthly -and -not $monthKeys.Contains($month)) {
            [void]$monthKeys.Add($month); [void]$keep.Add($f.Path); $monthlyKept++
        }
    }
    [void]$keep.Add($fulls[0].Path)   # the newest full is always kept

    $cutoff = $oldestDailyKept.Date
    $candidates = foreach ($f in $files) {
        if ($f.Type -eq "FULL") { if (-not $keep.Contains($f.Path)) { $f } }
        elseif ($f.Stamp -lt $cutoff -and $f.Stamp -lt $fulls[0].Stamp) { $f }
    }
    $candidates = @($candidates)
    $freed = ($candidates | Measure-Object Length -Sum).Sum

    Write-Host ("Folder {0}: {1} backup file(s); keeping {2}; {3} would be deleted ({4:N1} MB)." -f $BackupFolder, $files.Count, ($files.Count - $candidates.Count), $candidates.Count, ($freed / 1MB))
    $candidates | ForEach-Object { Write-Host "  $(if ($Delete) { 'deleting' } else { 'would delete' }): $($_.Name)" }

    if ($Delete -and $candidates.Count -gt 0) {
        foreach ($c in $candidates) { Remove-Item -LiteralPath $c.Path -Force }
        Set-BackupStatus $BackupFolder "Retention" $true "deleted $($candidates.Count) file(s), freed $([math]::Round($freed / 1MB, 1)) MB"
        Write-LimsEvent "Backup retention for [$Database] deleted $($candidates.Count) file(s) from $BackupFolder" "Information" 1004
    }
    elseif (-not $Delete) { Write-Host "Dry run only. Add -Delete to remove these files." }
}
catch {
    $msg = "Backup retention for [$Database] FAILED: $($_.Exception.Message)"
    Write-Host $msg -ForegroundColor Red
    try { Set-BackupStatus $BackupFolder "Retention" $false $_.Exception.Message } catch { }
    Write-LimsEvent $msg "Error" 1904
    Invoke-FailureHook $OnFailure $msg
    exit 1
}
