using System.Text.RegularExpressions;
using Lims.Application.Reports;
using Lims.Domain.Catalog;
using Lims.Domain.Orders;
using Lims.Domain.Patients;
using Lims.Domain.Reports;

namespace Lims.UnitTests;

public sealed class ReportRulesTests
{
    private static readonly DateTime T1 = new(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
    private static LabReport Issued(int released = 2, int active = 3, DateTime? latest = null) =>
        new() { ReleasedCount = released, ActiveTestCount = active, LatestApprovedAtUtc = latest ?? T1 };

    [Theory]
    [InlineData(0, 3, false)]
    [InlineData(1, 3, true)]
    [InlineData(3, 3, true)]
    public void A_report_can_be_issued_once_one_test_is_released(int released, int active, bool can) =>
        Assert.Equal(can, ReportRules.CanIssue(new ReleaseState(released, active, T1)));

    [Theory]
    [InlineData(1, 3, ReportStatus.Interim)]
    [InlineData(2, 3, ReportStatus.Interim)]
    [InlineData(3, 3, ReportStatus.Final)]
    [InlineData(1, 1, ReportStatus.Final)]
    public void Final_only_when_every_active_test_is_released(int released, int active, ReportStatus expected) =>
        Assert.Equal(expected, ReportRules.StatusFor(new ReleaseState(released, active, T1)));

    [Fact]
    public void An_issued_report_is_outdated_when_a_test_is_released_amended_or_cancelled()
    {
        var r = Issued();
        Assert.False(ReportRules.IsOutdated(r, new ReleaseState(2, 3, T1)));
        Assert.True(ReportRules.IsOutdated(r, new ReleaseState(3, 3, T1.AddMinutes(5))));      // another test released
        Assert.True(ReportRules.IsOutdated(r, new ReleaseState(2, 3, T1.AddMinutes(5))));      // an amendment approved (same count, later time)
        Assert.True(ReportRules.IsOutdated(r, new ReleaseState(2, 2, T1)));                    // a pending test was cancelled
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(7, true)]
    public void Only_the_first_print_is_the_original(int printsSoFar, bool duplicate) => Assert.Equal(duplicate, ReportRules.IsDuplicate(printsSoFar));

    [Fact]
    public void Fingerprint_is_deterministic_hex_and_detects_any_change()
    {
        var a = ReportRules.Fingerprint("{\"a\":1}");
        Assert.Equal(a, ReportRules.Fingerprint("{\"a\":1}"));
        Assert.Matches("^[0-9a-f]{64}$", a);
        Assert.NotEqual(a, ReportRules.Fingerprint("{\"a\":2}"));
        Assert.NotEqual(a, ReportRules.Fingerprint("{\"a\":1} "));
        Assert.True(ReportRules.Verify("{\"a\":1}", a));
        Assert.True(ReportRules.Verify("{\"a\":1}", a.ToUpperInvariant()));
        Assert.False(ReportRules.Verify("{\"a\":9}", a));
        Assert.Equal(12, ReportRules.ShortFingerprint(a).Length);
        Assert.Equal(ReportRules.ShortFingerprint(a), ReportRules.ShortFingerprint(a).ToUpperInvariant());
    }
}

public sealed class ImageSnifferTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10];

    [Fact] public void Png_and_jpeg_are_recognised_by_their_bytes() { Assert.Equal("image/png", ImageSniffer.Detect(Png)); Assert.Equal("image/jpeg", ImageSniffer.Detect(Jpeg)); }

    [Theory]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'></svg>")]
    [InlineData("GIF89a......")]
    [InlineData("<html><script>alert(1)</script></html>")]
    [InlineData("MZ\u0090\u0000\u0003")]                         // an executable
    [InlineData("")]
    public void Everything_else_is_refused_whatever_the_file_is_called(string content) =>
        Assert.Null(ImageSniffer.Detect(System.Text.Encoding.Latin1.GetBytes(content)));

    [Fact] public void Truncated_headers_are_refused() { Assert.Null(ImageSniffer.Detect(Png[..5])); Assert.Null(ImageSniffer.Detect(Jpeg[..2])); }
}

public sealed class ReportSnapshotBuilderTests
{
    private static readonly DateTime At = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    private static ReleasedValue Num(int id, string code, decimal value, int places = 1, ResultFlag flag = ResultFlag.Normal, decimal? lo = 10, decimal? hi = 20, string? rangeText = null, int sort = 0) =>
        new(id, code, code + " name", code + " اردو", null, sort, ResultType.Numeric, value, null, null, "g/dL", places, flag, lo, hi, 5, 30, rangeText);

    private static ReleasedOrderData Data(IReadOnlyList<ReleasedTest>? tests = null, IReadOnlyList<PendingTestData>? pending = null, IReadOnlyList<SignatoryData>? sig = null, int active = 2) => new(
        1, "ORD-2611-00001", OrderPriority.Routine, At, "Dr Khan", 1, "Main Laboratory", "مرکزی لیبارٹری",
        1, "Main Laboratory", "مرکزی لیبارٹری", "12 Mall Road", "مال روڈ 12", "042-111", "lab@example.test", "Demo Lab", "ڈیمو لیب",
        7, "P-000007", "Ayesha Siddiqui", "عائشہ صدیقی", Sex.Female, new DateOnly(1992, 11, 9), false, "03001234567", 34, 0, 1, At.AddHours(-3), At.AddHours(-2),
        active, At, tests ?? [], pending ?? [], sig ?? []);

    private static ReleasedTest Test(long item, string code, string dept, int deptSort, params ReleasedValue[] values) =>
        new(item, code, code + " test", code + " ٹیسٹ", dept, dept + " اردو", deptSort, "Method: fixture", 100 + item, 1, At, "Comment", values);

    private static ReportSnapshot Build(ReleasedOrderData d, ReportLanguage lang = ReportLanguage.English) =>
        ReportSnapshotBuilder.Build(d, new ReportTemplateText("Open 24h", "چوبیس گھنٹے", null, null, "Disclaimer", "دستبرداری"), lang, "REP-2611-0001", 1, ReportStatus.Final, At, "Dr Pathologist", "ڈاکٹر", null, null);

    [Fact]
    public void Values_are_displayed_rounded_but_the_stored_number_is_never_touched()
    {
        var d = Data([Test(1, "HB", "Haematology", 10, Num(1, "HGB", 12.345m, places: 1), Num(2, "RBC", 4.5m, places: 2, sort: 1))]);
        var p = Build(d).Sections[0].Tests[0].Parameters;
        Assert.Equal("12.3", p[0].Display);        // display rounding only
        Assert.Equal("4.50", p[1].Display);
        Assert.Equal(12.345m, d.Tests[0].Values[0].Number);
    }

    [Fact]
    public void Flags_get_a_short_code_and_unflagged_values_get_none()
    {
        var codes = new[] { ResultFlag.None, ResultFlag.Normal, ResultFlag.Low, ResultFlag.High, ResultFlag.CriticalLow, ResultFlag.CriticalHigh, ResultFlag.Abnormal }
            .Select(ReportSnapshotBuilder.FlagCode).ToArray();
        Assert.Equal(["", "", "L", "H", "LL", "HH", "A"], codes);
    }

    [Theory]
    [InlineData(10, 20, null, "10.0 – 20.0")]
    [InlineData(10, null, null, "≥ 10.0")]
    [InlineData(null, 20, null, "≤ 20.0")]
    [InlineData(null, null, null, null)]
    [InlineData(10, 20, "< 200 (desirable)", "< 200 (desirable)")]   // the approved display text wins
    public void The_reference_text_is_the_approved_range_copied_at_release(int? lo, int? hi, string? text, string? expected) =>
        Assert.Equal(expected, ReportSnapshotBuilder.ReferenceText(Num(1, "X", 15m, lo: (decimal?)lo, hi: (decimal?)hi, rangeText: text)));

    [Fact]
    public void Text_and_option_values_print_as_entered_with_their_urdu_text_when_there_is_one()
    {
        var opt = new ReleasedValue(3, "SCR", "Screen", "سکرین", null, 0, ResultType.Option, null, "Positive", "مثبت", null, 0, ResultFlag.Abnormal, null, null, null, null, null);
        var txt = new ReleasedValue(4, "NOTE", "Note", null, null, 1, ResultType.Text, null, "Lipaemic", null, null, 0, ResultFlag.None, null, null, null, null, null);
        var p = Build(Data([Test(1, "SCR", "Immunology", 20, opt, txt)])).Sections[0].Tests[0].Parameters;
        Assert.Equal(("Positive", "مثبت", "A"), (p[0].Display, p[0].DisplayUr, p[0].FlagCode));
        Assert.Equal(("Lipaemic", "Lipaemic", null), (p[1].Display, p[1].DisplayUr, p[1].ReferenceText));
    }

    [Fact]
    public void Tests_are_grouped_by_department_in_a_fixed_order_and_pending_tests_are_listed_not_guessed()
    {
        var d = Data(
            [Test(5, "LFT", "Chemistry", 20, Num(1, "ALT", 30)), Test(2, "CBC", "Haematology", 10, Num(2, "HGB", 14)), Test(3, "ESR", "Haematology", 10, Num(3, "ESR", 5))],
            pending: [new PendingTestData(9, "TSH", "TSH test", "ٹی ایس ایچ")], active: 4);
        var s = Build(d);
        Assert.Equal(["Haematology", "Chemistry"], s.Sections.Select(x => x.DepartmentNameEn));
        Assert.Equal(["CBC", "ESR"], s.Sections[0].Tests.Select(t => t.Code));
        Assert.Equal(["TSH"], s.Pending.Select(p => p.Code));
        Assert.DoesNotContain(s.Sections.SelectMany(x => x.Tests), t => t.Code == "TSH");
    }

    [Fact]
    public void Each_person_signs_once_and_approvers_come_first()
    {
        var sig = new[]
        {
            new SignatoryData(2, "Zed Technician", null, "Technologist", "R-1", false, false),
            new SignatoryData(1, "Dr Ahmed", "ڈاکٹر احمد", "Consultant Pathologist", "PMDC 123", true, true),
            new SignatoryData(3, "Amy Verifier", null, null, null, false, false),
        };
        var s = Build(Data([Test(1, "HB", "Haematology", 10, Num(1, "HGB", 14))], sig: sig));
        Assert.Equal([1, 3, 2], s.Signatories.Select(x => x.UserId));
        Assert.True(s.Signatories[0].Approved);
        Assert.True(s.Signatories[0].HasSignature);
    }

    [Fact]
    public void The_snapshot_round_trips_and_has_a_stable_fingerprint()
    {
        var snap = Build(Data([Test(1, "HB", "Haematology", 10, Num(1, "HGB", 14))]), ReportLanguage.Both);
        var json = ReportJson.Serialize(snap);
        Assert.Equal(json, ReportJson.Serialize(Build(Data([Test(1, "HB", "Haematology", 10, Num(1, "HGB", 14))]), ReportLanguage.Both)));
        var back = ReportJson.Deserialize(json)!;
        Assert.Equal("14.0", back.Sections[0].Tests[0].Parameters[0].Display);
        Assert.Equal("عائشہ صدیقی", back.Patient.NameUr);
        Assert.Equal(ReportLanguage.Both, back.Language);
        // A single changed digit is detected.
        var hash = ReportRules.Fingerprint(json);
        Assert.False(ReportRules.Verify(json.Replace("\"display\":\"14.0\"", "\"display\":\"41.0\""), hash));
    }

    [Fact]
    public void Letterhead_text_comes_from_the_template_in_both_languages()
    {
        var s = Build(Data([Test(1, "HB", "Haematology", 10, Num(1, "HGB", 14))]));
        Assert.Equal(("Open 24h", "چوبیس گھنٹے", "Disclaimer", "دستبرداری"), (s.Letterhead.HeaderNoteEn, s.Letterhead.HeaderNoteUr, s.Letterhead.DisclaimerEn, s.Letterhead.DisclaimerUr));
        Assert.Equal("مال روڈ 12", s.Letterhead.AddressUr);
    }

    [Fact]
    public void Numeric_lines_carry_the_value_and_limits_for_the_range_bar_and_other_lines_do_not()
    {
        var opt = new ReleasedValue(2, "SCR", "Screen", null, null, 1, ResultType.Option, null, "Positive", null, null, 0, ResultFlag.Abnormal, null, null, null, null, "Negative");
        var s = Build(Data([Test(1, "HB", "Haematology", 10, Num(1, "HGB", 12.345m, lo: 13, hi: null), opt)]));
        var p = s.Sections[0].Tests[0].Parameters;
        Assert.Equal((12.345m, 13m, (decimal?)null), (p[0].Value, p[0].Low, p[0].High));
        Assert.Equal(((decimal?)null, (decimal?)null, (decimal?)null), (p[1].Value, p[1].Low, p[1].High));
        Assert.Equal(3, s.SchemaVersion);
    }

    [Fact]
    public void Template_signatories_are_copied_into_the_report_and_older_reports_read_without_them()
    {
        var d = Data([Test(1, "HB", "Haematology", 10, Num(1, "HGB", 14))]);
        var t = new ReportTemplateText(null, null, null, null, null, null,
            [new SnapFixedSignatory(7, "Dr. Consultant", "ڈاکٹر کنسلٹنٹ", "MBBS, FCPS", "Consultant Pathologist", "PMDC 1-P", true)]);
        var s = ReportSnapshotBuilder.Build(d, t, ReportLanguage.English, "REP-2611-0002", 1, ReportStatus.Final, At, "Dr Pathologist", null, null, null);
        var fixedOne = Assert.Single(s.FixedSignatories!);
        Assert.Equal(("Dr. Consultant", "MBBS, FCPS", true), (fixedOne.NameEn, fixedOne.Qualification, fixedOne.HasSignature));
        // They never join the people who verified or approved results.
        Assert.DoesNotContain(s.Signatories, x => x.NameEn == "Dr. Consultant");
        Assert.Equal("Dr. Consultant", ReportJson.Deserialize(ReportJson.Serialize(s))!.FixedSignatories![0].NameEn);

        // A report issued before schema 3 has no such field; it still reads (its stored JSON, so its fingerprint, is untouched).
        var json = ReportJson.Serialize(Build(d));
        var old = Regex.Replace(json, ",\"fixedSignatories\":\\[[^\\]]*\\]", "");
        Assert.DoesNotContain("fixedSignatories", old);
        Assert.Null(ReportJson.Deserialize(old)!.FixedSignatories);
    }

    [Fact]
    public void A_report_issued_before_schema_2_still_reads_and_has_no_bar_values()
    {
        var json = ReportJson.Serialize(Build(Data([Test(1, "HB", "Haematology", 10, Num(1, "HGB", 14))])));
        // Remove the schema-2 fields, as in a report issued before them.
        var old = Regex.Replace(json, ",\"(value|low|high)\":[^,}]+", "");
        Assert.DoesNotContain("\"low\"", old);
        var p = ReportJson.Deserialize(old)!.Sections[0].Tests[0].Parameters[0];
        Assert.Equal("14.0", p.Display);
        Assert.Null(p.Value);
        Assert.Null(p.Low);
    }
}

public sealed class ReportDesignRulesTests
{
    [Fact]
    public void The_built_in_design_is_valid_and_survives_a_round_trip()
    {
        var d = new ReportDesign();
        Assert.Null(ReportDesignRules.Problem(d));
        var back = ReportDesignRules.Read(ReportDesignRules.Serialize(d));
        Assert.Equal(d.Blocks, back.Blocks);
        Assert.Equal(d.GraphTests, back.GraphTests);
        Assert.Equal(d with { Blocks = back.Blocks, GraphTests = back.GraphTests }, back);
    }

    [Fact]
    public void Designs_saved_before_header_and_footer_images_existed_read_with_both_off()
    {
        var old = """{"style":"modern","accentColor":"#0f766e","fontSizePt":9.5,"marginTopMm":12,"marginBottomMm":14,"marginSideMm":12,"showLetterhead":true,"logoPosition":"start","showRangeBars":true,"showFlagWords":true,"colourFlags":false,"tableStyle":"zebra","blocks":["patient","results","pending","signatures","disclaimer"]}""";
        var d = ReportDesignRules.Read(old);
        Assert.Equal("modern", d.Style);
        Assert.False(d.UseHeaderImage);
        Assert.False(d.ShowFooterImage);
        var withImages = ReportDesignRules.Read(ReportDesignRules.Serialize(d with { UseHeaderImage = true, ShowFooterImage = true }));
        Assert.True(withImages.UseHeaderImage);
        Assert.True(withImages.ShowFooterImage);
    }

    [Theory]
    [InlineData("style")]
    [InlineData("accentColor")]
    [InlineData("fontSizePt")]
    [InlineData("marginTopMm")]
    [InlineData("marginBottomMm")]
    [InlineData("marginSideMm")]
    [InlineData("logoPosition")]
    [InlineData("tableStyle")]
    public void Each_setting_outside_its_allowed_values_is_named(string setting)
    {
        var d = new ReportDesign();
        var bad = setting switch
        {
            "style" => d with { Style = "neon" },
            "accentColor" => d with { AccentColor = "red" },
            "fontSizePt" => d with { FontSizePt = 20 },
            "marginTopMm" => d with { MarginTopMm = 2 },
            "marginBottomMm" => d with { MarginBottomMm = 90 },
            "marginSideMm" => d with { MarginSideMm = 40 },
            "logoPosition" => d with { LogoPosition = "top" },
            _ => d with { TableStyle = "fancy" },
        };
        Assert.Equal(setting, ReportDesignRules.Problem(bad));
    }

    [Fact]
    public void Graph_tests_are_a_list_of_reasonable_test_codes()
    {
        Assert.Null(ReportDesignRules.Problem(new ReportDesign { GraphTests = ["HBA1C", "FBS"] }));
        Assert.Equal("graphTests", ReportDesignRules.Problem(new ReportDesign { GraphTests = [" "] }));
        Assert.Equal("graphTests", ReportDesignRules.Problem(new ReportDesign { GraphTests = [new string('X', 51)] }));
    }

    [Theory]
    [InlineData("patient,results,pending,signatures")]                 // a block left out
    [InlineData("patient,results,pending,signatures,disclaimer,qr")]   // an unknown block
    [InlineData("patient,results,results,signatures,disclaimer")]      // a block twice
    public void The_body_must_keep_every_block_exactly_once(string blocks)
    {
        Assert.Equal("blocks", ReportDesignRules.Problem(new ReportDesign { Blocks = blocks.Split(',') }));
    }

    [Fact]
    public void A_reordered_body_is_allowed()
    {
        Assert.Null(ReportDesignRules.Problem(new ReportDesign { Blocks = ["results", "signatures", "patient", "disclaimer", "pending"] }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"style\":\"neon\"}")]
    public void An_unreadable_or_invalid_stored_design_falls_back_to_classic(string json)
    {
        Assert.Equal("classic", ReportDesignRules.Read(json).Style);
    }
}

public sealed class PhoneRulesTests
{
    [Theory]
    [InlineData("03001234567", "923001234567")]
    [InlineData("0300-1234567", "923001234567")]
    [InlineData("0300 123 4567", "923001234567")]
    [InlineData("3001234567", "923001234567")]
    [InlineData("+92 300 1234567", "923001234567")]
    [InlineData("0092 300 1234567", "923001234567")]
    [InlineData("923001234567", "923001234567")]
    [InlineData("+44 7700 900123", "447700900123")]
    public void Numbers_become_international_digits(string input, string expected)
    {
        Assert.Equal(expected, PhoneRules.ToWhatsApp(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("0300-12ab567")]
    [InlineData("+0300 1234567")]
    [InlineData("1234567890123456")]
    public void Unusable_numbers_are_refused(string? input)
    {
        Assert.Null(PhoneRules.ToWhatsApp(input));
    }
}

public sealed class ReportArchitectureTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Lims.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }

    /// <summary>
    /// Issued reports bypass the branch query filter on purpose (a collection centre sees its patients' reports). Only
    /// ReportService may read them, and every read must be marked VISIBILITY-GATE (or be an Add of a new report).
    /// </summary>
    [Fact]
    public void Issued_reports_are_only_read_through_the_visibility_gate()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            var path = file.Replace('\\', '/');
            if (path.Contains("/Migrations/") || path.Contains("/Configurations/") || path.EndsWith("LimsDbContext.cs") || path.EndsWith("LimsDbContext.Save.cs")) continue;
            var inReports = path.Contains("Lims.Infrastructure/Reports/");
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!Regex.IsMatch(lines[i], @"\bdb\.LabReports\b|Set<LabReport>\(\)")) continue;
                if (!inReports) { offenders.Add($"{Path.GetFileName(file)}:{i + 1}: touches LabReports outside the Reports services"); continue; }
                if (!lines[i].Contains("VISIBILITY-GATE") && !lines[i].Contains(".Add(")) offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
            }
        }
        Assert.True(offenders.Count == 0, "Unmarked report access:\n" + string.Join("\n", offenders));
    }
}
