using Lims.Domain.Core;
using Lims.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lims.Infrastructure.Persistence.Configurations;

internal sealed class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users", "security", t =>
            t.HasCheckConstraint("CK_Users_PreferredLanguage", "[PreferredLanguage] IN ('en','ur')"));
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Username).HasMaxLength(64);
        b.Property(x => x.DisplayNameEn).HasMaxLength(Len.Name);
        b.Property(x => x.DisplayNameUr).HasMaxLength(Len.Name);
        b.Property(x => x.Email).HasMaxLength(Len.Email);
        b.Property(x => x.Phone).HasMaxLength(Len.Phone);
        b.Property(x => x.Designation).HasMaxLength(Len.Name);
        b.Property(x => x.PasswordHash).HasMaxLength(512).IsUnicode(false);
        b.Property(x => x.SecurityStamp).HasMaxLength(64).IsUnicode(false);
        b.Property(x => x.PreferredLanguage).HasMaxLength(2).IsUnicode(false);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.HasOne<Branch>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.DefaultBranchId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id });
        // Usernames are unique across the installation (default SQL collation is case-insensitive).
        b.HasIndex(x => x.Username).IsUnique();
    }
}

internal sealed class RoleConfig : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Roles", "security");
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Code).HasMaxLength(Len.Code).IsUnicode(false);
        b.Property(x => x.NameEn).HasMaxLength(Len.Name);
        b.Property(x => x.NameUr).HasMaxLength(Len.Name);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId);
        b.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
    }
}

internal sealed class PermissionConfig : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("Permissions", "security");
        b.Property(x => x.Id).ValueGeneratedNever(); // ids are fixed in code (PermissionCatalog)
        b.Property(x => x.Key).HasMaxLength(64).IsUnicode(false);
        b.Property(x => x.Module).HasMaxLength(40);
        b.Property(x => x.DescriptionEn).HasMaxLength(Len.Name);
        b.Property(x => x.DescriptionUr).HasMaxLength(Len.Name);
        b.HasIndex(x => x.Key).IsUnique();
        b.HasData(Permissions.All.Select(p => new Permission
        {
            Id = p.Id, Key = p.Key, Module = p.Module, DescriptionEn = p.DescriptionEn, DescriptionUr = p.DescriptionUr,
        }));
    }
}

internal sealed class RolePermissionConfig : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("RolePermissions", "security");
        b.HasKey(x => new { x.RoleId, x.PermissionId });
        b.HasOne(x => x.Role).WithMany(r => r.Permissions)
            .HasForeignKey(x => new { x.OrganizationId, x.RoleId })
            .HasPrincipalKey(r => new { r.OrganizationId, r.Id });
        b.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId);
    }
}

internal sealed class UserRoleConfig : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("UserRoles", "security");
        b.HasKey(x => new { x.UserId, x.RoleId });
        // Both composite keys share OrganizationId, so user and role must be in the same organisation.
        b.HasOne(x => x.User).WithMany(u => u.Roles)
            .HasForeignKey(x => new { x.OrganizationId, x.UserId })
            .HasPrincipalKey(u => new { u.OrganizationId, u.Id });
        b.HasOne(x => x.Role).WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.RoleId })
            .HasPrincipalKey(r => new { r.OrganizationId, r.Id });
        b.HasIndex(x => x.RoleId);
    }
}

internal sealed class UserBranchConfig : IEntityTypeConfiguration<UserBranch>
{
    public void Configure(EntityTypeBuilder<UserBranch> b)
    {
        b.ToTable("UserBranches", "security");
        b.HasKey(x => new { x.UserId, x.BranchId });
        b.HasOne(x => x.User).WithMany(u => u.Branches)
            .HasForeignKey(x => new { x.OrganizationId, x.UserId })
            .HasPrincipalKey(u => new { u.OrganizationId, u.Id });
        b.HasOne(x => x.Branch).WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.BranchId })
            .HasPrincipalKey(br => new { br.OrganizationId, br.Id });
        b.HasIndex(x => x.BranchId);
    }
}

internal sealed class LoginHistoryConfig : IEntityTypeConfiguration<LoginHistory>
{
    public void Configure(EntityTypeBuilder<LoginHistory> b)
    {
        b.ToTable("LoginHistory", "security");
        b.Property(x => x.UsernameAttempted).HasMaxLength(64);
        b.Property(x => x.FailureReason).HasMaxLength(100);
        b.Property(x => x.IpAddress).HasMaxLength(45).IsUnicode(false);
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.HasIndex(x => new { x.UserId, x.AtUtc });
        b.HasIndex(x => x.AtUtc);
    }
}
