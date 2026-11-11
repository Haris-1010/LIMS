<#
.SYNOPSIS
  Prepares the database for point-in-time recovery and creates the Windows Task Scheduler jobs for the whole backup routine.

.DESCRIPTION
  Run once, elevated, on the database server (SQL Server Agent does not exist on Express, so Task Scheduler is used).
  1. Sets the database to the FULL recovery model and takes a first full backup (needed before log backups work).
     Skipped with -SkipPrepare. Asks for confirmation unless -Confirm:$false.
  2. Registers these tasks (all named "LIMS Backup - ..."):
       Full          every day or week at -FullTime (default 22:00)
       Differential  every day at -DifferentialTime (default 13:00); use -NoDifferential to skip
       Log           every -LogMinutes minutes (default 15)
       Offsite       every 15 minutes, copies new files to -OffsiteDestination and checks them by hash (if given)
       Retention     daily at 03:00 with -Delete
       Health        every 30 minutes; writes an ERROR to the event log when backups are late or failed
  The tasks run the scripts in this folder, so keep it where it is. -RunAs defaults to SYSTEM; that account needs a SQL
  Server login with BACKUP rights on the database (for example db_backupoperator) and read access to the backup folder:
  see docs/BACKUP_RESTORE.md. -OnFailure names a script that receives the failure text (send an e-mail or SMS there).
  -Unregister removes the tasks. -WhatIf lists what would be created and changes nothing.

.EXAMPLE
  .\Register-LimsBackupTasks.ps1 -Database LIMS -BackupFolder D:\LimsBackups -OffsiteDestination E:\LimsOffsite,\\backup-pc\Lims `
      -ConfigFiles C:\inetpub\lims\appsettings.Production.json,D:\LimsData\keys -EncryptionCertificate LimsBackupCert
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = "High")]
param(
    [Parameter(Mandatory)][string]$BackupFolder,
    [string]$Database = "LIMS",
    [string]$Server = "localhost",
    [string[]]$OffsiteDestination = @(),
    [string[]]$ConfigFiles = @(),
    [string]$EncryptionCertificate,
    [string]$OnFailure,
    [ValidateSet("Daily", "Weekly")][string]$FullFrequency = "Daily",
    [string]$FullTime = "22:00",
    [string]$DifferentialTime = "13:00",
    [int]$LogMinutes = 15,
    [int]$RetentionDays = 14,
    [switch]$NoDifferential,
    [switch]$SkipPrepare,
    [switch]$NoPrompt,
    [string]$RunAs = "SYSTEM",
    [string]$TaskPrefix = "LIMS Backup",
    [switch]$Unregister
)
$ErrorActionPreference = "Stop"
if ($NoPrompt) { $ConfirmPreference = "None" }
. "$PSScriptRoot\LimsBackup.Common.ps1"
Assert-DbName $Database

$names = @("Full", "Differential", "Log", "Offsite", "Retention", "Health") | ForEach-Object { "$TaskPrefix - $_" }

if ($Unregister) {
    foreach ($n in $names) {
        if ((Get-ScheduledTask -TaskName $n -ErrorAction SilentlyContinue) -and $PSCmdlet.ShouldProcess($n, "Unregister scheduled task")) {
            Unregister-ScheduledTask -TaskName $n -Confirm:$false; Write-Host "Removed: $n"
        }
    }
    return
}

if ($LogMinutes -lt 5 -or $LogMinutes -gt 60) { throw "-LogMinutes must be between 5 and 60." }
if ($RetentionDays -lt 1 -or $RetentionDays -gt 365) { throw "-RetentionDays must be between 1 and 365." }
if (-not $WhatIfPreference -and -not (Test-Path -LiteralPath $BackupFolder)) { throw "Backup folder '$BackupFolder' does not exist. Create it and give the SQL Server service account Modify rights first." }

# ---- 1. recovery model and first full backup
if (-not $SkipPrepare) {
    $model = Invoke-SqlScalar $Server "SELECT recovery_model_desc FROM sys.databases WHERE name = $(SqlLiteral $Database)"
    if (-not $model) { throw "Database [$Database] not found on $Server." }
    if ($model -ne "FULL" -and $PSCmdlet.ShouldProcess("[$Database]", "Switch recovery model from $model to FULL (the transaction log will then grow until log backups run)")) {
        Invoke-SqlNonQuery $Server "ALTER DATABASE [$Database] SET RECOVERY FULL;"
        Write-Host "Recovery model of [$Database] set to FULL."
    }
    $fullArgs = @{ Server = $Server; Database = $Database; BackupFolder = $BackupFolder; Type = "Full" }
    if ($EncryptionCertificate) { $fullArgs.EncryptionCertificate = $EncryptionCertificate }
    if ($PSCmdlet.ShouldProcess("[$Database]", "Take a first full backup (starts the log chain)")) {
        $LASTEXITCODE = 0
        & "$PSScriptRoot\Backup-LimsDatabase.ps1" @fullArgs | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "The first full backup failed; tasks were not created." }
    }
}

# ---- 2. tasks
function Quote([string]$s) { '"' + $s.Replace('"', '\"') + '"' }
function ScriptCall([string]$Script, [hashtable]$Args2) {
    $parts = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (Quote (Join-Path $PSScriptRoot $Script)))
    foreach ($k in $Args2.Keys) {
        $v = $Args2[$k]
        if ($v -is [switch] -or $v -is [bool]) { if ($v) { $parts += "-$k" } }
        elseif ($v -is [array]) { $parts += "-$k"; $parts += (($v | ForEach-Object { Quote $_ }) -join ",") }
        elseif ($null -ne $v -and "$v" -ne "") { $parts += "-$k"; $parts += (Quote "$v") }
    }
    return $parts -join " "
}
function Add-Task([string]$Name, [string]$Arguments, $Trigger, [string]$Description) {
    if (-not $PSCmdlet.ShouldProcess($Name, "Register scheduled task")) { Write-Host "Would register: $Name"; return }
    $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument $Arguments
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 4) -MultipleInstances IgnoreNew -StartWhenAvailable
    $principal = New-ScheduledTaskPrincipal -UserId $RunAs -LogonType $(if ($RunAs -eq "SYSTEM") { "ServiceAccount" } else { "S4U" }) -RunLevel Highest
    try {
        Register-ScheduledTask -TaskName $Name -Action $action -Trigger $Trigger -Settings $settings -Principal $principal -Description $Description -Force -ErrorAction Stop | Out-Null
    }
    catch {
        throw "Could not register scheduled task '$Name': $($_.Exception.Message)"
    }
    Write-Host "Registered: $Name"
}
function Repeating([int]$Minutes) {
    $t = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval (New-TimeSpan -Minutes $Minutes)
    return $t
}

$common = @{ Database = $Database; BackupFolder = $BackupFolder }
$withFail = if ($OnFailure) { @{ OnFailure = $OnFailure } } else { @{} }
$backupBase = $common + @{ Server = $Server } + $withFail
if ($EncryptionCertificate) { $backupBase.EncryptionCertificate = $EncryptionCertificate }

$fullTrigger = if ($FullFrequency -eq "Weekly") {
    New-ScheduledTaskTrigger -Weekly -DaysOfWeek Monday -At $FullTime
} else {
    New-ScheduledTaskTrigger -Daily -At $FullTime
}
Add-Task "$TaskPrefix - Full" (ScriptCall "Backup-LimsDatabase.ps1" ($backupBase + @{ Type = "Full" })) $fullTrigger "$FullFrequency full backup of [$Database]"
if (-not $NoDifferential) {
    Add-Task "$TaskPrefix - Differential" (ScriptCall "Backup-LimsDatabase.ps1" ($backupBase + @{ Type = "Differential" })) (New-ScheduledTaskTrigger -Daily -At $DifferentialTime) "Midday differential backup of [$Database]"
}
Add-Task "$TaskPrefix - Log" (ScriptCall "Backup-LimsDatabase.ps1" ($backupBase + @{ Type = "Log" })) (Repeating $LogMinutes) "Transaction log backup of [$Database] every $LogMinutes minutes"
if ($OffsiteDestination.Count -gt 0) {
    $off = $common + @{ Destination = $OffsiteDestination } + $withFail
    if ($ConfigFiles.Count -gt 0) { $off.ConfigFiles = $ConfigFiles }
    Add-Task "$TaskPrefix - Offsite" (ScriptCall "Copy-LimsBackupOffsite.ps1" $off) (Repeating 15) "Copy new backups of [$Database] off the machine and verify them"
}
else { Write-Warning "No -OffsiteDestination given: backups stay on this machine, which does not protect against losing the machine or its disk." }
Add-Task "$TaskPrefix - Retention" (ScriptCall "Remove-OldBackups.ps1" ($common + @{ Delete = $true; Daily = $RetentionDays } + $withFail)) (New-ScheduledTaskTrigger -Daily -At "03:00") "Delete backups older than the retention rule"
Add-Task "$TaskPrefix - Health" (ScriptCall "Test-LimsBackupHealth.ps1" ($common + @{ MaxOffsiteAgeHours = $(if ($OffsiteDestination.Count -gt 0) { 26 } else { 0 }) } + $withFail)) (Repeating 30) "Report late or failed backups in the event log"

Write-Host "`nDone. Check with:  Get-ScheduledTask -TaskName '$TaskPrefix*' | Get-ScheduledTaskInfo"
