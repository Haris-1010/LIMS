# Shared helpers for the LIMS backup scripts. Dot-source it:  . "$PSScriptRoot\LimsBackup.Common.ps1"
# Uses System.Data.SqlClient (part of Windows PowerShell 5.1) with Windows authentication.

Set-StrictMode -Version 2

function Assert-DbName([string]$Name) {
    if ($Name -notmatch '^[A-Za-z0-9_]+$') { throw "Invalid database name '$Name' (letters, digits and underscore only)." }
}

function New-SqlConnection([string]$Server, [string]$Database = "master") {
    $cs = "Server=$Server;Database=$Database;Integrated Security=SSPI;TrustServerCertificate=True;Encrypt=True;Application Name=LIMS-backup"
    $c = New-Object System.Data.SqlClient.SqlConnection $cs
    $c.Open()
    return $c
}

# Runs a statement that returns no rows. No time limit (backups and restores can be long).
function Invoke-SqlNonQuery([string]$Server, [string]$Sql, [string]$Database = "master") {
    $c = New-SqlConnection $Server $Database
    try {
        $cmd = $c.CreateCommand(); $cmd.CommandText = $Sql; $cmd.CommandTimeout = 0
        [void]$cmd.ExecuteNonQuery()
    } finally { $c.Dispose() }
}

# Runs a statement and returns its first result set as objects (column name -> property).
function Invoke-SqlRows([string]$Server, [string]$Sql, [string]$Database = "master") {
    $c = New-SqlConnection $Server $Database
    try {
        $cmd = $c.CreateCommand(); $cmd.CommandText = $Sql; $cmd.CommandTimeout = 0
        $r = $cmd.ExecuteReader()
        $rows = New-Object System.Collections.Generic.List[object]
        while ($r.Read()) {
            $o = [ordered]@{}
            for ($i = 0; $i -lt $r.FieldCount; $i++) { $v = $r.GetValue($i); $n = $r.GetName($i); if (-not $n) { $n = "Column$i" }; $o[$n] = if ($v -is [DBNull]) { $null } else { $v } }
            $rows.Add([pscustomobject]$o)
        }
        $r.Dispose()
        return $rows.ToArray()   # callers wrap the call in @( ) so one row stays an array
    } finally { $c.Dispose() }
}

function Invoke-SqlScalar([string]$Server, [string]$Sql, [string]$Database = "master") {
    $rows = @(Invoke-SqlRows $Server $Sql $Database)
    if ($rows.Count -eq 0) { return $null }
    return $rows[0].PSObject.Properties.Value | Select-Object -First 1
}

function SqlLiteral([string]$Text) { return "N'" + $Text.Replace("'", "''") + "'" }

# --- Backup file naming ---------------------------------------------------------------
#   <Database>_<yyyyMMdd_HHmmss>_FULL.bak | _DIFF.bak | _LOG.trn   (older full backups: <Database>_<stamp>.bak)
function Get-BackupFileInfo([string]$Database, [System.IO.FileInfo]$File) {
    $m = [regex]::Match($File.Name, "^$([regex]::Escape($Database))_(\d{8})_(\d{6})(?:_(FULL|DIFF|LOG))?\.(bak|trn)$")
    if (-not $m.Success) { return $null }
    $type = if ($m.Groups[3].Success) { $m.Groups[3].Value } else { "FULL" }
    if ($type -eq "LOG" -ne ($m.Groups[4].Value -eq "trn")) { return $null }
    $stamp = [datetime]::ParseExact($m.Groups[1].Value + $m.Groups[2].Value, "yyyyMMddHHmmss", $null)
    return [pscustomobject]@{ Path = $File.FullName; Name = $File.Name; Type = $type; Stamp = $stamp; Length = $File.Length }
}

function Get-BackupFiles([string]$Database, [string]$Folder) {
    Get-ChildItem -LiteralPath $Folder -File -ErrorAction Stop |
        ForEach-Object { Get-BackupFileInfo $Database $_ } | Where-Object { $_ } | Sort-Object Stamp
}

# --- Status file and event log ----------------------------------------------------------
$script:EventSource = "LIMS Backup"

function Write-LimsEvent([string]$Message, [ValidateSet("Information", "Error", "Warning")][string]$Level = "Information", [int]$Id = 1000) {
    try {
        if (-not [System.Diagnostics.EventLog]::SourceExists($script:EventSource)) {
            New-EventLog -LogName Application -Source $script:EventSource -ErrorAction Stop
        }
        Write-EventLog -LogName Application -Source $script:EventSource -EntryType $Level -EventId $Id -Message $Message -ErrorAction Stop
    } catch {
        Write-Verbose "Windows event log not available: $($_.Exception.Message)"   # needs an elevated first run to create the source
    }
}

# Remembers the last success and last failure per kind (Full, Differential, Log, Offsite, Retention) in backup-status.json.
function Set-BackupStatus([string]$Folder, [string]$Kind, [bool]$Ok, [string]$Detail) {
  try {
    $path = Join-Path $Folder "backup-status.json"
    $status = @{}
    if (Test-Path $path) {
        try { (Get-Content $path -Raw | ConvertFrom-Json).PSObject.Properties | ForEach-Object { $status[$_.Name] = $_.Value } } catch { $status = @{} }
    }
    $entry = @{}
    if ($status.ContainsKey($Kind) -and $status[$Kind]) { $status[$Kind].PSObject.Properties | ForEach-Object { $entry[$_.Name] = $_.Value } }
    $now = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fff")
    if ($Ok) { $entry.lastSuccess = $now; $entry.lastDetail = $Detail } else { $entry.lastFailure = $now; $entry.lastFailureDetail = $Detail }
    $status[$Kind] = $entry
    $status | ConvertTo-Json -Depth 5 | Set-Content -Path $path -Encoding UTF8
  } catch {
    # A backup that worked must not be reported as failed because the status file could not be written (for example the
    # account running the script has no write access to the folder). The health check then reports the missing status.
    Write-Warning "Could not update backup-status.json in ${Folder}: $($_.Exception.Message)"
  }
}

# Runs a failure hook (e.g. a script that sends an e-mail or SMS) with the message as its only argument.
function Invoke-FailureHook([string]$Hook, [string]$Message) {
    if (-not $Hook) { return }
    try { & $Hook $Message } catch { Write-Warning "Failure hook failed: $($_.Exception.Message)" }
}
