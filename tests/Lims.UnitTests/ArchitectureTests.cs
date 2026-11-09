using System.Text.RegularExpressions;

namespace Lims.UnitTests;

/// <summary>Static checks on the source code that protect tenant isolation.</summary>
public sealed class ArchitectureTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Lims.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }

    /// <summary>
    /// IgnoreQueryFilters() switches off the organisation/branch filters for the WHOLE query, including
    /// any joins. It is only allowed in a statement that restricts by OrganizationId itself, by a
    /// globally unique username, or by a specific user id (sign-in, before an organisation is known).
    /// </summary>
    [Fact]
    public void IgnoreQueryFilters_is_always_paired_with_an_explicit_scope()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"IgnoreQueryFilters\(\)"))
            {
                // The statement containing the call (up to the next semicolon).
                var end = text.IndexOf(';', m.Index);
                var start = text.LastIndexOfAny([';', '{', '}'], m.Index) + 1;
                var statement = text[start..(end < 0 ? text.Length : end)];
                if (!Regex.IsMatch(statement, @"OrganizationId|Username|UserId == userId|u\.Id == userId"))
                    offenders.Add($"{Path.GetFileName(file)}: {statement.Trim().Split('\n')[0]}");
            }
        }
        Assert.True(offenders.Count == 0, "IgnoreQueryFilters without an explicit scope:\n" + string.Join("\n", offenders));
    }
}
