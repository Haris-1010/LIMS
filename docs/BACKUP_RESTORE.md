# Backup, restore and rollback

Scripts are in `db/scripts`. They use `sqlcmd` with Windows authentication and must be run on the database server by an account that is a SQL Server administrator.

## Daily backup

```powershell
.\db\scripts\Backup-LimsDatabase.ps1 -Database LIMS -BackupFolder D:\LimsBackups
```

- Takes a full backup `WITH CHECKSUM`, then runs `RESTORE VERIFYONLY`. The script stops with an error if either step fails.
- File name: `LIMS_yyyyMMdd_HHmmss.bak`.
- Without `-BackupFolder`, the instance's default backup folder is used. That folder is normally under `C:\Program Files\…` and only SQL Server can read it, so prefer a dedicated folder such as `D:\LimsBackups`. Grant the SQL Server service account (e.g. `NT SERVICE\MSSQLSERVER`) **Modify** rights on it.
- **Schedule:** run it from Windows Task Scheduler at least daily, after closing time. (SQL Server Agent is not available on Express edition.)
- **Off-machine copy:** copy each backup to a second disk and a second machine or removable drive. A backup on the same disk as the database does not protect against disk failure.
- **Retention (recommended):** 14 daily, 8 weekly and 12 monthly backups.

Recommended for production (set up in Phase 13): full recovery model with transaction-log backups every 15–30 minutes, so at most that much work can be lost.

## Restore test (do this monthly)

```powershell
.\db\scripts\Test-LimsRestore.ps1 -BackupFile D:\LimsBackups\LIMS_20261109_210000.bak -SourceDatabase LIMS
```

1. **Refuses** if the target database (default `LIMS_RestoreTest`) already exists. It never overwrites a database.
2. Restores into new data/log files, runs `DBCC CHECKDB`, and compares row counts of key tables with the source.
3. Drops the test database afterwards. Pass `-Keep` to keep it for inspection.

**Verified 2026-11-09:**
- `LIMS_Dev` was backed up, verified, restored to `LIMS_RestoreTest`, and passed CHECKDB.
- Row counts matched the source, and the test database was dropped.
- Restoring over the existing `LIMS_Dev` was refused.

## Real recovery (replacing the live database)

This is deliberately manual. Do it only with the lab manager's approval.

1. Stop the LIMS site in IIS so no one is writing.
2. Take a final backup of the current (damaged) database, if possible.
3. Restore the chosen backup into a **new** name with the restore-test script and `-Keep`, then check it.
4. Rename the databases in SSMS (`LIMS` → `LIMS_old_<date>`, `LIMS_RestoreTest` → `LIMS`), or point the connection string at the restored database.
5. Start the site, sign in and check recent orders. Keep `LIMS_old_<date>` until everyone is satisfied.

## Schema upgrades and rollback

1. Back up first (above).
2. Apply migrations either way:
   - `dotnet Lims.Api.dll db migrate`, or
   - the reviewed idempotent script `db/scripts/migrate-idempotent.sql` in SSMS.
3. **Rollback** means restoring the backup taken in step 1. Down-migrations are not used in production because they can drop data.
