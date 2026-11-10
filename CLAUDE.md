# LIMS — working notes for Claude

Offline-first, bilingual (English / Urdu RTL) Laboratory Information Management System for a multi-branch diagnostic lab. Runs on a central Windows server on the LAN (IIS); **no CDN and no internet dependency**. Current status per phase: `docs/IMPLEMENTATION_STATUS.md`. Decisions and open questions: `docs/DECISIONS.md`.

## Rules of the project
- Never say something works unless it is implemented **and** tested.
- Do not start the next phase, or add features outside the current one, without the owner's approval. Do not commit or push without approval.
- Fix confirmed bugs only; do not rewrite working modules. Run the relevant tests after every fix.
- Preserve existing behaviour; follow the established architecture below.

## Stack and layout
- .NET 9, EF Core 9, SQL Server. Projects: `Lims.Domain` → `Lims.Application` → `Lims.Infrastructure` → `Lims.Api` (serves the built SPA from `wwwroot`), tests in `tests/Lims.UnitTests` and `tests/Lims.IntegrationTests`.
- Frontend `web/`: React 19, Vite 6, TypeScript (strict), React Router 7, TanStack Query 5, react-i18next, react-hook-form + zod. Build output goes to `src/Lims.Api/wwwroot`.
- SQL schemas: `core`, `security`, `catalog`, `patient`, `lab`, `billing`, `audit`. Migration history table `core.__EFMigrationsHistory`; idempotent script `db/scripts/migrate-idempotent.sql`; log in `db/MIGRATIONS.md`.

## Commands
```bash
dotnet build Lims.sln
dotnet test                                   # unit + integration (integration creates a throwaway LIMS_Test_* database)
dotnet run --project src/Lims.Api -- db migrate | seed-demo | plan-samples | create-admin <ORG> <user> "<Name>"
cd web && npm run dev | npm run build | npm run lint
```
Dev database `LIMS_Dev` on the default local SQL Server instance. Dev logins are in `db/dev-accounts.local.md` (git-ignored).

## Architecture rules (the ones marked "test" are enforced by automated tests)
- **Tenant isolation, three layers:** composite `(OrganizationId, Id)` foreign keys, global query filters in `LimsDbContext` (tenant, branch, soft delete), and a save guard. `IgnoreQueryFilters()` disables *all* filters for the whole query, so every statement using it must also scope by `OrganizationId` (test).
- **Samples bypass the branch filter on purpose.** A user sees a sample if their branches include its origin, current or destination branch. This lives only in `SampleService.Visible()`; every other `db.Samples` use must carry a `VISIBILITY-GATE` comment or be an `.Add(` (test). Transfer manifests are visible to users of either branch (`TransferService`).
- **Results bypass the branch filter too.** A result is visible when its order branch or its lab branch is in the user's access (`ResultService.Visible()`); other `db.TestResults` uses need a `VISIBILITY-GATE` comment or be an `.Add(` (test). **Issued reports** (`LabReports`) follow the same pattern: only `ReportService` reads them, through `Visible()` (order branch or lab branch in the user's access), marked `VISIBILITY-GATE` (test); released results for a report are gathered by `ResultReportSource` inside the results folder. An issued report is an immutable snapshot with a SHA-256 fingerprint; never rebuild one from live data. Result workflow rules live in `ResultRules`; the person who entered a result cannot verify or approve it; verify, approve, return and amend must carry the `RowVersion` the reviewer saw.
- Every API action carries `[HasPermission(...)]` (several attributes = all required), by convention; state-changing requests need the anti-forgery header (test).
- Concurrency: `RowVersion` on entities, mapped to HTTP 409 `concurrency`. Money and order/invoice creation use idempotency keys; cash drawer rows are locked inside transactions.
- Every server error `code` needs an `errors.<code>` message in both `en.json` and `ur.json` (test). Keys and placeholders must match between languages.
- Audit: entities implementing `IAudited` are audited automatically; `[Sensitive]` fields are masked.

## SQL Server gotchas
- Filtered indexes cannot use `NOT IN`; use `<> a AND <> b`.
- Number sequences are atomic `UPDATE … OUTPUT`; sample barcodes are `S-yymmdd-nnnn`, unique per organisation, restarting daily.
- EF merges two `HasIndex` calls over the same columns into one: give each its own name (`HasIndex(x => x.Col, "UX_Name")`) when they differ only in filter.

## Frontend conventions
- Use the shared components in `web/src/components/ui`; logical CSS properties and the `flip-rtl` class for RTL; every string through i18n (en + ur).
- Sidebar and route permissions come from `web/src/app/navigation.ts` (a unit test compares them with the permission catalogue).
- Reports: one `ReportDocument` component draws preview, print and PDF (browser Save as PDF) from the snapshot; its own labels use `getFixedT` by report language, not the interface language. Urdu text needs the Nastaliq font loaded before printing (`PrintReport` waits for it). Images (logo, signatures) are accepted only after `ImageSniffer` checks their bytes.
- Printing: receipts and labels render into `<body>` and rely on print CSS; label printing goes through the app-wide `LabelPrintHost` (`labelBus.printLabels`), so it survives dialogs closing.
- Page size is clamped to 100 by the API.

## Tests
xUnit. Integration tests run the real API through `WebApplicationFactory` (`ApiFactory`, `ApiClient`) against a fresh database. Keep new behaviour covered there, plus unit tests for pure rules (`InvoiceCalculator`, `Code128`, `SampleRules`, `ResultRules`, `ResultValueParser`, `ReportRules`, `ReportSnapshotBuilder`, `ImageSniffer`).
