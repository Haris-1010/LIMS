using System.Threading.RateLimiting;
using Lims.Api.Cli;
using Lims.Api.Infrastructure;
using Lims.Api.Security;
using Lims.Infrastructure;
using Lims.Api.Licensing;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var config = builder.Configuration;

    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        // Console and file go through ScrubbingSink, which keeps SQL Server error text (it quotes data values) out of the logs.
        // Production sets Logging:Path to a writable folder outside the site folder (IIS gives the app no write access there).
        .WriteTo.Sink(new ScrubbingSink(new LoggerConfiguration()
            .MinimumLevel.Verbose()   // the outer logger already applies the configured level
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(ctx.Configuration["Logging:Path"] ?? "logs", "lims-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
            .CreateLogger())));

    // Every state-changing API call must carry the anti-forgery header (see AuthController.Csrf).
    builder.Services.AddControllers(o => o.Filters.Add<CsrfFilter>());
    builder.Services.AddAntiforgery(o =>
    {
        o.HeaderName = "X-XSRF-TOKEN";
        o.Cookie.Name = "lims.af";
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.HttpOnly = true;
    });
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new() { Title = "LIMS API", Version = "v1" }));
    builder.Services.AddInfrastructure(config);
    // Offline licence: enforced everywhere except Development and Testing; there is no setting that turns it off.
    builder.Services.AddSingleton(new LicenseSettings
    {
        Enforce = !(builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")),
        LicenseDirectory = config["License:LicenseDirectory"],
        ContentRoot = builder.Environment.ContentRootPath,
    });
    builder.Services.AddSingleton<LicenseService>();
    // Automatic database backups run inside the application (no Windows scheduled tasks needed). Backup:Automatic=false turns them off.
    builder.Services.AddHostedService<BackupScheduler>();

    // Cookie keys must survive restarts and be shared if the site ever runs on more than one worker.
    var dataProtection = builder.Services.AddDataProtection()
        .SetApplicationName("LIMS")
        .PersistKeysToFileSystem(new DirectoryInfo(config["DataProtection:KeysPath"]
            ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));
    // On the Windows server, encrypt the key files with the machine's DPAPI so a copied key folder is unreadable elsewhere.
    // Losing the keys only signs everyone out; it never loses data.
    if (config.GetValue("DataProtection:ProtectWithDpapi", false) && OperatingSystem.IsWindows())
        dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);

    builder.Services.AddScoped<SessionValidator>();
    builder.Services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(o =>
        {
            o.Cookie.Name = "lims.session";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            // Production on HTTPS should set Auth:RequireHttpsCookie=true (see deployment guide).
            o.Cookie.SecurePolicy = config.GetValue("Auth:RequireHttpsCookie", false)
                ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            o.ExpireTimeSpan = TimeSpan.FromHours(config.GetValue("Auth:SessionHours", 10));
            o.SlidingExpiration = true;
            o.EventsType = typeof(SessionValidator);
        });

    builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    builder.Services.AddAuthorization(o =>
        // Everything requires a signed-in user unless explicitly marked [AllowAnonymous].
        o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

    builder.Services.AddRateLimiter(o =>
    {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        o.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = config.GetValue("Auth:LoginAttemptsPerMinute", 10),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

        // Safety net for every endpoint: per signed-in user (else per address). Generous, so normal work never reaches it.
        o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            RateLimitPartition.GetFixedWindowLimiter(
                http.User.Identity?.IsAuthenticated == true
                    ? "u:" + http.User.Identity.Name
                    : "ip:" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = config.GetValue("RateLimit:RequestsPerMinute", 600),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
    });

    // Production on HTTPS: set Security:RequireHttps=true to redirect to HTTPS and send HSTS.
    var requireHttps = config.GetValue("Security:RequireHttps", false);
    if (requireHttps)
        builder.Services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(365); o.IncludeSubDomains = false; o.ExcludedHosts.Clear(); });

    var app = builder.Build();

    if (DatabaseCommands.IsCommand(args))
        return await DatabaseCommands.RunAsync(app, args);

    app.UseExceptionHandler();
    if (requireHttps)
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }
    app.UseSerilogRequestLogging();

    // Offline licence gate: API calls are refused until a valid signed licence is installed (the web app still loads).
    app.UseMiddleware<LicenseEnforcementMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.Use((http, next) =>
    {
        var headers = http.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "same-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
        // Same-origin React build: no external hosts, no inline scripts. Inline styles are used by the UI.
        // Swagger (development only) needs inline scripts, so it is left out.
        if (!http.Request.Path.StartsWithSegments("/swagger"))
            headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; font-src 'self' data:; connect-src 'self'; object-src 'none'; " +
                "base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
        // Patient data must never be kept in a shared cache or the back/forward cache.
        if (http.Request.Path.StartsWithSegments("/api"))
        {
            headers.CacheControl = "no-store";
            headers.Pragma = "no-cache";
        }
        return next(http);
    });

    // The built React app is copied to wwwroot for IIS deployment; the API serves it as static files.
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseAuthentication();
    app.UseMiddleware<RequestContextMiddleware>();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapControllers();
    app.MapFallbackToFile("index.html").AllowAnonymous();

    await app.RunAsync();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "LIMS API terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;

