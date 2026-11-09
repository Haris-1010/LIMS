# Implementation status

_Last updated: 2026-11-09_

| Phase | Scope | Status |
|---|---|---|
| 1 | Inspection, architecture, design system, login + dashboard prototype | **Done** (prototype) |
| 2 | Database: ERD, migrations, seed data | **Done** (foundation; module tables added per phase) |
| 3 | Auth, users, roles, permissions, organisations, branches | **Done** |
| 4 | Patients and doctors | **Done** |
| 5 | Test catalog, parameters, packages, reference configuration | **Done** |
| 6 | Orders, invoices, payments, receipts, cash drawer | **Done** |
| 7 | Sample collection, barcodes, tracking | Not started |
| 8 | Worklists, result entry, verification, critical alerts | Not started |
| 9 | Bilingual reports, PDF, printing | Not started |
| 10 | Corporate billing and reconciliation | Not started |
| 11 | Inventory and stock ledger | Not started |
| 12 | Live dashboard, audit viewer, system settings | Not started |
| 13 | Security review, performance, IIS deployment, backup/restore | Not started |
| 14 | Analyzer integration (needs device specs) | Blocked: needs device model, protocol and interface documentation |

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
