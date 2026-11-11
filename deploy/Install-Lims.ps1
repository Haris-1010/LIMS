<#
.SYNOPSIS
  One-step offline installation of LIMS on a single Windows PC or laptop (IIS + SQL Server Express on the same machine).

.DESCRIPTION
  Started by double-clicking Install.cmd in the kit made by New-LimsKit.ps1. It asks for administrator rights itself, then:
    1. turns on IIS (Windows features; no internet needed);
    2. installs the .NET 9 Hosting Bundle and SQL Server Express from the kit's "installers" folder when they are missing;
    3. creates the folders (site, data, licence) and the IIS application pool and website;
    4. writes appsettings.Production.json (plain http on this PC, Windows sign-in to SQL Server, no passwords) if there is none;
    5. runs Deploy-Lims.ps1: copies the application, creates/updates the database, sets every permission, checks health;
    6. on a first installation, creates the laboratory and the first administrator (the temporary password is shown once);
    7. puts a "LIMS" shortcut on the desktop and opens the browser at the licence activation screen.
  Running it again later updates the installation (Deploy-Lims.ps1 takes a database backup first).

  For a server used by several PCs over HTTPS, follow docs/DEPLOYMENT.md instead.

.EXAMPLE
  .\Install-Lims.ps1
.EXAMPLE
  .\Install-Lims.ps1 -LabName "City Diagnostic Lab" -LabNameUr "سٹی ڈائیگناسٹک لیب" -AdminUser admin -AdminName "Dr. Ahmed"
#>
[CmdletBinding()]
param(
    [string]$LabName,
    [string]$LabNameUr,
    [ValidatePattern('^[A-Za-z0-9]{2,10}$')][string]$LabCode = "LAB",
    [string]$AdminUser = "admin",
    [string]$AdminName,
    [int]$Port = 80,
    [string]$SiteFolder = "C:\inetpub\lims",
    [string]$DataFolder = "C:\LimsData",
    [string]$SiteName = "LIMS",
    [string]$AppPool = "LimsPool"
)
$ErrorActionPreference = "Stop"
$kit = $PSScriptRoot
$appFolder = Join-Path $kit "app"
$installers = Join-Path $kit "installers"
$licenseFolder = Join-Path $env:ProgramData "HARTLIMS"
$appcmd = Join-Path $env:windir "System32\inetsrv\appcmd.exe"

function Step([string]$Text) { Write-Host "`n== $Text" -ForegroundColor Cyan }
function Done([string]$Text) { Write-Host "   $Text" -ForegroundColor Green }
function Stop-Install([string]$Text) {
    Write-Host "`nINSTALLATION STOPPED: $Text" -ForegroundColor Red
    Read-Host "`nPress Enter to close"
    exit 1
}

# ---------------------------------------------------------------- administrator rights (asks once, then re-runs itself)
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    $pass = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`"")
    foreach ($p in $PSBoundParameters.GetEnumerator()) { $pass += "-$($p.Key)"; $pass += "`"$($p.Value)`"" }
    Start-Process powershell.exe -Verb RunAs -ArgumentList $pass
    exit 0
}

Write-Host "LIMS installation" -ForegroundColor Cyan
if (-not (Test-Path (Join-Path $appFolder "Lims.Api.dll"))) { Stop-Install "The 'app' folder is missing next to this script. Make the kit again with New-LimsKit.ps1." }
Get-ChildItem $kit -Recurse -File | Unblock-File -ErrorAction SilentlyContinue   # files copied from another PC may be marked "blocked"
$firstInstall = -not (Test-Path (Join-Path $SiteFolder "Lims.Api.dll"))

if ($firstInstall) {
    if (-not $LabName) { $LabName = Read-Host "Laboratory name (English)" }
    if (-not $LabName.Trim()) { Stop-Install "A laboratory name is needed." }
    if (-not $PSBoundParameters.ContainsKey("LabNameUr")) { $LabNameUr = Read-Host "Laboratory name in Urdu (optional, press Enter to skip)" }
    if (-not $AdminName) { $AdminName = Read-Host "Administrator's full name" }
    if (-not $AdminName.Trim()) { $AdminName = "Administrator" }
}

# ---------------------------------------------------------------- 1. IIS
Step "1/7 Turning on IIS"
$iisFeatures = @("IIS-WebServerRole", "IIS-WebServer", "IIS-CommonHttpFeatures", "IIS-StaticContent", "IIS-DefaultDocument", "IIS-HttpErrors",
    "IIS-RequestFiltering", "IIS-Security", "IIS-HttpCompressionStatic", "IIS-HttpCompressionDynamic", "IIS-Performance",
    "IIS-WebServerManagementTools", "IIS-ManagementConsole", "IIS-ApplicationInit")
$serverFeatures = @("Web-Server", "Web-Static-Content", "Web-Default-Doc", "Web-Http-Errors", "Web-Filtering", "Web-Stat-Compression",
    "Web-Dyn-Compression", "Web-Mgmt-Console", "Web-AppInit")
try {
    if (Get-Command Install-WindowsFeature -ErrorAction SilentlyContinue) {
        Install-WindowsFeature $serverFeatures | Out-Null                     # Windows Server
    } else {
        foreach ($f in $iisFeatures) {                                       # Windows 10 / 11
            $state = (Get-WindowsOptionalFeature -Online -FeatureName $f -ErrorAction SilentlyContinue).State
            if ($state -ne "Enabled") { Enable-WindowsOptionalFeature -Online -FeatureName $f -All -NoRestart | Out-Null }
        }
    }
} catch { Stop-Install "IIS could not be turned on: $($_.Exception.Message)" }
if (-not (Test-Path $appcmd)) { Stop-Install "IIS is not available after turning it on. Restart the computer and run Install.cmd again." }
Done "IIS is on."

# ---------------------------------------------------------------- 2. .NET Hosting Bundle (after IIS, so its IIS module registers)
Step "2/7 .NET 9 Hosting Bundle"
$env:Path = "$env:ProgramFiles\dotnet;$env:Path"
$hasAspNet9 = $false
if (Get-Command dotnet -ErrorAction SilentlyContinue) { $hasAspNet9 = [bool](& dotnet --list-runtimes 2>$null | Select-String "Microsoft.AspNetCore.App 9\.") }
$hasModule = Test-Path (Join-Path $env:ProgramFiles "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll")
if ($hasAspNet9 -and $hasModule) { Done "Already installed." }
else {
    $bundle = Get-ChildItem $installers -Filter "dotnet-hosting-9*.exe" -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
    if (-not $bundle) { Stop-Install "Put the .NET 9 Hosting Bundle (dotnet-hosting-9....-win.exe) in $installers" }
    Write-Host "   Installing $($bundle.Name) (a few minutes)..."
    $p = Start-Process $bundle.FullName -ArgumentList "/install", "/quiet", "/norestart" -Wait -PassThru
    if ($p.ExitCode -notin 0, 3010) { Stop-Install "The Hosting Bundle installer failed (exit code $($p.ExitCode))." }
    & iisreset /noforce | Out-Null
    Done "Installed."
}

# ---------------------------------------------------------------- 3. SQL Server
Step "3/7 SQL Server"
$sqlServer = $null
if (Get-Service 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue) { $sqlServer = ".\SQLEXPRESS" }
elseif (Get-Service 'MSSQLSERVER' -ErrorAction SilentlyContinue) { $sqlServer = "localhost" }
if ($sqlServer) { Done "Found SQL Server ($sqlServer)." }
else {
    $sql = Get-ChildItem $installers -Filter "SQLEXPR*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $sql) { Stop-Install "SQL Server is not installed and no SQL Server Express package (SQLEXPR_x64_ENU.exe) is in $installers" }
    Write-Host "   Installing SQL Server Express (10-20 minutes, the window may look idle)..."
    # Administrators of this PC become SQL administrators (the installer and Deploy-Lims.ps1 use Windows sign-in).
    $sqlArgs = @("/Q", "/ACTION=Install", "/FEATURES=SQLEngine", "/INSTANCENAME=SQLEXPRESS", "/SQLSYSADMINACCOUNTS=`"BUILTIN\Administrators`"",
        "/TCPENABLED=0", "/UPDATEENABLED=0", "/IACCEPTSQLSERVERLICENSETERMS")
    $p = Start-Process $sql.FullName -ArgumentList $sqlArgs -Wait -PassThru
    if ($p.ExitCode -notin 0, 3010) { Stop-Install "SQL Server Express setup failed (exit code $($p.ExitCode)). Its log is under C:\Program Files\Microsoft SQL Server\*\Setup Bootstrap\Log." }
    $sqlServer = ".\SQLEXPRESS"
    Done "Installed."
}
Set-Service -Name ($(if ($sqlServer -eq "localhost") { "MSSQLSERVER" } else { 'MSSQL$SQLEXPRESS' })) -StartupType Automatic
Start-Service -Name ($(if ($sqlServer -eq "localhost") { "MSSQLSERVER" } else { 'MSSQL$SQLEXPRESS' })) -ErrorAction SilentlyContinue

# ---------------------------------------------------------------- 4. folders, pool, site
Step "4/7 Folders and IIS website"
foreach ($f in @($SiteFolder, "$DataFolder\keys", "$DataFolder\logs", "$DataFolder\backups", $licenseFolder)) {
    New-Item -ItemType Directory -Force -Path $f | Out-Null
}
$poolAccount = "IIS APPPOOL\$AppPool"
& $appcmd list apppool "$AppPool" 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { & $appcmd add apppool /name:"$AppPool" /managedRuntimeVersion:"" /managedPipelineMode:Integrated | Out-Null }
& $appcmd set apppool "$AppPool" /managedRuntimeVersion:"" "/processModel.idleTimeout:00:00:00" | Out-Null
& $appcmd list site "$SiteName" 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    # A fresh IIS has "Default Web Site" on port 80: stop it so LIMS can answer there.
    $busy = & $appcmd list site /bindings:"http/*:${Port}:" 2>$null
    if ($busy) {
        foreach ($line in $busy) { if ($line -match '^SITE "([^"]+)"') { & $appcmd stop site /site.name:"$($Matches[1])" | Out-Null; Write-Host "   Stopped '$($Matches[1])' (it used port $Port)." } }
    }
    & $appcmd add site /name:"$SiteName" /physicalPath:"$SiteFolder" /bindings:"http/*:${Port}:" | Out-Null
    & $appcmd set app "$SiteName/" /applicationPool:"$AppPool" | Out-Null
}
$global:LASTEXITCODE = 0
# The licence folder is outside the website; the application must be able to save the licence there.
& icacls $licenseFolder /grant "${poolAccount}:(OI)(CI)M" /Q | Out-Null
& icacls $DataFolder /grant "${poolAccount}:(OI)(CI)M" /Q | Out-Null
$global:LASTEXITCODE = 0
Done "Site '$SiteName' on port $Port, pool '$AppPool', data in $DataFolder."

# ---------------------------------------------------------------- 5. settings
Step "5/7 Settings"
$settingsFile = Join-Path $SiteFolder "appsettings.Production.json"
if (Test-Path $settingsFile) { Done "Keeping the existing $settingsFile." }
else {
    $settings = [ordered]@{
        ConnectionStrings = @{ Lims = "Server=$sqlServer;Database=LIMS;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True" }
        AllowedHosts      = "*"
        # Plain http on this PC (no certificate). For several PCs over the network use HTTPS (docs/DEPLOYMENT.md).
        Security          = @{ RequireHttps = $false }
        Auth              = @{ RequireHttpsCookie = $false; SessionHours = 10 }
        DataProtection    = @{ KeysPath = "$DataFolder\keys"; ProtectWithDpapi = $true }
        Logging           = @{ Path = "$DataFolder\logs" }
        Backup            = @{ Folder = "$DataFolder\backups" }
        License           = @{ LicenseDirectory = $licenseFolder }
    }
    $json = $settings | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText($settingsFile, $json, (New-Object Text.UTF8Encoding $false))
    Done "Written $settingsFile"
}

# ---------------------------------------------------------------- 6. application + database (Deploy-Lims.ps1 does copy, migrate, permissions, health)
Step "6/7 Installing the application and database"
$deploy = Join-Path $kit "Deploy-Lims.ps1"
& $deploy -PackagePath $appFolder -SiteFolder $SiteFolder -HealthUrl "http://localhost:$Port/api/health" -SiteName $SiteName -AppPool $AppPool -SqlServer $sqlServer -Database "LIMS"
if ($LASTEXITCODE -ne 0) { Stop-Install "Deploy-Lims.ps1 reported a problem (see above)." }

# ---------------------------------------------------------------- 7. laboratory, administrator, shortcut
Step "7/7 Laboratory and administrator"
if ($firstInstall) {
    Push-Location $SiteFolder
    try {
        $env:ASPNETCORE_ENVIRONMENT = "Production"
        $orgArgs = @("Lims.Api.dll", "db", "create-org", $LabCode, $LabName.Trim())
        if ($LabNameUr -and $LabNameUr.Trim()) { $orgArgs += $LabNameUr.Trim() }
        & dotnet @orgArgs
        if ($LASTEXITCODE -ne 0) { Write-Host "   The laboratory was not created (it may already exist)." -ForegroundColor Yellow }
        Write-Host "`n   WRITE DOWN the temporary password shown below. It is shown only once." -ForegroundColor Yellow
        & dotnet Lims.Api.dll db create-admin $LabCode $AdminUser $AdminName
        if ($LASTEXITCODE -ne 0) { Write-Host "   The administrator was not created (the name may already exist)." -ForegroundColor Yellow }
    } finally { Pop-Location }
} else { Done "Existing installation updated; laboratory and users unchanged." }

$url = if ($Port -eq 80) { "http://localhost/" } else { "http://localhost:$Port/" }
try {
    $desktop = [Environment]::GetFolderPath("CommonDesktopDirectory")
    Set-Content -Path (Join-Path $desktop "LIMS.url") -Value "[InternetShortcut]`r`nURL=$url" -Encoding ASCII
} catch { }

Write-Host "`nLIMS is installed: $url" -ForegroundColor Green
if ($firstInstall) {
    Write-Host "Next: the browser opens the activation screen. Copy the Installation ID, send it for a licence,"
    Write-Host "install the licence file there, then sign in as '$AdminUser' with the temporary password above."
}
Start-Process $url
Read-Host "`nPress Enter to close"
