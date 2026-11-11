<#
.SYNOPSIS
  Installs or updates LIMS on an IIS server: backup, take the site offline, copy files, migrate, bring it back, check health.

.DESCRIPTION
  Update (default):
    1. Checks the package and the existing site's appsettings.Production.json.
    2. Full database backup (skipped with -SkipBackup, e.g. first install).
    3. Puts app_offline.htm in the site so users see a maintenance page.
    4. Keeps the current files in <SiteFolder>.previous, then copies the package over the site.
       appsettings.Production.json, App_Data and logs are never overwritten or deleted.
    5. Applies database migrations (`db migrate`) with the account running this script (use the deployment login).
    6. Removes app_offline.htm and calls /api/health. PASS needs database=true and schemaUpToDate=true.
  If any step fails the site stays offline and the script says what to do. It never restores a database by itself.

  -Rollback puts the files from <SiteFolder>.previous back. It does NOT undo migrations: if the update changed the
  database, restore the backup taken at step 2 (docs/BACKUP_RESTORE.md) into a new database and switch the connection.

.EXAMPLE
  .\Deploy-Lims.ps1 -PackagePath C:\Release\lims-2026.11.10 -SiteFolder C:\inetpub\lims -Database LIMS `
      -BackupFolder E:\LimsBackups -HealthUrl https://lims.lab.local/api/health

.EXAMPLE
  .\Deploy-Lims.ps1 -SiteFolder C:\inetpub\lims -Rollback -HealthUrl https://lims.lab.local/api/health
#>
[CmdletBinding()]
param(
    [string]$PackagePath,
    [Parameter(Mandatory)][string]$SiteFolder,
    [string]$SqlServer = "localhost",
    [string]$Database = "LIMS",
    [string]$BackupFolder,
    [Parameter(Mandatory)][string]$HealthUrl,
    [switch]$SkipBackup,
    [switch]$Rollback,
    [int]$HealthTimeoutSeconds = 90
)
$ErrorActionPreference = "Stop"
$preserve = @("appsettings.Production.json", "App_Data", "logs", "app_offline.htm")
$offline = Join-Path $SiteFolder "app_offline.htm"
$previous = "$SiteFolder.previous"

function Step([string]$Text) { Write-Host "`n== $Text" -ForegroundColor Cyan }
function Fail([string]$Text) { Write-Host "`nFAIL: $Text" -ForegroundColor Red; exit 1 }

function Test-Health {
    $deadline = (Get-Date).AddSeconds($HealthTimeoutSeconds)
    $last = "no answer"
    do {
        try {
            $h = Invoke-RestMethod -Uri $HealthUrl -TimeoutSec 10
            $last = "status=$($h.status) database=$($h.database) schemaUpToDate=$($h.schemaUpToDate)"
            if ($h.database -eq $true -and $h.schemaUpToDate -eq $true) { return $true }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 3
    } while ((Get-Date) -lt $deadline)
    Write-Host "Last health answer: $last"
    return $false
}

function Copy-Site([string]$From, [string]$To) {
    $xd = $preserve | Where-Object { $_ -notlike "*.*" }                 # folders
    $xf = $preserve | Where-Object { $_ -like "*.*" }                    # files
    & robocopy $From $To /MIR /NFL /NDL /NJH /NJS /NP /XD @xd /XF @xf | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with code $LASTEXITCODE" }
    $global:LASTEXITCODE = 0
}

# ---------------------------------------------------------------- rollback
if ($Rollback) {
    Step "Rollback of files"
    if (-not (Test-Path $previous)) { Fail "No $previous folder: nothing to roll back to." }
    Set-Content -Path $offline -Value "<html><body><h2>LIMS is being restored. Please wait a few minutes.</h2></body></html>" -Encoding UTF8
    Copy-Site $previous $SiteFolder
    Remove-Item $offline -Force
    Write-Host "Files restored. Database changes (if the update had migrations) are NOT undone."
    if (Test-Health) { Write-Host "`nPASS: site answers after rollback." -ForegroundColor Green; exit 0 }
    Fail "Site did not become healthy after rollback. If the database is ahead of these files, restore the backup."
}

# ---------------------------------------------------------------- checks
Step "Checking"
if (-not $PackagePath) { Fail "-PackagePath is required (the folder made by `dotnet publish`)." }
if (-not (Test-Path (Join-Path $PackagePath "Lims.Api.dll"))) { Fail "$PackagePath does not contain Lims.Api.dll." }
if (-not (Test-Path (Join-Path $PackagePath "wwwroot\index.html"))) { Fail "$PackagePath has no wwwroot\index.html: build the web app before publishing." }
if (Test-Path (Join-Path $PackagePath "appsettings.Production.json")) { Fail "The package contains appsettings.Production.json. Settings belong only on the server; remove it from the package." }
if ($Database -notmatch '^[A-Za-z0-9_]+$') { Fail "Invalid database name '$Database'." }
$firstInstall = -not (Test-Path (Join-Path $SiteFolder "Lims.Api.dll"))
if (-not (Test-Path (Join-Path $SiteFolder "appsettings.Production.json"))) {
    Fail "Create $SiteFolder\appsettings.Production.json first (start from deploy\appsettings.Production.example.json)."
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail "dotnet was not found. Install the .NET 9 Hosting Bundle." }
Write-Host ($(if ($firstInstall) { "First installation." } else { "Update of an existing installation." }))

# ---------------------------------------------------------------- backup
if ($SkipBackup) { Write-Host "Backup skipped (-SkipBackup)." }
else {
    Step "Database backup"
    $backupScript = Join-Path $PSScriptRoot "..\db\scripts\Backup-LimsDatabase.ps1"
    if (-not (Test-Path $backupScript)) { $backupScript = Join-Path $PSScriptRoot "Backup-LimsDatabase.ps1" }
    if (-not (Test-Path $backupScript)) { Fail "Backup-LimsDatabase.ps1 not found next to this script or in ..\db\scripts." }
    $args2 = @{ Server = $SqlServer; Database = $Database }
    if ($BackupFolder) { $args2.BackupFolder = $BackupFolder }
    $global:LASTEXITCODE = 0
    & $backupScript @args2 | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail "Backup failed. Nothing was changed." }
}

# ---------------------------------------------------------------- copy
Step "Taking the site offline and copying files"
New-Item -ItemType Directory -Force -Path $SiteFolder | Out-Null
Set-Content -Path $offline -Value "<html><body style='font-family:sans-serif'><h2>LIMS is being updated. Please wait a few minutes.</h2></body></html>" -Encoding UTF8
try {
    if (-not $firstInstall) {
        Write-Host "Keeping the current files in $previous"
        New-Item -ItemType Directory -Force -Path $previous | Out-Null
        Copy-Site $SiteFolder $previous
    }
    Copy-Site $PackagePath $SiteFolder
} catch { Fail "Copy failed: $($_.Exception.Message). The site is still offline; run with -Rollback to restore the previous files." }

# ---------------------------------------------------------------- migrate
Step "Applying database migrations"
Push-Location $SiteFolder
try {
    $env:ASPNETCORE_ENVIRONMENT = "Production"
    & dotnet .\Lims.Api.dll db migrate
    if ($LASTEXITCODE -ne 0) { Fail "Migration failed (exit $LASTEXITCODE). Site left offline. Fix the cause and run again, or -Rollback for the files and restore the backup for the database." }
} finally { Pop-Location }

# ---------------------------------------------------------------- start
Step "Bringing the site back"
Remove-Item $offline -Force
if (Test-Health) {
    Write-Host "`nPASS: LIMS answers at $HealthUrl, database connected, schema up to date." -ForegroundColor Green
    if ($firstInstall) { Write-Host "First installation: now create the first administrator (docs/DEPLOYMENT.md, step 'First administrator')." }
    exit 0
}
Set-Content -Path $offline -Value "<html><body><h2>LIMS is temporarily unavailable.</h2></body></html>" -Encoding UTF8
Fail "The site did not become healthy within $HealthTimeoutSeconds seconds. It was put offline again. Check the logs folder and the Windows event log, then run again or use -Rollback."
