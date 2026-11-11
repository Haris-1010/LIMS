using Lims.Application.Abstractions;
using Lims.Application.Common;
using Lims.Application.Patients;
using Lims.Domain.Core;
using Lims.Domain.Patients;
using Lims.Domain.Security;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Patients;

/// <summary>Patient registration, search and duplicate detection within the caller's organisation.</summary>
public sealed class PatientService(LimsDbContext db, INumberSequenceService numbers, ILabClock clock)
{
    private bool CanSeeSensitive => db.Request.Can(Permissions.PatientsViewSensitive);

    public async Task<PagedResult<PatientListItem>> ListAsync(PatientQuery q, CancellationToken ct)
    {
        var query = db.Patients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            var digits = new string(s.Where(char.IsDigit).ToArray());
            var cnic = PatientAge.NormalizeCnic(s);
            var upper = s.ToUpper();
            // A barcode scan or typed number matches the registration number exactly, a full phone number matches the phone, 13 digits
            // the CNIC. Those are looked up first, each on its own index (one OR across all the columns made SQL Server scan every
            // patient: 1.7 s at 300,000 patients). Only when no identifier matches does the wider name / partial-number search run.
            var exact = db.Patients.Where(p => p.PatientNo == upper).Select(p => p.Id);
            if (cnic != null) exact = exact.Union(db.Patients.Where(p => p.Cnic == cnic).Select(p => p.Id));
            if (digits.Length >= 7)
                exact = exact.Union(db.Patients.Where(p => p.Mobile == digits).Select(p => p.Id)).Union(db.Patients.Where(p => p.AltPhone == digits).Select(p => p.Id));
            var exactIds = await exact.Take(50).ToListAsync(ct);
            var looksLikeFullNumber = digits.Length >= 10 && !s.Any(char.IsLetter);
            if (exactIds.Count > 0) query = query.Where(p => exactIds.Contains(p.Id));
            else if (looksLikeFullNumber) query = query.Where(p => false);   // a complete phone or CNIC that matches nobody: nothing to scan for
            else
                query = query.Where(p =>
                    p.PatientNo == upper ||
                    p.FullName.Contains(s) ||
                    (p.FullNameUr != null && p.FullNameUr.Contains(s)) ||
                    (p.GuardianName != null && p.GuardianName.Contains(s)) ||
                    (digits.Length >= 4 && ((p.Mobile != null && p.Mobile.Contains(digits)) || (p.AltPhone != null && p.AltPhone.Contains(digits)) || p.PatientNo.EndsWith(digits))) ||
                    (cnic != null && p.Cnic == cnic));
        }

        query = (q.Sort, q.Desc) switch
        {
            ("name", false) => query.OrderBy(p => p.FullName),
            ("name", true) => query.OrderByDescending(p => p.FullName),
            ("patientNo", false) => query.OrderBy(p => p.Id),
            (_, _) => query.OrderByDescending(p => p.Id), // newest registrations first
        };

        var total = await query.CountAsync(ct);
        var items = await query.Skip(q.Skip).Take(q.PageSize)
            .Select(p => new PatientListItem(p.Id, p.PatientNo, p.Title, p.FullName, p.FullNameUr, p.GuardianName, p.Sex,
                p.DateOfBirth, p.IsDateOfBirthEstimated, p.Mobile, p.City, p.CreatedAtUtc))
            .ToListAsync(ct);
        return new PagedResult<PatientListItem>(items, total, q.Page, q.PageSize);
    }

    public async Task<PatientDetail> GetAsync(int id, CancellationToken ct)
    {
        var p = await db.Patients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Patient");
        var branchName = p.RegisteredBranchId is int b
            ? await db.Branches.Where(x => x.Id == b).Select(x => x.NameEn).SingleOrDefaultAsync(ct) : null;
        var registeredBy = p.CreatedByUserId is int u
            ? await db.Users.Where(x => x.Id == u).Select(x => x.DisplayNameEn).SingleOrDefaultAsync(ct) : null;

        var sensitive = CanSeeSensitive;
        if (sensitive && (p.Cnic ?? p.Email ?? p.Address) is not null)
        {
            // Access to identity details is logged (who looked at whose record, and when).
            db.AddBusinessAudit("Patient.ViewSensitive", nameof(Patient), p.Id);
            await db.SaveChangesAsync(ct);
        }

        return new PatientDetail(
            p.Id, p.PatientNo, p.Title, p.FullName, p.FullNameUr, p.GuardianName, p.Sex, p.DateOfBirth, p.IsDateOfBirthEstimated,
            sensitive ? p.Cnic : PatientAge.Mask(p.Cnic),
            p.Mobile, p.AltPhone,
            sensitive ? p.Email : PatientAge.Mask(p.Email, 0),
            sensitive ? p.Address : (p.Address is null ? null : "••••••"),
            p.City, p.Notes, sensitive, p.RegisteredBranchId, branchName, p.CreatedAtUtc, registeredBy,
            Convert.ToBase64String(p.RowVersion));
    }

    public async Task<IReadOnlyList<DuplicateCandidate>> FindDuplicatesAsync(DuplicateQuery q, CancellationToken ct)
    {
        var name = q.FullName?.Trim();
        var mobile = PatientAge.NormalizePhone(q.Mobile);
        var cnic = PatientAge.NormalizeCnic(q.Cnic);
        if (string.IsNullOrEmpty(name) && mobile is null && cnic is null) return [];

        var firstName = name?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var query = db.Patients.AsNoTracking().Where(p => p.Id != (q.ExcludeId ?? 0));
        var rows = await query
            .Where(p =>
                (cnic != null && p.Cnic == cnic) ||
                (mobile != null && p.Mobile == mobile && firstName != null && p.FullName.Contains(firstName)) ||
                (name != null && q.DateOfBirth != null && p.FullName == name && p.DateOfBirth == q.DateOfBirth))
            .OrderByDescending(p => p.Id).Take(10)
            .Select(p => new { p.Id, p.PatientNo, p.FullName, p.GuardianName, p.Sex, p.DateOfBirth, p.Mobile, p.Cnic })
            .ToListAsync(ct);

        return rows.Select(p =>
        {
            var reasons = new List<string>();
            if (cnic != null && p.Cnic == cnic) reasons.Add("same_cnic");
            if (mobile != null && p.Mobile == mobile) reasons.Add("same_mobile_and_name");
            if (name != null && q.DateOfBirth != null && p.FullName == name && p.DateOfBirth == q.DateOfBirth) reasons.Add("same_name_and_dob");
            return new DuplicateCandidate(p.Id, p.PatientNo, p.FullName, p.GuardianName, p.Sex, p.DateOfBirth, p.Mobile, reasons);
        }).ToList();
    }

    public async Task<int> CreateAsync(PatientCreate input, CancellationToken ct)
    {
        var patient = new Patient { OrganizationId = db.Request.OrganizationId ?? 0, RegisteredBranchId = db.Request.ActiveBranchId };
        Apply(patient, input, includeSensitive: true);

        if (!input.ConfirmNotDuplicate)
        {
            var dupes = await FindDuplicatesAsync(new DuplicateQuery
            {
                FullName = patient.FullName, Mobile = patient.Mobile, Cnic = patient.Cnic, DateOfBirth = patient.DateOfBirth,
            }, ct);
            if (dupes.Count > 0)
                throw new BusinessRuleException("possible_duplicate",
                    "A patient with matching details is already registered. Review the matches or confirm this is a new patient.",
                    409, dupes);
        }

        // Number and row commit together: a failed registration never consumes a number.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        patient.PatientNo = await numbers.NextAsync(SequenceTypes.Patient, db.Request.ActiveBranchId, ct);
        db.Patients.Add(patient);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return patient.Id;
    }

    public async Task UpdateAsync(int id, PatientUpdate input, CancellationToken ct)
    {
        var patient = await db.Patients.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw BusinessRuleException.NotFound("Patient");
        db.Entry(patient).Property(p => p.RowVersion).OriginalValue = RowVersion.FromToken(input.RowVersion);
        // Users who only see masked identity details cannot change them (they'd overwrite with the mask).
        Apply(patient, input, includeSensitive: CanSeeSensitive);
        await db.SaveChangesAsync(ct);
    }

    private void Apply(Patient p, PatientInput input, bool includeSensitive)
    {
        var name = input.FullName.Trim();
        if (name.Length == 0) throw new BusinessRuleException("name_required", "Enter the patient's name.");
        p.Title = Blank(input.Title);
        p.FullName = name;
        p.FullNameUr = Blank(input.FullNameUr);
        p.GuardianName = Blank(input.GuardianName);
        p.Sex = input.Sex;

        var today = DateOnly.FromDateTime(clock.LocalNow);
        if (input.DateOfBirth is DateOnly dob)
        {
            if (dob > today) throw new BusinessRuleException("dob_in_future", "Date of birth cannot be in the future.");
            if (dob < today.AddYears(-130)) throw new BusinessRuleException("dob_too_old", "Date of birth is more than 130 years ago.");
            p.DateOfBirth = dob;
            p.IsDateOfBirthEstimated = false;
        }
        else if (input.AgeYears is not null || input.AgeMonths is not null || input.AgeDays is not null)
        {
            p.DateOfBirth = PatientAge.Estimate(input.AgeYears ?? 0, input.AgeMonths ?? 0, input.AgeDays ?? 0, today);
            p.IsDateOfBirthEstimated = true;
        }
        else
        {
            // Age drives reference-range selection, so it is required.
            throw new BusinessRuleException("age_required", "Enter the date of birth or the patient's age.");
        }

        var mobile = PatientAge.NormalizePhone(input.Mobile);
        if (mobile is not null && (mobile.TrimStart('+').Length < 10 || mobile.Length > 15))
            throw new BusinessRuleException("mobile_invalid", "Enter a valid mobile number (10–15 digits).");
        p.Mobile = mobile;
        p.AltPhone = PatientAge.NormalizePhone(input.AltPhone);
        p.City = Blank(input.City);
        p.Notes = Blank(input.Notes);

        if (!includeSensitive) return;
        if (!string.IsNullOrWhiteSpace(input.Cnic))
            p.Cnic = PatientAge.NormalizeCnic(input.Cnic) ?? throw new BusinessRuleException("cnic_invalid", "CNIC must have 13 digits.");
        else
            p.Cnic = null;
        var email = Blank(input.Email);
        if (email is not null && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            throw new BusinessRuleException("email_invalid", "Enter a valid email address.");
        p.Email = email;
        p.Address = Blank(input.Address);
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
