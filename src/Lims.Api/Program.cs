using System.Threading.RateLimiting;
using Lims.Api.Cli;
using Lims.Api.Infrastructure;
using Lims.Api.Security;
using Lims.Infrastructure;
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
        .WriteTo.Console()
        .WriteTo.File("logs/lims-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30));

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

    // Cookie keys must survive restarts and be shared if the site ever runs on more than one worker.
    builder.Services.AddDataProtection()
        .SetApplicationName("LIMS")
        .PersistKeysToFileSystem(new DirectoryInfo(config["DataProtection:KeysPath"]
            ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));

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
    });

    var app = builder.Build();

    if (DatabaseCommands.IsCommand(args))
        return await DatabaseCommands.RunAsync(app, args);

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

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
