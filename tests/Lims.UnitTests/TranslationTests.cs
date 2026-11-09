using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lims.UnitTests;

/// <summary>Keeps the English and Urdu interface translations complete and consistent.</summary>
public sealed class TranslationTests
{
    private static string LocaleDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Lims.sln"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "web", "src", "i18n", "locales");
    }

    private static Dictionary<string, string> Flatten(string file)
    {
        var result = new Dictionary<string, string>();
        void Walk(JsonElement e, string prefix)
        {
            if (e.ValueKind == JsonValueKind.Object)
                foreach (var p in e.EnumerateObject()) Walk(p.Value, prefix.Length == 0 ? p.Name : $"{prefix}.{p.Name}");
            else
                result[prefix] = e.GetString() ?? "";
        }
        Walk(JsonDocument.Parse(File.ReadAllText(Path.Combine(LocaleDir(), file))).RootElement, "");
        return result;
    }

    [Fact]
    public void English_and_Urdu_have_the_same_keys()
    {
        var en = Flatten("en.json");
        var ur = Flatten("ur.json");
        Assert.Empty(en.Keys.Except(ur.Keys));
        Assert.Empty(ur.Keys.Except(en.Keys));
    }

    [Fact]
    public void No_translation_is_empty()
    {
        Assert.All(Flatten("ur.json"), kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"Empty Urdu text for {kv.Key}"));
        Assert.All(Flatten("en.json"), kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value), $"Empty English text for {kv.Key}"));
    }

    [Fact]
    public void Placeholders_match_between_languages()
    {
        var en = Flatten("en.json");
        var ur = Flatten("ur.json");
        var placeholder = new Regex(@"\{\{(\w+)\}\}");
        foreach (var (key, text) in en)
        {
            var expected = placeholder.Matches(text).Select(m => m.Groups[1].Value).Order();
            var actual = placeholder.Matches(ur[key]).Select(m => m.Groups[1].Value).Order();
            Assert.True(expected.SequenceEqual(actual), $"Placeholders differ for {key}");
        }
    }

    [Fact]
    public void Every_server_error_code_has_a_translation()
    {
        var root = new DirectoryInfo(LocaleDir()).Parent!.Parent!.Parent!.Parent!.FullName; // repository root
        var codes = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(f => Regex.Matches(File.ReadAllText(f),
                @"(?:BusinessRuleException(?:\.Conflict)?\(\s*|\[""code""\]\s*=\s*)""([a-z_]+)""").Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();
        var en = Flatten("en.json");

        Assert.NotEmpty(codes);
        Assert.All(codes, code => Assert.True(en.ContainsKey($"errors.{code}"), $"Missing errors.{code} in en.json"));
    }
}
