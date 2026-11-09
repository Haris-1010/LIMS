using Lims.Api.Security;
using Lims.Application.Patients;
using Lims.Domain.Security;
using Lims.Infrastructure.Patients;
using Microsoft.AspNetCore.Mvc;

namespace Lims.Api.Controllers;

[ApiController]
[Route("api/patients")]
public sealed class PatientsController(PatientService patients) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.PatientsView)]
    public async Task<IActionResult> List([FromQuery] PatientQuery query, CancellationToken ct) => Ok(await patients.ListAsync(query, ct));

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.PatientsView)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await patients.GetAsync(id, ct));

    /// <summary>Possible duplicates for the details being entered (live check in the registration form).</summary>
    [HttpGet("duplicates")]
    [HasPermission(Permissions.PatientsCreate, Permissions.PatientsEdit)]
    public async Task<IActionResult> Duplicates([FromQuery] DuplicateQuery query, CancellationToken ct) =>
        Ok(await patients.FindDuplicatesAsync(query, ct));

    [HttpPost]
    [HasPermission(Permissions.PatientsCreate)]
    public async Task<IActionResult> Create(PatientCreate input, CancellationToken ct)
    {
        var id = await patients.CreateAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id }, await patients.GetAsync(id, ct));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.PatientsEdit)]
    public async Task<IActionResult> Update(int id, PatientUpdate input, CancellationToken ct)
    {
        await patients.UpdateAsync(id, input, ct);
        return Ok(await patients.GetAsync(id, ct));
    }
}

[ApiController]
[Route("api/doctors")]
public sealed class DoctorsController(DoctorService doctors) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.DoctorsView)]
    public async Task<IActionResult> List([FromQuery] DoctorQuery query, CancellationToken ct) => Ok(await doctors.ListAsync(query, ct));

    /// <summary>Lightweight list for selecting a referring doctor (order registration).</summary>
    [HttpGet("options")]
    [HasPermission(Permissions.DoctorsView, Permissions.OrdersCreate)]
    public async Task<IActionResult> Options([FromQuery] string? search, CancellationToken ct) => Ok(await doctors.OptionsAsync(search, ct));

    [HttpGet("{id:int}")]
    [HasPermission(Permissions.DoctorsView)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await doctors.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.DoctorsManage)]
    public async Task<IActionResult> Create(DoctorInput input, CancellationToken ct)
    {
        var id = await doctors.CreateAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id }, await doctors.GetAsync(id, ct));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permissions.DoctorsManage)]
    public async Task<IActionResult> Update(int id, DoctorUpdate input, CancellationToken ct)
    {
        await doctors.UpdateAsync(id, input, ct);
        return Ok(await doctors.GetAsync(id, ct));
    }
}
