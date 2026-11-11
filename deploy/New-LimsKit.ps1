<#
.SYNOPSIS
  Makes the offline installation kit (a folder to copy to a USB stick) on a development PC.

.DESCRIPTION
  Builds the web app and the API, and puts in one folder:
    Install.cmd           double-click on the target PC (asks for administrator rights)
    Install-Lims.ps1      the installer (see that file)
    Deploy-Lims.ps1       copy, database, permissions and health check (also used for updates)
    app\                  the published application
    installers\           put the two offline installers here (not downloaded by this script):
                            - .NET 9 Hosting Bundle:  dotnet-hosting-9.x.x-win.exe
                            - SQL Server Express:     SQLEXPR_x64_ENU.exe (SQL Server Express setup -> "Download Media" -> "Express Core")
  Installers already in -InstallersFrom are copied into the kit.

.EXAMPLE
  .\deploy\New-LimsKit.ps1 -Output D:\LimsKit -InstallersFrom D:\Downloads\lims-installers
#>
[CmdletBinding()]
param(
    [string]$Output = (Join-Path (Split-Path $PSScriptRoot -Parent) "kit"),
    [string]$InstallersFrom
)
$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent

# Build tools print warnings on stderr, which Windows PowerShell would treat as errors: show them, judge by exit code.
function Invoke-Native([string]$Exe, [string[]]$Arguments) {
    $saved = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try { & $Exe @Arguments 2>&1 | ForEach-Object { "$_" } | Out-Host } finally { $ErrorActionPreference = $saved }
    if ($LASTEXITCODE -ne 0) { throw "'$Exe $Arguments' failed (exit code $LASTEXITCODE)" }
}

if (Test-Path (Join-Path $Output "app")) { throw "$Output already contains a kit. Choose another -Output folder (an old kit is never overwritten)." }
New-Item -ItemType Directory -Force -Path (Join-Path $Output "installers") | Out-Null

Write-Host "== Building the web app" -ForegroundColor Cyan
Push-Location (Join-Path $repo "web")
try {
    # A clean install only when packages are missing: npm ci deletes node_modules, which breaks a running dev server.
    if (-not (Test-Path "node_modules")) { Invoke-Native "npm.cmd" @("ci") }
    Invoke-Native "npm.cmd" @("run", "build")
} finally { Pop-Location }

Write-Host "== Publishing the API" -ForegroundColor Cyan
Invoke-Native "dotnet" @("publish", (Join-Path $repo "src\Lims.Api"), "-c", "Release", "-o", (Join-Path $Output "app"))
if (Test-Path (Join-Path $Output "app\appsettings.Production.json")) { throw "The published app contains appsettings.Production.json; settings belong only on the target PC." }

Copy-Item (Join-Path $PSScriptRoot "Install-Lims.ps1"), (Join-Path $PSScriptRoot "Deploy-Lims.ps1") $Output
Set-Content -Path (Join-Path $Output "Install.cmd") -Encoding ASCII -Value @'
@echo off
rem Double-click to install or update LIMS on this PC. Windows asks for administrator rights.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Lims.ps1"
'@

if ($InstallersFrom) {
    Get-ChildItem $InstallersFrom -File | Where-Object { $_.Name -like "dotnet-hosting-9*.exe" -or $_.Name -like "SQLEXPR*.exe" } |
        Copy-Item -Destination (Join-Path $Output "installers")
}
$have = Get-ChildItem (Join-Path $Output "installers") -File | Select-Object -ExpandProperty Name
Write-Host "`nKit ready: $Output" -ForegroundColor Green
if (-not ($have -like "dotnet-hosting-9*.exe")) { Write-Host "Still needed in installers\: dotnet-hosting-9.x.x-win.exe (.NET 9 Hosting Bundle)" -ForegroundColor Yellow }
if (-not ($have -like "SQLEXPR*.exe")) { Write-Host "Still needed in installers\: SQLEXPR_x64_ENU.exe (only if the target PC has no SQL Server)" -ForegroundColor Yellow }
