using System.Globalization;
using Lims.Domain.Common;

namespace Lims.Domain.Core;

public enum SequenceResetPolicy
{
    Never,
    Yearly,
    Monthly,
    Daily,
}

/// <summary>Well-known sequence types. Stored as text so new types need no schema change.</summary>
public static class SequenceTypes
{
    public const string Patient = "Patient";
    public const string Order = "Order";
    public const string Invoice = "Invoice";
    public const string Receipt = "Receipt";
    public const string Sample = "Sample";
    public const string CorporateInvoice = "CorporateInvoice";
    public const string PurchaseReceipt = "PurchaseReceipt";
    public const string Refund = "Refund";
    public const string Transfer = "Transfer";

    /// <summary>
    /// Default numbering for each type. New organisations get all of them; a type added in a later
    /// release is created with these defaults the first time it is used.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (string Prefix, int Padding, SequenceResetPolicy Reset)> Defaults =
        new Dictionary<string, (string, int, SequenceResetPolicy)>
        {
            [Patient] = ("P", 6, SequenceResetPolicy.Never),
            [Order] = ("ORD", 5, SequenceResetPolicy.Monthly),
            [Invoice] = ("INV", 5, SequenceResetPolicy.Monthly),
            [Receipt] = ("RCP", 5, SequenceResetPolicy.Monthly),
            [Refund] = ("RFD", 4, SequenceResetPolicy.Monthly),
            [Sample] = ("S", 4, SequenceResetPolicy.Daily),
            [Transfer] = ("TRF", 4, SequenceResetPolicy.Monthly),
            [CorporateInvoice] = ("CINV", 4, SequenceResetPolicy.Yearly),
            [PurchaseReceipt] = ("GRN", 4, SequenceResetPolicy.Yearly),
        };

    public static readonly string[] All = [.. Defaults.Keys];
}

/// <summary>
/// Human-readable document number generator (e.g. INV-2611-000042).
/// Numbers are issued by an atomic UPDATE in the database, never computed client-side.
/// </summary>
public class NumberSequence : AuditableEntity<int>, ITenantEntity
{
    public int OrganizationId { get; set; }
    /// <summary>Null = one sequence for the whole organisation.</summary>
    public int? BranchId { get; set; }
    public string SequenceType { get; set; } = "";
    public string Prefix { get; set; } = "";
    public int Padding { get; set; } = 6;
    public SequenceResetPolicy ResetPolicy { get; set; }
    /// <summary>Period the current counter belongs to, e.g. "2611" for November 2026.</summary>
    public string CurrentPeriod { get; set; } = "";
    public long NextValue { get; set; } = 1;

    /// <summary>Period key for a moment in the laboratory's local time.</summary>
    public static string PeriodFor(SequenceResetPolicy policy, DateTime local) => policy switch
    {
        SequenceResetPolicy.Yearly => local.ToString("yy", CultureInfo.InvariantCulture),
        SequenceResetPolicy.Monthly => local.ToString("yyMM", CultureInfo.InvariantCulture),
        SequenceResetPolicy.Daily => local.ToString("yyMMdd", CultureInfo.InvariantCulture),
        _ => "",
    };

    /// <summary>Formats an issued value: PREFIX-[period-]000123.</summary>
    public static string Format(string prefix, string period, long value, int padding)
    {
        var number = value.ToString(CultureInfo.InvariantCulture).PadLeft(padding, '0');
        var parts = new[] { prefix, period, number }.Where(p => p.Length > 0);
        return string.Join('-', parts);
    }
}
