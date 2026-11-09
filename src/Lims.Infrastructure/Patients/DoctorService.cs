using Lims.Application.Common;
using Lims.Application.Patients;
using Lims.Domain.Patients;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Patients;

public sealed class DoctorService(LimsDbContext db)
{
    public async Task<PagedResult<DoctorDto>> ListAsync(DoctorQuery q, CancellationToken ct)
    {
        var query = db.Doctors.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(d => d.FullName.Contains(s) || d.Code.Contains(s) || (d.FullNameUr != null && d.FullNameUr.Contains(s)) ||
                                     (d.Specialty != null && d.Specialty.Contains(s)) || (d.Hospital != null && d.Hospital.Contains(s)) ||
                                     (d.Phone != null && d.Phone.Contains(s)) || (d.PmdcNo != null && d.PmdcNo.Contains(s)));
        }
        if (q.IsActive is bool active) query = query.Where(d => d.IsActive == active);
        query = (q.Sort, q.Desc) switch
        {
            ("code", false) => query.OrderBy(d => d.Code),
            ("code", true) => query.OrderByDescending(d => d.Code),
            (_, true) => query.OrderByDescending(d => d.FullName),
            _ => query.OrderBy(d => d.FullName),
        };
        var total = await query.CountAsync(ct);
        var items = await query.Skip(q.Skip).Take(q.PageSize).Select(d => ToDto(d)).ToListAsync(ct);
        return new PagedResult<DoctorDto>(items, total, q.Page, q.PageSize);
    }

    /// <summary>Active doctors for selectors (order registration), matched by name/code/hospital.</summary>
    public Task<List<DoctorOption>> OptionsAsync(string? search, CancellationToken ct)
    {
        var query = db.Doctors.AsNoTracking().Where(d => d.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(d => d.FullName.Contains(s) || d.Code.Contains(s) || (d.Hospital != null && d.Hospital.Contains(s)));
        }
        return query.OrderBy(d => d.FullName).Take(30)
            .Select(d => new DoctorOption(d.Id, d.Code, d.FullName, d.FullNameUr, d.Specialty, d.Hospital))
            .ToListAsync(ct);
    }

    public async Task<DoctorDto> GetAsync(int id, CancellationToken ct) =>
        await db.Doctors.AsNoTracking().Where(d => d.Id == id).Select(d => ToDto(d)).SingleOrDefaultAsync(ct)
        ?? throw BusinessRuleException.NotFound("Doctor");

    public async Task<int> CreateAsync(DoctorInput input, CancellationToken ct)
    {
        var code = string.IsNullOrWhiteSpace(input.Code) ? await NextCodeAsync(ct) : input.Code.Trim().ToUpperInvariant();
        if (await db.Doctors.IgnoreQueryFilters().AnyAsync(d => d.OrganizationId == db.Request.OrganizationId && d.Code == code, ct))
            throw BusinessRuleException.Conflict("doctor_code_taken", "A doctor with this code already exists.");
        var doctor = new Doctor { OrganizationId = db.Request.OrganizationId ?? 0, Code = code };
        Apply(doctor, input);
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync(ct);
        return doctor.Id;
    }

    public async Task UpdateAsync(int id, DoctorUpdate input, CancellationToken ct)
    {
        var doctor = await db.Doctors.SingleOrDefaultAsync(d => d.Id == id, ct) ?? throw BusinessRuleException.NotFound("Doctor");
        db.Entry(doctor).Property(d => d.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        Apply(doctor, input);
        doctor.IsActive = input.IsActive;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>D0001, D0002 … (codes are never reused, including soft-deleted doctors).</summary>
    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var codes = await db.Doctors.IgnoreQueryFilters()
            .Where(d => d.OrganizationId == db.Request.OrganizationId && d.Code.StartsWith("D"))
            .Select(d => d.Code).ToListAsync(ct);
        var max = codes.Select(c => int.TryParse(c[1..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"D{max + 1:0000}";
    }

    private static void Apply(Doctor d, DoctorInput input)
    {
        var name = input.FullName.Trim();
        if (name.Length == 0) throw new BusinessRuleException("name_required", "Enter the doctor's name.");
        d.FullName = name;
        d.FullNameUr = Blank(input.FullNameUr);
        d.Qualification = Blank(input.Qualification);
        d.Specialty = Blank(input.Specialty);
        d.PmdcNo = Blank(input.PmdcNo);
        d.Phone = PatientAge.NormalizePhone(input.Phone);
        d.Email = Blank(input.Email);
        d.Hospital = Blank(input.Hospital);
        d.City = Blank(input.City);
        d.IsInHouse = input.IsInHouse;
    }

    private static DoctorDto ToDto(Doctor d) => new(d.Id, d.Code, d.FullName, d.FullNameUr, d.Qualification, d.Specialty, d.PmdcNo,
        d.Phone, d.Email, d.Hospital, d.City, d.IsInHouse, d.IsActive, Convert.ToBase64String(d.RowVersion));

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
