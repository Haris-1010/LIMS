using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lims.Application.Reports;

/// <summary>
/// How a report is drawn: style, colours, font size, page margins and the order of its parts. Presentation only — a design can
/// never remove what a report must show (results, pending tests, signatories, report number, fingerprint, interim/amended marks).
/// </summary>
public sealed record ReportDesign
{
    /// <summary>Starting look: classic (black and white), modern, compact or colourful.</summary>
    public string Style { get; init; } = "classic";
    /// <summary>Colour of headings and lines, as #rrggbb.</summary>
    public string AccentColor { get; init; } = "#1f4e79";
    /// <summary>Body text size in points.</summary>
    public decimal FontSizePt { get; init; } = 9.5m;
    public int MarginTopMm { get; init; } = 12;
    public int MarginBottomMm { get; init; } = 14;
    public int MarginSideMm { get; init; } = 12;
    /// <summary>False for pre-printed letterhead paper: the letterhead is not drawn and the top margin is left blank.</summary>
    public bool ShowLetterhead { get; init; } = true;
    /// <summary>start, center, end or none.</summary>
    public string LogoPosition { get; init; } = "start";
    /// <summary>With the letterhead on: print the uploaded header image across the top instead of the logo, name and address.</summary>
    public bool UseHeaderImage { get; init; }
    /// <summary>Print the uploaded footer image across the bottom of the page.</summary>
    public bool ShowFooterImage { get; init; }
    /// <summary>Small Low / Normal / High bar beside numeric results that have a range.</summary>
    public bool ShowRangeBars { get; init; } = true;
    /// <summary>Draw a larger current-result graph for tests containing exactly one numeric parameter.</summary>
    public bool ShowSingleParameterCharts { get; init; } = true;
    /// <summary>Flag words (High, Low…) printed under the flag code.</summary>
    public bool ShowFlagWords { get; init; } = true;
    /// <summary>Colour abnormal results (red high, blue low). Off keeps the report readable on black-and-white copies only.</summary>
    public bool ColourFlags { get; init; } = false;
    /// <summary>lines, zebra or boxed.</summary>
    public string TableStyle { get; init; } = "lines";
    /// <summary>Order of the report body, top to bottom. Must contain every block exactly once.</summary>
    public string[] Blocks { get; init; } = [.. ReportDesignRules.AllBlocks];
}

public static class ReportDesignRules
{
    public static readonly string[] Styles = ["classic", "modern", "compact", "colourful"];
    public static readonly string[] LogoPositions = ["start", "center", "end", "none"];
    public static readonly string[] TableStyles = ["lines", "zebra", "boxed"];
    public static readonly string[] AllBlocks = ["patient", "results", "pending", "signatures", "disclaimer"];

    private static readonly Regex Hex = new("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Null when the design is acceptable, otherwise the name of the first setting that is not.</summary>
    public static string? Problem(ReportDesign d)
    {
        if (!Styles.Contains(d.Style)) return "style";
        if (d.AccentColor is null || !Hex.IsMatch(d.AccentColor)) return "accentColor";
        if (d.FontSizePt < 7.5m || d.FontSizePt > 12m) return "fontSizePt";
        if (d.MarginTopMm is < 5 or > 80) return "marginTopMm";
        if (d.MarginBottomMm is < 5 or > 60) return "marginBottomMm";
        if (d.MarginSideMm is < 5 or > 30) return "marginSideMm";
        if (!LogoPositions.Contains(d.LogoPosition)) return "logoPosition";
        if (!TableStyles.Contains(d.TableStyle)) return "tableStyle";
        if (d.Blocks is null || d.Blocks.Length != AllBlocks.Length || d.Blocks.Distinct().Count() != AllBlocks.Length || d.Blocks.Any(b => !AllBlocks.Contains(b)))
            return "blocks";
        return null;
    }

    public static string Serialize(ReportDesign d) => JsonSerializer.Serialize(d, Json);

    /// <summary>A stored design; anything unreadable or no longer valid falls back to the built-in classic design.</summary>
    public static ReportDesign Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ReportDesign();
        try
        {
            var d = JsonSerializer.Deserialize<ReportDesign>(json, Json);
            return d is not null && Problem(d) is null ? d : new ReportDesign();
        }
        catch (JsonException)
        {
            return new ReportDesign();
        }
    }
}

public sealed record ReportLayoutDto(int Id, string Name, bool IsDefault, ReportDesign Design, string RowVersion);

public record ReportLayoutInput
{
    [Required, MaxLength(100)] public string Name { get; init; } = "";
    public bool IsDefault { get; init; }
    [Required] public ReportDesign Design { get; init; } = new();
}

public sealed record ReportLayoutUpdate : ReportLayoutInput
{
    [Required] public string RowVersion { get; init; } = "";
}

/// <summary>Sent when a report has been printed (or saved as PDF): which design was used and, for a partial print, which tests.</summary>
public sealed record PrintedRequest
{
    public int? LayoutId { get; init; }
    /// <summary>Null or empty = every test of the report.</summary>
    [MaxLength(200)] public long[]? OrderItemIds { get; init; }
    /// <summary>
    /// Language of this copy; null = the language the report was issued in. The snapshot holds every name in English and Urdu,
    /// so an Urdu report can be printed in English (and the reverse) without changing what was issued.
    /// </summary>
    public Lims.Domain.Reports.ReportLanguage? Language { get; init; }
}

public sealed record ShareRequest
{
    /// <summary>Only "whatsapp" for now.</summary>
    [Required, MaxLength(20)] public string Channel { get; init; } = "";
    [Required, MaxLength(30)] public string Phone { get; init; } = "";
}

public sealed record ShareResult(string Phone);

public static class PhoneRules
{
    /// <summary>
    /// The number in the international digits-only form WhatsApp links use (e.g. 923001234567), or null when it is not a usable
    /// mobile number. Local Pakistani numbers (0300 1234567, 300 1234567) get the 92 country code; +, 00, spaces, dashes and
    /// brackets are removed.
    /// </summary>
    public static string? ToWhatsApp(string? input, string defaultCountryCode = "92")
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var s = input.Trim();
        var plus = s.StartsWith('+');
        var digits = new string(s.Where(char.IsAsciiDigit).ToArray());
        if (s.Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '-' or '(' or ')' or '+' or '.'))) return null;
        if (!plus && digits.StartsWith("00")) digits = digits[2..];
        else if (!plus && digits.StartsWith('0') && digits.Length == 11) digits = defaultCountryCode + digits[1..];
        else if (!plus && digits.Length == 10 && digits.StartsWith('3')) digits = defaultCountryCode + digits;
        return digits.Length is >= 10 and <= 15 && digits[0] != '0' ? digits : null;
    }
}
