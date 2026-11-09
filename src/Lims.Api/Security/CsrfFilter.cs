using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Lims.Api.Security;

/// <summary>
/// Validates the anti-forgery token (X-XSRF-TOKEN header + lims.af cookie) on every POST/PUT/PATCH/DELETE,
/// including sign-in. Together with SameSite=Strict cookies this blocks cross-site request forgery.
/// </summary>
public sealed class CsrfFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)) return;

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The security token is missing or expired. Reload the page and try again.",
                Extensions = { ["code"] = "csrf_invalid" },
            }) { StatusCode = StatusCodes.Status400BadRequest };
        }
    }
}
