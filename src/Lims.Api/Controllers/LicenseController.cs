using Lims.Api.Licensing;
using Lims.Application.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lims.Api.Controllers;

/// <summary>
/// Licence activation. Anonymous on purpose: nobody can sign in until a licence is installed (or after it expires). Installing
/// is safe without sign-in because only a file signed with the issuer's private key for this server is accepted; the
/// anti-forgery header is still required.
/// </summary>
[ApiController]
[Route("api/license")]
public sealed class LicenseController(LicenseService license) : ControllerBase
{
    public const string InvalidCode = "license_invalid";

    /// <summary>This server's licence state and installation ID (sent to the issuer to get a licence).</summary>
    [HttpGet]
    [AllowAnonymous]
    public LicenseStatus Status() => license.GetStatus(fresh: true);

    /// <summary>Verifies a signed licence file and installs it for this server.</summary>
    [HttpPost("install")]
    [AllowAnonymous]
    [RequestSizeLimit(LicenseService.MaximumLicenseFileBytes + 16_384)]
    public IActionResult Install(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return Refuse(new LicenseStatus(false, "no_file", "", null, null, null, "Choose a signed license file to install."));
        if (file.Length > LicenseService.MaximumLicenseFileBytes)
            return Refuse(new LicenseStatus(false, "too_large", "", null, null, null, "The license file exceeds the 16 KB limit."));

        using var stream = file.OpenReadStream();
        var status = license.Install(stream);
        return status.IsValid ? Ok(status) : Refuse(status);
    }

    private ObjectResult Refuse(LicenseStatus s) => Problem(statusCode: 400, title: s.Message,
        extensions: new Dictionary<string, object?> { ["code"] = InvalidCode, ["reason"] = s.Reason, ["installationId"] = s.InstallationId });
}
