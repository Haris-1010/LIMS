# Phase 13 plan: security review, performance, IIS deployment, backup and restore

_Status: **proposed, not started.** Nothing in this plan is built. It needs the owner's approval and the answers in section 8 before work begins. Written 2026-11-10 after reading the current code, scripts and docs (section 2 is what was actually found)._

## 1. Goal and what "done" means
Make the system safe and recoverable enough to run the laboratory on it: reviewed for security weaknesses, measured under realistic load, installable on the real Windows server by someone following written steps, and restorable after a failure within agreed limits.

Phase 13 is **done** only when all of these were *demonstrated*, not just written:
1. A security review report exists, every High finding is fixed and re-tested, and the remaining findings have an owner decision.
2. Measured response times at the agreed data volume meet the agreed targets (section 8), or the gaps are listed honestly.
3. The system was installed from the written guide **on a clean Windows Server machine** (not the development PC) and passed the post-install checks.
4. A restore drill from backups, including to a point in time, was timed and checked against the agreed recovery limits.

I cannot do 3 and 4 without a second machine (section 8). Until they are done, the status document will say "not verified on a server" rather than "ready".

## 2. Starting point (found by reading the repository, 2026-11-10)
**Already in place**
- Cookie sign-in (HttpOnly, SameSite=Strict, sliding 10 h), anti-forgery header on every state-changing call, login rate limit per IP (10 per minute), account lockout, PBKDF2 hashing, sessions re-checked against the database, deny-by-default authorisation (fallback policy) plus `[HasPermission]` on actions.
- Tenant isolation in three layers, tested; audit log of changes; uploads checked by their bytes; security headers `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`.
- Data Protection keys persisted to a folder (default `App_Data/keys` under the site); Serilog daily files kept 30 days; Swagger only in Development; no CORS (same-origin site).
- `/api/health`; `db migrate`, `db create-admin` commands; idempotent migration script; the app never migrates by itself at start-up.
- `Backup-LimsDatabase.ps1` (full backup WITH CHECKSUM + VERIFYONLY) and `Test-LimsRestore.ps1` (restore to a new name, CHECKDB, row counts, refuses to overwrite). Both were run successfully on 2026-11-09. `docs/BACKUP_RESTORE.md` already says log backups and a production schedule are "set up in Phase 13".
- No dependency on the internet at run time (fonts are bundled).

**Gaps and findings so far**
| # | Finding | Severity (proposed) |
|---|---|---|
| F1 | `npm audit` (production dependencies): **3 high** — `react-router` / `react-router-dom` (XSS via open redirects, unexpected external redirect) and `zod` (memory exhaustion on oversized collections). Fixes are available. NuGet packages: none vulnerable today. | High |
| F2 | No HTTPS handling: no HSTS, no redirect, no forwarded-headers handling; cookie `Secure` flag only if `Auth:RequireHttpsCookie` is set (default off); `AllowedHosts` is `*`. | High for a production LAN with patient data |
| F3 | No Content-Security-Policy and no `Cache-Control: no-store` on API responses carrying patient data. | Medium |
| F4 | Rate limiting exists only for sign-in; nothing limits other endpoints. | Low-Medium |
| F5 | Data Protection keys sit unencrypted in the site folder and are not in the backup procedure. Losing them signs everyone out; reading them allows forging session cookies. | Medium |
| F6 | The application's database account is not defined. If it runs as a SQL administrator, a flaw anywhere becomes a full database compromise. The audit log and the stock ledger are append-only only by convention in the code, not by database permission. | Medium-High |
| F7 | `db seed-demo` could be run against a production database by mistake (it creates demo data and a known organisation). | Medium |
| F8 | "Every action has authorisation" is enforced by convention; there is no test that fails when a new action forgets it. | Medium |
| F9 | No IIS artefacts at all: no `web.config` / publish profile, no deployment script, no deployment guide (a code comment refers to one that does not exist). | Blocking for go-live |
| F10 | Log files are written relative to the working directory, which under IIS is not a place we control; log content has not been reviewed for personal data. | Medium |
| F11 | Backups: full backups only. The development database is in FULL recovery model with no log backups (the log grows without limit and there is no point-in-time recovery). No retention clean-up, no off-machine copy script, no scheduled task, no backup of the key folder or production config, nobody is told if a backup fails. | High |
| F12 | Performance has never been measured above a few hundred rows. Places I expect to be slow at volume, **to be measured, not assumed**: the stock items list pages in memory, the "ready to report" list builds up to 1,000 orders before paging, the dashboard runs on every open page every minute, text search with `LIKE '%…%'` on patients and the audit log, branch filters expressed as `IN (…)`. | Unknown |
| F13 | Static files are served without long-lived caching or compression configured; the main script is about 680 kB (210 kB compressed). | Low |

## 3. Workstream A: security review
**A1. Fix what is already known**
- Update `react-router`, `react-router-dom` and `zod` (F1); re-run lint, type-check, build and all tests; record the versions.
- Make `dotnet list package --vulnerable --include-transitive` and `npm audit --omit=dev` a written routine (run before every release).

**A2. Close the code gaps**
- HTTPS: redirect, HSTS (only when HTTPS is on), forwarded headers behind IIS, `Auth:RequireHttpsCookie=true` in the production template, `AllowedHosts` set to the server name (F2).
- Add a Content-Security-Policy suited to a same-origin React build with no external hosts (checked against the built app so the screens still work), `Permissions-Policy`, and `Cache-Control: no-store` for `/api/*` (F3).
- Add a general per-user rate limit for the API as a safety net, with a generous limit so normal work never hits it (F4). Value to be confirmed after the load test.
- Protect the Data Protection keys (Windows DPAPI for the machine, folder outside the site folder, restricted permissions) and include them in the backup (F5).
- Guard `db seed-demo`: refuse unless explicitly told the database is a development one (F7).
- Log review: confirm no password, token, national ID, phone or result value is written to the log files; fix any that are (F10).

**A3. Make the rules testable**
- Test that fails if any controller action has neither `[HasPermission]` nor `[AllowAnonymous]` (F8).
- Tests for the new headers, the HTTPS cookie flag, the rate limit and the `seed-demo` guard.

**A4. Database permissions (F6)**
- Write the SQL to create a least-privilege login for the application (read, write, execute; no schema changes, no backup rights) and a separate deployment login for migrations.
- Deny `UPDATE`/`DELETE` on `audit.AuditLogs` and on `inventory.StockMovements` for the application login, so "append-only" is enforced by the database. Verify the whole test suite still passes when the tests run as that login.

**A5. Written review**
- `docs/SECURITY_REVIEW.md`: checklist of what was examined (authentication, session, authorisation, tenant isolation, input validation and uploads, error messages, headers, secrets, logging, dependencies, database permissions, backups), each finding with severity, fix, and the test that proves it.
- Self-run checks only: code review, the automated tests above, and manual probing of the running site. **An independent penetration test is not part of this phase** (decision 53).

## 4. Workstream B: performance
1. **Load-data generator** (`db seed-load`, refuses to run on anything but a database explicitly named for load testing): creates a realistic volume of patients, orders, invoices, payments, samples, results, reports, stock movements and audit lines. Volume comes from decision 51.
2. **Measurement tool**: a small console project under `tests/` that signs in a number of simulated users and times the main operations (patient search, new order, payments list, sample scan, result queue, result save, verification queue, reports list, report preview, dashboard, audit search, inventory list). Reports median, 95th percentile and errors. No new third-party tooling.
3. **Analysis**: for any operation above its target, read the actual query plan, find the cause, fix **only confirmed problems** (project rule), re-measure. Candidates are listed in F12.
4. **Indexes**: review the SQL Server missing-index information after the load run; add indexes in one migration `AddPerformanceIndexes` only where a measured slow query needs them.
5. **Static files**: long cache for fingerprinted assets, compression on (in IIS if available, else in the app); measure first-load time on the LAN.
6. **Result**: a table of before/after timings in the status document, and the volume it was measured at.

## 5. Workstream C: IIS deployment
Deliverables in a new `deploy/` folder and `docs/DEPLOYMENT.md`:
- Prerequisites list: Windows Server version, IIS features, .NET 9 Hosting Bundle, SQL Server, certificate.
- `dotnet publish` profile and `web.config` (in-process hosting), including the maximum upload size needed for logo and signature images.
- Application pool: no managed code, its own identity; folder permissions (site folder read-only; separate writable data folder for keys and logs, outside the site folder).
- `appsettings.Production.example.json` (committed, no secrets) → real `appsettings.Production.json` (already git-ignored): connection string with integrated security, absolute log and key paths, HTTPS cookie on.
- HTTPS on the LAN: certificate from the lab's own authority or a self-signed certificate installed on every PC (decision 46); binding and redirect.
- `Deploy-Lims.ps1`: put up `app_offline.htm`, take a backup, copy the published files, apply migrations with the deployment login, start, call `/api/health`, and print a clear pass or fail. Matching rollback steps.
- First installation steps: create the database, apply migrations, `db create-admin`, set the organisation, branches and settings through the screens. **No demo data.**
- Post-install checklist (sign in, create a patient, order, collect, result, report, print, check a backup ran) and an "update an existing installation" procedure.
- Offline check: confirm from the browser's network list that nothing is requested from outside the server; confirm the Asia/Karachi time zone works on Windows Server.
- Health: `/api/health` also reports the database connection and the schema version; scheduled check and who is notified (decision 50).

## 6. Workstream D: backup and restore
Extends the two scripts that exist:
- **Recovery model and log backups**: `Backup-LimsLog.ps1` every 15 minutes (target in decision 49), with the full backup nightly and optionally a differential at midday.
- **Scheduling**: `Register-LimsBackupTasks.ps1` creates the Task Scheduler jobs (SQL Server Agent is not available on Express edition; edition is decision 48). Jobs write a status file and the Windows event log; a failed or missing backup is visible.
- **Retention**: `Remove-OldBackups.ps1` keeps 14 daily, 8 weekly, 12 monthly backups; deletes only files matching the exact backup name pattern in the backup folder, and shows what it would delete before it deletes.
- **Off-machine copy**: script to copy each backup to a second disk or network share and verify the copy by checksum; encryption of backups at rest (decision 50).
- **Point-in-time restore**: `Restore-LimsPointInTime.ps1` restores the last full, differential and log backups up to a time given by the user, always to a **new** database name, then runs CHECKDB and row-count checks (never overwrites, like the existing script).
- **Everything that is not in the database**: the Data Protection keys and `appsettings.Production.json` go into the backup set (encrypted), with the matching restore steps.
- **Restore drill**: on the test server, restore at the load-test volume, time it, and compare with the agreed limits; repeat monthly (already in the existing document).
- Update `docs/BACKUP_RESTORE.md` to match, including the real-recovery procedure with the log chain.

## 7. Order of work and testing
1. A1, A2, A3 (small, high value, fully testable here) — then the full test suite.
2. A4 and C (artefacts), then **stop and install on the test server** (needs section 8).
3. D scripts, run on the test server; timed restore drill.
4. B on the test server at the agreed volume (so numbers reflect the real hardware, not the development PC).
5. A5 report, status document, decisions updated.

Automated tests cover what can be tested in code (headers, authorisation coverage, rate limit, guards, scripts' parameter checks). Scripts, IIS and backups are verified by running them on the test server and recording the output, as was done for the first backup scripts in Phase 2. Anything not run will be listed as not verified.

## 8. What I need from the owner (decisions 46-53, proposed defaults)
| # | Question | Proposed default |
|---|---|---|
| 46 | HTTPS on the LAN? | Yes, required, with a certificate from the lab's own authority or self-signed and installed on every PC |
| 47 | Hosting | IIS in-process on Windows Server with the .NET 9 Hosting Bundle |
| 48 | Production SQL Server edition and version | **Needed.** (The development PC has SQL Server 2022 Developer.) If Express: no SQL Agent, so Task Scheduler is used and backup compression/encryption options are limited |
| 49 | Recovery limits | Lose at most 15 minutes of work; be running again within 2 hours |
| 50 | Backup destination, encryption and who is told of a failure | A second disk plus a second machine or removable drive; encrypted; the lab manager and the IT person are told |
| 51 | Expected volume: patients and orders per day, branches, users working at the same time, years of data kept | Assumed 300 orders/day, 3 branches, 30 users at once, 5 years (about 550,000 orders). **Please correct** |
| 52 | Audit log retention | Keep everything for now; revisit when the table is large (the viewer is indexed by date and record) |
| 53 | External penetration test | Out of scope for this phase; recommended before going live if the system is reachable from outside the LAN |
| — | Performance targets | 95th percentile under 0.5 s for searches and lists, under 1.5 s for the dashboard, report preview and audit search, at the volume in 51 |
| — | A **second Windows machine or VM** that resembles the production server, for the installation and the restore drill | **Needed** to claim items 3 and 4 of section 1 |
| — | Server name / address users will type | Needed for the certificate and `AllowedHosts` |

## 9. Not in this phase
Two-factor sign-in, password expiry rules, single sign-on, email or SMS, high availability / a second server, antivirus configuration, network firewall and Windows hardening of the server (the lab's IT person owns these; the guide will list what the application needs open), the analyzer integration (Phase 14), and the open business decisions carried from the Phase 6 audit (cash refund role, supervisor closing an abandoned drawer, enforcing a discount limit). Those last items should be decided **before go-live**; they are listed in `IMPLEMENTATION_STATUS.md`.
