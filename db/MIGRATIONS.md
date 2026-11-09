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
