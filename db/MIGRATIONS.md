# Database migration history

- **Code:** EF Core migrations in `src/Lims.Infrastructure/Persistence/Migrations`.
- **History table:** `core.__EFMigrationsHistory`.
- **Script:** `db/scripts/migrate-idempotent.sql`, regenerated after every migration. It is safe to run more than once.

| # | Migration | Date | Phase | Description |
|---|---|---|---|---|
| 1 | `20261109060843_InitialFoundation` | 2026-11-09 | 2 | See below. |
| 2 | `20261109092300_AddPatientsAndDoctors` | 2026-11-09 | 4 | `patient` schema: Patients, Doctors (soft delete, audited). |
| 3 | `20261109094313_AddTestCatalog` | 2026-11-09 | 5 | `catalog`: categories, tests, branches, parameters, options, reference ranges (draft/approved/retired), packages. |
| 4 | `20261109100152_AddOrdersAndBilling` | 2026-11-09 | 6 | `lab`: Orders, OrderItems. `billing`: Invoices, InvoiceLines, Payments, Refunds, CashDrawerSessions. |
| 5 | `20261109103202_HardenOrderBilling` | 2026-11-09 | 6 audit | CHECK constraints: order priority/status, order-item status, refund method. |
| 14 | `20261111203221_AddReportSignatories` | 2026-11-12 | Reports (owner request) | New table `lab.ReportSignatories`: signatories printed on every new report from the template (name English/Urdu, qualification, designation, registration number, signature image PNG/JPEG up to 200 KB with a CHECK like the user signature). Removing one only sets `IsActive = 0`, so issued reports keep showing it. Report snapshot schema 3 copies them at issue. No data change. Idempotent script regenerated. |
| 13 | `20261111193546_AddReportBannerImages` | 2026-11-12 | Reports (owner request) | `lab.ReportTemplates` gains `HeaderImage`, `HeaderImageContentType`, `FooterImage`, `FooterImageContentType` (full-width header and footer images, PNG or JPEG up to 600 KB, CHECK constraints like the logo's). Nullable columns only: no data change. Idempotent script regenerated. |
| 12 | `20261111183315_AddReportLayouts` | 2026-11-11 | Reports (owner request) | New table `lab.ReportLayouts`: named report designs per organisation (name, default flag, design settings as JSON). CHECK that the design is valid JSON; unique name per organisation; filtered unique index allows one default design per organisation. No data change: with no design saved, reports keep the built-in classic look. Idempotent script regenerated. |
| 11 | `20261110154051_AddPerformanceIndexes` | 2026-11-10 | 13 | 21 indexes found necessary by the 550,000-order load test (`docs/PERFORMANCE.md`): report, result, invoice, order, sample, payment, audit and patient lookups and sorts (`PerformanceIndexConfigurations.cs`). Indexes only: no data or table change. Building them locks the tables for a few seconds each at this volume: apply outside opening hours. Idempotent script regenerated. |
| 10 | `20261110081027_AddInventory` | 2026-11-10 | 11 | New schema `inventory`: `Suppliers`, `StockItems`, `StockBatches`, `StockMovements`, `PurchaseReceipts`, `PurchaseLines`, `ConsumptionProfileLines`, `StockShortfalls`. CHECK constraints keep stock non-negative (except the `UNBATCHED` catch-all) and ledger direction consistent with type; unique automatic-charge key per test run. Permissions `inventory.*` already exist. New sequences `PurchaseReceipt` (`GRN`) and `StockTransfer` (`STR`) are created on first use. No data to backfill. Idempotent script regenerated. |
| 9 | `20261110073457_AddCorporateBilling` | 2026-11-10 | 10 | `billing.CorporateClients`, `CorporateContracts`, `CorporateContractPrices`, `CorporateReceipts`, `CorporateAllocations`; `lab.Orders` and `billing.Invoices` gain `CorporateClientId` and `ContractId` (Invoices also `DueDate`); payment-method CHECK now allows `Corporate`; the system Corporate Billing Officer role of every organisation gets `orders.create` and `catalog.view`. New sequence `CorporateReceipt` (`CRC`, monthly) is created on first use. Idempotent script regenerated. |
| 8 | `20261110063645_AddReports` | 2026-11-10 | 9 | `lab.LabReports`, `ReportTemplates`, `ReportTemplateTranslations`; `security.Users` gains `RegistrationNo`, `SignatureImage`, `SignatureContentType`; permissions `reports.issue` and `reports.templates`, granted by the migration to the existing System Administrator, Lab Manager (both) and Pathologist (issue) roles of every organisation. One live version per order, hash and final-status CHECKs. New sequence `Report` (`REP`, monthly) is created on first use. |
| 7 | `20261110061320_AddResults` | 2026-11-10 | 8 | `lab.TestResults`, `TestResultValues`, `ResultApprovals`, `CriticalValueAlerts`, `Worklists`, `WorklistItems`. Filtered unique indexes: one in-progress and one released version per ordered test, one open worklist per test. CHECK constraints tie each status to who and when. New sequence `Worklist` (`WL`, daily) is created on first use. No data to backfill. |
| 6 | `20261109110026_AddSamples` | 2026-11-09 | 7 | `lab.Samples`, `SampleTests`, `SampleEvents`, `SampleRejectionReasons`, `SampleTransfers`, `SampleTransferItems`; alternate key `(OrganizationId, Id)` on order items; seeds the 10 default rejection reasons for existing organisations. After applying, run `dotnet run --project src/Lims.Api -- db plan-samples` once to plan samples for older orders (repeatable, safe). |

**What `InitialFoundation` creates:**
- **Schemas:** `core`, `security`, `catalog`, `audit`.
- **core:** Organizations, Branches, Departments, NumberSequences, BusinessSettings.
- **security:** Users, Roles, Permissions, RolePermissions, UserRoles, UserBranches, LoginHistory.
- **catalog:** Units, SampleTypes.
- **audit:** AuditLogs.
- **Seed data:** 48 permissions, 42 units, 16 sample types. Identity values for units and sample types are reseeded to start at 1000.

## Commands

**Add a migration** (from the repository root):

```bash
dotnet ef migrations add <Name> --project src/Lims.Infrastructure --startup-project src/Lims.Api --output-dir Persistence/Migrations
```

**Regenerate the idempotent script:**

```bash
dotnet ef migrations script --idempotent --project src/Lims.Infrastructure --startup-project src/Lims.Api -o db/scripts/migrate-idempotent.sql
```

**Apply pending migrations** to the configured database:

```bash
dotnet run --project src/Lims.Api -- db migrate
```

**Create a demo organisation** (development and training only):

```bash
dotnet run --project src/Lims.Api -- db seed-demo
```

## Rules

- Never edit a migration that has been applied to a shared or production database. Add a new one instead.
- Every migration is tested on a fresh database: the integration tests create `LIMS_Test_*`, apply every migration and drop it afterwards.
- Seeded IDs (permissions, units, sample types) are never renumbered. New rows are appended.
- Production: back up, apply, verify. Rollback = restore the backup (see `docs/BACKUP_RESTORE.md`).
