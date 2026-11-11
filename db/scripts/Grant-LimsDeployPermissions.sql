/*
  Separate login for deployments and migrations (the person or scheduled job that runs Deploy-Lims.ps1).
  It may change the database structure; the running application may not (see Grant-LimsAppPermissions.sql).

    sqlcmd -S localhost -E -C -d LIMS -v DeployLogin="LABSERVER\lims-deploy" -i Grant-LimsDeployPermissions.sql

  The login must already exist at server level (CREATE LOGIN ... FROM WINDOWS).
  Backups use db_backupoperator-level rights; give them to the backup job's own account, not to this one.
*/
SET NOCOUNT ON;

IF USER_ID(N'$(DeployLogin)') IS NULL
    CREATE USER [$(DeployLogin)] FOR LOGIN [$(DeployLogin)];
GO

ALTER ROLE [db_owner] ADD MEMBER [$(DeployLogin)];
GO

PRINT N'Deployment permissions granted to $(DeployLogin).';
