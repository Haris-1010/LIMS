# Implementation status

_Last updated: 2026-11-10_

| Phase | Scope | Status |
|---|---|---|
| 1 | Inspection, architecture, design system, login + dashboard prototype | **Done** (prototype) |
| 2 | Database: ERD, migrations, seed data | **Done** (foundation; module tables added per phase) |
| 3 | Auth, users, roles, permissions, organisations, branches | **Done** |
| 4 | Patients and doctors | **Done** |
| 5 | Test catalog, parameters, packages, reference configuration | **Done** |
| 6 | Orders, invoices, payments, receipts, cash drawer | **Done** |
| 7 | Sample collection, barcodes, tracking | **Done** (see Phase 7 below, with limitations) |
| 8 | Worklists, result entry, verification, critical alerts | **Done** (see Phase 8 below, with limitations) |
| 9 | Bilingual reports, PDF, printing | **Done** (see Phase 9 below, with limitations; no server-side PDF) |
| 10 | Corporate billing and reconciliation | **Done** (see Phase 10 below, with limitations) |
| 11 | Inventory and stock ledger | **Done** (see Phase 11 below, with limitations) |
| 12 | Live dashboard, audit viewer, system settings | **Done** (see Phase 12 below, with limitations) |
| 13 | Security review, performance, IIS deployment, backup/restore | **Everything that can be done on one development PC is done and tested; not complete.** Still open: installing from the guide on a clean Windows Server, the timed restore drill, real Task Scheduler registration, HTTPS/IIS settings, and repeating the load test on server hardware (all need a second machine or an elevated session; see below) |
| 14 | Analyzer integration (needs device specs) | Blocked: needs device model, protocol and interface documentation |

## Phase 13: progress (plan: `PHASE13_PLAN.md`)

**Done and tested (workstream A1-A3):**
- F1: `react-router`, `react-router-dom` and `zod` updated via `npm audit fix`; `npm audit --omit=dev` now reports 0 vulnerabilities; lint and build clean.
- F2 (code part): setting `Security:RequireHttps=true` turns on HTTPS redirect and HSTS; `Auth:RequireHttpsCookie=true` makes the session cookie Secure. Both default to off so development still works over HTTP. For the redirect to know the HTTPS port, IIS binding or `HTTPS_PORT` must be set (to be covered in the deployment guide). `AllowedHosts` is still `*`: needs the server name (open question).
- F3: Content-Security-Policy (same-origin only, no inline scripts), `Permissions-Policy`, and `Cache-Control: no-store` on `/api/*`. Checked in the browser: the app loads with no CSP violations in the console (only the start page, not every screen).
- F4: general rate limit per signed-in user (else per address), `RateLimit:RequestsPerMinute`, default 600. The value is a placeholder until the load test.
- F7: `db seed-demo` now refuses unless the environment is Development or `--allow-demo` is given.
- F8: test that fails when a controller action has neither `[HasPermission]` nor `[AllowAnonymous]` nor an entry in an explicit allowlist. It found `OrganizationController.Departments` (any signed-in user can list departments, as before); it is on the allowlist deliberately.

**Also done and tested (A4 and the IIS files, 2026-11-10):**
- F6: `db/scripts/Grant-LimsAppPermissions.sql` (least-privilege role `lims_app`: data access on the LIMS schemas, no structure changes, no backup; `DENY UPDATE, DELETE` on `audit.AuditLogs` and `inventory.StockMovements`; migration history read-only) and `Grant-LimsDeployPermissions.sql` (`db_owner` for the deployment login). The integration test fixture now creates a SQL login with exactly these permissions and **the whole API runs as that login** (`LIMS_TEST_ADMIN_API=1` switches back to the admin connection). A test proves the denies and that DDL is refused.
- **Bug found by this and fixed:** a stock transfer updated its two ledger lines after inserting them, which the append-only rule forbids. The second line now carries the link at insert time (`PairedMovementId` on the "in" line points to the "out" line; the "out" line's own pointer is no longer set; nothing read it).
- F5: `DataProtection:ProtectWithDpapi=true` encrypts the key files with the machine's DPAPI (tested: key file holds an encrypted element).
- F10 (partly): log folder is configurable (`Logging:Path`). **The log content review for personal data has not been done.**
- F9: `src/Lims.Api/web.config` (in-process, 2 MB request limit, compression, no server banner), `deploy/appsettings.Production.example.json`, `deploy/Deploy-Lims.ps1` (checks, backup, offline page, copy keeping config/keys/logs, migrate, health check, `-Rollback` of files), `docs/DEPLOYMENT.md`.
- `/api/health` now also reports `schemaUpToDate` and is `degraded` when migrations are pending.
- New CLI command `db create-org <CODE> "<name>" ["<Urdu name>"]`: before this a production installation could only get an organisation from `seed-demo`. Tried on an empty throwaway database together with `db migrate` and `db create-admin`.
- Published build started in Production mode on this PC (Kestrel): health healthy, headers present, Swagger not served, logs and keys written to the configured folders.

**Tests**: 409 pass (231 unit, 178 integration), with the API running as the restricted login. 7 tests in `SecurityHardeningTests`.

**Not verified:** IIS itself, the application pool identity, HTTPS binding and certificate, `Deploy-Lims.ps1` (syntax-checked, never run), the grant scripts on Windows-authentication logins (tested with a SQL login), Windows Server. `AllowedHosts` still needs the real server name.

**Backup and restore scripts (workstream D, 2026-11-10), in `db/scripts`, described in `BACKUP_RESTORE.md`:** `Backup-LimsDatabase.ps1` now takes `-Type Full|Differential|Log`, compresses (not on Express), can encrypt with a certificate, and records status / event log / failure hook; new `Remove-OldBackups.ps1` (14 daily, 8 weekly, 12 monthly; dry run by default), `Copy-LimsBackupOffsite.ps1` (SHA-256 verified copies, daily config archive), `Test-LimsBackupHealth.ps1`, `Register-LimsBackupTasks.ps1` (FULL recovery model + Task Scheduler jobs, 15-minute log backups) and `Restore-LimsPointInTime.ps1` (chooses full, differential and log chain from the files, detects a missing log, restores to a new database, CHECKDB). **Exercised on throw-away databases on this PC (all Windows PowerShell 5.1):** encrypted full/differential/log chain restored to three different moments with the expected user counts (1, 2, 3), refusal of an existing target, a hidden log file detected, a log backup refused with a clear message before any full backup, off-machine copy verified and a tampered copy detected, health check flagging it, retention on 434 synthetic files (34 fulls, 14 days of logs, unrelated files untouched). **Not verified:** real Task Scheduler registration (session not elevated; `-WhatIf` only), the event log source, SQL Express, a network share, the production server and volume, the timed restore drill. The scripts have no automated test in the .NET suite. Left behind on the dev SQL Server: a database master key in `master` created for the encryption test (harmless; the test certificate and databases were dropped).

**Log review (F10, 2026-11-10):** four places write to the application log (request log, exception handler, stock-charge failure, CLI). A test (`LogContentTests`) sends real requests carrying patient name, CNIC, mobile, e-mail, address, search terms and passwords with the production log settings and proves none of it is in the log file; the request log writes only method, path, status and time. **Finding fixed:** SQL Server error messages quote data values (verified: a duplicate-key message contained a CNIC, a truncation message the start of a name) and would have been written by the exception logging; a `ScrubbingSink` (Console and file) now replaces the message of any exception chain containing a SQL error by its error number/class/state and keeps the stack traces (tested with real SQL errors, including inside an `AggregateException`, whose own message copies the inner one). **Finding documented, not fixable in code:** IIS's own access log records the query string (patient searches) in clear text; the fix is a site setting (untick `cs-uri-query`) plus folder permissions and retention, in `DEPLOYMENT.md` section 6b, **not yet done on a server**. The audit log was not part of this review.

**Tests**: 411 pass (231 unit, 180 integration). A development API process (port 5088, not started by this session) held `Lims.Api.exe` locked during this work, so the suites were built into a separate output folder; run the whole suite once more with that server stopped.

**Security review report (A5, 2026-11-10):** `SECURITY_REVIEW.md`: what was examined (authentication, session, anti-forgery, authorisation, isolation, injection, XSS, uploads, errors, headers, secrets, logging, dependencies, database permissions, backups), 11 findings fixed with the test that proves each (2 High, 7 Medium, 2 Low), 4 open findings needing an owner decision (lockout reveals usernames, 8-character password rule, 7-day "remember me", public health endpoint; decisions 54-57), 5 that can only be closed on the server, and what was not examined. It is a self-review: no independent penetration test. Decisions 46-57 added to `DECISIONS.md`. The checks behind the report were run again today: NuGet and npm production audits clean, no secrets in the repository, all four raw SQL statements parameterised.

**Load test and performance (workstream B, 2026-11-10)** (`docs/PERFORMANCE.md`): `db seed-load` (set-based generator, refuses any database not named `LIMS_Load*`) filled a database with 550,000 orders (302,500 patients, 1.65 million results, 467,069 reports, 2.2 million audit lines; 13.4 GB, 19 minutes); `tests/Lims.LoadTest` times 45 screen calls for 30 signed-in users. **Found and fixed:** the orders search timed out at 30 s even at 20,000 orders; missing indexes (21, migration `AddPerformanceIndexes`); the tenant filter keeps SQL Server from seeking on indexes that start with `OrganizationId`; EF's `OPENJSON` translation of `Contains(list)` made SQL Server pick scans; four searches written as one OR across columns; a replacement-sample lookup that scanned all samples. **Result at 550,000 orders, one user (p95): 43 of 45 calls meet the target** (lists/searches under 0.5 s, dashboard/preview/audit under 1.5 s); orders search 17 s -> 0.1 s, reports search 31 s -> 0.47 s, dashboard 6.5 s -> 0.5 s, barcode scan 0.64 s -> 0.03 s. **Over target:** audit free-text search (1.7 s) and tracking search by patient name (0.57 s). **30 users at once (10 s between actions): medians good, but p95 shows 1-4 s stalls** on a single machine that also runs SQL Server and the load tool; this needs re-measuring on server hardware. Tried and rejected: `OPTION (RECOMPILE)` (slower), `MAXDOP 2` (no effect). The data is synthetic (no stock movements, cash drawers, corporate billing or real report snapshots). Static-file caching headers (F13) were not added.

**Backup status page (2026-11-10):** `/admin/backup` is now a real page (it was a placeholder): read-only status of the backup jobs from `backup-status.json` and the backup folder (last full/differential/log/off-machine/clean-up, how far the database can be restored, problems, latest files), English and Urdu RTL, `GET /api/backup/status` with `backup.manage` only, configured with `Backup:Folder`. It cannot run or restore anything, by design. Checked in the browser against a real backup folder (healthy, "no log backup" warning, Urdu layout, no horizontal overflow); the limited-role case is covered by API tests only. Bugs found while checking the backup scripts again: a backup in a folder the account cannot write status to was reported as failed (fixed earlier today), and the off-machine copy script failed when the folder held a single backup file (fixed).

**Tests**: 437 pass (251 unit, 186 integration), with the API running as the restricted SQL login. New this round: 20 unit tests for the backup rules, 4 backup-page API tests, an index test (21 indexes, column order and direction) and 2 log-content tests earlier. Lint and build of the frontend are clean. A development API process (port 5088, not started by this session) held the build files locked, so the suites were built into separate output folders.

**Not done yet (Phase 13):** everything that needs the second Windows machine or an elevated session: installation drill from `DEPLOYMENT.md`, timed restore drill, real Task Scheduler registration (`Register-LimsBackupTasks.ps1` only ran with `-WhatIf`), HTTPS and IIS settings (including `cs-uri-query` off), `AllowedHosts`, and repeating the load test on server hardware. Also open: decisions 46-57 in `DECISIONS.md`, and the three business items before go-live (cash refund role, closing an abandoned drawer, discount limit). The load database `LIMS_Load_Full` (13.4 GB, on the C: drive) was left in place for repeat measurements; drop it when no longer needed.

## Phase 12: what exists and was verified

Design and scope: `PHASE12_PLAN.md`; decisions 39-45 in `DECISIONS.md`. **No migration.**
- **Live dashboard** (`GET /api/dashboard/summary`) replaces the prototype's sample data: patients, orders, samples (waiting, overdue after 24 h), results in progress and awaiting approval, reports issued and ready, payments received with a daily trend against corporate credit billed, corporate outstanding, a pipeline, the oldest results awaiting approval and a recent-activity feed. Each part follows the working branch and appears only to people holding its permission; the open page refreshes every minute. The fake "Export" button and the "preview data" banner were removed.
- **Audit viewer** (`/api/audit`, `/admin/audit`): the organisation's audit log, read-only, with date, kind, record-type and text filters and a detail view showing the before and after value of each field. Sensitive values stay masked.
- **System settings** (`/api/settings`, `/admin/settings`): the five settings the code really reads (`Samples.RequirePaymentBeforeCollection`, `Results.AllowSelfVerification`, `Results.BlockApprovalUntilCriticalNotified`, `Inventory.AllowNegativeStock`, `Inventory.ExpiryAlertDays`), validated, versioned against overwriting, audited, with reset to default. This closes the "no settings screen" limitation of Phases 7, 8 and 11.

**Tests**: **402 automated tests pass (231 unit, 171 integration)**. New for this phase: 8 integration tests (figures that move with real work: registration, order, payment, pending sample, pipeline, feed and trend; an overdue sample after 30 hours; per-permission visibility of dashboard parts for System Administrator, Receptionist and Inventory Manager; range validation, anonymous refusal and no leakage from another organisation; audit list, detail, masking, named actions, filters and date range; audit permission, read-only and isolation; settings validation, unknown keys, effect on stock alerts, version conflicts and the audit record of a change; boolean setting and permissions) and 18 unit tests (period change percentage, ranges, audit change parsing, settings catalogue). The translation test covers the two new error codes.

**Checked in the browser (dev database, English and Urdu RTL):** the dashboard showing real orders, payments, a rejected sample, critical values and approvals; the empty "nothing waiting for approval" state; the audit list, detail dialogs (before/after, IP, request id) and kind filters; the settings page (Save disabled for an out-of-range number, number saved, switch toggled, reset to default; the stock alert window changed accordingly); Urdu pages without horizontal overflow. The test changes to settings were reset to defaults. A defect found and fixed during this check: with nothing awaiting approval the card still said "Oldest waiting 0 min ago", and the empty approvals table had no message. **Not browser-checked:** the dashboard seen by a limited role such as a receptionist (covered by API tests), the 7- and 30-day ranges and the audit date filters in the UI. The browser pane was hidden, so no screenshots were reviewed.

**Known limitations**
- No export of the dashboard or the audit log; no saved searches.
- Audit viewers see the whole organisation's log, not only their branches (decision 43); viewing is not itself logged.
- "Payments received" is not net of refunds and excludes corporate allocations (decision 41). "Overdue" is a fixed 24 hours (decision 40).
- `Billing.MaxDiscountPercentWithoutApproval` is stored but not enforced, so it is not shown on the settings screen.
- Action names of named audit lines (for example `Inventory.PurchaseReceived`) are shown as written, not translated.

## Phase 11: what exists and was verified

Design and scope: `PHASE11_PLAN.md`; decisions 31-38 in `DECISIONS.md`. Stock items, suppliers, purchases ("receive stock") with batches and expiry, an append-only stock ledger, manual issue/adjust/write-off, transfers between branches, automatic consumption of reagents when a result is submitted (earliest expiry first, never blocking the result, shortfalls recorded), and low-stock/expiry/shortfall alerts. Migration `AddInventory` (8 tables in the new `inventory` schema).

**Tests**: with the whole solution in the working tree, **376 automated tests pass (213 unit, 163 integration)**. New for this phase: 13 integration tests (permissions and validation; purchase creating batches and ledger lines; purchase validation and expiry rules; the same purchase sent 5 times at once recorded once; stock never below zero and 8 simultaneous issues of 3 from a batch of 10 allowing exactly 3; adjust/write-off permissions and reasons; transfer between branches with the expiry travelling and branch-limited users unable to touch other branches' batches; result submission charging stock earliest-expiry-first and once only after a return and re-submit; short stock still saving the result and recording a shortfall; profile rules; low/expiring/expired alerts; isolation between organisations, anonymous and token-less refusal; database constraints refusing negative stock and a wrong-direction ledger line) and 20 unit tests for `InventoryRules` (batch order, expired batches, rounding, expiry window, low stock, movement direction). The translation test now covers the 12 new server error codes. The frontend lints and type-checks clean.

**Checked in the browser (dev database, English and Urdu RTL):** creating a supplier and a stock item, receiving stock (blocked with a clear message in the "All branches" view until a branch is chosen, then recorded as `GRN-26-0001`), the items list showing Low and "expiring" badges and the alerts strip, the batch action menu, an over-sized issue blocked and a valid issue (80 → 75 ml) reflected, the purchases and ledger pages and the usage dialog in Urdu without horizontal overflow. **Not browser-checked:** transfer dialog, write-off, shortfall resolution and the automatic charge from a real result in the UI (all covered by API tests). The browser pane was hidden, so no screenshots were reviewed.

**Known limitations**
- Settings `Inventory.AllowNegativeStock` (default off) and `Inventory.ExpiryAlertDays` (default 30) work in the API but have no settings screen yet.
- Which moment charges the reagents (result submission) is a default for the laboratory to confirm (decision 32).
- The transfer screen offers only the signed-in user's own branches; the API accepts any active branch of the organisation. Transfers are immediate (no in-transit state).
- No purchase orders, supplier payments, stocktake screen, pack barcode scanning or consumption reports.
- An item's list shows stock for the working branch (all visible branches when none is selected), and the item page lists every visible branch's batches.
- A development supplier (`MEDICO`), item (`GLU-R1`) and purchase `GRN-26-0001` (80 ml, then 5 ml issued) were created in `LIMS_Dev` during the check.

## Phase 10: what exists and was verified

Design and scope: `PHASE10_PLAN.md`. Clients, contracts with fixed prices and discount, credit orders and invoices with due dates, credit limit, receipts and credit notes, allocation to invoices, account balance with aging, statement, and a books reconciliation check. Migration `AddCorporateBilling`.

**Tests**: with the unfinished Phase 11 inventory code removed (in a scratch copy, so the working tree was not touched), **343 automated tests pass (193 unit, 150 integration)**, including 10 integration tests and 6 unit tests for this phase: contract rules, contract pricing and credit invoices, refusal of counter payment, manual discount and inactive clients, credit limit, allocation that never over-allocates, concurrent allocation of one receipt, receipt permissions and idempotency, aging and statement, cancellation after allocation, isolation, anonymous and token-less refusal, database constraints. The frontend lints and type-checks clean.

**Checked in the browser (dev database):** corporate clients list, creating a client (Save disabled until code and name are filled), the client account page in English and Urdu RTL without horizontal overflow. **Not browser-checked:** contracts, a credit order, receipts, allocation, statement and "Check books" (covered by API tests only). The browser pane was hidden, so no screenshots were reviewed.

**Known limitations**
- ~~The full test suite did not run in the working tree~~: this was caused by unfinished Phase 11 inventory code (no migration, untranslated error codes). Phase 11 is now finished and the whole suite passes (376 tests).
- Credit invoices are one per order; there is no monthly grouped invoice. No printable statement or PDF, and no email or SMS.
- The "Due" and "Balance due" column headings in the Urdu credit-invoices table use the same Urdu word.
- Clients are organisation-wide; there are no per-branch accounts.
- A development client `ACME` ("Acme Textiles (demo)") was created in `LIMS_Dev` during the browser check.

## Phase 9: what exists and was verified

**Backend**
- A patient report **per order**, built only from released (approved) results, in English, Urdu or both. Issuing freezes the report as a snapshot (patient, sample dates, every value with its flag, unit and the approved range used, comments, signatories, letterhead text) with a **SHA-256 fingerprint**; later changes to the patient, catalogue or ranges never alter an issued report. Verifying recomputes the fingerprint.
- **Preview** shows what would be issued now and stores nothing. **Interim** reports list unreleased tests as pending; **Final** means every non-cancelled test is released.
- **Versions**: a newly released test, an amendment or a cancelled test makes an issued report *outdated*; issuing again creates the next version (same report number, mandatory reason), and the earlier version stays viewable and is marked superseded. Two people issuing at once produce one report.
- **Printing** is counted atomically; the first print is the original and every later print is marked *Duplicate copy*.
- **Secure access**: `reports.view` / `reports.print` / new `reports.issue` / new `reports.templates`; branch visibility (a collection centre sees its own patients' reports); every issue, view, print and integrity check is written to the audit log; the patient-identifying snapshot is never copied into the audit log.
- **Template**: header, footer and disclaimer text in English and Urdu, and a logo. **Signatories**: registration number and signature image per user. Images are accepted only as real PNG or JPEG (checked by their bytes, not file name), within size limits, and are served with `nosniff`.
- Database: migration `AddReports` (`LabReports`, `ReportTemplates`, `ReportTemplateTranslations`, new columns on `Users`, two permissions granted to the existing standard roles of every organisation, sequence `Report`), with CHECK constraints (status, hash length, final needs every test released, image size and type) and one live version per order.

**Frontend** (English and Urdu RTL): Reports list (ready to report, issued), the order report page (language, preview, issue, new version, print or save as PDF, integrity check, versions), the A4 report document (letterhead with logo, patient block, results by department with flag codes and words, pending tests, signatories with signatures, disclaimer, report number, version and fingerprint, watermarks for preview, superseded and duplicate), template settings, and registration number and signature on the user form. A Report link appears on the order page.

**API**: `GET /api/reports[?tab=ready|issued]`, `GET /api/reports/{id}`, `POST /api/reports/{id}/printed`, `GET /api/reports/{id}/verify`, `GET /api/reports/signatures/{userId}`, `GET|POST /api/orders/{id}/reports[/preview]`, `GET|PUT /api/report-template`, `GET|PUT|DELETE /api/report-template/logo`, `PUT|DELETE /api/users/{id}/signature`.

**Tests**: 309 automated tests pass (169 unit, 140 integration): all earlier tests plus 14 integration tests for this phase (preview stores nothing, frozen content, ready and issued lists, interim then final versions with supersede, amendment makes a report outdated while the old version keeps the old value, atomic print counting and duplicate marking, issue race, role matrix, isolation between organisations and branches, tampering detected, raw-SQL constraints, template and logo rules including a disguised SVG and an oversized image, signatures and registration numbers, audit records, anonymous and token-less refusal) and 32 unit tests (interim and final rules, outdated detection, fingerprint, image sniffing, snapshot builder: display rounding only, flag codes, approved range text, grouping, pending tests, signatories, bilingual names, round trip; an architecture test guarding access to issued reports).

**Checked in the browser (EN, Urdu RTL and bilingual):** ready list, preview of a two-test order with a 28-parameter panel (about two A4 pages), Urdu and bilingual layouts with the local Nastaliq font and RTL direction, the sheet kept at 210 mm, issue as bilingual, original print then "Duplicate copy" on later views, integrity check, template text saved, a template change after issuing leaving the issued report untouched, user form signature controls, Urdu list and page without horizontal overflow. Defects found and fixed during this check: the report sheet was squeezed below A4 width, the rotated watermark caused horizontal scrolling, and the first Urdu print could have used a fallback font because fonts load on first use.

**Known limitations**
- **No one-click server-side PDF.** PDF export is the browser's "Save as PDF" from the print dialog (same layout, correct Urdu, works offline). A server-side PDF needs a library such as QuestPDF whose licence has not been accepted (decision 1); `GET /api/reports/{id}/pdf` is not built.
- The browser reports `afterprint` even if the person cancels the print dialog, so a cancelled print is still counted as a print.
- Repeating header and footer on each page and the fingerprint line rely on the browser's print engine; they were checked in the page structure, **not on paper or in a physical print preview**. "Page x of y" is not shown (browsers cannot count total pages reliably).
- The logo and signature images are read when the report is shown or printed, not frozen: replacing a logo or signature changes how old reports look (the clinical content and the letterhead text stay frozen).
- The "ready to report" list looks at results released in the last 60 days (adjustable up to 365, at most 1000 orders).
- One template per organisation (no per-branch letterhead yet). Any user who can view reports can fetch a signatory's signature image (it is printed on reports anyway).
- No screenshots were reviewed (the browser pane was hidden); layout was checked through page structure and measurements.

## Phase 8: what exists and was verified

**Backend**
- Result entry per ordered test, per parameter (numeric, option, text, narrative). Numbers are read in English or Urdu digits; anything ambiguous (thousands separators, exponents, more than 6 decimals) is refused, never guessed or rounded.
- Flags (low, high, critical, abnormal option) come **only** from approved reference ranges, chosen by sex and the patient's age on the collection date. A draft or retired range never flags; with no approved range the value is simply not flagged and the screen says so. The range used is copied onto the result so reports can reproduce it later.
- Workflow: draft, submitted, verified, approved (released). Verify and approve need the matching permission; **the person who entered a result cannot verify or approve it**. A reviewer can return a result with a reason. Verify, approve, return and amend carry the version the reviewer saw, so a result changed in the meantime is refused (409).
- Critical values raise an alert when the result is submitted; approval is blocked until someone records who was told, how, and that the value was read back.
- Released results are locked. Changing one means an amendment: reason required, new version, the old version stays released until the new one is approved (then it is superseded and kept). An amendment that was only started can be abandoned.
- Worklists: batches of received tests for one department at a branch, with a number (`WL-...`), one open worklist per test (enforced by the database), a printable sheet, close.
- A sample whose tests have saved results cannot be rejected until the drafts are discarded.
- Database: six new tables (migration `AddResults`), CHECK constraints tying status to who and when, one released and one in-progress version per test, one value kind per parameter.

**Frontend** (English and Urdu): Result entry queue (urgent first, overdue marker, scan or search), the result screen (typed inputs per parameter type, live flag preview, Enter moves to the next field, save draft, submit, verify, return, approve, amend, discard, history, critical alerts), Verification & approval queues, Critical alerts (report with read-back), Worklists (create, print sheet, close), and a Results card on the order page.

**API**: `GET /api/results/pending`, `GET /api/results/queue`, `GET|PUT|DELETE /api/results/items/{orderItemId}[/draft]`, `GET /api/results/{id}`, `GET /api/results/{id}/history`, `POST /api/results/{id}/submit|verify|return|approve|amend`, `GET /api/orders/{id}/results`, `GET|POST /api/worklists[/{id}[/close]]`, `GET /api/critical-alerts[/open-count]`, `POST /api/critical-alerts/{id}/notify`. No new permission keys (`results.view|enter|verify|approve|amend|critical`).

**Tests**: 263 automated tests pass (137 unit, 126 integration): all earlier tests plus 17 integration tests for this phase (full workflow, flags only from approved ranges, option and numeric validation, critical alert blocking approval, separation of duties, return and resubmit, stale-version and parallel-review races, locked results and amendment, abandoned amendment, sample and branch guards, the reject-sample guard, queues and permissions, worklists, isolation between organisations and branches, anonymous and token-less refusal, role matrix, raw-SQL database constraints) and unit tests (state machine, separation of duties, value parser, range selection by collection-date age, an architecture test guarding result visibility). Defects caught by these tests and fixed: the one-version-in-progress index was silently merged with the released-version index by EF; a worklist read samples around the sample visibility gate; an amendment that was started could never be abandoned.

**Checked in the browser (EN and Urdu RTL):** queue ordering, invalid input blocked, live critical preview and row highlight, Enter-key flow, submit, the enterer seeing no Verify button, a second user verifying, approval refused while the critical alert was open, reporting with a required read-back, release, amendment banner and draft, Urdu layout without overflow, worklist create, print and close, the alerts page, and the order page card.

**Known limitations**
- Settings `Results.AllowSelfVerification` (default off) and `Results.BlockApprovalUntilCriticalNotified` (default on) work in the API but have no settings screen yet.
- No bulk approval, no auto-verification, no delta checks, no analyzer results (Phase 14); no report layout or PDF (Phase 9).
- Seeing the Results card on an order needs `results.view`; reception does not have it.
- Worklist sheets print through the browser print dialog (A4); not tried on a physical printer.
- Reviewing an amendment shows the new values only; a side-by-side comparison with the released version is not built.
- No screenshots were reviewed (the browser pane was hidden); layout was checked through page structure and overflow measurements.
- A second development login (`verifier2`) exists in `LIMS_Dev` for review tests (see the git-ignored `db/dev-accounts.local.md`).

## Phase 7: what exists and was verified

**Backend**
- Samples are planned when an order is created (one per sample type, barcode `S-yymmdd-nnnn`, unique across the organisation, counter restarts each day). Cancelling a test cancels its sample if nothing was collected yet; a collected sample is never cancelled automatically.
- States: planned → collected → (in transit) → received, or rejected / cancelled; one function decides which moves are legal.
- Collect (needs the identity confirmation), receive, reject (reason, plus a note for some reasons; a replacement sample is planned on request), recollect, label-printed counter, editable rejection reasons.
- Transfers: create a manifest from collected samples, receive each sample at the destination (the manifest closes itself when everything arrived), close with missing samples, cancel before anything arrived. A sample can only be on one open manifest, even under simultaneous requests.
- Code 128 barcodes are encoded on the server; the browser only draws them. No barcode library and no internet.
- `db plan-samples` plans samples for orders that existed before this phase.

**Frontend** (English and Urdu)
- Collection (queue, scan box, identity confirmation, print after collect), Receiving (scan to receive with a visible result for every scan), Tracking (search and history), Rejections & recollection (and the reasons list), Transfers (build, view, print, receive, close, cancel), a Samples card on the order page, label size settings per computer.

**Tests**: 191 automated tests (82 unit, 109 integration); the 189 that existed before the closure additions all passed together, and the 2 closure additions (anonymous and CSRF refusal on every sample endpoint; role permission matrix) pass: the Phase 6 regression suite plus 15 new integration tests (planning and grouping, permissions, double and parallel collection, receive and reject rules, replacement, isolation between organisations, the branch visibility rule, a database constraint, manifests including a race) and unit tests (barcode symbol table, an independent decoder round trip for every printable character, state transitions, an architecture test that guards the sample visibility gate).

**Checked in the browser (EN and Urdu RTL):** order page with the Samples card, collect all with printing, scan-to-receive (lower case and padded scans, repeat scan, unknown code, wrong branch), reject with a required note and an automatic replacement, collection of the replacement, building a manifest, receiving one sample at the other branch, closing with one missing, Urdu layout without horizontal overflow, label size change. Two defects found this way were fixed (a crash after "collect all" and labels not printing after "collect").

**Known limitations**
- Not tried with a real label printer or a hardware barcode scanner. The print path was verified up to the browser print call (50 × 25 mm page size, one label per page); scanner input is simulated as typed text plus Enter.
- Branch names in lists and labels are shown in English (the sample screens do not yet use the branch's Urdu name).
- The label preview shows no bars when there is no sample to take them from.
- The setting `Samples.RequirePaymentBeforeCollection` is enforced by the API but has no settings screen yet.
- The Phlebotomist role does not include `samples.transfer` (the Technician does). Add it per organisation in Roles if collection centres should send manifests.
- No screenshots were reviewed (the browser pane was hidden); layout was checked through the page structure and measurements.

## Phase 6 audit (orders & billing) — 2026-11-09

An independent audit added `OrderAuditTests` (23 tests) and `ClockExtensionsTests`, covering security, isolation, money rules, data integrity and concurrency. **Result: 153 tests pass (59 unit, 94 integration). Phase 6 stays Done.**

**Bugs found and fixed:**

| Severity | Bug | Fix |
|---|---|---|
| High | A cash payment racing with "close drawer" could be missing from the closing figures (reconciliation showed Rs 500 while 12 payments were tied to the drawer) | Cash payments, cash refunds and closing now lock the drawer row in one statement; closing waits for in-flight payments and later payments see the drawer closed. The race test passes in repeated runs. |
| Medium | The day summary mixed money from all of a user's branches even when one branch was selected | Summary now follows the selected branch ("all branches" view still covers everything the user may see). |
| Medium | A refund with an invalid payment method (e.g. 99) was accepted and saved | Validated in the service, plus database CHECK constraints for refund method, order priority and status, and order-item status (migration `HardenOrderBilling`). |
| Medium | Reusing a submission key on a different invoice returned success without recording the payment or refund; an empty refund key was accepted | Now returns "key already used for a different record"; an empty refund key is refused. |
| Low | Invalid order priority (e.g. 99) was accepted | Validated and constrained in the database. |
| Low | Day boundaries were off by a few milliseconds (two separate clock readings subtracted) | One shared helper using a single reading, rounded to whole minutes. |

**Verified with no defects found:** every endpoint needs sign-in and a CSRF token; permission matrix for Receptionist, Cashier, Phlebotomist, Inventory and Lab Manager; other organisations and branch-limited users cannot read, pay, cancel or refund; 12 parallel orders get distinct numbers; 5 parallel refunds refund once; payment racing a cancellation leaves a consistent invoice; invalid amounts, discounts and methods are refused; free tests and 100% discounts; cancelled orders accept no payment; collected tests can't be cancelled; partial payments and refunds stay exact; the database refuses inconsistent money rows even through raw SQL; every money and order change is in the audit log.

**Remaining risks and open decisions (not bugs; need an owner decision or a later phase):**
1. **Cash refunds need a role that can both refund and handle cash.** By default only Lab Manager and System Administrator can. The Cashier can open a drawer but can't refund; the Accountant can refund but can't open a drawer. Options: give Cashiers a refund permission, or let a manager approve a refund that a cashier pays out. *(Medium)*
2. **A cash refund can exceed the cash in the drawer** (expected cash can go negative) and can refund cash against a card payment. *(Medium, policy)*
3. **No way for a supervisor to close an abandoned drawer** (e.g. a cashier who left). *(Medium, missing feature)*
4. **Orders can't be edited after creation** (no adding tests to an existing order); the workaround is to cancel and re-create. *(Low, missing feature)*
5. **Payments are recorded at the branch where they are received,** which may differ from the invoice's branch (a patient ordering at one centre and paying at another). Branch revenue reports in Phase 12 must decide which to use. *(Low, design)*
6. **Orders and payments lists show all of a user's branches,** while the day summary follows the selected branch. *(Low, design)*
7. **"Today" in the orders list uses the browser's date;** if a PC's time zone differs from the laboratory's, the range can differ by a day. *(Low)*
8. **The drawer race and parallel tests take about 2 minutes** of the suite; fine for CI but slower locally. *(Info)*

## Phase 6: what exists and was verified

- **Migration `AddOrdersAndBilling`** adds 7 tables:
  - `lab.Orders` and `lab.OrderItems`;
  - `billing.Invoices`, `InvoiceLines`, `Payments`, `Refunds` and `CashDrawerSessions`.
  - Database checks: invoice amounts must add up, payments must be positive, cash needs a drawer, and one open drawer per cashier per branch.
  - Order, invoice, receipt and refund numbers are unique, and each order/payment/refund has a one-time submission key.
- **Server-side pricing:**
  - Prices come from the catalog, never from the browser. The form shows what `/api/orders/quote` returns.
  - Packages expand into their tests. The same test twice (alone and in a package) is rejected. Tests are checked against the branch's availability.
- **Order creation** is one transaction: order, invoice, lines, items and the first payment. A failure leaves nothing behind and does not use up numbers.
- **No double submission:**
  - Each form carries a one-time key, so double clicks and retries return the same order.
  - 5 simultaneous submissions produced 1 order. 4 simultaneous payments produced 1 payment.
- **Money rules (domain code, unit-tested):**
  - 2-decimal rounding, half away from zero.
  - Percentage discounts are re-applied if a test is cancelled.
  - Statuses: Unpaid, Partially paid, Paid, Refund due, Cancelled.
  - Discounts need `billing.discount` and a reason. Overpayment is refused.
- **Cancellations:**
  - Cancelling a test (or the order) needs `orders.cancel` and a reason, and is audited. A cancelled test is removed from the bill.
  - Tests already collected or processed can't be cancelled (this rule applies from Phase 7).
  - If money was paid, the invoice shows *refund due*; only `billing.refund` can settle it, up to the amount due.
- **Cash drawer:**
  - A cashier opens a drawer with a float. Cash payments and refunds are tied to it; card, transfer and wallet are not.
  - At close: expected = float + cash in − cash out. Any difference needs a note.
  - Supervisors (`cash.close`) see all sessions at the branch.
- **Branch rules:** orders and payments need a working branch selected (not "All branches"). Reads respect the user's branch access and organisation.
- **Screens:**
  - New order: patient search, test/package search by code + Enter, live server pricing, discount, payment with change calculation.
  - Orders list (also used for Invoices), order detail with invoice, payments, refunds and cancel actions.
  - Payments list with the day's totals by method; cash drawer page.
  - Printable 80 mm receipt in English or Urdu.
- **Tests: 153 passing** after the audit (59 unit, 94 integration; 128 before it). New in this phase:
  - Invoice maths, and a source-code check that every `IgnoreQueryFilters()` has an explicit scope.
  - A cross-organisation leak found and fixed: a join that disabled filters for the whole query let a user see other branches' orders in lists.
  - API tests for the order, payment, refund, drawer, permission and isolation rules above.
- **Browser-checked:**
  - Branch prompt; code + Enter search; duplicate-test message; discount; cash blocked while the drawer is closed.
  - Triple-click gave one order; partial cash, then card payment with a receipt.
  - Cancel, refund, then drawer close (expected 740, short by 40, note required); Urdu pages.

**Known limitations:**
- **Receipts:**
  - Browser printing only.
  - Barcode scanning and label printing for samples come in Phase 7.
  - No automatic receipt print on the order screen; the receipt opens from the payment dialog or the printer icon.
- **Cash drawer is per cashier;** there is no day-end closing across cashiers yet.
- **Reports:** a financial report and PDF/Excel export of the payments list are not built yet.
- **Corporate/credit billing** is Phase 10. Payment methods are fixed: cash, card, bank transfer, wallet and cheque.
- **Urdu time format** uses 12-hour time while English uses 24-hour.

## Phase 5: what exists and was verified

- **Migration `AddTestCatalog`** creates 8 tables in `catalog`:
  - TestCategories, Tests, TestBranches, TestParameters, ParameterOptions, ReferenceRanges, TestPackages, PackageTests.
  - Composite organisation foreign keys, check constraints, and `decimal(18,6)` for clinical limits.
- **Tests:**
  - Code, English/Urdu names, short name, category, department, sample type and volume, collection notes, method, turnaround, standard price, report notes.
  - Available at all branches or at selected ones.
  - **Changing a price requires `catalog.pricing`.**
- **Parameters:**
  - Result types: numeric (unit, decimal places for display only), choice list (values in English/Urdu, with abnormal marks), short text, narrative.
  - Optional report group.
  - The result type is locked while reference ranges exist.
- **Reference ranges, Draft → Approved → Retired:**
  - Ranges vary by sex and by age band (stored in days; entered in days, months or years).
  - Each range has normal and critical limits, a printed text, and its source.
  - **Approval requires `catalog.clinical` and a recorded source.**
  - Approved ranges can't be edited. A replacing draft retires the old range when it is approved, in one transaction.
  - Overlapping approved ranges are blocked. Approvals are written to the audit log.
  - The pathologist's approval queue is at *Reference ranges*.
  - **No ranges are seeded.**
- **Range rules (in the domain layer, unit-tested), ready for result entry in Phase 8:**
  - A sex-specific range takes priority over an "any sex" one; otherwise the narrowest age band wins.
  - A value equal to a limit is within range.
  - With no approved range, results get no flag.
- **Packages:** at least two active tests and a bundle price. The screen shows the individual total and the saving.
- **Screens:** Tests list and editor (parameters, ranges dialog), Categories, Packages, range approval queue.
  - Stacked dialogs now behave correctly: only the top one reacts to Escape.
- **Tests: 110 passing** (50 unit, 60 integration).
  - Unit: flag limits, range selection, overlap, validation.
  - API: the full range lifecycle, the source requirement, immutability, overlap and replacement, clinical-only approval, pricing permission, packages, isolation.
- **Browser-checked:**
  - Category, test and parameter creation (numeric and choice list).
  - A draft range: inconsistent limits caught as typed, approval blocked without a source, then approved.
  - Approval queue; package with savings; Urdu lists.
  - Demo range data is labelled "DEMO data for software testing — not for clinical use".

**Decisions to confirm (see `DECISIONS.md`):**
- A user with clinical permission may approve a range they drafted themselves (recorded in the audit log). A strict two-person rule can be switched on if required.
- Age-band conversion: 1 year = 365.25 days, 1 month = 30.4375 days.

**Known limitations:**
- **Unit and sample-type lists are view-only:** the 42 units and 16 sample types are seeded, with no screen to edit them yet.
- **No corporate prices yet:** corporate price lists come in Phase 10.
- **No reordering by drag:** parameters are reordered with the display-order number.

## Phase 4: what exists and was verified

- **Migration `AddPatientsAndDoctors`:** `patient.Patients` and `patient.Doctors`. Both are soft-delete and audited, with a unique patient number per organisation and indexes for mobile, CNIC and name.
- **Patients are organisation-wide** (any branch can serve them). The registering branch is recorded.
- **Registration:**
  - Patient number from the `Patient` sequence (P-000001), issued in the same transaction as the record.
  - Age is required: an exact date of birth, or a stated age that is stored as an *estimated* date and shown as a year only.
  - CNIC and phone numbers are normalised and validated.
- **Duplicate detection:** same CNIC, same mobile plus similar name, or same name plus date of birth.
  - A live warning appears while typing, and the server blocks the save until staff confirm "new patient".
- **Sensitive data:**
  - CNIC, email and address are masked without `patients.viewSensitive`.
  - Every unmasked view is logged (`Patient.ViewSensitive`).
  - Users who see masked values cannot overwrite them.
  - Identity values are never written to the audit log.
- **Search:** patient number, name, Urdu name, father's/husband's name, mobile digits, CNIC. Scanning a patient number and pressing Enter opens the record directly.
- **Screens:**
  - Patients list.
  - Registration/edit dialog.
  - Patient profile, with a printable A5 registration slip.
  - Doctors page (automatic codes D0001…, referring or in-house).
- **Tests: 88 passing** (35 unit, 53 integration). Unit tests cover age maths, including month-end and leap days, plus CNIC, phone and masking. API tests cover registration, validation, duplicates, masking and logging, search, permissions and isolation.
- **Browser-checked:**
  - Registration and the duplicate warning.
  - Profile and doctors in English and Urdu.
  - As a receptionist: limited menu, fixed branch, masked CNIC, and "No access" on admin pages. This closes the Phase 3 gap.

**Known limitations:**
- **No orders yet:** the patient's order history and the "New order" button lead to Phase 6.
- **One contact on the record:** the ERD's separate `PatientContacts`/`PatientAddresses` tables were simplified to fields on the patient. The audit log keeps the history.
- **No merging of duplicate patients yet:** the permission `patients.merge` exists; merging needs orders to move, so it comes after Phase 6.
- **Date picker format:** it follows the browser's language setting.
- **No branch when registering in the "All branches" view:** the patient's registered branch is left empty.

## Phase 3: what exists and was verified

- **Sign-in:**
  - httpOnly, SameSite=Strict session cookie; CSRF token on every change request.
  - Lockout after 5 failures for 15 minutes; per-IP sign-in rate limit; login history.
  - Passwords hashed with PBKDF2; the policy is 8+ characters with a letter and a digit, not containing the username.
  - Forced password change after an admin reset or first sign-in.
- **Sessions:** re-checked against the database every 2 minutes. Deactivation, a password change or a reset ends other sessions; role and branch changes apply without signing out.
- **Access:** permission-based checks on every endpoint; branch switching limited to permitted branches; last-administrator guard.
- **APIs:** users, roles and permissions, organisation, branches, departments. Errors carry codes that are translated in the UI.
- **Admin command:** `db create-admin <ORG> <username>` creates the first administrator with a temporary password.
- **UI:**
  - Real sign-in and forced password change; account menu; branch switcher.
  - Users page: search, filters, sorting, paging; create/edit dialog; sign-in history; password reset; unlock.
  - Roles page: permission editor with a clinical-permission warning.
  - Branches & departments page.
  - Reusable dialog, table, tabs, switch and menu components.
- **Tests: 62 passing** (18 unit, 44 integration, including real-HTTP API tests). The new translation tests check English/Urdu key and placeholder parity and that every server error code is translated.
- **Browser-checked (1440×900):**
  - Sign-in error and forced password change.
  - Creating a user with validation.
  - Roles editor, and Branches in Urdu RTL.
  - Sign-out ends the server session.
  - A session survives an API restart.

**Not yet checked in the browser:**
- Signing in as a limited (non-admin) user to see the sidebar hide pages. The API side is covered by tests.
- Department editing in the UI.

## Phase 2: what exists and was verified

**Design:** the full data model for every module is in `ERD.md`. Phase 2 built the foundation that every module depends on. Each later phase adds its own tables in its own migration, together with the code and tests that use them.

**Migration `InitialFoundation`:** 16 tables in the `core`, `security`, `catalog` and `audit` schemas. Applied to `LIMS_Dev`, with the demo organisation created by `db seed-demo`.

**Isolation, three layers:**
1. Composite foreign keys `(OrganizationId, Id)` reject cross-organisation links, even through raw SQL.
2. EF query filters scope every read to the caller's organisation. With no organisation known, queries return nothing.
3. A save guard assigns or verifies `OrganizationId` and blocks writes or moves into another organisation.

**Save pipeline:**
- Created/updated stamps that callers cannot override.
- Optimistic concurrency (`RowVersion`).
- Soft-delete support.
- Audit rows with old/new values written in the same transaction. Password hash and security stamp are never logged.

**Number sequences:**
- One atomic `UPDATE … OUTPUT`, with optional monthly/yearly/daily reset and per-branch override.
- Numbers are only consumed if the surrounding transaction commits.

**Provisioning** creates for a new organisation:
- the main branch and 6 departments;
- 10 editable roles built from templates;
- 7 number sequences and the default settings.

**Seed data:** 48 permissions (English and Urdu descriptions), 42 units, 16 sample types. **No reference ranges or clinical rules.**

**Tests: 41 passing** (14 unit, 27 integration against a fresh SQL Server database per run). They cover:
- migrations and seed data, and the Urdu round-trip;
- unique and check constraints;
- tenant isolation: read, write, move, no-org context and raw-SQL foreign keys;
- audit content and atomicity;
- concurrency conflicts;
- 40 parallel number requests (no duplicates, no gaps), rollback and period reset;
- frontend permission keys match the backend catalog.

**Backup and restore:**
- `Backup-LimsDatabase.ps1` and `Test-LimsRestore.ps1` were both run successfully on 2026-11-09 (CHECKDB passed, row counts matched).
- The restore script refuses to overwrite an existing database.

## Known limitations (Phase 2)

- **No user can sign in yet.** Users are created in Phase 3, along with authentication and a first-admin setup command.
- **The API has no signed-in organisation yet**, so tenant-filtered data is invisible through the API. This is fail-closed by design until Phase 3.
- **Units and sample types are installation-wide** reference data, shared by all organisations. Per-organisation customisation can be added later if needed.
- **Log backups and a production backup schedule** are part of Phase 13 deployment.

## Phase 1: what exists and was verified

**Repository inspection:** the target folder was empty. No existing code, schema or migrations. Built from scratch.

**Backend** (`Lims.sln`, .NET 9). Verified: builds with 0 warnings and 0 errors.
- Layered projects: Domain, Application, Infrastructure, Api, UnitTests.
- Serilog logging, ProblemDetails error handler, Swagger.
- `GET /api/health` returns 200. It reports `database: false` because `LIMS_Dev` is created in Phase 2.
- The API serves the built React app, and deep links such as `/samples/collection` load after refresh.

**Frontend** (`web/`):
- Design tokens and components (see `DESIGN_SYSTEM.md`).
- Shell: collapsible sidebar grouped by module, nested items, active state, collapse remembered.
- Header: title, breadcrumbs, branch selector, EN/اردو switch, notifications, profile, sign-out confirmation.
- Login: zod validation with translated messages, show/hide password, admin-assisted recovery dialog.
- Dashboard: 9 KPI tiles, collections chart with a table view, sample pipeline, approvals table, activity feed, date-range toggle, refresh, skeleton loaders.
- Every other module page shows a "planned for Phase N" empty state.
- `npm run lint` and `npm run build` are clean.

**Checked in the browser (1440×900):**
- Login in English and Urdu.
- Validation errors, including the short-password error in Urdu.
- Sign-in, then the dashboard in English and in Urdu RTL.
- Nested navigation and breadcrumbs.
- Sidebar collapse.
- Sign-out dialog (focus lands on Cancel; Escape closes it).
- Refresh keeps the session, language and page.
- No console errors and no horizontal overflow.

## Known limitations (Phase 1)

- **Sign-in is a prototype.** Any username plus an 8+ character password works. Nothing is checked on the server. Replaced in Phase 3.
- **Dashboard figures are sample data**, generated in `features/dashboard/dashboardApi.ts` and clearly labelled on screen. Replaced by `GET /api/dashboard/summary` in Phase 12.
- **Branch list** in the header is sample data (`app/previewData.ts`). Replaced in Phase 3.
- **Not yet checked:** layout below 1000px wide, beyond CSS breakpoints. The target is desktop browsers.
- **Frontend bundle** is about 890 kB before compression, 273 kB gzipped. Route-level code splitting is planned as modules are added.
- ~~No unit tests yet~~: added in Phase 2.
