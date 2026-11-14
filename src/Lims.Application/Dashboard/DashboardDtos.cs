namespace Lims.Application.Dashboard;

/// <summary>
/// The selected period: today, yesterday, d7/d30 (including today), this_month, last_month, all or custom (with From/To).
/// </summary>
public sealed record DashboardQuery
{
    public string Range { get; init; } = "today";
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

/// <summary>
/// Which parts of the dashboard the caller may see. A part the caller has no permission for is empty (not zero), and the page hides it.
/// </summary>
public sealed record DashboardSections(bool Patients, bool Orders, bool Samples, bool Results, bool Reports, bool Money, bool Corporate);

public sealed record DashboardStats(
    int TotalPatients,
    int Registrations, double RegistrationsDelta,
    int Orders, double OrdersDelta,
    int PendingSamples, int OverdueSamples,
    int TestsInProgress, int Departments,
    int AwaitingApproval, int OldestWaitingMinutes,
    int CompletedReports, int ReadyToPrint,
    decimal CashCollected, double CashDelta,
    decimal CorporateOutstanding, int CorporateClients);

public sealed record TrendPoint(string Date, decimal Cash, decimal Credit);
public sealed record PipelineItem(string Key, int Count);

/// <summary>A result waiting for approval. Flag is critical, abnormal or normal.</summary>
public sealed record ApprovalRow(long ResultId, string OrderNo, string Patient, string? PatientUr, string PatientNo, string Test, string? TestUr, string Department, string? DepartmentUr, int WaitingMinutes, string Flag);

/// <summary>One recent event. Type is registration, payment, approval, rejection or critical; Params fill the sentence.</summary>
public sealed record ActivityItem(string Id, string Type, int MinutesAgo, Dictionary<string, string> Params);

public sealed record DashboardSummary(
    string Range, DateTime GeneratedAtUtc, int? BranchId, DashboardSections Sections, DashboardStats Stats,
    IReadOnlyList<TrendPoint> Trend, IReadOnlyList<PipelineItem> Pipeline, IReadOnlyList<ApprovalRow> Approvals, IReadOnlyList<ActivityItem> Activity);
