using System.Globalization;
using Lims.Domain.Catalog;
using Lims.Domain.Common;
using Lims.Domain.Orders;

namespace Lims.Domain.Results;

public enum ResultStatus
{
    /// <summary>Saved but not yet handed over; editable by people who can enter results.</summary>
    Draft = 0,
    /// <summary>Entered and waiting for technical verification.</summary>
    Submitted = 10,
    /// <summary>Verified and waiting for approval.</summary>
    Verified = 20,
    /// <summary>Released. Locked: only an amendment (a new version) can change it.</summary>
    Approved = 30,
    /// <summary>Replaced by an approved amendment. Kept for history.</summary>
    Superseded = 90,
}

public static class ResultSteps
{
    public const string Submit = "Submit";
    public const string Verify = "Verify";
    public const string Return = "Return";
    public const string Approve = "Approve";
    public const string Amend = "Amend";
}

/// <summary>
/// One version of the result for one ordered test. Not branch-filtered by the global query filter (a collection centre
/// must see results of its patients while another branch processed the sample), so visibility is applied explicitly
/// in ResultService through <see cref="OrderBranchId"/> and <see cref="LabBranchId"/>.
/// </summary>
public class TestResult : AuditableEntity<long>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public long OrderItemId { get; set; }
    public long OrderId { get; set; }
    public long SampleId { get; set; }
    public int PatientId { get; set; }
    /// <summary>Branch that registered the order.</summary>
    public int OrderBranchId { get; set; }
    /// <summary>Branch whose laboratory processed the sample.</summary>
    public int LabBranchId { get; set; }
    /// <summary>1 for the first release; an amendment is the next number.</summary>
    public int Version { get; set; } = 1;
    public ResultStatus Status { get; set; }
    /// <summary>The person who handed the result over for verification (separation of duties is checked against this).</summary>
    public int? EnteredByUserId { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public int? VerifiedByUserId { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    /// <summary>Why a released result was amended (versions after the first).</summary>
    public string? AmendmentReason { get; set; }
    public long? ReplacesResultId { get; set; }
    public string? Comment { get; set; }

    public ICollection<TestResultValue> Values { get; } = [];
}

/// <summary>
/// The value of one parameter, with everything needed to reproduce the report later: unit, decimal places, the range that
/// was applied (copied, because ranges are retired and replaced over time) and the flag. The stored number is never rounded.
/// </summary>
public class TestResultValue : Entity<long>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public long ResultId { get; set; }
    public int ParameterId { get; set; }
    public decimal? ValueNumeric { get; set; }
    public int? OptionId { get; set; }
    /// <summary>Text / narrative value, or the English text of the chosen option at the time of entry.</summary>
    public string? ValueText { get; set; }
    public string? ValueTextUr { get; set; }
    public string? UnitText { get; set; }
    public int DecimalPlaces { get; set; }
    public ResultFlag Flag { get; set; }
    public int? RangeId { get; set; }
    public decimal? RangeLow { get; set; }
    public decimal? RangeHigh { get; set; }
    public decimal? RangeCriticalLow { get; set; }
    public decimal? RangeCriticalHigh { get; set; }
    public string? RangeText { get; set; }
}

/// <summary>Append-only workflow history: who submitted, verified, returned, approved or amended, and why.</summary>
public class ResultApproval : Entity<long>, ITenantEntity
{
    public int OrganizationId { get; set; }
    public long ResultId { get; set; }
    public string Step { get; set; } = "";
    public int UserId { get; set; }
    public DateTime AtUtc { get; set; }
    public string? Comment { get; set; }
}

public enum AlertStatus { Open = 0, Notified = 1 }
public enum NotifyMethod { Phone = 1, InPerson = 2, Other = 3 }

/// <summary>A result beyond an approved critical limit that someone must communicate and confirm (read-back).</summary>
public class CriticalValueAlert : AuditableEntity<long>, ITenantEntity, IAudited
{
    public int OrganizationId { get; set; }
    public long ResultId { get; set; }
    public long OrderItemId { get; set; }
    public int PatientId { get; set; }
    public int LabBranchId { get; set; }
    public int ParameterId { get; set; }
    /// <summary>The value as shown to staff, e.g. "2.1".</summary>
    public string ValueText { get; set; } = "";
    public decimal? Limit { get; set; }
    public bool IsHigh { get; set; }
    public AlertStatus Status { get; set; }
    public string? NotifiedTo { get; set; }
    public NotifyMethod? Method { get; set; }
    public bool ReadBackConfirmed { get; set; }
    public int? NotifiedByUserId { get; set; }
    public DateTime? NotifiedAtUtc { get; set; }
    public string? Comment { get; set; }
}

public enum WorklistStatus { Open = 0, Closed = 1 }

/// <summary>A batch of tests for one department at one branch, printed as a bench sheet.</summary>
public class Worklist : AuditableEntity<long>, IBranchEntity, IAudited
{
    public int OrganizationId { get; set; }
    public int BranchId { get; set; }
    public string WorklistNo { get; set; } = "";
    public int DepartmentId { get; set; }
    public WorklistStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public int? ClosedByUserId { get; set; }

    public ICollection<WorklistItem> Items { get; } = [];
}

public class WorklistItem : ITenantEntity
{
    public int OrganizationId { get; set; }
    public long WorklistId { get; set; }
    public long OrderItemId { get; set; }
    /// <summary>Only open worklists hold items exclusively (enforced by a filtered unique index).</summary>
    public bool IsOpen { get; set; } = true;
}

/// <summary>The only place that decides which result moves are allowed, and who may perform them.</summary>
public static class ResultRules
{
    private static readonly Dictionary<ResultStatus, ResultStatus[]> Allowed = new()
    {
        [ResultStatus.Draft] = [ResultStatus.Submitted],
        [ResultStatus.Submitted] = [ResultStatus.Verified, ResultStatus.Draft],
        [ResultStatus.Verified] = [ResultStatus.Approved, ResultStatus.Draft],
        [ResultStatus.Approved] = [ResultStatus.Superseded],
        [ResultStatus.Superseded] = [],
    };

    public static bool CanMove(ResultStatus from, ResultStatus to) => Allowed[from].Contains(to);

    /// <summary>Values may only be changed while the result is a draft.</summary>
    public static bool IsEditable(ResultStatus s) => s == ResultStatus.Draft;

    /// <summary>A released result that clinicians may rely on.</summary>
    public static bool IsReleased(ResultStatus s) => s is ResultStatus.Approved;

    /// <summary>Status of the ordered test while its current result is in the given state.</summary>
    public static OrderItemStatus ItemStatusFor(ResultStatus s) => s == ResultStatus.Approved ? OrderItemStatus.Completed : OrderItemStatus.InProgress;

    /// <summary>
    /// Separation of duties: the person who handed a result over cannot verify or approve it, unless the laboratory
    /// has explicitly relaxed this (small laboratories with a single technologist).
    /// </summary>
    public static bool MayReview(int reviewerId, int? enteredById, bool allowSelf) => allowSelf || enteredById is null || enteredById != reviewerId;

    /// <summary>Critical flags always need a recorded notification; abnormal and ordinary flags do not.</summary>
    public static bool IsCritical(ResultFlag f) => f is ResultFlag.CriticalLow or ResultFlag.CriticalHigh;
}

/// <summary>Turns what a person typed into a number, rejecting anything ambiguous instead of guessing.</summary>
public static class ResultValueParser
{
    public const int MaxDecimals = 6;
    public const decimal MaxAbs = 999_999_999_999.999999m;   // the largest value decimal(18,6) holds

    public enum ParseError { None, Empty, NotANumber, TooManyDecimals, OutOfRange }

    /// <summary>
    /// Accepts an optional sign and digits with one decimal point. Urdu/Arabic-Indic digits and the Arabic decimal
    /// separator typed on an Urdu keyboard are converted. No thousands separators, exponents or percent signs. More than
    /// six decimals is refused rather than rounded (stored values are never rounded).
    /// </summary>
    public static ParseError TryParseNumeric(string? input, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input)) return ParseError.Empty;
        var s = NormaliseDigits(input.Trim());
        var i = 0;
        if (s[0] is '+' or '-') i = 1;
        if (i >= s.Length) return ParseError.NotANumber;
        var dot = -1;
        for (var k = i; k < s.Length; k++)
        {
            if (s[k] == '.') { if (dot >= 0) return ParseError.NotANumber; dot = k; }
            else if (s[k] is < '0' or > '9') return ParseError.NotANumber;
        }
        if (s.Length - i == 1 && dot == i) return ParseError.NotANumber;          // "." alone
        if (dot >= 0 && s.Length - dot - 1 > MaxDecimals) return ParseError.TooManyDecimals;
        if (!decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value))
            return ParseError.OutOfRange;
        if (Math.Abs(value) > MaxAbs) { value = 0; return ParseError.OutOfRange; }
        return ParseError.None;
    }

    public static string NormaliseDigits(string s)
    {
        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c is >= '٠' and <= '٩') chars[i] = (char)('0' + (c - '٠'));      // Arabic-Indic
            else if (c is >= '۰' and <= '۹') chars[i] = (char)('0' + (c - '۰')); // Extended Arabic-Indic (Urdu)
            else if (c is '٫' or '٬') chars[i] = c == '٫' ? '.' : ',';          // decimal / thousands separators
            else if (c == '−') chars[i] = '-';                                              // unicode minus
        }
        return new string(chars);
    }

    /// <summary>Number as typed on reports: rounded to the parameter's decimal places for display only.</summary>
    public static string Format(decimal value, int decimalPlaces) =>
        Math.Round(value, Math.Clamp(decimalPlaces, 0, MaxDecimals), MidpointRounding.AwayFromZero).ToString("F" + Math.Clamp(decimalPlaces, 0, MaxDecimals), CultureInfo.InvariantCulture);
}
