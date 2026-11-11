using System.Diagnostics;
using Lims.Application.Abstractions;
using Lims.Application.Security;
using Lims.Domain.Catalog;
using Lims.Domain.Patients;
using Lims.Infrastructure.Persistence;
using Lims.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Lims.Api.Cli;

/// <summary>
/// Phase 13 load-test data: `db seed-load [orders]` fills a database with a realistic laboratory history (default 550,000
/// orders over five years, about 300 a day) using set-based SQL. It refuses to run on any database whose name does not start with
/// "LIMS_Load", so it can never touch a real installation. The data is synthetic: it satisfies the database constraints and is
/// what the queues, lists and search screens read, but it was not produced by the application's own workflow (see
/// docs/PERFORMANCE.md for what that means for the figures).
/// </summary>
public static class LoadDataSeeder
{
    public const string OrgCode = "LOADLAB";
    public const string UserPassword = "Load-test-2026";   // load databases only; the guard above keeps this off real systems

    public static async Task<int> RunAsync(WebApplication app, LimsDbContext systemDb, string[] args, ILogger log)
    {
        var dbName = systemDb.Database.GetDbConnection().Database;
        if (!dbName.StartsWith("LIMS_Load", StringComparison.OrdinalIgnoreCase))
        {
            log.LogError("seed-load only runs on a database whose name starts with LIMS_Load (this one is '{Name}'). Create one with db migrate first.", dbName);
            return 1;
        }
        var orders = args.Length > 2 && long.TryParse(args[2], out var n) && n >= 1000 ? n : 550_000;
        if (await systemDb.Organizations.AnyAsync(o => o.Code == OrgCode))
        {
            log.LogError("Organisation {Code} already exists in {Db}: seed-load runs once per database. Drop the database and start again.", OrgCode, dbName);
            return 1;
        }

        var sw = Stopwatch.StartNew();
        using var sysScope = app.Services.CreateScope();
        sysScope.ServiceProvider.GetRequiredService<RequestContext>().BecomeSystem();
        var provisioner = sysScope.ServiceProvider.GetRequiredService<IOrganizationProvisioner>();
        var orgId = await provisioner.ProvisionAsync(new OrganizationSetup(OrgCode, "Load Test Laboratory", null, "MAIN", "Main Laboratory", null));
        log.LogInformation("Organisation created ({Ms} ms)", sw.ElapsedMilliseconds);

        await SeedDimensionsAsync(app, orgId, log);
        log.LogInformation("Users, catalog, doctors created ({Ms} ms)", sw.ElapsedMilliseconds);

        await using var conn = new SqlConnection(systemDb.Database.GetConnectionString());
        await conn.OpenAsync();
        await Exec(conn, "ALTER DATABASE CURRENT SET RECOVERY SIMPLE; " +
            "DECLARE @f nvarchar(300); " +
            "DECLARE c CURSOR FOR SELECT name FROM sys.database_files; OPEN c; FETCH NEXT FROM c INTO @f; " +
            "WHILE @@FETCH_STATUS = 0 BEGIN EXEC('ALTER DATABASE CURRENT MODIFY FILE (NAME = [' + @f + '], FILEGROWTH = 512MB)'); FETCH NEXT FROM c INTO @f; END CLOSE c; DEALLOCATE c;", orgId, orders);

        var steps = Steps();
        foreach (var (name, sql) in steps)
        {
            var t = Stopwatch.StartNew();
            try { await Exec(conn, sql, orgId, orders); }
            catch (SqlException ex)
            {
                // Load databases hold no real data, so the SQL message is safe to print (the log scrubber would hide it).
                log.LogError("Step '{Step}' failed: {Message} (line {Line})", name, ex.Message, ex.LineNumber);
                return 1;
            }
            log.LogInformation("{Step}: {Seconds:N1} s", name, t.Elapsed.TotalSeconds);
        }
        log.LogInformation("Load data ready: {Orders:N0} orders in {Minutes:N1} minutes. Users load.rec01 ... (password in LoadDataSeeder.UserPassword).", orders, sw.Elapsed.TotalMinutes);
        return 0;
    }

    private static async Task Exec(SqlConnection conn, string sql, int orgId, long orders)
    {
        // Values are declared in the batch text, not passed as parameters: a parameterised command runs through sp_executesql,
        // which would drop the temp tables the following steps need.
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var declare = string.Create(inv, $"""
            DECLARE @org int = {orgId}, @n bigint = {orders}, @p int = {(int)(orders * 55 / 100)}, @live int = {(int)Math.Max(60, orders * 2 / 1825)},
                    @span float = {5.0 * 365 * 86400:R}, @now datetime2 = '{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fff}';

            """);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = declare + sql;
        cmd.CommandTimeout = 0;
        await cmd.ExecuteNonQueryAsync();
    }

    // ---------------------------------------------------------------- small dimension tables through the real code paths

    private static async Task SeedDimensionsAsync(WebApplication app, int orgId, ILogger log)
    {
        using var scope = app.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<RequestContext>();
        ctx.BecomeSystem();
        ctx.OrganizationId = orgId;
        var db = scope.ServiceProvider.GetRequiredService<LimsDbContext>();

        // Branches: the main one plus two collection centres.
        db.Branches.AddRange(
            new Domain.Core.Branch { OrganizationId = orgId, Code = "CC-A", NameEn = "Collection Centre A" },
            new Domain.Core.Branch { OrganizationId = orgId, Code = "CC-B", NameEn = "Collection Centre B" });
        await db.SaveChangesAsync();
        var branches = await db.Branches.OrderByDescending(b => b.IsMain).ThenBy(b => b.Id).Select(b => b.Id).ToListAsync();

        // Catalog: 40 tests, one to three numeric parameters each, spread over four sample types.
        var department = await db.Departments.OrderBy(d => d.Id).Select(d => d.Id).FirstAsync();
        var sampleTypes = await db.SampleTypes.OrderBy(s => s.Id).Select(s => s.Id).Take(4).ToListAsync();
        var category = new TestCategory { OrganizationId = orgId, Code = "LOAD", NameEn = "Load test category" };
        db.TestCategories.Add(category);
        await db.SaveChangesAsync();
        for (var i = 0; i < 40; i++)
        {
            var test = new LabTest
            {
                OrganizationId = orgId, Code = $"T{i + 1:000}", NameEn = $"Load test {i + 1:000}", CategoryId = category.Id, DepartmentId = department,
                SampleTypeId = sampleTypes[i % sampleTypes.Count], Price = 300 + (i % 12) * 150, TurnaroundHours = 24, SortOrder = i,
            };
            for (var p = 0; p <= i % 3; p++)
                test.Parameters.Add(new TestParameter { OrganizationId = orgId, Code = $"T{i + 1:000}P{p + 1}", NameEn = $"Parameter {p + 1}", ResultType = ResultType.Numeric, DecimalPlaces = 2, SortOrder = p });
            db.Tests.Add(test);
        }
        for (var i = 0; i < 50; i++)
            db.Doctors.Add(new Doctor { OrganizationId = orgId, Code = $"D{i + 1:0000}", FullName = $"Dr. Referrer {i + 1}", IsInHouse = i < 3 });
        await db.SaveChangesAsync();

        // Users through the real service (so the password hashes are genuine). Branch-limited roles get one branch each.
        var roles = await db.Roles.ToDictionaryAsync(r => r.Code, r => r.Id);
        var users = scope.ServiceProvider.GetRequiredService<UserService>();
        async Task Make(string role, string prefix, int count, bool limited)
        {
            for (var i = 0; i < count; i++)
                await users.CreateAsync(new UserCreate
                {
                    Username = $"load.{prefix}{i + 1:00}", DisplayNameEn = $"Load {prefix} {i + 1}", Password = UserPassword,
                    AllBranches = !limited, BranchIds = limited ? [branches[i % branches.Count]] : [], RoleIds = [roles[role]],
                }, CancellationToken.None);
        }
        await Make("RECEPTION", "rec", 10, true);
        await Make("PHLEBOTOMY", "phl", 6, true);
        await Make("TECHNICIAN", "tec", 8, false);
        await Make("PATHOLOGIST", "pat", 4, false);
        await Make("LABMGR", "mgr", 2, false);
        await db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, false));
        log.LogInformation("30 users created");
    }

    // ---------------------------------------------------------------- bulk facts

    private static (string Name, string Sql)[] Steps() =>
    [
        ("lookups", """
            IF OBJECT_ID('tempdb..#T') IS NOT NULL DROP TABLE #T;
            SELECT CAST(ROW_NUMBER() OVER (ORDER BY Id) - 1 AS int) AS ix, Id, Code, NameEn, SampleTypeId, Price INTO #T FROM catalog.Tests WHERE OrganizationId = @org;
            IF OBJECT_ID('tempdb..#B') IS NOT NULL DROP TABLE #B;
            SELECT CAST(ROW_NUMBER() OVER (ORDER BY IsMain DESC, Id) - 1 AS int) AS ix, Id INTO #B FROM core.Branches WHERE OrganizationId = @org;
            IF OBJECT_ID('tempdb..#D') IS NOT NULL DROP TABLE #D;
            SELECT CAST(ROW_NUMBER() OVER (ORDER BY Id) - 1 AS int) AS ix, Id INTO #D FROM patient.Doctors WHERE OrganizationId = @org;
            IF OBJECT_ID('tempdb..#U') IS NOT NULL DROP TABLE #U;
            SELECT r.Code AS RoleCode, CAST(ROW_NUMBER() OVER (PARTITION BY r.Code ORDER BY u.Id) - 1 AS int) AS ix, u.Id INTO #U
              FROM security.Users u JOIN security.UserRoles ur ON ur.UserId = u.Id JOIN security.Roles r ON r.Id = ur.RoleId WHERE u.OrganizationId = @org;
            """),

        ("orders skeleton", """
            IF OBJECT_ID('tempdb..#O') IS NOT NULL DROP TABLE #O;
            SELECT CAST(g.value AS bigint) AS n,
                   CAST(1 + (g.value * 7) % 5 AS int) AS k,
                   CAST(CASE g.value % 4 WHEN 0 THEN 0 WHEN 1 THEN 0 WHEN 2 THEN 1 ELSE 2 END AS int) AS bx,
                   DATEADD(second, -CAST((@n - g.value) * (@span / @n) AS bigint), @now) AS created,
                   CAST(CASE WHEN g.value > @n - @live THEN 1 ELSE 0 END AS bit) AS live,
                   CAST(CASE WHEN g.value > @n - @live THEN g.value % 6 ELSE 5 END AS int) AS st
              INTO #O FROM GENERATE_SERIES(CAST(1 AS bigint), @n) g;
            CREATE UNIQUE CLUSTERED INDEX ix_O ON #O (n);

            IF OBJECT_ID('tempdb..#OI') IS NOT NULL DROP TABLE #OI;
            DECLARE @tc int = (SELECT COUNT(*) FROM #T);
            SELECT o.n * 10 + j.value AS Id, o.n, o.created, o.st, o.live, o.bx, t.Id AS TestId, t.Code, t.NameEn, t.SampleTypeId, t.Price
              INTO #OI FROM #O o CROSS APPLY GENERATE_SERIES(1, 5) j JOIN #T t ON t.ix = (o.n + j.value * 7) % @tc WHERE j.value <= o.k;
            CREATE UNIQUE CLUSTERED INDEX ix_OI ON #OI (Id);
            CREATE INDEX ix_OI_n ON #OI (n);

            IF OBJECT_ID('tempdb..#Rec') IS NOT NULL DROP TABLE #Rec;
            SELECT ix, Id INTO #Rec FROM #U WHERE RoleCode = 'RECEPTION';
            IF OBJECT_ID('tempdb..#Phl') IS NOT NULL DROP TABLE #Phl;
            SELECT ix, Id INTO #Phl FROM #U WHERE RoleCode = 'PHLEBOTOMY';
            IF OBJECT_ID('tempdb..#Tec') IS NOT NULL DROP TABLE #Tec;
            SELECT ix, Id INTO #Tec FROM #U WHERE RoleCode = 'TECHNICIAN';
            IF OBJECT_ID('tempdb..#Pat') IS NOT NULL DROP TABLE #Pat;
            SELECT ix, Id INTO #Pat FROM #U WHERE RoleCode = 'PATHOLOGIST';
            """),

        ("patients", """
            SET IDENTITY_INSERT patient.Patients ON;
            INSERT patient.Patients (Id, OrganizationId, PatientNo, FullName, Sex, DateOfBirth, IsDateOfBirthEstimated, Cnic, Mobile, Address, City, RegisteredBranchId, IsDeleted, CreatedAtUtc)
            SELECT g.value, @org, 'LP-' + RIGHT('0000000' + CAST(g.value AS varchar(10)), 7),
                   CHOOSE(1 + g.value % 20, N'Muhammad', N'Ahmed', N'Ali', N'Hassan', N'Usman', N'Bilal', N'Fatima', N'Ayesha', N'Zainab', N'Maryam', N'Imran', N'Kamran', N'Saad', N'Hamza', N'Noor', N'Sana', N'Hira', N'Asad', N'Faisal', N'Tariq')
                     + N' ' + CHOOSE(1 + (g.value / 20 + g.value) % 24, N'Khan', N'Malik', N'Sheikh', N'Butt', N'Chaudhry', N'Qureshi', N'Siddiqui', N'Raza', N'Hussain', N'Abbasi', N'Mirza', N'Baig', N'Jutt', N'Awan', N'Bhatti', N'Gill', N'Rana', N'Iqbal', N'Shah', N'Naqvi', N'Ansari', N'Farooqi', N'Lodhi', N'Zafar'),
                   1 + g.value % 2,
                   DATEADD(day, -(365 * (1 + (g.value * 17) % 80) + g.value % 365), CAST(@now AS date)), 0,
                   RIGHT('00000' + CAST(30000 + (g.value * 7) % 60000 AS varchar(5)), 5) + '-' + RIGHT('0000000' + CAST((CAST(g.value AS bigint) * 104729) % 10000000 AS varchar(7)), 7) + '-' + CAST(g.value % 10 AS varchar(1)),
                   '03' + RIGHT('000000000' + CAST((CAST(g.value AS bigint) * 7919 + 12345) % 1000000000 AS varchar(9)), 9),
                   N'House ' + CAST(g.value % 500 AS nvarchar(5)) + N', Street ' + CAST(g.value % 40 AS nvarchar(5)), N'Lahore',
                   b.Id, 0, DATEADD(second, -CAST((@n - g.value * 1.8) * (@span / @n) AS bigint), @now)
              FROM GENERATE_SERIES(1, @p) g JOIN #B b ON b.ix = g.value % 3
             ORDER BY g.value;
            SET IDENTITY_INSERT patient.Patients OFF;
            """),

        ("orders", """
            DECLARE @rc int = (SELECT COUNT(*) FROM #Rec), @dc int = (SELECT COUNT(*) FROM #D);
            SET IDENTITY_INSERT lab.Orders ON;
            INSERT lab.Orders (Id, OrganizationId, BranchId, OrderNo, PatientId, ReferringDoctorId, Priority, Status, IdempotencyKey, CreatedAtUtc, CreatedByUserId)
            SELECT o.n, @org, b.Id, 'LO-' + RIGHT('00000000' + CAST(o.n AS varchar(9)), 8), CAST(1 + o.n % @p AS int),
                   CASE WHEN o.n % 3 = 0 THEN NULL ELSE d.Id END, CASE WHEN o.n % 20 = 0 THEN 1 ELSE 0 END, 0, NEWID(), o.created, r.Id
              FROM #O o JOIN #B b ON b.ix = o.bx JOIN #Rec r ON r.ix = o.n % @rc LEFT JOIN #D d ON d.ix = o.n % @dc
             ORDER BY o.n;
            SET IDENTITY_INSERT lab.Orders OFF;
            """),

        ("invoices, lines, items", """
            DECLARE @rc int = (SELECT COUNT(*) FROM #Rec);
            SET IDENTITY_INSERT billing.Invoices ON;
            INSERT billing.Invoices (Id, OrganizationId, BranchId, InvoiceNo, OrderId, PatientId, Gross, DiscountAmount, Net, Paid, Refunded, Balance, Status, CreatedAtUtc, CreatedByUserId)
            SELECT o.n, @org, b.Id, 'LI-' + RIGHT('00000000' + CAST(o.n AS varchar(9)), 8), o.n, CAST(1 + o.n % @p AS int),
                   g.Gross, 0, g.Gross, pd.Paid, 0, g.Gross - pd.Paid, CASE WHEN pd.Paid = g.Gross THEN 2 ELSE 1 END, o.created, r.Id
              FROM #O o JOIN #B b ON b.ix = o.bx JOIN #Rec r ON r.ix = o.n % @rc
              JOIN (SELECT n, SUM(Price) AS Gross FROM #OI GROUP BY n) g ON g.n = o.n
              CROSS APPLY (SELECT CASE WHEN o.live = 1 AND o.n % 10 = 0 THEN CAST(ROUND(g.Gross / 2, 0) AS decimal(18,2)) ELSE g.Gross END AS Paid) pd
             ORDER BY o.n;
            SET IDENTITY_INSERT billing.Invoices OFF;

            SET IDENTITY_INSERT billing.InvoiceLines ON;
            INSERT billing.InvoiceLines (Id, OrganizationId, InvoiceId, Kind, TestId, Code, NameEn, Price, IsCancelled, CreatedAtUtc)
            SELECT Id, @org, n, 1, TestId, Code, NameEn, Price, 0, created FROM #OI ORDER BY Id;
            SET IDENTITY_INSERT billing.InvoiceLines OFF;

            SET IDENTITY_INSERT lab.OrderItems ON;
            INSERT lab.OrderItems (Id, OrganizationId, OrderId, InvoiceLineId, TestId, TestCode, TestNameEn, Status, CreatedAtUtc)
            SELECT Id, @org, n, Id, TestId, Code, NameEn, CASE st WHEN 0 THEN 0 WHEN 1 THEN 10 WHEN 2 THEN 20 WHEN 3 THEN 30 WHEN 4 THEN 30 ELSE 40 END, created FROM #OI;
            SET IDENTITY_INSERT lab.OrderItems OFF;
            """),

        ("payments", """
            DECLARE @rc int = (SELECT COUNT(*) FROM #Rec);
            SET IDENTITY_INSERT billing.Payments ON;
            INSERT billing.Payments (Id, OrganizationId, BranchId, ReceiptNo, InvoiceId, PatientId, Method, Amount, ReceivedAtUtc, ReceivedByUserId, IdempotencyKey, CreatedAtUtc)
            SELECT i.Id, @org, i.BranchId, 'LR-' + RIGHT('00000000' + CAST(i.Id AS varchar(9)), 8), i.Id, i.PatientId, CAST(2 + i.Id % 3 AS int), i.Paid, i.CreatedAtUtc, r.Id, NEWID(), i.CreatedAtUtc
              FROM billing.Invoices i JOIN #Rec r ON r.ix = i.Id % @rc WHERE i.OrganizationId = @org AND i.Paid > 0
             ORDER BY i.Id;
            SET IDENTITY_INSERT billing.Payments OFF;
            """),

        ("samples", """
            DECLARE @pc int = (SELECT COUNT(*) FROM #Phl), @tcn int = (SELECT COUNT(*) FROM #Tec);
            SET IDENTITY_INSERT lab.Samples ON;
            INSERT lab.Samples (Id, OrganizationId, Barcode, OrderId, PatientId, SampleTypeId, Status, Priority, OriginBranchId, CurrentBranchId,
                                CollectedAtUtc, CollectedByUserId, IdentityConfirmed, ReceivedAtUtc, ReceivedByUserId, LabelPrintCount, CreatedAtUtc)
            SELECT CAST(s.n AS bigint) * 100000 + s.SampleTypeId, @org, 'LS-' + CAST(s.n AS varchar(10)) + '-' + CAST(s.SampleTypeId AS varchar(6)), s.n,
                   CAST(1 + s.n % @p AS int), s.SampleTypeId, CASE WHEN o.st = 0 THEN 0 WHEN o.st = 1 THEN 10 ELSE 20 END, CASE WHEN s.n % 20 = 0 THEN 1 ELSE 0 END, b.Id, b.Id,
                   CASE WHEN o.st >= 1 THEN LEAST(DATEADD(minute, 10, o.created), @now) END, CASE WHEN o.st >= 1 THEN ph.Id END, CASE WHEN o.st >= 1 THEN 1 ELSE 0 END,
                   CASE WHEN o.st >= 2 THEN LEAST(DATEADD(minute, 40, o.created), @now) END, CASE WHEN o.st >= 2 THEN te.Id END, 1, o.created
              FROM (SELECT n, SampleTypeId FROM #OI GROUP BY n, SampleTypeId) s
              JOIN #O o ON o.n = s.n JOIN #B b ON b.ix = o.bx JOIN #Phl ph ON ph.ix = s.n % @pc JOIN #Tec te ON te.ix = s.n % @tcn
             ORDER BY s.n, s.SampleTypeId;
            SET IDENTITY_INSERT lab.Samples OFF;

            INSERT lab.SampleTests (SampleId, OrderItemId, OrganizationId)
            SELECT CAST(n AS bigint) * 100000 + SampleTypeId, Id, @org FROM #OI;
            """),

        ("results", """
            DECLARE @tcn int = (SELECT COUNT(*) FROM #Tec), @pn int = (SELECT COUNT(*) FROM #Pat);
            SET IDENTITY_INSERT lab.TestResults ON;
            INSERT lab.TestResults (Id, OrganizationId, OrderItemId, OrderId, SampleId, PatientId, OrderBranchId, LabBranchId, Version, Status,
                                    EnteredByUserId, SubmittedAtUtc, VerifiedByUserId, VerifiedAtUtc, ApprovedByUserId, ApprovedAtUtc, CreatedAtUtc)
            SELECT oi.Id, @org, oi.Id, oi.n, CAST(oi.n AS bigint) * 100000 + oi.SampleTypeId, CAST(1 + oi.n % @p AS int), b.Id, lb.Id, 1,
                   CASE o.st WHEN 3 THEN 10 WHEN 4 THEN 20 ELSE 30 END,
                   te.Id, LEAST(DATEADD(hour, 3, oi.created), @now),
                   CASE WHEN o.st >= 4 THEN pa.Id END, CASE WHEN o.st >= 4 THEN LEAST(DATEADD(hour, 5, oi.created), @now) END,
                   CASE WHEN o.st >= 5 THEN pa.Id END, CASE WHEN o.st >= 5 THEN LEAST(DATEADD(hour, 6, oi.created), @now) END, oi.created
              FROM #OI oi JOIN #O o ON o.n = oi.n AND o.st >= 3 JOIN #B b ON b.ix = o.bx JOIN #B lb ON lb.ix = 0
              JOIN #Tec te ON te.ix = oi.n % @tcn JOIN #Pat pa ON pa.ix = oi.n % @pn
             ORDER BY oi.Id;
            SET IDENTITY_INSERT lab.TestResults OFF;

            INSERT lab.TestResultValues (OrganizationId, ResultId, ParameterId, ValueNumeric, UnitText, DecimalPlaces, Flag)
            SELECT @org, oi.Id, p.Id, CAST(4 + ((oi.Id * 31 + p.Id * 17) % 1000) / 100.0 AS decimal(18,6)), N'mg/dL', 2, CASE WHEN oi.Id % 10 = 0 THEN 3 ELSE 1 END
              FROM #OI oi JOIN #O o ON o.n = oi.n AND o.st >= 3 JOIN catalog.TestParameters p ON p.TestId = oi.TestId AND p.OrganizationId = @org
             ORDER BY oi.Id, p.Id;
            """),

        ("reports", """
            DECLARE @pn int = (SELECT COUNT(*) FROM #Pat);
            SET IDENTITY_INSERT lab.LabReports ON;
            INSERT lab.LabReports (Id, OrganizationId, OrderId, PatientId, OrderBranchId, LabBranchId, ReportNo, Version, Status, Language, IssuedByUserId, IssuedAtUtc,
                                   ReleasedCount, ActiveTestCount, LatestApprovedAtUtc, SnapshotJson, SnapshotHash, PrintCount, CreatedAtUtc)
            SELECT o.n, @org, o.n, CAST(1 + o.n % @p AS int), b.Id, lb.Id, 'LREP-' + RIGHT('00000000' + CAST(o.n AS varchar(9)), 8), 1, 1, 0, pa.Id,
                   LEAST(DATEADD(hour, 7, o.created), @now), o.k, o.k, LEAST(DATEADD(hour, 6, o.created), @now), j.Json, CONVERT(char(64), HASHBYTES('SHA2_256', j.Json), 2), 1, o.created
              FROM #O o JOIN #B b ON b.ix = o.bx JOIN #B lb ON lb.ix = 0 JOIN #Pat pa ON pa.ix = o.n % @pn
              CROSS APPLY (SELECT N'{"seeded":true,"pad":"' + REPLICATE(CAST(N'x' AS nvarchar(max)), 900) + N'"}' AS Json) j
             WHERE o.st = 5 AND o.n % 20 < 17
             ORDER BY o.n;
            SET IDENTITY_INSERT lab.LabReports OFF;
            """),

        ("audit log", """
            DECLARE @rc int = (SELECT COUNT(*) FROM #Rec);
            INSERT audit.AuditLogs (OrganizationId, BranchId, UserId, Action, EntityType, EntityId, Changes, IpAddress, AtUtc)
            SELECT @org, b.Id, r.Id, a.Action, a.EntityType, CAST(o.n AS varchar(20)), N'{"Status":{"Old":null,"New":"0"},"Priority":{"Old":null,"New":"0"}}',
                   '10.0.0.' + CAST(o.n % 200 + 1 AS varchar(3)), LEAST(DATEADD(second, a.Secs, o.created), @now)
              FROM #O o JOIN #B b ON b.ix = o.bx JOIN #Rec r ON r.ix = o.n % @rc
              CROSS JOIN (VALUES ('Insert', 'Order', 0), ('Insert', 'Invoice', 1), ('Insert', 'Payment', 2), ('Update', 'TestResult', 21600)) a(Action, EntityType, Secs)
             ORDER BY o.n;
            """),

        ("statistics", """
            EXEC sp_updatestats;
            """),
    ];
}
