# LIMS — Laboratory Information Management System

Offline-first, bilingual (English / اردو) LIMS for a multi-branch diagnostic laboratory.
ASP.NET Core + SQL Server backend, React + TypeScript frontend, hosted on one on-premises Windows server.

## Run in development

Requirements: .NET 9 SDK, Node 20+, SQL Server (default instance).

First time only: restore the local EF tool, create the `LIMS_Dev` database, and add a demo organisation.

```bash
dotnet tool restore
```

```bash
dotnet run --project src/Lims.Api -- db migrate
```

```bash
dotnet run --project src/Lims.Api -- db seed-demo
```

Then start the API:

```bash
dotnet run --project src/Lims.Api --launch-profile http
```

```bash
npm --prefix web install
```

```bash
npm --prefix web run dev
```

Open http://localhost:5173. The dev server forwards `/api` to the API on port 5088.
Swagger: http://localhost:5088/swagger

## Tests

```bash
dotnet test
```

The integration tests create a throwaway database (`LIMS_Test_*`) on `localhost`, apply all migrations and drop it afterwards. Set `LIMS_TEST_SERVER` to use another SQL Server.

## Build for deployment

```bash
npm --prefix web run build
```

```bash
dotnet publish src/Lims.Api -c Release -o publish
```

The web build is written to `src/Lims.Api/wwwroot`, so the published API serves the whole application.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Data model (ERD)](docs/ERD.md)
- [Backup, restore and rollback](docs/BACKUP_RESTORE.md)
- [Design system](docs/DESIGN_SYSTEM.md)
- [Decisions & open questions](docs/DECISIONS.md)
- [Implementation status](docs/IMPLEMENTATION_STATUS.md)
- [Migration history](db/MIGRATIONS.md)
