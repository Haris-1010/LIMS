using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

// Phase 13 timing tool. Signs in the 30 users made by `db seed-load`, then times API calls the way the screens make them.
//   --base URL        server (default http://localhost:5099)
//   --orders N        number of orders in the load database (default 550000), used to pick record ids
//   --seconds S       length of the concurrent run (default 120)
//   --think MS        average pause between a user's actions in the concurrent run (default 2000; 0 = stress)
//   --baseline N      single-user repetitions of every operation before the concurrent run (default 15)
//   --writes false    skip operations that change data (order creation, result drafts)
//   --out FILE        also write the report as Markdown
//   --only TEXT       run only operations whose name contains TEXT (for looking at one screen)
var opt = Args.Parse(args);
var runner = new Runner(opt);
await runner.LoginAllAsync();
await runner.DiscoverAsync();
await runner.WarmUpAsync();
var baseline = await runner.BaselineAsync();
var loaded = await runner.ConcurrentAsync();
var report = Report.Build(opt, baseline, loaded, runner);
Console.WriteLine(report);
if (opt.Out is not null) await File.WriteAllTextAsync(opt.Out, report);
return 0;

// ------------------------------------------------------------------------------------------------

sealed record Options(string Base, long Orders, int Seconds, int ThinkMs, int Baseline, bool Writes, string? Out, string Password, string? Only);

static class Args
{
    public static Options Parse(string[] a)
    {
        string Get(string k, string d) { var i = Array.IndexOf(a, "--" + k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : d; }
        return new Options(Get("base", "http://localhost:5099"), long.Parse(Get("orders", "550000")), int.Parse(Get("seconds", "120")),
            int.Parse(Get("think", "2000")), int.Parse(Get("baseline", "15")), bool.Parse(Get("writes", "true")), a.Contains("--out") ? Get("out", "") : null,
            Get("password", "Load-test-2026"), a.Contains("--only") ? Get("only", "") : null);
    }
}

sealed class Session
{
    public string Role = "", Username = "";
    public HttpClient Http = null!;
    public string Csrf = "";
    public Random Rng = new();

    public async Task<(HttpStatusCode Status, string Body, double Ms)> SendAsync(HttpMethod method, string url, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (method != HttpMethod.Get) { req.Content = JsonContent.Create(body ?? new { }); req.Headers.Add("X-XSRF-TOKEN", Csrf); }
        var sw = Stopwatch.StartNew();
        using var res = await Http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        sw.Stop();
        return (res.StatusCode, text, sw.Elapsed.TotalMilliseconds);
    }
}

sealed class Op(string name, string role, int weight, bool write, Func<Runner, Session, Task<(HttpStatusCode, string, double)>> run)
{
    public string Name = name, Role = role; public int Weight = weight; public bool Write = write;
    public Func<Runner, Session, Task<(HttpStatusCode, string, double)>> Run = run;
}

sealed class Stats
{
    public readonly List<double> Ms = new(); public int Errors; public readonly Dictionary<int, int> Codes = new();
    public readonly object Lock = new();
    public string? FirstError;
    public void Add(HttpStatusCode code, double ms, string? body = null)
    {
        lock (Lock)
        {
            Ms.Add(ms); Codes[(int)code] = Codes.GetValueOrDefault((int)code) + 1;
            if ((int)code >= 400) { Errors++; FirstError ??= $"{(int)code} {body?[..Math.Min(160, body.Length)]}"; }
        }
    }
    public double P(double q) { lock (Lock) { if (Ms.Count == 0) return 0; var s = Ms.OrderBy(x => x).ToList(); return s[Math.Min(s.Count - 1, (int)Math.Ceiling(q * s.Count) - 1)]; } }
}

sealed class Runner
{
    public readonly Options O;
    public readonly List<Session> Users = [];
    public readonly List<Op> Ops = [];
    public int[] TestIds = [];
    readonly System.Collections.Concurrent.ConcurrentDictionary<int, System.Collections.Concurrent.ConcurrentBag<string>> _barcodes = new();
    // Barcodes are remembered per branch: a collection user can only scan samples of their own branch.
    public void AddBarcode(Session s, string bc) => _barcodes.GetOrAdd(BranchIx(s), _ => new()).Add(bc);
    public string BarcodeFor(Session s) => _barcodes.TryGetValue(BranchIx(s), out var bag) && bag.TryPeek(out var b) ? b : "LS-1-1000";
    public readonly System.Collections.Concurrent.ConcurrentBag<long> PendingItems = [];
    long _live;
    long Done => O.Orders - _live;
    static readonly string[] First = ["Muhammad", "Ahmed", "Ali", "Hassan", "Usman", "Bilal", "Fatima", "Ayesha", "Zainab", "Maryam", "Imran", "Kamran", "Saad", "Hamza", "Noor", "Sana", "Hira", "Asad", "Faisal", "Tariq"];
    static readonly string[] Last = ["Khan", "Malik", "Sheikh", "Butt", "Chaudhry", "Qureshi", "Siddiqui", "Raza", "Hussain", "Abbasi", "Mirza", "Baig", "Jutt", "Awan", "Bhatti", "Gill", "Rana", "Iqbal", "Shah", "Naqvi", "Ansari", "Farooqi", "Lodhi", "Zafar"];

    public Runner(Options o)
    {
        O = o; _live = Math.Max(60, o.Orders * 2 / 1825);
        BuildOps();
        if (o.Only is not null) Ops.RemoveAll(op => !op.Name.Contains(o.Only, StringComparison.OrdinalIgnoreCase));
    }

    // Patient i was generated with these formulas (see LoadDataSeeder), so the tool can search for people that exist.
    long PatientCount => O.Orders * 55 / 100;
    long Pid(Random r) => 1 + r.NextInt64(PatientCount);
    static string NameOf(long i) => First[i % 20] + " " + Last[(i / 20 + i) % 24];
    static string MobileOf(long i) => "03" + ((i * 7919 + 12345) % 1_000_000_000).ToString("D9");
    static string CnicOf(long i) => (30000 + (i * 7) % 60000).ToString("D5") + "-" + ((i * 104729) % 10_000_000).ToString("D7") + "-" + (i % 10);
    // Branch-limited users (reception, collection) only see their own branch's orders. The seeder puts order n at branch
    // 0 (main) when n%4 is 0 or 1, branch 1 when 2, branch 2 when 3; user number i belongs to branch (i-1)%3.
    static int BranchIx(Session s) => s.Role is "rec" or "phl" ? (int.Parse(s.Username[^2..]) - 1) % 3 : -1;
    static long Fit(long n, int bx) => bx switch { 0 => n - n % 4 + (n % 2), 1 => n - n % 4 + 2, 2 => n - n % 4 + 3, _ => n };
    long RandOrder(Session s) => Math.Clamp(Fit(1 + s.Rng.NextInt64(Done), BranchIx(s)), 1, Done);          // an order that is fully processed
    long LiveOrder(Session s) => Math.Clamp(Fit(Done + 1 + s.Rng.NextInt64(_live), BranchIx(s)), Done + 1, O.Orders);   // an order from the last two days
    static string Today => DateTime.Today.ToString("yyyy-MM-dd");

    // ----------------------------------------------------------------------------------------- operations
    void BuildOps()
    {
        Task<(HttpStatusCode, string, double)> Get(Session s, string url) => s.SendAsync(HttpMethod.Get, url);
        Task<(HttpStatusCode, string, double)> Post(Session s, string url, object? b = null) => s.SendAsync(HttpMethod.Post, url, b);
        void Add(string name, string role, int w, Func<Runner, Session, Task<(HttpStatusCode, string, double)>> f, bool write = false) => Ops.Add(new Op(name, role, w, write, f));

        // reception
        Add("patients: search by name", "rec", 20, (r, s) => Get(s, "/api/patients?pageSize=25&search=" + Uri.EscapeDataString(NameOf(r.Pid(s.Rng)))));
        Add("patients: search by mobile", "rec", 10, (r, s) => Get(s, "/api/patients?pageSize=25&search=" + MobileOf(r.Pid(s.Rng))));
        Add("patients: search by patient no", "rec", 10, (r, s) => Get(s, "/api/patients?pageSize=25&search=LP-" + r.Pid(s.Rng).ToString("D7")));
        Add("patients: search by CNIC", "rec", 5, (r, s) => Get(s, "/api/patients?pageSize=25&search=" + CnicOf(r.Pid(s.Rng))));
        Add("patients: open one", "rec", 10, (r, s) => Get(s, "/api/patients/" + r.Pid(s.Rng)));
        Add("patients: duplicate check", "rec", 4, (r, s) => { var i = r.Pid(s.Rng); return Get(s, $"/api/patients/duplicates?fullName={Uri.EscapeDataString(NameOf(i))}&mobile={MobileOf(i)}"); });
        Add("orders: list today", "rec", 10, (r, s) => Get(s, $"/api/orders?pageSize=25&from={Today}&to={Today}"));
        Add("orders: list default (no filter)", "rec", 6, (r, s) => Get(s, "/api/orders?pageSize=25"));
        Add("orders: list for a patient", "rec", 8, (r, s) => Get(s, "/api/orders?pageSize=25&patientId=" + r.Pid(s.Rng)));
        Add("orders: outstanding balances", "rec", 3, (r, s) => Get(s, "/api/orders?pageSize=25&outstanding=true"));
        Add("orders: search by order no", "rec", 5, (r, s) => Get(s, "/api/orders?pageSize=25&search=LO-" + r.RandOrder(s).ToString("D8")));
        Add("orders: open one", "rec", 10, (r, s) => Get(s, "/api/orders/" + r.RandOrder(s)));
        Add("payments: list today", "rec", 5, (r, s) => Get(s, $"/api/billing/payments?pageSize=25&from={Today}&to={Today}"));
        Add("payments: list default", "rec", 3, (r, s) => Get(s, "/api/billing/payments?pageSize=25"));
        Add("payments: day summary", "rec", 3, (r, s) => Get(s, "/api/billing/day-summary"));
        Add("orders: create with payment", "rec", 4, async (r, s) =>
        {
            var tests = r.TestIds; var a = tests[s.Rng.Next(tests.Length)]; int b; do b = tests[s.Rng.Next(tests.Length)]; while (b == a);
            var quote = await Post(s, "/api/orders/quote", new { items = new[] { new { testId = (int?)a, packageId = (int?)null }, new { testId = (int?)b, packageId = (int?)null } } });
            if ((int)quote.Item1 >= 400) return quote;
            var total = JsonDocument.Parse(quote.Item2).RootElement.TryGetProperty("net", out var n) ? n.GetDecimal() : 1000m;
            var res = await Post(s, "/api/orders", new
            {
                idempotencyKey = Guid.NewGuid(), patientId = (int)r.Pid(s.Rng), priority = 0,
                items = new[] { new { testId = (int?)a, packageId = (int?)null }, new { testId = (int?)b, packageId = (int?)null } },
                payment = new { method = 2, amount = total, reference = "load" },
            });
            return (res.Item1, res.Item2, res.Item3 + quote.Item3);
        }, write: true);

        // sample collection and receiving
        Add("samples: collection queue", "phl", 35, async (r, s) =>
        {
            var res = await Get(s, "/api/samples/collection-queue");
            foreach (var bc in Find(res.Item2, "barcode").Take(5)) r.AddBarcode(s, bc);
            return res;
        });
        Add("samples: list awaiting receipt", "phl", 15, (r, s) => Get(s, "/api/samples?pageSize=25&awaitingReceipt=true"));
        Add("samples: list received", "phl", 10, async (r, s) =>
        {
            var res = await Get(s, "/api/samples?pageSize=25&status=20");
            foreach (var bc in Find(res.Item2, "barcode").Take(5)) r.AddBarcode(s, bc);
            return res;
        });
        Add("samples: samples of an order", "phl", 20, async (r, s) =>
        {
            var res = await Get(s, $"/api/orders/{r.LiveOrder(s)}/samples");
            foreach (var bc in Find(res.Item2, "barcode").Take(3)) r.AddBarcode(s, bc);
            return res;
        });
        Add("samples: scan a barcode", "phl", 20, (r, s) => Get(s, "/api/samples/by-barcode/" + r.BarcodeFor(s)));

        // result entry
        Add("results: pending list", "tec", 35, async (r, s) =>
        {
            var res = await Get(s, "/api/results/pending?pageSize=50");
            foreach (var id in FindLong(res.Item2, "orderItemId").Take(20)) r.PendingItems.Add(id);
            return res;
        });
        Add("results: pending, urgent only", "tec", 8, (r, s) => Get(s, "/api/results/pending?pageSize=25&urgentOnly=true"));
        Add("results: open entry screen", "tec", 25, (r, s) => Get(s, "/api/results/items/" + (r.PendingItems.TryPeek(out var id) ? id : r.LiveOrder(s) * 10 + 1)));
        Add("results: results of an order", "tec", 12, (r, s) => Get(s, $"/api/orders/{r.RandOrder(s)}/results"));
        Add("results: critical alerts count", "tec", 8, (r, s) => Get(s, "/api/critical-alerts/open-count"));
        Add("results: save a draft", "tec", 8, async (r, s) =>
        {
            if (!r.PendingItems.TryTake(out var item)) return await Get(s, "/api/results/pending?pageSize=10");
            var entry = await Get(s, "/api/results/items/" + item);
            var values = FindLong(entry.Item2, "parameterId").Distinct().Select(p => new { parameterId = (int)p, value = "5.25", optionId = (int?)null }).ToArray();
            if (values.Length == 0) return entry;
            var rowVersion = Find(entry.Item2, "rowVersion").FirstOrDefault();   // present when a draft already exists
            var res = await s.SendAsync(HttpMethod.Put, $"/api/results/items/{item}/draft", new { rowVersion, comment = (string?)null, values });
            return (res.Item1, res.Item2, res.Item3 + entry.Item3);
        }, write: true);

        // verification, approval, reports
        Add("verification queue", "pat", 25, (r, s) => Get(s, "/api/results/queue?pageSize=25&stage=verification"));
        Add("approval queue", "pat", 25, (r, s) => Get(s, "/api/results/queue?pageSize=25&stage=approval"));
        Add("reports: ready to report", "pat", 15, (r, s) => Get(s, "/api/reports?pageSize=25&tab=ready"));
        Add("reports: issued list", "pat", 15, (r, s) => Get(s, "/api/reports?pageSize=25&tab=issued"));
        Add("reports: issued, search by patient", "pat", 6, (r, s) => Get(s, "/api/reports?pageSize=25&tab=issued&search=" + Uri.EscapeDataString(NameOf(r.Pid(s.Rng)))));
        Add("reports: preview for an order", "pat", 10, (r, s) => Post(s, $"/api/orders/{r.RandOrder(s)}/reports/preview", new { language = 0 }));
        Add("reports: versions of an order", "pat", 4, (r, s) => Get(s, $"/api/orders/{r.RandOrder(s)}/reports"));

        // management
        Add("dashboard: today", "mgr", 20, (r, s) => Get(s, "/api/dashboard/summary?range=today"));
        Add("dashboard: 7 days", "mgr", 15, (r, s) => Get(s, "/api/dashboard/summary?range=d7"));
        Add("dashboard: 30 days", "mgr", 12, (r, s) => Get(s, "/api/dashboard/summary?range=d30"));
        Add("audit: latest", "mgr", 10, (r, s) => Get(s, "/api/audit?pageSize=25"));
        Add("audit: date range", "mgr", 8, (r, s) => Get(s, $"/api/audit?pageSize=25&from={DateTime.Today.AddDays(-30):yyyy-MM-dd}&to={Today}"));
        Add("audit: one record's history", "mgr", 8, (r, s) => Get(s, $"/api/audit?pageSize=25&entityType=Order&entityId={r.RandOrder(s)}"));
        Add("audit: text search", "mgr", 6, (r, s) => Get(s, "/api/audit?pageSize=25&search=Invoice"));
        Add("audit: filter by kind and type", "mgr", 6, (r, s) => Get(s, "/api/audit?pageSize=25&kind=Update&entityType=TestResult"));
        Add("inventory: items list", "mgr", 5, (r, s) => Get(s, "/api/inventory/items?pageSize=25"));
        Add("samples: tracking search by order no", "mgr", 3, (r, s) => Get(s, "/api/samples?pageSize=25&search=LO-" + r.RandOrder(s).ToString("D8")));
        Add("samples: tracking search by patient name", "mgr", 3, (r, s) => Get(s, "/api/samples?pageSize=25&search=" + Uri.EscapeDataString(NameOf(r.Pid(s.Rng)))));
    }

    // ----------------------------------------------------------------------------------------- JSON helpers (response shapes are not fixed here)
    static IEnumerable<string> Find(string json, string prop)
    {
        try { using var d = JsonDocument.Parse(json); var l = new List<string>(); Walk(d.RootElement, prop, e => { if (e.ValueKind == JsonValueKind.String) l.Add(e.GetString()!); }); return l; }
        catch (JsonException) { return []; }
    }
    static IEnumerable<long> FindLong(string json, string prop)
    {
        try { using var d = JsonDocument.Parse(json); var l = new List<long>(); Walk(d.RootElement, prop, e => { if (e.ValueKind == JsonValueKind.Number) l.Add(e.GetInt64()); }); return l; }
        catch (JsonException) { return []; }
    }
    static void Walk(JsonElement e, string prop, Action<JsonElement> hit)
    {
        if (e.ValueKind == JsonValueKind.Object)
            foreach (var p in e.EnumerateObject()) { if (string.Equals(p.Name, prop, StringComparison.OrdinalIgnoreCase)) hit(p.Value); Walk(p.Value, prop, hit); }
        else if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) Walk(x, prop, hit);
    }

    // ----------------------------------------------------------------------------------------- phases
    static readonly (string Role, string Prefix, int Count)[] Staff = [("rec", "rec", 10), ("phl", "phl", 6), ("tec", "tec", 8), ("pat", "pat", 4), ("mgr", "mgr", 2)];

    public async Task LoginAllAsync()
    {
        foreach (var (role, prefix, count) in Staff)
            for (var i = 1; i <= count; i++)
            {
                var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true, AutomaticDecompression = DecompressionMethods.All };
                var s = new Session { Role = role, Username = $"load.{prefix}{i:00}", Http = new HttpClient(handler) { BaseAddress = new Uri(O.Base), Timeout = TimeSpan.FromSeconds(120) }, Rng = new Random(HashCode.Combine(role, i)) };
                s.Csrf = JsonDocument.Parse(await s.Http.GetStringAsync("/api/auth/csrf")).RootElement.GetProperty("token").GetString()!;
                var login = await s.SendAsync(HttpMethod.Post, "/api/auth/login", new { username = s.Username, password = O.Password });
                if (login.Status != HttpStatusCode.OK) throw new InvalidOperationException($"Sign-in failed for {s.Username}: {(int)login.Status} {login.Body}");
                s.Csrf = JsonDocument.Parse(await s.Http.GetStringAsync("/api/auth/csrf")).RootElement.GetProperty("token").GetString()!;
                if (role is "tec" or "pat")   // lab staff work at the main laboratory: choose it, as the top-bar branch switcher does
                {
                    var me = JsonDocument.Parse(await s.Http.GetStringAsync("/api/auth/me")).RootElement;
                    var main = me.GetProperty("branches").EnumerateArray().First(b => b.GetProperty("nameEn").GetString()!.Contains("Main"));
                    var sw = await s.SendAsync(HttpMethod.Post, "/api/auth/active-branch", new { branchId = main.GetProperty("id").GetInt32() });
                    if (sw.Status != HttpStatusCode.OK) throw new InvalidOperationException("Could not select a branch: " + sw.Body);
                    s.Csrf = JsonDocument.Parse(await s.Http.GetStringAsync("/api/auth/csrf")).RootElement.GetProperty("token").GetString()!;
                }
                Users.Add(s);
            }
        Console.WriteLine($"Signed in {Users.Count} users.");
    }

    public async Task DiscoverAsync()
    {
        var s = Users.First(u => u.Role == "mgr");
        var res = await s.SendAsync(HttpMethod.Get, "/api/catalog/tests?pageSize=100");
        TestIds = FindLong(res.Body, "id").Select(x => (int)x).Distinct().ToArray();
        if (TestIds.Length < 5) throw new InvalidOperationException("Could not read the test catalog: " + (int)res.Status + " " + res.Body[..Math.Min(200, res.Body.Length)]);
        Console.WriteLine($"Catalog: {TestIds.Length} tests. Orders: {O.Orders:N0} (last {_live} in progress).");
    }

    IEnumerable<Op> OpsFor(Session s) => Ops.Where(o => o.Role == s.Role && (O.Writes || !o.Write));

    public async Task WarmUpAsync()
    {
        Console.WriteLine("Warm-up (one pass of every operation, not timed)...");
        foreach (var op in Ops.Where(o => O.Writes || !o.Write))
        {
            var s = Users.First(u => u.Role == op.Role);
            try { await op.Run(this, s); } catch (Exception ex) { Console.WriteLine($"  warm-up {op.Name}: {ex.Message}"); }
        }
    }

    public async Task<Dictionary<string, Stats>> BaselineAsync()
    {
        Console.WriteLine($"Single-user baseline ({O.Baseline} repetitions each)...");
        var result = new Dictionary<string, Stats>();
        foreach (var op in Ops.Where(o => O.Writes || !o.Write))
        {
            var s = Users.First(u => u.Role == op.Role); var st = new Stats(); result[op.Name] = st;
            for (var i = 0; i < O.Baseline; i++)
            {
                try { var (c, b, ms) = await op.Run(this, s); st.Add(c, ms, b); } catch (Exception ex) { st.Add(HttpStatusCode.RequestTimeout, 0); Console.WriteLine($"  {op.Name}: {ex.Message}"); }
            }
        }
        return result;
    }

    public async Task<Dictionary<string, Stats>> ConcurrentAsync()
    {
        Console.WriteLine($"Concurrent run: {Users.Count} users, {O.Seconds} s, think time {O.ThinkMs} ms...");
        var result = Ops.Where(o => O.Writes || !o.Write).ToDictionary(o => o.Name, _ => new Stats());
        var until = DateTime.UtcNow.AddSeconds(O.Seconds);
        var total = 0;
        var tasks = Users.Select(async s =>
        {
            var ops = OpsFor(s).ToList(); var sum = ops.Sum(o => o.Weight);
            if (ops.Count == 0) return;   // nothing for this role (for example with --only)
            while (DateTime.UtcNow < until)
            {
                var pick = s.Rng.Next(sum); Op op = ops[0];
                foreach (var o in ops) { if (pick < o.Weight) { op = o; break; } pick -= o.Weight; }
                try { var (c, b, ms) = await op.Run(this, s); result[op.Name].Add(c, ms, b); }
                catch (Exception) { result[op.Name].Add(HttpStatusCode.RequestTimeout, 0); }
                Interlocked.Increment(ref total);
                if (O.ThinkMs > 0) await Task.Delay(s.Rng.Next(O.ThinkMs / 2, O.ThinkMs * 3 / 2));
            }
        }).ToArray();
        await Task.WhenAll(tasks);
        Console.WriteLine($"{total} requests ({total / (double)O.Seconds:N1}/s).");
        TotalRequests = total;
        return result;
    }
    public int TotalRequests;
}

static class Report
{
    public static string Build(Options o, Dictionary<string, Stats> baseline, Dictionary<string, Stats> loaded, Runner r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## Timing report ({DateTime.Now:yyyy-MM-dd HH:mm})");
        sb.AppendLine($"Server {o.Base}; {o.Orders:N0} orders; {r.Users.Count} users; concurrent run {o.Seconds} s, think time {o.ThinkMs} ms, {r.TotalRequests} requests.");
        sb.AppendLine();
        sb.AppendLine("| Operation | 1 user p50 | 1 user p95 | 30 users p50 | 30 users p95 | 30 users p99 | max | n | errors |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var (name, b) in baseline.OrderBy(kv => kv.Key))
        {
            var l = loaded.GetValueOrDefault(name) ?? new Stats();
            string F(double v) => v.ToString("N0");
            var codes = string.Join(",", b.Codes.Concat(l.Codes).Where(c => c.Key >= 400).GroupBy(c => c.Key).Select(g => $"{g.Key}x{g.Sum(x => x.Value)}"));
            sb.AppendLine($"| {name} | {F(b.P(.5))} | {F(b.P(.95))} | {F(l.P(.5))} | {F(l.P(.95))} | {F(l.P(.99))} | {F(l.Ms.DefaultIfEmpty(0).Max())} | {l.Ms.Count} | {b.Errors + l.Errors}{(codes.Length > 0 ? " (" + codes + ")" : "")} |");
        }
        sb.AppendLine();
        sb.AppendLine("Times in milliseconds, measured at the client including reading the whole response.");
        var errs = baseline.Concat(loaded).Where(kv => kv.Value.FirstError is not null).GroupBy(kv => kv.Key).Select(g => $"- {g.Key}: {g.First().Value.FirstError}").ToList();
        if (errs.Count > 0) { sb.AppendLine(); sb.AppendLine("First error seen per operation:"); errs.ForEach(e => sb.AppendLine(e)); }
        return sb.ToString();
    }
}
