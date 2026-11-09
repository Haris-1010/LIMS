# LIMS — Architecture

## Deployment model

```
 Branch PCs (browser)  ──LAN / VPN──►  Central Windows Server
                                       ├─ IIS → Lims.Api (ASP.NET Core)
                                       │        └─ wwwroot/ (built React app)
                                       └─ SQL Server (LIMS database, local backups)
```

- One central server hosts the API, the web app and SQL Server. Clients only need a browser.
- No internet needed at runtime: fonts, icons and scripts are bundled into `wwwroot`.
- No database copies on client PCs.
- **Assumption (unconfirmed):** every branch reaches the central server over LAN, VPN or a leased line. Branches that work fully disconnected and sync later are out of scope until approved (see `DECISIONS.md`).

## Solution layout

| Path | Purpose |
|---|---|
| `src/Lims.Domain` | Entities, value objects, domain rules. No dependencies. |
| `src/Lims.Application` | Use cases, DTOs, validation, interfaces (e.g. current user, clock). |
| `src/Lims.Infrastructure` | EF Core `LimsDbContext`, migrations, SQL Server, file storage, password hashing. |
| `src/Lims.Api` | Controllers, auth, ProblemDetails error handling, Serilog, Swagger, static web hosting. |
| `tests/Lims.UnitTests` | Unit tests (integration tests against a throwaway SQL database are added in Phase 2). |
| `web/` | React + Vite + TypeScript frontend. `npm run build` outputs into `src/Lims.Api/wwwroot`. |
| `db/` | Hand-written SQL (views, backup scripts) and the migration history. |
| `docs/` | Architecture, status, design system, decisions. |

## Backend

- **.NET 9** (the only SDK installed). Moving to .NET 10 LTS is a target-framework change plus package bumps. Recommended before go-live because .NET 9 support ends in November 2026.
- **Data access:** EF Core 9 + SQL Server, one approach throughout. Raw SQL only for reporting views where EF is a poor fit.
  - Migrations history table: `core.__EFMigrationsHistory`.
- **Errors:** `GlobalExceptionHandler` returns RFC 7807 ProblemDetails with a `traceId`. Stack traces are logged, never returned.
- **Logging:** Serilog to the console and to rolling daily files under `logs/`, kept for 30 days.
- **API docs:** Swagger UI at `/swagger` in Development.
- **Health:** `GET /api/health` reports API status and database reachability.

### Cross-cutting rules

Implemented in Phase 2 (details in `ERD.md`):

- **Request context:** `RequestContext` (scoped) says who the caller is: organisation, user, permitted branches. It is empty by default, and an empty context sees no tenant data (fail closed). `RequestContext.System()` is used only by server-side commands (migrate, provisioning) and never by HTTP requests.
- **Tenant and branch isolation:**
  - Every tenant-owned table carries `OrganizationId`, plus `BranchId` where it applies.
  - EF global query filters scope every query to the caller's organisation and permitted branches.
  - Composite foreign keys `(OrganizationId, Id)` make cross-organisation references impossible at the database level.
  - A save guard rejects writes to another organisation.
- **Save pipeline** (`LimsDbContext.Save.cs`): stamps, soft delete and audit rows, all in one transaction.
- **No retrying execution strategy:** the database is on the LAN, and the business workflows use explicit transactions.
- **Admin commands:** `Lims.Api db migrate` and `Lims.Api db seed-demo`.

Planned for Phase 3 onwards:
- **Authentication:**
  - Passwords hashed with ASP.NET Core Identity's `PasswordHasher` (PBKDF2).
  - Sessions in short-lived httpOnly, SameSite=Strict cookies, with an anti-forgery token on state-changing requests.
  - Lockout after repeated failures, and a login history table.
- **Authorization:** permission-based policies (`orders.create`, `results.approve`…). Roles are configurable bundles of permissions; the API checks permissions, never role names.
- **Money and clinical values:** `decimal(18,2)` for money and `decimal(18,6)` for numeric results. All totals are computed on the server.
- **Time:** stored as UTC (`datetime2`), shown in the server's local time zone (Asia/Karachi by default).
- **Transactions:** order + invoice + payment, stock movements and result approval each run in a single database transaction. Idempotency keys prevent duplicate submissions.
- **Audit:** an EF `SaveChanges` interceptor writes before/after values for audited entities to `audit.AuditLogs`.

## Frontend

| Concern | Choice | Why |
|---|---|---|
| Framework | React 19 + Vite 6 + TypeScript (strict) | Fast build to static files, served by IIS/Kestrel with no Node at runtime |
| Routing | React Router 7 | Nested layout (shell + pages), deep links work after refresh |
| Server state | TanStack Query 5 | Caching, retries, no duplicate requests |
| Forms | react-hook-form + zod | Typed validation with translated messages |
| i18n | i18next + react-i18next | Central `en.json` / `ur.json`; switches `<html dir>` |
| Icons | lucide-react | One consistent outline icon set, tree-shaken |
| Charts | Recharts | Small, React-native, accessible tooltips |
| Fonts | `@fontsource-variable/inter`, `@fontsource/noto-nastaliq-urdu` | Bundled locally for offline use |

Structure: `src/app` holds navigation, session and providers. `src/components/ui` has the design-system primitives and `src/components/layout` the shell. `src/features/<module>` has the pages plus that module's API client, and `src/i18n` the translations.

Navigation is defined once in `src/app/navigation.ts`, with a permission key and delivery phase per item. The sidebar, routes and breadcrumbs are all generated from it.
