using Lims.Application.Abstractions;
using Lims.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lims.Api.Infrastructure;

/// <summary>
/// Turns exceptions into RFC 7807 problem responses with a stable "code" the UI translates.
/// Internal details are logged, never returned.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, code, title) = exception switch
        {
            BusinessRuleException b => (b.Status, b.Code, b.Message),
            DbUpdateConcurrencyException => (409, "concurrency",
                "This record was changed by someone else. Reload it and apply your changes again."),
            // Never reveal that another organisation's record exists.
            TenantViolationException => (404, "not_found", "The record was not found."),
            _ => (500, "server_error", "An unexpected error occurred."),
        };

        if (status >= 500)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        else if (exception is TenantViolationException)
            logger.LogWarning(exception, "Tenant violation blocked for {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Extensions = { ["code"] = code, ["traceId"] = context.TraceIdentifier },
        };
        if (exception is BusinessRuleException { Details: not null } withDetails)
            problem.Extensions["details"] = withDetails.Details;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, Exception = exception, ProblemDetails = problem });
    }
}
