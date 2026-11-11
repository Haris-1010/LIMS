# Deployment on Windows Server (IIS)

_Status: **written and partly tested on the development PC. Not yet followed on a clean Windows Server**, so treat every step as unverified until the installation drill in `PHASE13_PLAN.md` (section 1, item 3) has been done and this note is replaced by its result._

What was actually exercised on the development PC: `dotnet publish` output and its `web.config`; the published build started in Production mode (Kestrel, not IIS) with correct security headers, Swagger off, log and key folders taken from configuration; `db migrate` → `db create-org` → `db create-admin` on an empty database; the `seed-demo` refusal in Production; the application running against a database login that has only the permissions below (whole test suite). **Not exercised:** IIS itself, the application pool identity, HTTPS binding, `Deploy-Lims.ps1` (syntax-checked only), Windows Server.

## 1. What you need
| Item | Notes |
|---|---|
| Windows Server with IIS | Features: Web Server, Static Content, Default Document, HTTP Errors, Request Filtering, HTTP Compression (static and dynamic). **No** ASP.NET 4.x feature is needed. |
| **.NET 9 Hosting Bundle** | Installs the runtime and the IIS module. Install **after** IIS; run `iisreset` afterwards. |
| SQL Server (same machine or LAN) | Edition and version are an open decision (48). Windows authentication is used; no SQL passwords. |
| A certificate for the name users type | From the lab's own authority or self-signed and installed on every PC (decision 46). The name is also used in `AllowedHosts`. |
| This repository's release package | Built on any PC, section 3. The server needs no Node.js and no internet. |

Names used below (change them): site `LIMS`, pool `LimsPool`, host name `lims.lab.local`, database `LIMS`, folders `C:\inetpub\lims` (site, read-only for the application) and `D:\LimsData` (keys and logs, writable).

## 2. One-time server setup
1. **Folders**
   ```powershell
   New-Item -ItemType Directory C:\inetpub\lims, D:\LimsData\keys, D:\LimsData\logs
   ```
2. **Application pool** (IIS Manager → Application Pools → Add): name `LimsPool`, *.NET CLR version: No Managed Code*, pipeline Integrated. Leave the identity as *ApplicationPoolIdentity*. Under *Advanced Settings* set *Idle Time-out* to 0 (the first request after an idle period is slow) and *Regular time interval* off, or at night.
3. **Permissions**
   ```powershell
   icacls C:\inetpub\lims   /grant "IIS APPPOOL\LimsPool:(OI)(CI)RX"
   icacls D:\LimsData       /grant "IIS APPPOOL\LimsPool:(OI)(CI)M"
   ```
   The application can read its files but not change them; it can write only keys and logs.
4. **Site**: add a website `LIMS`, physical path `C:\inetpub\lims`, pool `LimsPool`, an **https** binding for `lims.lab.local` with the certificate (and no plain http binding, or a redirect-only one). Port 443 must be reachable from the lab PCs and nothing else needs to be open to them.
5. **HTTPS redirect port**: if you keep an http binding, the application's redirect needs to know the https port. IIS supplies it from the binding; if the redirect does not happen, set the machine environment variable `HTTPS_PORT=443`.
6. **Database logins** (run once, as a SQL administrator; `sqlcmd` or SSMS):
   ```sql
   CREATE DATABASE LIMS;                                   -- collation: leave default; the app stores Urdu as nvarchar
   CREATE LOGIN [IIS APPPOOL\LimsPool] FROM WINDOWS;       -- the running application
   CREATE LOGIN [LABSERVER\lims-deploy] FROM WINDOWS;      -- whoever runs Deploy-Lims.ps1 (a real Windows account)
   ```
   After the first migration (section 5, step 3) and after any update that adds tables, run:
   ```powershell
   sqlcmd -S localhost -E -C -d LIMS -v AppLogin="IIS APPPOOL\LimsPool"    -i db\scripts\Grant-LimsAppPermissions.sql
   sqlcmd -S localhost -E -C -d LIMS -v DeployLogin="LABSERVER\lims-deploy" -i db\scripts\Grant-LimsDeployPermissions.sql
   ```
   The application login can read and write data but cannot change structure, back up or restore. **`audit.AuditLogs` and `inventory.StockMovements` are append-only by database permission**, and the migration history is read-only to it. (The Phase 13 test suite runs the whole application as such a login. A stock transfer used to update a ledger line after inserting it; that was changed so the ledger needs no updates.)
7. **Time zone**: the laboratory's clock is Asia/Karachi. Set the Windows time zone to *(UTC+05:00) Islamabad, Karachi* and keep time synchronised. Check `/api/health` `serverTimeUtc` against a reliable clock after installation. _(Not yet checked on Windows Server.)_

## 3. Build the release package (on a development PC)
```powershell
cd web;  npm ci;  npm run build;  cd ..          # writes src\Lims.Api\wwwroot
dotnet publish src\Lims.Api -c Release -o publish\lims-2026.11.10
```
The package includes `web.config` (in-process hosting, 2 MB request limit, no server banner, compression). Before every release also run `dotnet list package --vulnerable --include-transitive` and `cd web; npm audit --omit=dev`; both must be clean or the findings accepted in writing.

## 4. Settings file on the server
Copy `deploy\appsettings.Production.example.json` to `C:\inetpub\lims\appsettings.Production.json` and edit the server name, SQL server name, host name and folders. **No passwords**: the database uses the pool's Windows identity. This file is never in git and never in the package; `Deploy-Lims.ps1` leaves it alone and refuses a package that contains one.

| Setting | Meaning |
|---|---|
| `ConnectionStrings:Lims` | Database connection (integrated security). |
| `AllowedHosts` | The host name users type. Requests for other names are refused. |
| `Security:RequireHttps` | Redirect to HTTPS and send HSTS. **Keep true in production.** |
| `Auth:RequireHttpsCookie` | Session cookie only travels over HTTPS. **Keep true.** |
| `DataProtection:KeysPath` | Where sign-in keys live (outside the site folder). |
| `DataProtection:ProtectWithDpapi` | `true` encrypts the key files with the machine (Windows). Recommended; it is on in the example file. |
| `Logging:Path` | Daily application log files, kept 30 days. They hold request method, path (record numbers), status and timing, and errors; no request bodies, query strings, passwords or patient fields (tested; see section 6b). |
| `RateLimit:RequestsPerMinute` | Per-user safety limit (default 600). A busy user makes well under 100 requests a minute; the load test (`PERFORMANCE.md`) used up to 30 a minute per user. |
| `Backup:Folder` | The folder the backup jobs write to. Turns on the read-only **Backup status** page (Administration, System Administrator only). The application pool needs **Read** on this folder. Leave empty to show "not set up". |
| `Backup:Database` | Database name used in the backup file names (default `LIMS`). |
| `Backup:MaxFullAgeHours`, `Backup:MaxLogAgeMinutes`, `Backup:MaxOffsiteAgeHours`, `Backup:ExpectLogBackups`, `Backup:ExpectOffsite` | When the page calls a backup late (defaults 26 h, 45 min, 26 h, true, true; the same limits as `Test-LimsBackupHealth.ps1`). |

## 5. First installation
1. Publish (section 3) and copy the package folder to the server.
2. Create `appsettings.Production.json` (section 4).
3. Run as the deployment account, from an elevated PowerShell:
   ```powershell
   .\Deploy-Lims.ps1 -PackagePath C:\Release\lims-2026.11.10 -SiteFolder C:\inetpub\lims `
       -Database LIMS -SkipBackup -HealthUrl https://lims.lab.local/api/health
   ```
   It copies the files, applies all migrations, and checks `/api/health` (`database` and `schemaUpToDate` must both be true). Then run the two `Grant-…` scripts from section 2 step 6 **and re-run the health check**.
4. **Organisation and first administrator** (no demo data is ever created):
   ```powershell
   cd C:\inetpub\lims
   $env:ASPNETCORE_ENVIRONMENT = "Production"
   dotnet .\Lims.Api.dll db create-org LAB "Your Laboratory Name" "لیبارٹری کا نام"
   dotnet .\Lims.Api.dll db create-admin LAB admin "Administrator Name"
   ```
   The temporary password is printed once on the screen (not in the logs); the administrator must change it at first sign-in. Then set up branches, users, roles, tests, prices and report letterhead through the screens.
5. Back up the key folder and `appsettings.Production.json` with the database backups (see `BACKUP_RESTORE.md`).

## 6. Updating an installation
_The first update that includes migration `AddPerformanceIndexes` builds 21 indexes; at 550,000 orders that takes about half a minute and locks the tables while each is built. Do it outside opening hours._

```powershell
.\Deploy-Lims.ps1 -PackagePath C:\Release\lims-NEW -SiteFolder C:\inetpub\lims -Database LIMS `
    -BackupFolder E:\LimsBackups -HealthUrl https://lims.lab.local/api/health
```
Order inside the script: checks → full backup → site offline (`app_offline.htm`) → previous files kept in `C:\inetpub\lims.previous` → new files copied → `db migrate` → site online → health check → **PASS** or **FAIL** with the next step. If a migration adds tables, re-run `Grant-LimsAppPermissions.sql` (schema-level grants already cover new tables in existing schemas; it is only needed for a brand-new schema, but running it is harmless).

**Rollback**: `.\Deploy-Lims.ps1 -SiteFolder C:\inetpub\lims -Rollback -HealthUrl …` restores the previous files. It cannot undo migrations. If the update changed the database, restore the pre-update backup into a new database name and point `ConnectionStrings:Lims` at it; do not restore over the live database.

## 6b. Logs and personal data (log review, 2026-11-10)
**Application log** (`Logging:Path`): reviewed and tested with real requests (patient registration with name, CNIC, mobile, e-mail and address; patient search by name and by CNIC; duplicate check; wrong passwords; password change; a rejected registration). None of that text reaches the file: the request log writes only method, path, status code and time, and a test (`LogContentTests`) fails if that changes. Paths contain record numbers (for example `/api/patients/1042`) and sample barcodes, which identify a record only to someone who can open it in the system. SQL Server error messages quote data values (duplicate-key and truncation messages were shown to contain a CNIC and a patient name), so the application replaces the message of any exception chain containing a SQL error with its error number, keeps the stack trace, and the same test proves it. The log does not name the signed-in user or the address; that is the audit log's job (`/admin/audit`).

**IIS access log: not covered by the application, and it does contain personal data.** IIS records the full URL **including the query string** for every request, so patient searches (`?search=<name or CNIC>`, `fullName=…&mobile=…`) are written to `C:\inetpub\logs\LogFiles` in clear text, with the client address. Before go-live, in IIS Manager → site → *Logging* → *Select Fields*, **untick "URI Query (cs-uri-query)"**, and give the log folder to administrators only (the default allows the local Administrators group and SYSTEM; keep it that way). Set a retention (for example delete files older than 30 days with a scheduled task). _(Not yet done or verified on a server.)_

**Other places that can hold personal data:** database backups (encrypt them, `BACKUP_RESTORE.md`), the Windows event log (backup jobs write only database and file names), and crash dumps if Windows Error Reporting is on (the lab's IT person decides).

## 7. After installing: checks to run and record
- [ ] `https://lims.lab.local/api/health` shows `healthy`, `database: true`, `schemaUpToDate: true`; `http://` redirects to `https://`.
- [ ] Response headers on a page and on an API call include `Content-Security-Policy`, `Strict-Transport-Security`, `X-Frame-Options`, and (API) `Cache-Control: no-store`.
- [ ] Sign in as the administrator, change the password, create a branch and a user, sign in as that user from another PC.
- [ ] Register a patient, create an order and a payment, collect and receive the sample, enter, verify and approve a result, issue and print a report (including one Urdu report: the Nastaliq font must show).
- [ ] **Offline check**: open the browser's network list on a few pages; every request goes to `lims.lab.local`, none to another host.
- [ ] Upload a logo and a signature (images up to 300 KB / 200 KB).
- [ ] Restart the IIS site and the server: users are still signed in or are asked to sign in again, and nothing is lost.
- [ ] A database backup ran and its restore test passed (`BACKUP_RESTORE.md`).
- [ ] Windows event log and `D:\LimsData\logs` show no errors; search the log folder for a test patient's name and CNIC: no hits. In IIS logging, `cs-uri-query` is off.

## 8. If it does not start
| Symptom | Look at |
|---|---|
| HTTP Error 500.19 | The Hosting Bundle is missing or installed before IIS (repair it, `iisreset`), or `web.config` was edited. |
| HTTP Error 500.30 | The app failed to start: Windows Application event log; check the connection string and that the pool identity has a SQL login. |
| Health says `schemaUpToDate: false` | A migration was not applied: run `dotnet .\Lims.Api.dll db migrate` as the deployment account. |
| Everyone is signed out after each restart | The key folder is not writable, or the keys are not persisted (`DataProtection:KeysPath`). |
| 400 Bad Request / blank for some names | The name used is not in `AllowedHosts`. |
| Cookie not accepted | The site is opened over `http://` while `Auth:RequireHttpsCookie` is true. |
| 429 Too Many Requests | The per-user safety limit was reached; check for a runaway script or raise `RateLimit:RequestsPerMinute`. |

## 9. What this guide does not cover
Windows hardening, firewall rules, antivirus exclusions and certificate authority operation belong to the lab's IT person. The application needs only inbound 443 from the lab network and access to SQL Server. Backup scheduling, retention, off-machine copy and point-in-time restore are described in `BACKUP_RESTORE.md`; they were run only on the development PC, and the timed restore drill on the server is still to be done.
