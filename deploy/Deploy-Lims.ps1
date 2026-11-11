<#
.SYNOPSIS
  Installs or updates LIMS on an IIS server: backup, take the site offline, copy files, migrate, set permissions,
  bring it back, check health.

.DESCRIPTION
  Run from an elevated PowerShell (Run as administrator) with an account that is a SQL Server administrator or has the
  deployment permissions (db\scripts\Grant-LimsDeployPermissions.sql).

  Update (default):
    1. Checks the package and the existing site's appsettings.Production.json.
    2. Full database backup with the new build's backup engine (`db backup`), into the folder set on the Backup page
       (or -BackupFolder). Skipped on a first installation and with -SkipBackup.
    3. Puts app_offline.htm in the site so users see a maintenance page.
    4. Keeps the current files in <SiteFolder>.previous, then copies the package over the site.
       appsettings.Production.json, App_Data and logs are never overwritten or deleted.
    5. Applies database migrations (`db migrate`) with the account running this script.
    6. Permissions, so nothing has to be done by hand (each step says what to run if it cannot do it):
         - the application pool may read the site and write its data folder (keys, logs, backups, settings);
         - the SQL Server service may write the backup folder;
         - the application pool's database login exists and has Grant-LimsAppPermissions.sql (data + db_backupoperator);
         - the database uses the FULL recovery model (needed for log backups);
         - the application pool is always running, so the automatic backups run even when nobody uses the site.
    7. Removes app_offline.htm and calls /api/health. PASS needs database=true and schemaUpToDate=true.
  If a required step fails the site stays offline and the script says what to do. It never restores a database by itself.
  The automatic backups themselves are made by the application (Administration > Backup & restore); no scheduled tasks.

  -Rollback puts the files from <SiteFolder>.previous back. It does NOT undo migrations: if the update changed the
  database, restore the backup taken at step 2 (docs/BACKUP_RESTORE.md) into a new database and switch the connection.

.EXAMPLE
  .\Deploy-Lims.ps1 -PackagePath C:\Release\lims-2026.11.10 -SiteFolder C:\inetpub\lims -HealthUrl https://lims.lab.local/api/health

.EXAMPLE
  .\Deploy-Lims.ps1 -SiteFolder C:\inetpub\lims -Rollback -HealthUrl https://lims.lab.local/api/health
#>
[CmdletBinding()]
param(
    [string]$PackagePath,
    [Parameter(Mandatory)][string]$SiteFolder,
    [Parameter(Mandatory)][string]$HealthUrl,
    [string]$SiteName = "LIMS",
    [string]$AppPool,
    [string]$SqlServer,
    [string]$Database,
    [string]$BackupFolder,
    [switch]$SkipBackup,
    [switch]$SkipPermissions,
    [switch]$Rollback,
    [int]$HealthTimeoutSeconds = 90
)
$ErrorActionPreference = "Stop"
$preserve = @("appsettings.Production.json", "App_Data", "logs", "app_offline.htm")
$offline = Join-Path $SiteFolder "app_offline.htm"
$previous = "$SiteFolder.previous"
$appcmd = Join-Path $env:windir "System32\inetsrv\appcmd.exe"
$script:warnings = New-Object System.Collections.Generic.List[string]

function Step([string]$Text) { Write-Host "`n== $Text" -ForegroundColor Cyan }
function Fail([string]$Text) { Write-Host "`nFAIL: $Text" -ForegroundColor Red; exit 1 }
function Warn([string]$Text) { Write-Host "WARNING: $Text" -ForegroundColor Yellow; $script:warnings.Add($Text) }

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

function Invoke-Lims([string]$Dll, [string[]]$Arguments) {
    Push-Location $SiteFolder     # content root = site folder, so the site's appsettings.Production.json is used
    try {
        $env:ASPNETCORE_ENVIRONMENT = "Production"
        $global:LASTEXITCODE = 0
        & dotnet $Dll @Arguments | Out-Host
        return $LASTEXITCODE
    } finally { Pop-Location }
}

function Invoke-Sql([string]$Server, [string]$Db, [string]$Sql) {
    $c = New-Object System.Data.SqlClient.SqlConnection "Server=$Server;Database=$Db;Integrated Security=SSPI;TrustServerCertificate=True;Encrypt=True;Application Name=LIMS-deploy"
    $c.Open()
    try {
        $cmd = $c.CreateCommand(); $cmd.CommandTimeout = 0; $cmd.CommandText = $Sql
        return $cmd.ExecuteScalar()
    } finally { $c.Dispose() }
}

function Grant-Folder([string]$Path, [string]$Account, [string]$Rights) {
    if (-not (Test-Path -LiteralPath $Path)) { New-Item -ItemType Directory -Force -Path $Path | Out-Null }
    & icacls $Path /grant "${Account}:(OI)(CI)$Rights" /Q | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "icacls could not give $Account $Rights on $Path" }
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
$PackagePath = (Resolve-Path $PackagePath).Path
$newDll = Join-Path $PackagePath "Lims.Api.dll"
if (-not (Test-Path $newDll)) { Fail "$PackagePath does not contain Lims.Api.dll." }
if (-not (Test-Path (Join-Path $PackagePath "wwwroot\index.html"))) { Fail "$PackagePath has no wwwroot\index.html: build the web app before publishing." }
if (Test-Path (Join-Path $PackagePath "appsettings.Production.json")) { Fail "The package contains appsettings.Production.json. Settings belong only on the server; remove it from the package." }
$firstInstall = -not (Test-Path (Join-Path $SiteFolder "Lims.Api.dll"))
$settingsFile = Join-Path $SiteFolder "appsettings.Production.json"
if (-not (Test-Path $settingsFile)) {
    Fail "Create $settingsFile first (start from deploy\appsettings.Production.example.json)."
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail "dotnet was not found. Install the .NET 9 Hosting Bundle." }
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { Warn "Not running as administrator: folder permissions and IIS settings cannot be set. Re-run from an elevated PowerShell." }

# What the server's settings say (database, folders).
try { $settings = Get-Content $settingsFile -Raw | ConvertFrom-Json } catch { Fail "$settingsFile is not valid JSON: $($_.Exception.Message)" }
$cs = $null
if ($settings.ConnectionStrings -and $settings.ConnectionStrings.Lims) { $cs = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $settings.ConnectionStrings.Lims }
if (-not $SqlServer) { $SqlServer = if ($cs -and $cs.DataSource) { $cs.DataSource } else { "localhost" } }
if (-not $Database) { $Database = if ($cs -and $cs.InitialCatalog) { $cs.InitialCatalog } else { "LIMS" } }
if ($Database -notmatch '^[A-Za-z0-9_]+$') { Fail "Invalid database name '$Database'." }
$keysPath = if ($settings.DataProtection -and $settings.DataProtection.KeysPath) { $settings.DataProtection.KeysPath } else { $null }
$dataFolder = if ($settings.Backup -and $settings.Backup.DataFolder) { $settings.Backup.DataFolder } elseif ($keysPath) { Split-Path $keysPath -Parent } else { Join-Path $SiteFolder "App_Data" }
$backupTarget = if ($BackupFolder) { $BackupFolder } else {
    $saved = Join-Path $dataFolder "backup-settings.json"
    if (Test-Path $saved) { try { (Get-Content $saved -Raw | ConvertFrom-Json).folder } catch { $null } } else { $null }
}
if (-not $backupTarget) { $backupTarget = if ($settings.Backup -and $settings.Backup.Folder) { $settings.Backup.Folder } else { Join-Path $dataFolder "backups" } }
$sqlIsLocal = $SqlServer -match "^(localhost|\.|\(local\)|127\.0\.0\.1|$([regex]::Escape($env:COMPUTERNAME)))(\\.*)?(,\d+)?$"

if (-not $AppPool -and (Test-Path $appcmd)) {
    $AppPool = (& $appcmd list app "$SiteName/" /text:applicationPool 2>$null | Select-Object -First 1)
    $global:LASTEXITCODE = 0
}
if (-not $AppPool) { $AppPool = "LimsPool" }
Write-Host ($(if ($firstInstall) { "First installation." } else { "Update of an existing installation." }))
Write-Host "Database [$Database] on $SqlServer; application pool '$AppPool'; data folder $dataFolder; backups $backupTarget"

# ---------------------------------------------------------------- backup
if ($SkipBackup) { Write-Host "Backup skipped (-SkipBackup)." }
elseif ($firstInstall) { Write-Host "First installation: nothing to back up yet." }
else {
    Step "Database backup"
    $code = Invoke-Lims $newDll @("db", "backup", "--folder", $backupTarget)
    if ($code -ne 0) { Fail "Backup failed (see the message above). Nothing was changed." }
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
$migrateArgs = @("db", "migrate")
if (-not $SkipBackup -and -not $firstInstall) { $migrateArgs += "--no-backup" }   # step 2 has just taken one
$code = Invoke-Lims (Join-Path $SiteFolder "Lims.Api.dll") $migrateArgs
if ($code -ne 0) { Fail "Migration failed (exit $code). Site left offline. Fix the cause and run again, or -Rollback for the files and restore the backup for the database." }

# ---------------------------------------------------------------- permissions
if ($SkipPermissions) { Write-Host "`nPermissions skipped (-SkipPermissions)." }
else {
    Step "Permissions and automatic backups"
    $poolAccount = "IIS APPPOOL\$AppPool"

    if ($isAdmin) {
        try { & icacls $SiteFolder /grant "${poolAccount}:(OI)(CI)RX" /Q | Out-Null; $global:LASTEXITCODE = 0; Write-Host "Site folder: $poolAccount may read." }
        catch { Warn "Could not give $poolAccount read access to ${SiteFolder}: $($_.Exception.Message)" }
        foreach ($f in @($dataFolder, $keysPath, $(if ($settings.Logging) { $settings.Logging.Path }), $backupTarget) | Where-Object { $_ } | Select-Object -Unique) {
            try { Grant-Folder $f $poolAccount "M"; Write-Host "${f}: $poolAccount may write." }
            catch { Warn "$($_.Exception.Message). Run: icacls `"$f`" /grant `"${poolAccount}:(OI)(CI)M`"" }
        }
    }

    # SQL Server service account: it writes the backup files.
    $sqlService = $null
    try {
        $svc = [string](Invoke-Sql $SqlServer "master" "SELECT @@SERVICENAME")
        $sqlService = if ($svc -eq "MSSQLSERVER") { "NT SERVICE\MSSQLSERVER" } else { "NT SERVICE\MSSQL`$$svc" }
    } catch { Warn "Could not connect to SQL Server $SqlServer as $env:USERNAME`: $($_.Exception.Message)" }
    if ($sqlService -and $sqlIsLocal -and $isAdmin) {
        try { Grant-Folder $backupTarget $sqlService "M"; Write-Host "${backupTarget}: $sqlService may write backups." }
        catch { Warn "$($_.Exception.Message). Run: icacls `"$backupTarget`" /grant `"${sqlService}:(OI)(CI)M`"" }
    } elseif (-not $sqlIsLocal) {
        Warn "SQL Server runs on another computer: the backup folder ($backupTarget) must exist there and be writable by its SQL Server service."
    }

    # Database login of the application pool, with data and backup rights.
    if ($sqlIsLocal) {
        $grant = @((Join-Path $SiteFolder "backup-scripts\Grant-LimsAppPermissions.sql"), (Join-Path $PSScriptRoot "..\db\scripts\Grant-LimsAppPermissions.sql")) |
            Where-Object { Test-Path $_ } | Select-Object -First 1
        try {
            if (-not $grant) { throw "Grant-LimsAppPermissions.sql not found in the site's backup-scripts folder." }
            $login = $poolAccount.Replace("]", "]]")
            Invoke-Sql $SqlServer "master" "IF SUSER_ID(N'$($poolAccount.Replace("'", "''"))') IS NULL CREATE LOGIN [$login] FROM WINDOWS;" | Out-Null
            $script = (Get-Content $grant -Raw).Replace('$(AppLogin)', $poolAccount)
            foreach ($batch in ($script -split '(?im)^\s*GO\s*$')) { if ($batch.Trim()) { Invoke-Sql $SqlServer $Database $batch | Out-Null } }
            Write-Host "Database login ${poolAccount}: data access and db_backupoperator granted."
        } catch {
            Warn "Could not set the database permissions of ${poolAccount}: $($_.Exception.Message). As a SQL administrator run: sqlcmd -S $SqlServer -E -C -d $Database -v 'AppLogin=`"$poolAccount`"' -i `"$grant`""
        }
    } else {
        Warn "SQL Server is remote: give the web server's machine account a login and run Grant-LimsAppPermissions.sql for it (docs/DEPLOYMENT.md)."
    }

    # Point-in-time recovery needs the FULL recovery model; the application's login may not switch it, so do it here.
    try {
        $model = [string](Invoke-Sql $SqlServer "master" "SELECT recovery_model_desc FROM sys.databases WHERE name = N'$Database'")
        if ($model -and $model -ne "FULL") {
            Invoke-Sql $SqlServer "master" "ALTER DATABASE [$Database] SET RECOVERY FULL;" | Out-Null
            Write-Host "Recovery model of [$Database] set to FULL (the application takes a new full backup within a few minutes)."
        }
    } catch { Warn "Could not set the FULL recovery model: $($_.Exception.Message). Log backups will report this on the Backup page." }

    # Keep the application running so automatic backups happen even when no one uses the site.
    if ($isAdmin -and (Test-Path $appcmd)) {
        try {
            if (Get-Command Get-WindowsFeature -ErrorAction SilentlyContinue) {
                if (-not (Get-WindowsFeature Web-AppInit).Installed) { Install-WindowsFeature Web-AppInit | Out-Null }
            } elseif (Get-Command Enable-WindowsOptionalFeature -ErrorAction SilentlyContinue) {
                Enable-WindowsOptionalFeature -Online -FeatureName IIS-ApplicationInit -NoRestart -All -ErrorAction Stop | Out-Null
            }
        } catch { Warn "IIS Application Initialization could not be installed ($($_.Exception.Message)); the site then starts on its first visit after a restart." }
        & $appcmd set apppool "$AppPool" /startMode:AlwaysRunning "/processModel.idleTimeout:00:00:00" | Out-Null
        $ok1 = $LASTEXITCODE -eq 0
        & $appcmd set app "$SiteName/" /preloadEnabled:true | Out-Null
        $ok2 = $LASTEXITCODE -eq 0
        $global:LASTEXITCODE = 0
        if ($ok1 -and $ok2) { Write-Host "Application pool '$AppPool' always running; site preloaded." }
        else { Warn "Could not set the application pool to always running (pool '$AppPool', site '$SiteName'). Set Start mode = AlwaysRunning, Idle time-out = 0 and Preload enabled = True in IIS Manager." }
    }
}

# ---------------------------------------------------------------- start
Step "Bringing the site back"
Remove-Item $offline -Force
if (Test-Health) {
    Write-Host "`nPASS: LIMS answers at $HealthUrl, database connected, schema up to date." -ForegroundColor Green
    if ($script:warnings.Count -gt 0) {
        Write-Host "`nDone, but $($script:warnings.Count) step(s) need attention:" -ForegroundColor Yellow
        $script:warnings | ForEach-Object { Write-Host " - $_" -ForegroundColor Yellow }
    }
    Write-Host "Automatic backups start by themselves within a few minutes; check Administration > Backup & restore."
    if ($firstInstall) { Write-Host "First installation: now create the organisation and first administrator (docs/DEPLOYMENT.md, section 5 step 4)." }
    exit 0
}
Set-Content -Path $offline -Value "<html><body><h2>LIMS is temporarily unavailable.</h2></body></html>" -Encoding UTF8
if ($script:warnings.Count -gt 0) { $script:warnings | ForEach-Object { Write-Host " - $_" -ForegroundColor Yellow } }
Fail "The site did not become healthy within $HealthTimeoutSeconds seconds. It was put offline again. Check the warnings above, the logs folder and the Windows event log, then run again or use -Rollback."
