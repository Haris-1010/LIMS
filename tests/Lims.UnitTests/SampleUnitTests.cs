using System.Text.RegularExpressions;
using Lims.Application.Barcodes;
using Lims.Domain.Orders;
using Lims.Domain.Samples;

namespace Lims.UnitTests;

public sealed class Code128Tests
{
    [Fact]
    public void Symbol_table_has_107_distinct_patterns_of_the_right_width()
    {
        var p = Code128.SymbolPatterns;
        Assert.Equal(107, p.Count);
        Assert.Equal(107, p.Distinct().Count());
        for (var i = 0; i < 106; i++) Assert.Equal(11, p[i].Sum(c => c - '0'));   // every symbol is 11 modules wide
        Assert.Equal(13, p[106].Sum(c => c - '0'));                                // the stop pattern is 13
    }

    [Fact]
    public void Every_symbol_has_even_total_bar_width_which_catches_transcription_errors()
    {
        // A property of the standard: bars (positions 1,3,5[,7]) of each symbol sum to an even number.
        for (var i = 0; i <= 106; i++)
        {
            var bars = Code128.SymbolPatterns[i].Where((_, idx) => idx % 2 == 0).Sum(c => c - '0');
            Assert.True(bars % 2 == 0, $"Symbol {i} ({Code128.SymbolPatterns[i]}) breaks the bar-parity rule");
        }
    }

    [Fact]
    public void Known_published_patterns()
    {
        // Values published in the Code 128 specification / reference encoders.
        Assert.Equal("11011001100", Pattern(0));          // space
        Assert.Equal("11010010000", Pattern(104));        // Start B
        Assert.Equal("1100011101011", Pattern(106));      // Stop
        // "A" = start B (104) + 33 + checksum (104 + 33*1) mod 103 = 34 + stop
        Assert.Equal(new[] { 104, 33, 34 }, Code128.Symbols("A"));
        Assert.Equal(Pattern(104) + Pattern(33) + Pattern(34) + Pattern(106), Code128.Encode("A"));
    }

    private static string Pattern(int value)
    {
        var sb = new System.Text.StringBuilder();
        var bar = true;
        foreach (var w in Code128.SymbolPatterns[value]) { sb.Append(bar ? '1' : '0', w - '0'); bar = !bar; }
        return sb.ToString();
    }

    /// <summary>Independent decoder: splits the modules into symbols, checks the checksum and returns the text.</summary>
    private static string Decode(string modules)
    {
        var lookup = Enumerable.Range(0, 107).ToDictionary(Pattern, i => i);
        Assert.EndsWith(Pattern(106), modules);
        var body = modules[..^13];
        Assert.Equal(0, body.Length % 11);
        var symbols = Enumerable.Range(0, body.Length / 11).Select(i => lookup[body.Substring(i * 11, 11)]).ToList();
        Assert.Equal(104, symbols[0]);
        var checksum = (104 + symbols.Skip(1).SkipLast(1).Select((s, i) => s * (i + 1)).Sum()) % 103;
        Assert.Equal(checksum, symbols[^1]);
        return new string(symbols.Skip(1).SkipLast(1).Select(s => (char)(s + 32)).ToArray());
    }

    [Theory]
    [InlineData("S-261109-0001")]
    [InlineData("S-261231-9999")]
    [InlineData("CC1-001")]
    [InlineData("0123456789")]
    [InlineData("a~ !Z")]
    public void Round_trip_through_an_independent_decoder(string value) => Assert.Equal(value, Decode(Code128.Encode(value)));

    [Fact]
    public void Every_printable_ascii_character_round_trips()
    {
        var all = new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray());
        Assert.Equal(all, Decode(Code128.Encode(all)));
    }

    [Fact]
    public void Unsupported_input_is_refused_instead_of_producing_a_wrong_barcode()
    {
        Assert.False(Code128.CanEncode(""));
        Assert.False(Code128.CanEncode("عائشہ"));
        Assert.False(Code128.CanEncode("tab\there"));
        Assert.Throws<ArgumentException>(() => Code128.Encode("é"));
    }

    [Theory]
    [InlineData("s-261109-0001", "S-261109-0001")]
    [InlineData("  S-261109-0001\r\n", "S-261109-0001")]
    [InlineData("S-261109-0001\t", "S-261109-0001")]
    [InlineData(null, "")]
    public void Scanner_input_is_normalised(string? raw, string expected) => Assert.Equal(expected, Code128.NormalizeScan(raw));
}

public sealed class SampleRulesTests
{
    [Fact]
    public void Exactly_the_documented_moves_are_allowed()
    {
        var expected = new HashSet<(SampleStatus, SampleStatus)>
        {
            (SampleStatus.Planned, SampleStatus.Collected), (SampleStatus.Planned, SampleStatus.Cancelled),
            (SampleStatus.Collected, SampleStatus.InTransit), (SampleStatus.Collected, SampleStatus.Received), (SampleStatus.Collected, SampleStatus.Rejected),
            (SampleStatus.InTransit, SampleStatus.Collected), (SampleStatus.InTransit, SampleStatus.Received), (SampleStatus.InTransit, SampleStatus.Rejected),
            (SampleStatus.Received, SampleStatus.Rejected),
        };
        foreach (var from in Enum.GetValues<SampleStatus>())
            foreach (var to in Enum.GetValues<SampleStatus>())
                Assert.Equal(expected.Contains((from, to)), SampleRules.CanMove(from, to));
    }

    [Fact]
    public void Rejected_and_cancelled_are_final_and_not_live()
    {
        foreach (var s in new[] { SampleStatus.Rejected, SampleStatus.Cancelled })
        {
            Assert.True(SampleRules.IsFinal(s));
            Assert.False(SampleRules.IsLive(s));
        }
        Assert.All(new[] { SampleStatus.Planned, SampleStatus.Collected, SampleStatus.InTransit, SampleStatus.Received },
            s => { Assert.True(SampleRules.IsLive(s)); Assert.False(SampleRules.IsFinal(s)); });
    }

    [Theory]
    [InlineData(SampleStatus.Planned, OrderItemStatus.Ordered)]
    [InlineData(SampleStatus.Collected, OrderItemStatus.Collected)]
    [InlineData(SampleStatus.InTransit, OrderItemStatus.Collected)]
    [InlineData(SampleStatus.Received, OrderItemStatus.Received)]
    [InlineData(SampleStatus.Rejected, OrderItemStatus.Ordered)]
    public void Test_status_follows_its_sample(SampleStatus sample, OrderItemStatus item) => Assert.Equal(item, SampleRules.ItemStatusFor(sample));

    [Fact]
    public void Default_rejection_reasons_are_unique_and_bilingual()
    {
        var all = SampleRejectionReasonDefaults.All;
        Assert.Equal(all.Count, all.Select(r => r.Code).Distinct().Count());
        Assert.All(all, r => { Assert.False(string.IsNullOrWhiteSpace(r.NameEn)); Assert.False(string.IsNullOrWhiteSpace(r.NameUr)); });
        Assert.Contains(all, r => r.Code == "OTHER" && r.RequiresNote);
    }
}

public sealed class SampleArchitectureTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Lims.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }

    /// <summary>
    /// Samples bypass the branch query filter on purpose (visibility = origin/current/destination branch). That is only safe
    /// if every read goes through the visibility gate in SampleService, so no other file may touch the Samples set, and inside
    /// the Samples folder each use must be marked VISIBILITY-GATE (or be an Add of a brand new sample).
    /// </summary>
    [Fact]
    public void Samples_are_only_read_through_the_visibility_gate()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            var path = file.Replace('\\', '/');
            if (path.Contains("/Migrations/") || path.Contains("/Configurations/") || path.EndsWith("LimsDbContext.cs") || path.EndsWith("LimsDbContext.Save.cs")) continue;
            var inSamplesFolder = path.Contains("Lims.Infrastructure/Samples/");
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!Regex.IsMatch(line, @"\bdb\.Samples\b|Set<Sample>\(\)|\.Samples\s*\.(Where|Include|Any|Single|First|Count|Select|AsNoTracking)")) continue;
                if (!inSamplesFolder) { offenders.Add($"{Path.GetFileName(file)}:{i + 1}: touches Samples outside the Samples services"); continue; }
                if (!line.Contains("VISIBILITY-GATE") && !line.Contains(".Add(")) offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {line.Trim()}");
            }
        }
        Assert.True(offenders.Count == 0, "Unmarked sample access:\n" + string.Join("\n", offenders));
    }
}
