# Phase 12: live dashboard, audit viewer and system settings

_Status: **built and tested** (see `IMPLEMENTATION_STATUS.md`). The decisions below (39-45 in `DECISIONS.md`) were taken as sensible defaults so the phase could proceed; they are open for the owner to change._

## 1. Scope
**Live dashboard** (`GET /api/dashboard/summary?range=today|d7|d30`) replaces the sample data of the Phase 1 prototype. For the working branch (every branch the user may see when none is selected), all from live data:
- **Patients and orders**: total patients (organisation-wide), new registrations and orders in the period, each with the change against the previous period of the same length (a change from nothing shows as +100%).
- **Samples**: waiting to be collected or on their way, and *overdue* = still planned or collected more than 24 hours after being created.
- **Results**: in progress (drafted, submitted or verified, not released), the departments they are in, awaiting approval (verified) with the oldest wait, the six oldest waiting for approval (with a critical/abnormal/normal flag), and released in the period.
- **Reports**: issued in the period and orders ready for a report.
- **Money**: payments received in the period (counter payments: cash, card, transfer, wallet, cheque; corporate allocations are not counted twice), and a daily trend of payments received against corporate credit billed. Corporate outstanding = unpaid balance of credit invoices and how many clients owe it.
- **Pipeline**: samples awaiting collection / collected now, received in the period, results in process now, results released in the period, samples rejected in the period.
- **Recent activity**: registrations, payments, approved results, rejected samples and critical values, newest first.
- A part of the page is shown only if the user holds its permission (`patients.view`, `orders.view`, `samples.view`, `results.view`, `reports.view`, `billing.view`, `corporate.view`); other parts come back empty and are hidden. The page refreshes itself every minute.

**Audit viewer** (`GET /api/audit`, `/api/audit/{id}`, `/api/audit/filters`; permission `audit.view`): the organisation's audit log, newest first, filtered by date, kind (created / changed / deleted / named actions), record type, and free text (record number, type, action, reason). A line opens to show the old and new value of every field. **Read-only**: there is no way to create, change or delete a line through the system. Sensitive fields (identity numbers, email, addresses, password data) were masked when the line was written, so they cannot appear here.

**System settings** (`GET|PUT /api/settings`; permission `settings.manage`): a screen for the five settings the system really acts on:
`Samples.RequirePaymentBeforeCollection`, `Results.AllowSelfVerification`, `Results.BlockApprovalUntilCriticalNotified`, `Inventory.AllowNegativeStock`, `Inventory.ExpiryAlertDays` (1-365). Values are validated, a change carries the version the user saw (a stale page cannot overwrite a newer value), every change is audited with old and new value, and each can be reset to its default.

**Out of scope**: exporting the dashboard or the audit log to Excel/PDF, saved audit searches, financial and operational reports (a reporting module), settings for things the system does not yet act on (the `Billing.MaxDiscountPercentWithoutApproval` setting exists in the database but is not enforced, so it is **not** offered), per-branch setting overrides, notifications.

## 2. Database
No migration: the phase reads existing tables and edits existing `core.BusinessSettings` rows (a row is created on first change; the five defaults used by the code are listed in `SettingsCatalog`).

## 3. Design notes
- Samples, results and reports are read through the services that own their branch-visibility rules (`SampleService`, `ResultService.DashboardAsync`, `ReportService.DashboardAsync`, `CriticalAlertService.RecentAsync`), so the dashboard can never show more than those screens do. The architecture tests that guard those gates still pass.
- The audit table has no automatic organisation filter (system events have no organisation), so every audit query is scoped to the caller's organisation by hand and tested against another organisation.
- Pure rules (`DashboardRules`, `AuditRules`, `SettingsCatalog`) live in the Application layer with unit tests.

## 4. Screens
Home (dashboard), Administration → Audit log, Administration → System settings. English and Urdu RTL.
