using Lims.Api.Security;
using Lims.Application.Organization;
using Lims.Application.Security;
using Lims.Domain.Security;
using Lims.Infrastructure.Organizations;
using Lims.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace Lims.Api.Controllers;

[ApiController]
[Route("api/users")]
[HasPermission(Permissions.UsersManage)]
public sealed class UsersController(UserService users, Lims.Infrastructure.Reports.SignatureService signatures) : ControllerBase
{
    [HttpPut("{id:int}/signature")]
    [RequestSizeLimit(Lims.Domain.Reports.ImageSniffer.MaxSignatureBytes + 100_000)]
    public async Task<IActionResult> SetSignature(int id, IFormFile file, CancellationToken ct)
    {
        if (file.Length > Lims.Domain.Reports.ImageSniffer.MaxSignatureBytes) throw new Lims.Application.Common.BusinessRuleException("image_too_large", "The image is too large.", 400, new { maxBytes = Lims.Domain.Reports.ImageSniffer.MaxSignatureBytes });
        await using var s = file.OpenReadStream();
        var bytes = new byte[file.Length];
        await s.ReadExactlyAsync(bytes, ct);
        await signatures.SetAsync(id, bytes, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}/signature")]
    public async Task<IActionResult> DeleteSignature(int id, CancellationToken ct)
    {
        await signatures.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] UserQuery query, CancellationToken ct) => Ok(await users.ListAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await users.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(UserCreate input, CancellationToken ct)
    {
        var id = await users.CreateAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id }, await users.GetAsync(id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UserUpdate input, CancellationToken ct)
    {
        await users.UpdateAsync(id, input, ct);
        return Ok(await users.GetAsync(id, ct));
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordRequest input, CancellationToken ct)
    {
        await users.ResetPasswordAsync(id, input.NewPassword, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/unlock")]
    public async Task<IActionResult> Unlock(int id, CancellationToken ct)
    {
        await users.UnlockAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/login-history")]
    public async Task<IActionResult> LoginHistory(int id, CancellationToken ct) => Ok(await users.LoginHistoryAsync(id, ct));
}

[ApiController]
[Route("api/roles")]
public sealed class RolesController(RoleService roles) : ControllerBase
{
    /// <summary>Role names are needed to assign roles to users, so either permission can list them.</summary>
    [HttpGet]
    [HasPermission(Permissions.RolesManage)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await roles.ListAsync(ct));

    [HttpGet("options")]
    [HasPermission(Permissions.UsersManage)]
    public async Task<IActionResult> Options(CancellationToken ct) =>
        Ok((await roles.ListAsync(ct)).Where(r => r.IsActive).Select(r => new { r.Id, r.Code, r.NameEn, r.NameUr }));

    [HttpGet("permissions")]
    [HasPermission(Permissions.RolesManage)]
    public async Task<IActionResult> PermissionCatalog(CancellationToken ct) => Ok(await roles.PermissionsAsync(ct));

    [HttpPost]
    [HasPermission(Permissions.RolesManage)]
    public async Task<IActionResult> Create(RoleCreate input, CancellationToken ct) => Ok(new { id = await roles.CreateAsync(input, ct) });

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.RolesManage)]
    public async Task<IActionResult> Update(int id, RoleUpdate input, CancellationToken ct)
    {
        await roles.UpdateAsync(id, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permissions.RolesManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await roles.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/organization")]
public sealed class OrganizationController(OrganizationService org) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.OrgManage)]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await org.GetAsync(ct));

    [HttpPut]
    [HasPermission(Permissions.OrgManage)]
    public async Task<IActionResult> Update(OrganizationUpdate input, CancellationToken ct)
    {
        await org.UpdateAsync(input, ct);
        return Ok(await org.GetAsync(ct));
    }

    /// <summary>Full branch list for administration and for assigning branch access to users.</summary>
    [HttpGet("branches")]
    [HasPermission(Permissions.OrgManage, Permissions.UsersManage)]
    public async Task<IActionResult> Branches(CancellationToken ct) => Ok(await org.ListBranchesAsync(ct));

    [HttpPost("branches")]
    [HasPermission(Permissions.OrgManage)]
    public async Task<IActionResult> CreateBranch(BranchCreate input, CancellationToken ct) =>
        Ok(new { id = await org.CreateBranchAsync(input, ct) });

    [HttpPut("branches/{id:int}")]
    [HasPermission(Permissions.OrgManage)]
    public async Task<IActionResult> UpdateBranch(int id, BranchUpdate input, CancellationToken ct)
    {
        await org.UpdateBranchAsync(id, input, ct);
        return NoContent();
    }

    [HttpGet("departments")]
    public async Task<IActionResult> Departments(CancellationToken ct) => Ok(await org.ListDepartmentsAsync(ct));

    [HttpPost("departments")]
    [HasPermission(Permissions.OrgManage)]
    public async Task<IActionResult> CreateDepartment(DepartmentCreate input, CancellationToken ct) =>
        Ok(new { id = await org.CreateDepartmentAsync(input, ct) });

    [HttpPut("departments/{id:int}")]
    [HasPermission(Permissions.OrgManage)]
    public async Task<IActionResult> UpdateDepartment(int id, DepartmentUpdate input, CancellationToken ct)
    {
        await org.UpdateDepartmentAsync(id, input, ct);
        return NoContent();
    }
}
