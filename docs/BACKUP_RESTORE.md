# Backup, restore and rollback

Scripts are in `db/scripts`. They run on the database server in **Windows PowerShell 5.1** with Windows authentication, by an account that can back up and restore (a SQL Server administrator for setup and drills; the scheduled jobs need only backup rights, see section 2).

**Status: written and exercised on the development PC against throw-away databases** (full, differential and log backups, encrypted with a certificate; restores to three different moments; a deleted log file detected; tampered off-machine copy detected; retention on 434 synthetic files). **Not yet exercised:** the Task Scheduler registration itself (only `-WhatIf` ran; the session was not elevated), the Windows event-log entries (the source "LIMS Backup" is created on the first elevated run), SQL Server Express (no compression), a network share as destination, the production SQL Server, and the timed restore drill at production data volume (needs the second machine; see `PHASE13_PLAN.md`).

## 1. What runs and when (scheduled-task alternative, after `Register-LimsBackupTasks.ps1`)
| Job | When | Script | What it does |
|---|---|---|---|
| Full | every night, 22:00 | `Backup-LimsDatabase.ps1 -Type Full` | whole database, `CHECKSUM`, `RESTORE VERIFYONLY` |
| Differential | every day, 13:00 | `-Type Differential` | changes since the last full |
| Log | every 15 minutes | `-Type Log` | transaction log; makes point-in-time recovery possible. **At most 15 minutes of work can be lost** (decision 49 default) |
| Offsite | every 15 minutes | `Copy-LimsBackupOffsite.ps1` | copies new files to the second disk / machine and proves each copy by SHA-256 |
| Retention | every night, 03:00 | `Remove-OldBackups.ps1 -Delete` | 14 daily, 8 weekly, 12 monthly full backups; differential and log backups of the last 14 days |
| Health | every 30 minutes | `Test-LimsBackupHealth.ps1` | writes an **Error** to the Windows event log (source "LIMS Backup", ids 1900-1904) when a full backup is over 26 h old, no log backup for 45 min, a job failed, or nothing was copied off the machine for 26 h |

File names: `<db>_<yyyyMMdd_HHmmss>_FULL.bak`, `_DIFF.bak`, `_LOG.trn`; a daily `<db>_config_<stamp>.zip` holds `appsettings.Production.json` and the key folder. Times in file names and in `-StopAt` are the server's local time.

Every script records its last success and failure in `<BackupFolder>\backup-status.json`, writes to the Windows Application event log, and can run `-OnFailure <script>` with the failure text, which is where an e-mail or SMS to the lab manager and the IT person goes. **Who is told and how is still an open decision (50)**: until it is made, failures are visible in the event log and in `backup-status.json` only. Point whatever monitoring the lab has at event source "LIMS Backup", level Error.

**Automatic backups by the application (default since 2026-11-11).** The application makes the backups itself; nothing has to be registered in Windows and no administrator rights are needed at run time. *Administration > Backup & restore* (System Administrator only) shows and changes the schedule:

| Setting | Default | Notes |
|---|---|---|
| Back up automatically | on | Also on for a new installation that has never been set up. `Backup:Automatic=false` in the settings file turns the scheduler off for the whole server (the tests do this). |
| Backup folder | `Backup:Folder`, else `<data folder>\backups` | Data folder = parent of `DataProtection:KeysPath` (`D:\LimsData`), else `App_Data` in the site. |
| Full backup | every day at 22:00 (or weekly, Monday) | A missed slot (site stopped, server off) is caught up as soon as the application runs again; a new installation gets its first full backup within about a minute of starting. |
| Log backups | every 15 minutes (off, 5, 10, 15, 30, 60) | Need the FULL recovery model; `Deploy-Lims.ps1` sets it. After a broken log chain the application takes a full backup first by itself. |
| Second copy folder | none | Each new file is copied and proved by SHA-256. |
| Days to keep | 14 | Keeps everything from the newest full backup that is at least that old, so every moment in the window can still be restored; never deletes the newest full backup. Applied to the second copy too. |

The schedule is saved in `<data folder>\backup-settings.json`. Saving it creates the folder and gives the SQL Server service (`NT SERVICE\MSSQLSERVER`, or `NT SERVICE\MSSQL$<instance>`) Modify on it; Windows allows this because the application created the folder. If Windows refuses (for example a folder an administrator made), the page shows the exact `icacls` command to run once. **Back up now** takes a full backup immediately. A failed job is retried after 10 minutes and shown on the page with its reason. Results are written to `backup-status.json` in the same format as the scripts, so the page and `Test-LimsBackupHealth.ps1` agree.

What the application needs (all set by `Deploy-Lims.ps1`): its database login in `db_backupoperator` (in `Grant-LimsAppPermissions.sql`; it can back up but not restore), Modify on the data folder, and an IIS application pool that is always running (*Start mode* AlwaysRunning, *Idle time-out* 0, *Preload* on), so backups run at night when no one uses the site. The application does not restore and does not run `RESTORE VERIFYONLY` when its login lacks the right for it (the `CHECKSUM` taken during the backup is then the check). Before every update `Deploy-Lims.ps1` takes a full backup with the same engine (`dotnet Lims.Api.dll db backup`), and `db migrate` takes one itself when there are migrations to apply (unless `--no-backup`).

The scheduled-task scripts below still work and use the same file names; use them only if IT prefers Task Scheduler, and then set `Backup:Automatic=false` so the two do not both run.

_Tested on the development PC (integration tests, least-privileged login): full and log backup, second copy, retention, status; schedule rules by unit tests. Not yet run under IIS on the production server._

## 2. Setting up the scheduled-task alternative (once, elevated PowerShell on the database server)
1. Create the backup folder on a different disk from the database (for example `D:\LimsBackups`) and give the SQL Server service account (`NT SERVICE\MSSQLSERVER`) **Modify**.
2. **Backup encryption (recommended; decision 50).** In `master`: `CREATE MASTER KEY ENCRYPTION BY PASSWORD = '...'; CREATE CERTIFICATE LimsBackupCert WITH SUBJECT = 'LIMS backups';` then **back up the certificate and its private key to a safe place that is not the server** (`BACKUP CERTIFICATE ... TO FILE ... WITH PRIVATE KEY (...)`). Without them, encrypted backups cannot be restored on a new server. Pass `-EncryptionCertificate LimsBackupCert` to the scripts.
3. If the jobs run as SYSTEM or a service account, give that account a SQL login with backup rights on the database: `CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS; USE LIMS; CREATE USER [NT AUTHORITY\SYSTEM] FOR LOGIN [NT AUTHORITY\SYSTEM]; ALTER ROLE db_backupoperator ADD MEMBER [NT AUTHORITY\SYSTEM];` The backup script also reads `msdb.dbo.backupset` (to check that a full backup exists), so add the same user to `msdb` with `db_datareader`. Test with a manual run first.
4. Register everything:
   ```powershell
   .\db\scripts\Register-LimsBackupTasks.ps1 -Database LIMS -BackupFolder D:\LimsBackups `
       -OffsiteDestination E:\LimsOffsite,\\backup-pc\LimsBackups `
       -ConfigFiles C:\inetpub\lims\appsettings.Production.json,D:\LimsData\keys `
       -EncryptionCertificate LimsBackupCert -OnFailure C:\LimsScripts\Notify.ps1
   ```
   It first switches the database to the **FULL recovery model** (the transaction log then grows until log backups run; it asks first) and takes a first full backup. `-WhatIf` shows the plan; `-Unregister` removes the jobs.
5. Check after the first day: `Get-ScheduledTask -TaskName 'LIMS Backup*' | Get-ScheduledTaskInfo`, `backup-status.json`, and a restore test (section 4).

SQL Server **Express** has no SQL Agent (hence Task Scheduler) and no backup compression; the scripts skip compression there. Express also limits database size to 10 GB: at the planned volume (decision 51) check this before choosing it.

## 3. Manual backup
```powershell
.\db\scripts\Backup-LimsDatabase.ps1 -Database LIMS -BackupFolder D:\LimsBackups                 # full (default)
.\db\scripts\Backup-LimsDatabase.ps1 -Database LIMS -BackupFolder D:\LimsBackups -Type Log
```
`Deploy-Lims.ps1` takes a full backup before every update. Log and differential backups refuse to run (with the reason) when the database is not in FULL recovery or has no full backup yet. **Retention preview**: `Remove-OldBackups.ps1 -BackupFolder D:\LimsBackups -Database LIMS` only lists what it would delete; `-Delete` deletes. It touches only files with the exact backup file-name pattern, never the newest full backup, and refuses to delete anything if no full backup exists or if the newest backup is over 3 days old (a sign that backups have stopped).

## 4. Restore test (monthly, and after any change to the backup set)
```powershell
.\db\scripts\Test-LimsRestore.ps1 -BackupFile D:\LimsBackups\LIMS_20261109_210000_FULL.bak -SourceDatabase LIMS
```
Restores one full backup into a new database (default `LIMS_RestoreTest`), runs `DBCC CHECKDB`, compares key row counts and drops it. It never overwrites a database. Verified on 2026-11-09 with the first version of the backup script. For the monthly drill also run a point-in-time restore (below) of a moment from the previous day and **time it**: that time is the real "back up and running" figure to compare with the agreed limit (default 2 hours, decision 49).

## 5. Point-in-time restore (recover to a chosen moment)
```powershell
.\db\scripts\Restore-LimsPointInTime.ps1 -BackupFolder D:\LimsBackups -SourceDatabase LIMS `
    -TargetDatabase LIMS_Recovered -StopAt "2026-11-12 10:41"
```
- Reads the backup files themselves (so it also works on the off-machine copy, on another server), picks the newest full that finished before `-StopAt`, the matching differential, and the log backups in order, **checks that no log backup is missing**, prints that plan, restores, runs `CHECKDB` and prints row counts (and the live database's, for comparison).
- `-PlanOnly` prints the plan and stops. Without `-StopAt` it restores to the last backup available.
- Always restores to a **new** name; refuses an existing one. If `-StopAt` is later than the last log backup it restores to that last log backup and says so.
- Encrypted backups need their certificate installed in `master` on the server doing the restore.
- If a log backup is missing, the script stops and names the file after the gap; recovery then reaches only as far as the gap (restore up to just before it, or find the file in the off-machine copy).

Also run on 2026-11-10 against the real development database `LIMS_Dev`: full backup with checksum and verify, then `Test-LimsRestore.ps1` (CHECKDB passed, row counts matched, test database dropped). That run found that a backup in the instance's default folder (under Program Files, not writable by an ordinary account) was reported as failed because the status file could not be written; fixed as described above.

Verified 2026-11-10 on a scratch database: `StopAt` before the differential gave 1 user, between two log backups 2 users, no `StopAt` 3 users (the live database had 3); a deliberately hidden log file was reported as a gap.

## 6. Real recovery (replacing the live database)
This is deliberately manual. Do it only with the lab manager's approval.

1. Stop the LIMS site in IIS so no one is writing.
2. If the database is still readable, take a final log backup (`-Type Log`) so the work since the last scheduled one is not lost.
3. Run `Restore-LimsPointInTime.ps1` (section 5) into a **new** name. Check the row counts and look at the latest orders with a read-only query.
4. Put it into service: rename the databases in SSMS (`LIMS` → `LIMS_old_<date>`, the recovered one → `LIMS`), or change `ConnectionStrings:Lims`. Re-run `Grant-LimsAppPermissions.sql` (a restored database keeps its users, but check) and confirm `/api/health`.
5. Start the site, sign in and check recent orders and payments. Keep `LIMS_old_<date>` until everyone is satisfied.
6. If the server itself is lost: install per `DEPLOYMENT.md`, restore the certificate (section 2 step 2), take `appsettings.Production.json` from the config archive, restore the database from the off-machine copy, and accept that everybody signs in again (the sign-in keys are machine-protected).

## 7. Schema upgrades and rollback
1. Back up first (`Deploy-Lims.ps1` does).
2. Apply migrations with `dotnet Lims.Api.dll db migrate`, or the reviewed idempotent script `db/scripts/migrate-idempotent.sql`.
3. **Rollback** means restoring the backup taken in step 1 (section 5 with `-StopAt` just before the update). Down-migrations are not used in production because they can drop data.

## 8. Not covered
Backup of the IIS configuration and certificates, Windows system state, and the lab's own network share or removable-drive routine belong to the lab's IT person. Where the off-machine copy physically is kept (so that one fire or theft cannot take both copies) is the owner's decision.
