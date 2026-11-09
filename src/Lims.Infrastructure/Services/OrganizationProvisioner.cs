using Lims.Application.Abstractions;
using Lims.Domain.Core;
using Lims.Domain.Security;
using Lims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lims.Infrastructure.Services;

/// <summary>
/// Creates a ready-to-configure organisation in one transaction. Everything created here is
/// operational configuration (departments, roles, numbering, settings) — editable afterwards.
/// No clinical data (tests, ranges) is created.
/// </summary>
public sealed class OrganizationProvisioner(LimsDbContext db) : IOrganizationProvisioner
{
    private static readonly (string Code, string En, string Ur)[] DefaultDepartments =
    [
        ("HAEM", "Haematology", "ہیماٹولوجی"),
        ("CHEM", "Clinical Chemistry", "کلینیکل کیمسٹری"),
        ("IMMU", "Immunology & Serology", "امیونولوجی اور سیرولوجی"),
        ("MICRO", "Microbiology", "مائیکروبائیولوجی"),
        ("CPATH", "Clinical Pathology", "کلینیکل پیتھالوجی"),
        ("HISTO", "Histopathology", "ہسٹوپیتھالوجی"),
    ];


    private static readonly (string Key, string Value)[] DefaultSettings =
    [
        ("Currency", "PKR"),
        ("TimeZone", "Asia/Karachi"),
        ("DefaultLanguage", "en"),
        ("Inventory.AllowNegativeStock", "false"),
        ("Billing.MaxDiscountPercentWithoutApproval", "0"),
    ];

    public async Task<int> ProvisionAsync(OrganizationSetup setup, CancellationToken ct = default)
    {
        if (!db.Request.IsSystem)
            throw new TenantViolationException("Provisioning an organisation is a system operation.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var org = new Organization { Code = setup.Code.Trim().ToUpperInvariant(), NameEn = setup.NameEn.Trim(), NameUr = setup.NameUr?.Trim() };
        db.Organizations.Add(org);
        await db.SaveChangesAsync(ct);

        db.Branches.Add(new Branch
        {
            OrganizationId = org.Id,
            Code = setup.MainBranchCode.Trim().ToUpperInvariant(),
            NameEn = setup.MainBranchNameEn.Trim(),
            NameUr = setup.MainBranchNameUr?.Trim(),
            IsMain = true,
        });

        var order = 0;
        foreach (var (code, en, ur) in DefaultDepartments)
            db.Departments.Add(new Department { OrganizationId = org.Id, Code = code, NameEn = en, NameUr = ur, SortOrder = ++order * 10 });

        foreach (var (type, (prefix, padding, reset)) in SequenceTypes.Defaults)
            db.NumberSequences.Add(new NumberSequence { OrganizationId = org.Id, SequenceType = type, Prefix = prefix, Padding = padding, ResetPolicy = reset });

        foreach (var (key, value) in DefaultSettings)
            db.BusinessSettings.Add(new BusinessSetting { OrganizationId = org.Id, Key = key, Value = value });

        var sort = 0;
        foreach (var r in Domain.Samples.SampleRejectionReasonDefaults.All)
            db.SampleRejectionReasons.Add(new Domain.Samples.SampleRejectionReason
            {
                OrganizationId = org.Id, Code = r.Code, NameEn = r.NameEn, NameUr = r.NameUr, RequiresNote = r.RequiresNote, SortOrder = ++sort * 10,
            });

        var permissionIds = await db.Permissions.ToDictionaryAsync(p => p.Key, p => p.Id, ct);
        foreach (var template in DefaultRoles.All)
        {
            var role = new Role
            {
                OrganizationId = org.Id, Code = template.Code, NameEn = template.NameEn, NameUr = template.NameUr,
                Description = template.Description, IsSystem = true,
            };
            foreach (var key in template.Permissions)
                role.Permissions.Add(new RolePermission { OrganizationId = org.Id, PermissionId = permissionIds[key] });
            db.Roles.Add(role);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return org.Id;
    }
}
