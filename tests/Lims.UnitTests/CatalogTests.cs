using System.Text.RegularExpressions;
using Lims.Domain.Core;
using Lims.Domain.Security;

namespace Lims.UnitTests;

public sealed class PermissionCatalogTests
{
    [Fact]
    public void Ids_and_keys_are_unique()
    {
        Assert.Equal(Permissions.All.Count, Permissions.All.Select(p => p.Id).Distinct().Count());
        Assert.Equal(Permissions.All.Count, Permissions.All.Select(p => p.Key).Distinct().Count());
    }

    [Fact]
    public void Every_permission_has_English_and_Urdu_description()
    {
        Assert.All(Permissions.All, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.DescriptionEn));
            Assert.False(string.IsNullOrWhiteSpace(p.DescriptionUr));
        });
    }

    [Fact]
    public void Default_roles_only_reference_existing_permissions()
    {
        var keys = Permissions.All.Select(p => p.Key).ToHashSet();
        Assert.All(DefaultRoles.All, r => Assert.All(r.Permissions, k => Assert.Contains(k, keys)));
        Assert.Equal(DefaultRoles.All.Count, DefaultRoles.All.Select(r => r.Code).Distinct().Count());
    }

    [Fact]
    public void Only_admin_roles_can_manage_users_and_roles()
    {
        var admins = DefaultRoles.All.Where(r => r.Permissions.Contains(Permissions.RolesManage)).Select(r => r.Code);
        Assert.Equal(["SYSADMIN", "LABMGR"], admins);
    }

    [Fact]
    public void Clinical_approval_is_limited_to_pathologists_and_managers()
    {
        var approvers = DefaultRoles.All.Where(r => r.Permissions.Contains(Permissions.ResultsApprove)).Select(r => r.Code).Order();
        Assert.Equal(["LABMGR", "PATHOLOGIST", "SYSADMIN"], approvers);
    }

    [Fact]
    public void Frontend_navigation_uses_only_known_permission_keys()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Lims.sln"))) root = root.Parent;
        Assert.NotNull(root);

        var nav = File.ReadAllText(Path.Combine(root!.FullName, "web", "src", "app", "navigation.ts"));
        var used = Regex.Matches(nav, @"permission:\s*'([^']+)'").Select(m => m.Groups[1].Value).Distinct().ToList();
        var known = Permissions.All.Select(p => p.Key).ToHashSet();

        Assert.NotEmpty(used);
        Assert.All(used, key => Assert.Contains(key, known));
    }
}

public sealed class NumberSequenceFormatTests
{
    [Theory]
    [InlineData(SequenceResetPolicy.Never, "")]
    [InlineData(SequenceResetPolicy.Yearly, "26")]
    [InlineData(SequenceResetPolicy.Monthly, "2611")]
    [InlineData(SequenceResetPolicy.Daily, "261109")]
    public void Period_key_follows_reset_policy(SequenceResetPolicy policy, string expected) =>
        Assert.Equal(expected, NumberSequence.PeriodFor(policy, new DateTime(2026, 11, 9, 23, 59, 0)));

    [Theory]
    [InlineData("P", "", 42, 6, "P-000042")]
    [InlineData("ORD", "2611", 7, 5, "ORD-2611-00007")]
    [InlineData("", "", 5, 3, "005")]
    [InlineData("S", "261109", 123456, 4, "S-261109-123456")] // never truncated when it outgrows padding
    public void Format_pads_and_joins(string prefix, string period, long value, int padding, string expected) =>
        Assert.Equal(expected, NumberSequence.Format(prefix, period, value, padding));
}
