namespace Lims.Application.Barcodes;

/// <summary>
/// Code 128 (subset B) encoder. Returns the bar pattern as a string of modules: '1' = bar, '0' = space.
/// The server computes it so the browser only has to draw rectangles (no barcode library, fully offline).
/// Subset B covers printable ASCII (32–126), which is enough for sample numbers like "S-261109-0001".
/// </summary>
public static class Code128
{
    // Bar/space widths for symbol values 0..106 (bar, space, bar, space, bar, space; 11 modules each).
    // 103–105 are Start A/B/C; 106 is Stop and has a 7th element (13 modules).
    private static readonly string[] Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112",
    ];

    private const int StartB = 104;
    private const int Stop = 106;

    public static IReadOnlyList<string> SymbolPatterns => Patterns;

    public static bool CanEncode(string? value) =>
        !string.IsNullOrEmpty(value) && value.All(c => c is >= ' ' and <= '~');

    /// <summary>Symbol values for a text: start B, each character (ASCII − 32), checksum.</summary>
    public static int[] Symbols(string value)
    {
        if (!CanEncode(value)) throw new ArgumentException("Only printable ASCII characters can be encoded.", nameof(value));
        var symbols = new List<int> { StartB };
        symbols.AddRange(value.Select(c => c - 32));
        var checksum = StartB;
        for (var i = 1; i < symbols.Count; i++) checksum += symbols[i] * i;
        symbols.Add(checksum % 103);
        return [.. symbols];
    }

    /// <summary>The modules ('1' bar / '0' space) of the full symbol including the stop pattern.</summary>
    public static string Encode(string value)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var s in Symbols(value)) AppendPattern(sb, Patterns[s]);
        AppendPattern(sb, Patterns[Stop]);
        return sb.ToString();
    }

    private static void AppendPattern(System.Text.StringBuilder sb, string widths)
    {
        var bar = true;
        foreach (var w in widths)
        {
            sb.Append(bar ? '1' : '0', w - '0');
            bar = !bar;
        }
    }

    /// <summary>Normalises what a scanner or a person typed: trims, drops control characters, upper-cases.</summary>
    public static string NormalizeScan(string? scanned) =>
        new string((scanned ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim().ToUpperInvariant();
}
