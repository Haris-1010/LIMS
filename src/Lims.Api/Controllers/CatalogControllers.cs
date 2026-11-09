using Lims.Api.Security;
using Lims.Application.Catalog;
using Lims.Domain.Security;
using Lims.Infrastructure.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace Lims.Api.Controllers;

[ApiController]
[Route("api/catalog")]
public sealed class CatalogController(CatalogService catalog) : ControllerBase
{
    [HttpGet("units")]
    [HasPermission(Permissions.CatalogView)]
    public async Task<IActionResult> Units(CancellationToken ct) => Ok(await catalog.UnitsAsync(ct));

    [HttpGet("sample-types")]
    [HasPermission(Permissions.CatalogView)]
    public async Task<IActionResult> SampleTypes(CancellationToken ct) => Ok(await catalog.SampleTypesAsync(ct));

    // ---- categories
    [HttpGet("categories")]
    [HasPermission(Permissions.CatalogView)]
    public async Task<IActionResult> Categories(CancellationToken ct) => Ok(await catalog.CategoriesAsync(ct));

    [HttpPost("categories")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<IActionResult> CreateCategory(CategoryInput input, CancellationToken ct) => Ok(new { id = await catalog.CreateCategoryAsync(input, ct) });

    [HttpPut("categories/{id:int}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<IActionResult> UpdateCategory(int id, CategoryUpdate input, CancellationToken ct)
    {
        await catalog.UpdateCategoryAsync(id, input, ct);
        return NoContent();
    }

    // ---- tests
    [HttpGet("tests")]
    [HasPermission(Permissions.CatalogView)]
    public async Task<IActionResult> Tests([FromQuery] TestQuery query, CancellationToken ct) => Ok(await catalog.TestsAsync(query, ct));

    [HttpGet("tests/{id:int}")]
    [HasPermission(Permissions.CatalogView)]
    public async Task<IActionResult> Test(int id, CancellationToken ct) => Ok(await catalog.TestAsync(id, ct));

    [HttpPost("tests")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<IActionResult> CreateTest(TestInput input, CancellationToken ct)
    {
        var id = await catalog.CreateTestAsync(input, ct);
        return CreatedAtAction(nameof(Test), new { id }, await catalog.TestAsync(id, ct));
    }

    [HttpPut("tests/{id:int}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<IActionResult> UpdateTest(int id, TestUpdate input, CancellationToken ct)
    {
        await catalog.UpdateTestAsync(id, input, ct);
        return Ok(await catalog.TestAsync(id, ct));
    }

    // ---- parameters
    [HttpPost("tests/{testId:int}/parameters")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<IActionResult> AddParameter(int testId, ParameterInput input, CancellationToken ct) =>
        Ok(new { id = await catalog.AddParameterAsync(testId, input, ct) });

    [HttpPut("parameters/{id:int}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<IActionResult> UpdateParameter(int id, ParameterUpdate input, CancellationToken ct)
    {
        await catalog.UpdateParameterAsync(id, input, ct);
        return NoContent();
    }

    // ---- packages
    [HttpGet("packages")]
    [HasPermission(Permissions.CatalogView)]
    public async Task<IActionResult> Packages(CancellationToken ct) => Ok(await catalog.PackagesAsync(ct));

    [HttpPost("packages")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<IActionResult> CreatePackage(PackageInput input, CancellationToken ct) => Ok(new { id = await catalog.CreatePackageAsync(input, ct) });

    [HttpPut("packages/{id:int}")]
    [HasPermission(Permissions.CatalogManage)]
    public async Task<IActionResult> UpdatePackage(int id, PackageUpdate input, CancellationToken ct)
    {
        await catalog.UpdatePackageAsync(id, input, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/catalog")]
public sealed class ReferenceRangesController(ReferenceRangeService ranges) : ControllerBase
{
    [HttpGet("parameters/{parameterId:int}/ranges")]
    [HasPermission(Permissions.CatalogView)]
    public async Task<IActionResult> ForParameter(int parameterId, CancellationToken ct) => Ok(await ranges.ForParameterAsync(parameterId, ct));

    [HttpGet("ranges/pending")]
    [HasPermission(Permissions.CatalogManage, Permissions.CatalogClinical)]
    public async Task<IActionResult> Pending(CancellationToken ct) => Ok(await ranges.PendingAsync(ct));

    /// <summary>Drafting a range is configuration work; approving it is a clinical decision.</summary>
    [HttpPost("parameters/{parameterId:int}/ranges")]
    [HasPermission(Permissions.CatalogManage, Permissions.CatalogClinical)]
    public async Task<IActionResult> Create(int parameterId, RangeInput input, CancellationToken ct) =>
        Ok(new { id = await ranges.CreateAsync(parameterId, input, ct) });

    [HttpPut("ranges/{id:int}")]
    [HasPermission(Permissions.CatalogManage, Permissions.CatalogClinical)]
    public async Task<IActionResult> Update(int id, RangeUpdate input, CancellationToken ct)
    {
        await ranges.UpdateAsync(id, input, ct);
        return NoContent();
    }

    [HttpDelete("ranges/{id:int}")]
    [HasPermission(Permissions.CatalogManage, Permissions.CatalogClinical)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await ranges.DeleteDraftAsync(id, ct);
        return NoContent();
    }

    [HttpPost("ranges/{id:int}/approve")]
    [HasPermission(Permissions.CatalogClinical)]
    public async Task<IActionResult> Approve(int id, CancellationToken ct)
    {
        await ranges.ApproveAsync(id, ct);
        return NoContent();
    }

    public sealed record RetireRequest(string? Reason);

    [HttpPost("ranges/{id:int}/retire")]
    [HasPermission(Permissions.CatalogClinical)]
    public async Task<IActionResult> Retire(int id, RetireRequest input, CancellationToken ct)
    {
        await ranges.RetireAsync(id, input.Reason, ct);
        return NoContent();
    }
}
