using Lims.Application.Common;

namespace Lims.Application.Settings;

public sealed record SettingDefinition(string Key, string Group, SettingKind Kind, string Default, int? Min, int? Max, string DescriptionEn, string DescriptionUr);

/// <summary>The business settings the system actually acts on, each with a fixed kind and limits.</summary>
public static class SettingsCatalog
{

    /// <summary>
    /// Every setting here is read by the code today. The defaults match the defaults used where the setting is read.
    /// The two result settings default to a one-person laboratory (owner, 2026-11-12): self-verification on, critical
    /// read-back not blocking approval. A laboratory with separate reviewers switches them on the settings page.
    /// </summary>
    public static readonly IReadOnlyList<SettingDefinition> Known =
    [
        new SettingDefinition("Samples.RequirePaymentBeforeCollection", "Samples", SettingKind.Boolean, "false", null, null,
            "A sample cannot be collected until its order is fully paid.", "آرڈر کی مکمل ادائیگی تک نمونہ جمع نہیں کیا جا سکتا۔"),
        new SettingDefinition("Results.AllowSelfVerification", "Results", SettingKind.Boolean, "true", null, null,
            "Let the person who entered a result also verify or approve it (for one-person laboratories).", "جس نے نتیجہ درج کیا وہی اسے تصدیق یا منظور بھی کر سکے (ایک فرد کی لیبارٹری کے لیے)۔"),
        new SettingDefinition("Results.BlockApprovalUntilCriticalNotified", "Results", SettingKind.Boolean, "false", null, null,
            "A result with a critical value cannot be approved until who was told, and the read-back, is recorded.", "نازک قدر والا نتیجہ اس وقت تک منظور نہیں ہو سکتا جب تک بتانے والے کا نام اور دہرائی درج نہ ہو۔"),
        new SettingDefinition("Inventory.AllowNegativeStock", "Inventory", SettingKind.Boolean, "false", null, null,
            "When a test needs more reagent than is in stock, take the difference into a negative balance instead of recording a shortfall.", "ٹیسٹ کو اسٹاک سے زیادہ ری ایجنٹ درکار ہو تو کمی درج کرنے کے بجائے فرق منفی بیلنس میں لیا جائے۔"),
        new SettingDefinition("Inventory.ExpiryAlertDays", "Inventory", SettingKind.Integer, "30", 1, 365,
            "Warn about stock batches that expire within this many days.", "جن بیچز کی میعاد اتنے دنوں میں ختم ہو ان کی وارننگ دی جائے۔"),
    ];


    public static string Normalise(SettingDefinition d, string raw)
    {
        var v = raw.Trim();
        if (d.Kind == SettingKind.Boolean)
        {
            if (v.Equals("true", StringComparison.OrdinalIgnoreCase)) return "true";
            if (v.Equals("false", StringComparison.OrdinalIgnoreCase)) return "false";
            throw new BusinessRuleException("setting_value_invalid", "Choose yes or no.");
        }
        if (!int.TryParse(v, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) || n < (d.Min ?? int.MinValue) || n > (d.Max ?? int.MaxValue))
            throw new BusinessRuleException("setting_value_invalid", $"Enter a whole number from {d.Min} to {d.Max}.", 400, new { min = d.Min, max = d.Max });
        return n.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
