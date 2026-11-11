# Performance (Phase 13, workstream B)

_Measured 2026-11-10 on the development PC. **These are not production figures**: SQL Server, the application and the load tool all ran on one 8-core / 24 GB Windows 11 machine, the data is synthetic, and nothing was measured over a network or through IIS. The method, the tools and the fixes are reusable; the numbers must be repeated on the test server (`PHASE13_PLAN.md`, section 1, item 2) before anything is called verified._

## 1. What was measured
- **Data volume** (decision 51, assumed): `db seed-load 550000` filled a database named `LIMS_Load_Full` with 5 years of work at 300 orders a day: 550,121 orders, 302,500 patients, 1.54 million samples, 1.65 million results with 3.3 million values, 467,069 issued reports, 2.2 million audit lines (13.4 GB, 19 minutes to generate). Three branches, 40 tests, 30 staff users.
- **Synthetic data**: written by set-based SQL that satisfies every database constraint (`LoadDataSeeder`), not by the application's own workflow. It has no stock movements, no corporate billing, no result approvals/critical alerts, only card/transfer/wallet payments (no cash drawers), and a placeholder instead of a real report snapshot. The last two days of orders are in mixed in-progress states so the queues are not empty. Real data will differ in distribution.
- **Tool**: `tests/Lims.LoadTest` signs in the 30 users (10 reception, 6 collection, 8 technicians, 4 pathologists, 2 managers) and times 45 API calls the way the screens make them (searches, lists, queues, order creation with payment, result draft saving, dashboard, audit, report preview). Times are measured at the client including reading the whole response. `--think` is the pause between a user's actions.
- **Targets** (95th percentile, from the plan): searches, lists and queues under **0.5 s**; dashboard, report preview and audit under **1.5 s**.
- Two kinds of figure: **one user** (8 repetitions per call, after a warm-up) and **30 users at once for 180 s** with a 10 s pause between actions (about 3 requests a second, a busy but realistic lab). A harsher run with a 2 s pause (11 requests a second) is reported as a stress test.

## 2. What was found and changed
Every change below was tried in the load database first, kept only if it measurably helped, and then re-measured.

| # | Finding | Change | Effect (one user, p95) |
|---|---|---|---|
| 1 | **No index for common sorts and lookups** (reports by issue date, approved results, samples by status/barcode, payments by date, audit by type) | Migration `AddPerformanceIndexes`: 21 indexes (`PerformanceIndexConfigurations.cs`) | Reports issued list 1.9 s → 0.16 s; verification queue 0.35 s → 0.02 s; orders list for a patient 0.1 s → 0.01 s; collection queue 0.26 s → 0.03 s; samples received 0.9 s → 0.17 s |
| 2 | **The tenant filter `(@IsSystemContext = 1 OR OrganizationId = @org)` stops SQL Server from seeking on indexes that start with `OrganizationId`** when the query's own condition is on another column (an order number, patient id, barcode, status) | Natural-key and foreign-key indexes **without** the `OrganizationId` prefix (it rides along as an included column). Rule added to `CLAUDE.md` | Barcode scan 0.6 s → 0.03 s; order lookup by number 17 s → 0.1 s (together with 4); reports by patient 0.2 s → 0.1 s |
| 3 | **Index sort direction must match the query**: `(Status, ApprovedAtUtc DESC)` did not serve `ORDER BY ApprovedAtUtc DESC, Id DESC` | `(Status, ApprovedAtUtc DESC, Id DESC)`; a test checks column order and direction | Latest approved results 531 ms → 47 ms |
| 4 | **EF Core turns `list.Contains(x)` into `OPENJSON(@json)`**, for which SQL Server guesses the list size and picks scans (a branch or status list: 12 ms as literals, 525 ms as OPENJSON) | `TranslateParameterizedCollectionsToConstants()` in `AddLimsDbContext` | Dashboard 3.5–6 s → 1.1 s; orders list 0.84 s → 0.29 s; payments list 1.0 s → 0.3 s; tracking search 7.8 s → 1.1 s; reports search 31 s → 4.3 s |
| 5 | **Searches were one `OR` across columns of several tables** (order no OR invoice no OR patient name OR phone...), which forced scans | Exact identifiers (order, invoice, report, patient number, barcode, CNIC, full phone) are looked up first, each on its own index, as separate queries joined by `UNION`; only without a match does the wider name search run | Orders search 17 s → 0.1 s; patient search by number/phone/CNIC 1.7 s → 0.18 s; reports search by patient 31 s → 0.47 s |
| 6 | **Replacement-sample lookup scanned all samples** on every sample view | Small filtered index `IX_Samples_ReplacesSampleId` | Viewing a sample by barcode 0.64 s → 0.03 s |
| 7 | Dashboard's remaining cost: recent patients, orders in a date window, approved results grouped by order | `Patients(CreatedAtUtc)`, `Orders(CreatedAtUtc)`, `OrderId` included in the approved-results index | Dashboard 1.05 s → 0.5 s |

**Tried and rejected** (documented so nobody repeats them): appending `OPTION (RECOMPILE)` to every SELECT to let SQL Server fold the tenant filter made most screens *slower* (compile time of 100-300 ms per EF query), so it was removed. Setting `MAXDOP 2` on the database did not help under concurrency. Reader/writer blocking is not a factor (concurrent waits were CPU and parallelism only; runs without any writes behaved the same).

**Behaviour changes to know about**
- Searching: when an exact order/invoice/report number, barcode, patient number, CNIC or full phone number matches, only those records are returned (before, weaker name matches were mixed in). A complete phone number or CNIC (10+ digits, no letters) that matches nobody now returns an empty list immediately instead of scanning.
- A name search in the orders list considers the 1,000 newest matching patients (reports and samples: 500, as before); with more matches, narrow the search.
- No API shape changed. No data was changed.

## 3. Results at 550,000 orders
`1 user` = p95 of 8 repetitions after the changes; `before` = the first clean one-user run (already including the first order-search fix; the original order search timed out after 30 s even at 20,000 orders). `30 users` = p50 / p95 with a 10 s pause, 180 s, n = requests per call (about 3-80, so the p95 of a rarely used call is close to its maximum).

| Operation | before (ms) | 1 user p95 (ms) | 30 users p50 / p95 (ms) | target (ms) | |
|---|---:|---:|---:|---:|---|
﻿| approval queue | 379 | 22 | 26 / 172 | 500 | ok |
| audit: date range | 22 | 36 | 44 / 163 | 1,500 | ok |
| audit: filter by kind and type | 421 | 147 | 155 / 2,358 | 1,500 | ok |
| audit: latest | 143 | 193 | 239 / 380 | 1,500 | ok |
| audit: one record's history | 7 | 7 | 15 / 23 | 1,500 | ok |
| audit: text search | 1,014 | 1,671 | 1,907 / 2,230 | 1,500 | **over** |
| dashboard: 30 days | 5,002 | 551 | 640 / 685 | 1,500 | ok |
| dashboard: 7 days | 5,125 | 508 | 504 / 1,325 | 1,500 | ok |
| dashboard: today | 6,468 | 498 | 531 / 867 | 1,500 | ok |
| inventory: items list | 6 | 7 | 9 / 11 | 500 | ok |
| orders: create with payment | 568 | 105 | 93 / 2,165 | 500 | ok |
| orders: list default (no filter) | 964 | 237 | 218 / 1,869 | 500 | ok |
| orders: list for a patient | 100 | 9 | 11 / 693 | 500 | ok |
| orders: list today | 217 | 17 | 21 / 44 | 500 | ok |
| orders: open one | 474 | 17 | 23 / 138 | 500 | ok |
| orders: outstanding balances | 285 | 94 | 136 / 541 | 500 | ok |
| orders: search by order no | 6,862 | 128 | 133 / 1,262 | 500 | ok |
| patients: duplicate check | 99 | 5 | 8 / 18 | 500 | ok |
| patients: open one | 4 | 5 | 9 / 888 | 500 | ok |
| patients: search by CNIC | 1,292 | 208 | 209 / 409 | 500 | ok |
| patients: search by mobile | 1,409 | 248 | 186 / 871 | 500 | ok |
| patients: search by name | 187 | 275 | 255 / 449 | 500 | ok |
| patients: search by patient no | 1,361 | 209 | 201 / 625 | 500 | ok |
| payments: day summary | 359 | 9 | 22 / 137 | 500 | ok |
| payments: list default | 1,095 | 335 | 308 / 2,159 | 500 | ok |
| payments: list today | 51 | 16 | 21 / 45 | 500 | ok |
| reports: issued list | 1,941 | 163 | 175 / 518 | 500 | ok |
| reports: issued, search by patient | 30,923 | 468 | 613 / 620 | 500 | ok |
| reports: preview for an order | 213 | 55 | 69 / 1,231 | 1,500 | ok |
| reports: ready to report | 284 | 357 | 566 / 2,291 | 500 | ok |
| reports: versions of an order | 273 | 40 | 33 / 56 | 500 | ok |
| results: critical alerts count | 6 | 3 | 8 / 14 | 500 | ok |
| results: open entry screen | 41 | 18 | 26 / 105 | 500 | ok |
| results: pending list | 378 | 486 | 538 / 3,351 | 500 | ok |
| results: pending, urgent only | 579 | 458 | 534 / 3,341 | 500 | ok |
| results: results of an order | 365 | 180 | 211 / 547 | 500 | ok |
| results: save a draft | 103 | 57 | 77 / 273 | 500 | ok |
| samples: collection queue | 256 | 30 | 37 / 636 | 500 | ok |
| samples: list awaiting receipt | 179 | 17 | 18 / 55 | 500 | ok |
| samples: list received | 898 | 176 | 233 / 536 | 500 | ok |
| samples: samples of an order | 6 | 128 | 40 / 470 | 500 | ok |
| samples: scan a barcode | 592 | 27 | 33 / 140 | 500 | ok |
| samples: tracking search by order no | n/a | 90 | n/a | 500 | ok |
| samples: tracking search by patient name | n/a | 566 | n/a | 500 | **over** |
| verification queue | 346 | 16 | 27 / 995 | 500 | ok |

### Reading the table honestly
- **One user: 43 of 45 calls meet the target.** Over: **audit free-text search** (1.7 s against 1.5 s: it scans four text columns of 2.2 million rows; narrow with the date or type filter, which take 0.03-0.15 s) and **tracking search by patient name** (0.57 s against 0.5 s). Close to the line: results pending list (0.49 s), reports search by patient (0.47 s), dashboard (0.45-0.55 s).
- **30 users at once: the middle (p50) is good, the tail (p95) is not.** The p50 of almost every call is under 0.3 s, and the heavier ones (pending results, ready-to-report, dashboard, audit text) sit around 0.5-0.6 s or above. But the p95 shows 1-4 second stalls on otherwise fast calls (for example orders list for a patient 11 ms p50, 693 ms p95), a few per run. They are not locks and not a particular query; they appear when a heavy call (dashboard, pending list) runs on the same machine at the same moment. With SQL Server, the application and the load generator sharing 8 cores this is expected and **cannot be judged here**; it is the main thing to re-measure on separate server hardware.
- **Stress (11 requests a second, 4 times a busy lab): no errors, median call 0.68 s**, but the heavy screens reach 2-4 s (dashboard 7 days p95 3.9 s, pending list 3.4 s, ready-to-report 3.0 s). The system is not meant to sustain that rate; it shows which screens would limit growth.
- Not measured: report PDF/print time (browser side), the login page, the audit "one record" and inventory pages at volume (inventory has no volume in the load data), IIS and TLS overhead, a LAN, first-load time of the web app (compression is enabled in `web.config`; long-lived caching headers for the fingerprinted assets were **not** added, finding F13 is therefore only partly addressed).

## 4. Things this does not prove, and what to do next
1. Repeat on the test server with the same tool (commands below) and the real SQL Server edition. Compare against the table; expect better tails on separate hardware, and re-check the audit text search and the pending-results list.
2. Real data: after a month of real use, run the same calls against a copy of the real database; the dashboard and pending list depend on how many results are in progress at a time.
3. Costs added by this phase: 21 indexes slow writes slightly (order creation with payment measured 83 ms one user) and add about 630 MB at this volume (of 13.4 GB). The migration builds them in seconds at 550,000 orders but would lock the tables while it runs: apply it outside opening hours (it is in `migrate-idempotent.sql`).
4. If the audit log grows beyond about 5 million lines, give the free-text search its own treatment (decision 52 left retention open).

## 5. How to repeat
```powershell
# 1. a database named LIMS_Load_* (the seeder refuses any other name), migrated, then filled (about 20 minutes for 550,000 orders)
$env:ConnectionStrings__Lims = "Server=localhost;Database=LIMS_Load_Full;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True"
dotnet run -c Release --project src/Lims.Api -- db migrate
dotnet run -c Release --project src/Lims.Api -- db seed-load 550000
# 2. start the application against it (Production mode; raise the sign-in and request limits so 30 users from one address are not refused)
$env:Auth__LoginAttemptsPerMinute = "100000"; $env:RateLimit__RequestsPerMinute = "1000000"; $env:ASPNETCORE_URLS = "http://localhost:5099"
dotnet run -c Release --project src/Lims.Api --no-launch-profile
# 3. time it
dotnet run -c Release --project tests/Lims.LoadTest -- --base http://localhost:5099 --orders 550000 --seconds 180 --baseline 8 --think 10000 --out timing.md
```
`--only "text"` runs just the calls whose name contains the text; `--writes false` skips order creation and result drafts. To see the SQL behind a slow call, start the application with `Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Command=Information` (parameter values are masked) and read the log. The load users' password is in `LoadDataSeeder`; **never run `seed-load` anywhere but a throw-away `LIMS_Load_*` database**.
