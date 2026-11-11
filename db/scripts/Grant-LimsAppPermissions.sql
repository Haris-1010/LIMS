/*
  Least-privilege database access for the running application (IIS application pool).

  Run in SQLCMD mode (sqlcmd, or SSMS with Query > SQLCMD Mode) as a SQL administrator,
  AFTER the database exists and migrations were applied:

    sqlcmd -S localhost -E -C -d LIMS -v AppLogin="IIS APPPOOL\LimsPool" -i Grant-LimsAppPermissions.sql

  The login must already exist at server level. For an IIS application pool identity:
    CREATE LOGIN [IIS APPPOOL\LimsPool] FROM WINDOWS;
  The script is safe to run again (it is run after every migration that adds a schema or table).

  What the application login can do:  read and write data in the LIMS schemas.
  What it cannot do:                  change the database structure, back up, restore, or touch other databases.
  Append-only by database permission: audit.AuditLogs and inventory.StockMovements can be read and added to
                                      but never updated or deleted, whatever a bug or attacker tries.
                                      core.__EFMigrationsHistory is read-only.
*/
SET NOCOUNT ON;

IF USER_ID(N'$(AppLogin)') IS NULL
    CREATE USER [$(AppLogin)] FOR LOGIN [$(AppLogin)] WITH DEFAULT_SCHEMA = [core];
GO

IF DATABASE_PRINCIPAL_ID(N'lims_app') IS NULL
    CREATE ROLE [lims_app];
GO

ALTER ROLE [lims_app] ADD MEMBER [$(AppLogin)];
GO

-- Data access on every LIMS schema (schema-level grants also cover tables added later).
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[core]      TO [lims_app];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[security]  TO [lims_app];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[catalog]   TO [lims_app];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[patient]   TO [lims_app];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[lab]       TO [lims_app];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[billing]   TO [lims_app];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[audit]     TO [lims_app];
IF SCHEMA_ID(N'inventory') IS NOT NULL
    GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[inventory] TO [lims_app];
GO

-- Append-only tables and migration history. DENY wins over the schema-level GRANT above.
DENY UPDATE, DELETE ON OBJECT::[audit].[AuditLogs] TO [lims_app];
IF OBJECT_ID(N'[inventory].[StockMovements]') IS NOT NULL
    DENY UPDATE, DELETE ON OBJECT::[inventory].[StockMovements] TO [lims_app];
IF OBJECT_ID(N'[core].[__EFMigrationsHistory]') IS NOT NULL
    DENY INSERT, UPDATE, DELETE ON OBJECT::[core].[__EFMigrationsHistory] TO [lims_app];
GO

PRINT N'Application permissions granted to $(AppLogin).';
