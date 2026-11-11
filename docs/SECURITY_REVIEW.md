# Security review (Phase 13)

_Date: 2026-11-10. Scope: the LIMS application, its database permissions, its backups and its deployment files, as they exist in the repository. Method: reading the code, automated tests, probing the running application on the development PC. **This is a self-review. No independent penetration test was done** (decision 53); it is recommended before go-live if the system can be reached from outside the lab network. Nothing here has been checked on the real server, IIS, or with a real certificate._

## 1. Summary
- **Fixed during this phase and re-tested:** 2 High, 7 Medium, 2 Low (section 3; the "Medium-High" and "Low-Medium" items are counted as Medium and Low).
- **Open, needing an owner decision:** 4 (section 4): account-lockout behaviour, minimum password length, "remember me" length, and whether the public health endpoint stays open.
- **Open, can only be closed on the server:** 5 (section 5): HTTPS and certificate, IIS access-log setting, `AllowedHosts`, the installation drill, the timed restore drill.
- **Not examined / out of scope:** section 7.
- No Critical findings. No High finding is open in the code; the High items that remain open (HTTPS on the real server) are deployment steps.

Tests behind this review: **411 automated tests pass** (231 unit, 180 integration), including `SecurityHardeningTests`, `LogContentTests`, the tenant-isolation, anti-forgery, permission and upload tests from earlier phases. The integration suite runs the API as a **restricted SQL login** (section 2, "Database permissions").

## 2. What was examined, and the result
| Area | What was checked | Result |
|---|---|---|
| **Authentication** | Sign-in code (`AuthService.LoginAsync`), password rules, hashing, lockout, rate limit | Passwords use ASP.NET Core's `PasswordHasher` (PBKDF2, per-user salt); an unknown username still costs one hash verification (no timing shortcut). 5 wrong passwords lock the account for 15 minutes; 10 sign-in attempts per minute per address; every attempt is recorded in the login history. Findings O1-O3 below. |
| **Session** | Cookie flags, expiry, server re-check | `HttpOnly`, `SameSite=Strict`, `Secure` when `Auth:RequireHttpsCookie` (on in the production template; tested). Sliding 10 h (7 days with "remember me"). The session is re-checked against the database every 2 minutes: deactivation, password change or reset ends other sessions; role and branch changes apply without signing out (tested). Sign-in tokens are not stored in the browser (`localStorage` holds only language, sidebar state and label sizes). |
| **Cross-site request forgery** | `CsrfFilter` | Every POST/PUT/PATCH/DELETE, sign-in included, needs the anti-forgery header; the whole API surface is tested for refusal without it. |
| **Authorisation** | `[HasPermission]` on actions, fallback policy, role matrix tests | Deny-by-default (fallback policy requires sign-in). New test fails the build if any action lacks `[HasPermission]`/`[AllowAnonymous]` unless it is on a short, explicit "signed-in users only" list (5 entries: own session, own password, branch switch, language, department lookup list). |
| **Tenant and branch isolation** | Three layers (composite foreign keys, query filters, save guard), `IgnoreQueryFilters` rule, visibility gates | Enforced and tested by earlier phases (cross-organisation reads, writes, moves, raw SQL, branch-limited users). Unchanged by this phase; no new finding. |
| **Injection** | All raw SQL in the application (4 statements: stock balance, cash-drawer lock, two number-sequence statements) | All use EF's interpolated-string API, so every value is a SQL parameter; each also filters by organisation. No string-built SQL. |
| **Cross-site scripting** | Frontend search for `dangerouslySetInnerHTML`, `innerHTML`, `eval`, `document.write`; security headers | None used; React escapes output. A Content-Security-Policy (same-origin only, no inline scripts, no `eval`, `frame-ancestors 'none'`) is now sent and the app loads under it with no violations (checked on the start page; not every screen). `style-src` still allows inline styles because the UI uses them (accepted, Low). |
| **Open redirects** | Frontend search for redirect parameters | None; the only `window.location` use reads a tab name and checks it against two fixed values. `react-router` versions with redirect vulnerabilities were upgraded (F1). |
| **Input validation and uploads** | Existing tests; logo/signature handling | Images are accepted only after their bytes are checked as PNG/JPEG within size limits, served with `nosniff`; a disguised SVG and oversized images are refused (tested). Request size limit 2 MB at IIS. Text fields carry length limits through the DTOs; the database also refuses what validation misses. |
| **Error messages** | `GlobalExceptionHandler` | Clients get a stable error code and a generic message; details are logged, never returned. Another organisation's record gives "not found", not "forbidden". |
| **Headers and transport** | Response headers in tests and in a published build | `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy`, CSP, `Cache-Control: no-store` on `/api/*`; HSTS and HTTPS redirect when `Security:RequireHttps` is on (tested). Swagger is served only in Development (checked on a Production-mode build: 401). |
| **Secrets** | Search of tracked and untracked files for passwords, keys, certificates, connection strings; `.gitignore` | None found. `appsettings.Production.json`, `*.local.md` (development logins), keys, certificates and backups are git-ignored; the production settings template contains no password (Windows authentication). Data Protection keys can be encrypted with the machine's DPAPI (tested). |
| **Logging** | `LogContentTests`, review of all four logging sites | Patient fields, search terms and passwords do not reach the application log (tested). SQL Server error text, which quotes data values, is withheld (fixed, tested). Open: IIS access log (O5). |
| **Dependencies** | `dotnet list package --vulnerable --include-transitive`; `npm audit` | NuGet: none vulnerable. npm production dependencies: **0** (3 High fixed). `npm audit` including development tools: 7 Moderate, all in `minimatch` through `typescript-eslint` (lint tooling; not in the delivered bundle). To be re-run before every release. |
| **Database permissions** | Least-privilege role, deny rules, whole suite as that login | The running application cannot change structure, back up or restore; **`audit.AuditLogs` and `inventory.StockMovements` cannot be updated or deleted by it**, and the migration history is read-only. Verified by tests that attempt each forbidden statement. |
| **Backups** | Scripts, a restore under test and the read-only status page (`GET /api/backup/status`, `backup.manage` only; reads a configured folder, never writes or runs anything; tested for permission, read-only and unreadable-folder behaviour) | Full, differential and log backups can be encrypted (AES-256, certificate); copies off the machine are checked by SHA-256; restores to a chosen moment were run on scratch databases. Not yet run on the server. The certificate backup is the critical secret (see `BACKUP_RESTORE.md`). |
| **Denial of service** | Rate limits, request size | Sign-in limit per address and a general limit per user (default 600/min, a placeholder until the load test). No limit yet on expensive reports/searches beyond that. |

## 3. Findings fixed in this phase
| # | Severity | Finding | Fix | Proof |
|---|---|---|---|---|
| F1 | **High** | Three high-severity npm advisories in production dependencies (`react-router`, `react-router-dom`: open redirects/XSS; `zod`: memory exhaustion) | Updated (`npm audit fix`) | `npm audit --omit=dev`: 0; lint and build clean; suite passes |
| F2 | **High** (production) | No HTTPS handling: no redirect, no HSTS, cookie `Secure` optional | `Security:RequireHttps` (redirect + HSTS), `Auth:RequireHttpsCookie`; both on in the production template | `SecurityHardeningTests` (redirect, HSTS, `Secure` cookie). Real certificate: see section 5 |
| F6 | Medium-High | Application database account undefined; audit log and stock ledger append-only only by convention | `Grant-LimsAppPermissions.sql` with `DENY UPDATE/DELETE`; test suite runs as that login | Test attempts update/delete/create/drop/alter and expects refusal |
| N1 | Medium | A stock transfer **updated** its two ledger rows after inserting them, contradicting the append-only rule (found only because the suite ran as the restricted login) | The "in" row is inserted pointing at the "out" row; no update | Inventory transfer test passes as restricted login |
| N2 | Medium | SQL Server error messages (which quote CNIC, names, other values) would have been written to log files by exception logging | `ScrubbingSink` replaces them by error number/class/state, keeps stack traces, including inside `AggregateException` | `LogContentTests` with real SQL errors |
| F3 | Medium | No Content-Security-Policy; API responses with patient data cacheable | CSP, `Permissions-Policy`, `Cache-Control: no-store` on `/api/*` | `SecurityHardeningTests`; app loads under CSP |
| F5 | Medium | Data Protection (sign-in) keys unencrypted and not in the backup procedure | `DataProtection:ProtectWithDpapi`; keys folder in the config archive | Test reads the key file and finds an encrypted element |
| F7 | Medium | `db seed-demo` could create demo data on a production database | Refuses unless Development or `--allow-demo` | Run in Production mode: refused, exit code 1 |
| F8 | Medium | "Every action declares its access" enforced only by convention | Reflection test over all controller actions | `SecurityHardeningTests`; it found the lookup-list action now on the list |
| F4 | Low-Medium | Only sign-in was rate-limited | General per-user limit | `SecurityHardeningTests` (429 after the limit) |
| N3 | Low | No way to create a real organisation without `seed-demo`'s demo organisation | `db create-org` command | Run on an empty database with `db migrate` and `db create-admin` |

Also done, not security findings: configurable log and key folders; `/api/health` reports pending migrations; backup scripts with failure reporting.

## 4. Open findings that need an owner decision
| # | Severity | Finding | Options |
|---|---|---|---|
| O1 | Low-Medium | **Lockout reveals which usernames exist and can be used to lock someone out.** After 5 wrong passwords an existing account answers "locked" (423) while an unknown name keeps answering "invalid" (401). Anyone on the LAN can therefore confirm usernames, and can keep a named colleague locked out by repeating wrong attempts (limited by 10 attempts per minute per address, and 15-minute locks). | (a) accept, since usernames are internal and a lock-out is visible to the lab manager through the login history; (b) answer "invalid" in both cases and tell the user by other means; (c) lock by address-and-username pair. Recommend (a) for now. |
| O2 | Low | Password rule is 8 characters with a letter and a digit, no breached-password list. The system holds patient data. | Raise the minimum to 10-12 characters; keep the rule of no username inside the password. A change in `PasswordPolicy` and its tests. |
| O3 | Low | "Remember me" keeps a person signed in for 7 days. Reception PCs may be shared. | Remove the option, shorten it, or keep it only for named computers. |
| O4 | Low | `/api/health` is public and shows database state, migration state and server time. Useful to monitoring, harmless to most people, but it tells an outsider whether the database is down. | Keep (needed by the deploy script and monitoring), or restrict by network (IIS IP restriction) to the monitoring machine. |

## 5. Open findings that can only be closed on the server
| # | Severity | What is needed | Where |
|---|---|---|---|
| S1 | **High** until done | A real certificate, an https-only binding, HTTPS redirect port, then check headers (`Strict-Transport-Security`) and the `Secure` cookie from a lab PC | `DEPLOYMENT.md` sections 2 and 7 |
| S2 | Medium | **IIS access log records the query string**, so patient searches (names, CNIC, mobile) sit in clear text with client addresses: untick `cs-uri-query`, restrict the folder, set retention | `DEPLOYMENT.md` section 6b |
| S3 | Medium | `AllowedHosts` is `*` in the repository default; production template names one host; needs the real server name (open question) | `appsettings.Production.json` |
| S4 | Medium | Application pool identity, folder permissions and database logins created as written; `Grant-LimsAppPermissions.sql` run with a Windows login (tested only with a SQL login) | `DEPLOYMENT.md` section 2 |
| S5 | Medium | Backup tasks registered (needs an elevated session; only `-WhatIf` ran), backup certificate created **and its private key stored off the server**, off-machine destination chosen (decisions 49-50), timed restore drill | `BACKUP_RESTORE.md` |

## 6. Accepted risks and design notes
- `style-src 'unsafe-inline'` remains because the interface uses inline styles. Scripts have no inline allowance.
- The audit viewer shows the whole organisation's log to holders of the audit permission, and viewing it is not itself logged (decision 43).
- Unmasked patient identity data is logged when viewed (`Patient.ViewSensitive`), and masked for people without the permission; the audit log never stores identity values.
- Business rules not security-tested here but carried from earlier phases and still open: cash refund role, supervisor closing an abandoned drawer, enforcing a discount limit (listed in `IMPLEMENTATION_STATUS.md`). They concern fraud control and should be decided before go-live.
- Role design: roles are editable per organisation, so a System Administrator can grant any permission, including clinical ones. That is by design; the audit log records role changes.

## 7. Not examined
Two-factor sign-in, single sign-on, e-mail/SMS (not built). Windows Server hardening, firewall, antivirus, certificate authority operation, physical security, staff procedures (the lab's IT person). The analyzer interface (Phase 14, does not exist). Browser-side behaviour on printers and scanners. Load behaviour and denial-of-service resistance beyond the rate limits (Phase 13 workstream B has not been run). Third-party code beyond what the dependency audits report. The audit log's own content and retention (decision 52).

## 8. Before go-live (checklist)
1. Close S1-S5 on the real server and record the output.
2. Decide O1-O4 and the three business items in section 6.
3. Repeat `dotnet list package --vulnerable --include-transitive` and `npm audit --omit=dev` on the release being installed.
4. Run the full test suite on the release (with no development server holding the build files).
5. Consider an independent penetration test if the system is reachable from outside the lab network (decision 53).
