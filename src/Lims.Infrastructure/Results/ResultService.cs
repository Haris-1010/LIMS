using Lims.Application.Abstractions;
using Lims.Application.Barcodes;
using Lims.Application.Common;
using Lims.Application.Results;
using Lims.Domain.Catalog;
using Lims.Domain.Core;
using Lims.Domain.Orders;
using Lims.Domain.Patients;
using Lims.Domain.Results;
using Lims.Domain.Security;
using Lims.Domain.Samples;
using Lims.Infrastructure.Persistence;
using Lims.Infrastructure.Samples;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lims.Infrastructure.Results;

/// <summary>
/// Result entry and the release workflow. Results are not branch-filtered by the global query filter: a collection centre must
/// see results of its patients although another branch processed the sample. Visibility is applied in one place, <see cref="Visible"/>,
/// and the architecture tests allow `db.TestResults` only on lines marked VISIBILITY-GATE (or when adding a new result).
/// Reading the sample goes through <see cref="SampleService"/> so its own visibility rule applies too.
/// </summary>
public sealed class ResultService(LimsDbContext db, SampleService samples, ILabClock clock, Inventory.InventoryService inventory, ILogger<ResultService> log)
{
    public const string SettingAllowSelfVerification = "Results.AllowSelfVerification";
    public const string SettingBlockApproval = "Results.BlockApprovalUntilCriticalNotified";
    // Defaults when the organisation has not saved the setting; they must match SettingsCatalog.
    public const bool DefaultAllowSelfVerification = true;
    public const bool DefaultBlockApproval = false;

    private int OrgId => db.Request.OrganizationId ?? throw new TenantViolationException("No organisation in the request context.");
    private int UserId => db.Request.UserId ?? throw new BusinessRuleException("forbidden", "Sign in required.", 403);
    private int ActiveBranch => db.Request.ActiveBranchId
        ?? throw new BusinessRuleException("branch_required", "Select the branch you are working at (top bar) first.");

    // ================= Visibility =================

    private IQueryable<TestResult> Visible()
    {
        var ctx = db.Request;
        var all = db.TestResults.AsQueryable(); // VISIBILITY-GATE
        if (ctx.IsSystem || ctx.AllBranches) return all;
        var ids = ctx.BranchIds;
        return all.Where(r => ids.Contains(r.OrderBranchId) || ids.Contains(r.LabBranchId));
    }

    private async Task<bool> SettingAsync(string key, bool fallback, CancellationToken ct)
    {
        var v = await db.BusinessSettings.AsNoTracking().Where(x => x.Key == key && x.BranchId == null).Select(x => x.Value).FirstOrDefaultAsync(ct);
        return v is null ? fallback : string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
    }

    // ================= Pending (worklist queue) =================

    public async Task<PagedResult<PendingItem>> PendingAsync(PendingQuery q, CancellationToken ct)
    {
        var branch = ActiveBranch;
        var orgId = OrgId;
        var query =
            from st in db.SampleTests.AsNoTracking()
            join s in samples.VisibleReadOnly() on st.SampleId equals s.Id
            join i in db.OrderItems.AsNoTracking() on st.OrderItemId equals i.Id
            where s.Status == SampleStatus.Received && s.CurrentBranchId == branch
                  && (i.Status == OrderItemStatus.Received || i.Status == OrderItemStatus.InProgress)
                  // Submitted and verified results wait in the verification queues, not here.
                  && !db.TestResults.Any(r => r.OrderItemId == i.Id && (r.Status == ResultStatus.Submitted || r.Status == ResultStatus.Verified)) // VISIBILITY-GATE (item already authorised through its sample)
            select new { Sample = s, Item = i };

        if (q.DepartmentId is int dep)
        {
            var testIds = await db.Tests.IgnoreQueryFilters().Where(t => t.OrganizationId == orgId && t.DepartmentId == dep).Select(t => t.Id).ToListAsync(ct);
            query = query.Where(x => testIds.Contains(x.Item.TestId));
        }
        if (q.UrgentOnly) query = query.Where(x => x.Sample.Priority == OrderPriority.Urgent);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            var norm = Code128.NormalizeScan(term);
            var patientIds = await db.Patients.AsNoTracking()
                .Where(p => p.PatientNo == norm || p.FullName.Contains(term) || (p.FullNameUr != null && p.FullNameUr.Contains(term)))
                .Select(p => p.Id).Take(500).ToListAsync(ct);
            var orderIds = await db.Orders.IgnoreQueryFilters().Where(o => o.OrganizationId == orgId && o.OrderNo == norm).Select(o => o.Id).ToListAsync(ct);
            query = query.Where(x => x.Sample.Barcode == norm || patientIds.Contains(x.Sample.PatientId) || orderIds.Contains(x.Sample.OrderId) || x.Item.TestCode.Contains(term));
        }

        var ordered = query.OrderByDescending(x => x.Sample.Priority).ThenBy(x => x.Sample.ReceivedAtUtc).ThenBy(x => x.Item.Id);
        List<(Sample Sample, LabOrderItem Item)> rows;
        int total;
        if (q.OverdueOnly)
        {
            // Overdue depends on the test's turnaround time, which lives in another table: filter after loading a bounded set.
            var all = (await ordered.Take(1000).ToListAsync(ct)).Select(x => (x.Sample, x.Item)).ToList();
            var tat = await TurnaroundAsync(all.Select(x => x.Item.TestId).Distinct().ToList(), ct);
            var now = clock.UtcNow;
            all = all.Where(x => tat.TryGetValue(x.Item.TestId, out var h) && h > 0 && x.Sample.ReceivedAtUtc!.Value.AddHours(h) < now).ToList();
            total = all.Count;
            rows = all.Skip(q.Skip).Take(q.PageSize).ToList();
        }
        else
        {
            total = await query.CountAsync(ct);
            rows = (await ordered.Skip(q.Skip).Take(q.PageSize).ToListAsync(ct)).Select(x => (x.Sample, x.Item)).ToList();
        }
        return new PagedResult<PendingItem>(await HydratePendingAsync(rows, ct), total, q.Page, q.PageSize);
    }

    private async Task<Dictionary<int, int>> TurnaroundAsync(List<int> testIds, CancellationToken ct)
    {
        var orgId = OrgId;
        return await db.Tests.IgnoreQueryFilters().AsNoTracking().Where(t => t.OrganizationId == orgId && testIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.TurnaroundHours, ct);
    }

    private async Task<List<PendingItem>> HydratePendingAsync(List<(Sample Sample, LabOrderItem Item)> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var orgId = OrgId;
        var testIds = rows.Select(r => r.Item.TestId).Distinct().ToList();
        var tests = await db.Tests.IgnoreQueryFilters().AsNoTracking().Where(t => t.OrganizationId == orgId && testIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        var depIds = tests.Values.Select(t => t.DepartmentId).Distinct().ToList();
        var deps = await db.Departments.AsNoTracking().Where(d => depIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        var patientIds = rows.Select(r => r.Sample.PatientId).Distinct().ToList();
        var patients = await db.Patients.AsNoTracking().Where(p => patientIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var orderIds = rows.Select(r => r.Sample.OrderId).Distinct().ToList();
        var orders = await db.Orders.IgnoreQueryFilters().AsNoTracking().Where(o => o.OrganizationId == orgId && orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OrderNo, ct);
        var itemIds = rows.Select(r => r.Item.Id).ToList();
        var results = await db.TestResults.AsNoTracking().Where(r => itemIds.Contains(r.OrderItemId) && r.Status < ResultStatus.Approved) // VISIBILITY-GATE (items already authorised through their samples)
            .Select(r => new { r.OrderItemId, r.Id, r.Status }).ToListAsync(ct);
        var open = await db.WorklistItems.AsNoTracking().Where(w => w.IsOpen && itemIds.Contains(w.OrderItemId)).Select(w => new { w.OrderItemId, w.WorklistId }).ToListAsync(ct);
        var wlIds = open.Select(o => o.WorklistId).Distinct().ToList();
        var wlNos = await db.Worklists.AsNoTracking().Where(w => wlIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.WorklistNo, ct);
        var now = clock.UtcNow;

        return rows.Select(r =>
        {
            tests.TryGetValue(r.Item.TestId, out var t);
            var d = t is null ? null : deps.GetValueOrDefault(t.DepartmentId);
            var p = patients.GetValueOrDefault(r.Sample.PatientId);
            var res = results.FirstOrDefault(x => x.OrderItemId == r.Item.Id);
            var wl = open.FirstOrDefault(x => x.OrderItemId == r.Item.Id);
            var received = r.Sample.ReceivedAtUtc ?? r.Sample.CreatedAtUtc;
            var tat = t?.TurnaroundHours ?? 0;
            DateTime? due = tat > 0 ? received.AddHours(tat) : null;
            return new PendingItem(
                r.Item.Id, r.Sample.OrderId, orders.GetValueOrDefault(r.Sample.OrderId) ?? "", r.Sample.PatientId, p?.PatientNo ?? "", p?.FullName ?? "", p?.FullNameUr, p?.Sex ?? Sex.Unknown, p?.DateOfBirth,
                r.Item.TestId, r.Item.TestCode, r.Item.TestNameEn, r.Item.TestNameUr, t?.DepartmentId ?? 0, d?.NameEn ?? "", d?.NameUr,
                r.Sample.Id, r.Sample.Barcode, received, r.Sample.Priority, tat, due, due is DateTime dd && dd < now,
                res?.Id, res?.Status, wl is null ? null : wlNos.GetValueOrDefault(wl.WorklistId));
        }).ToList();
    }

    // ================= Entry model =================

    private sealed record Ctx(LabOrderItem Item, LabOrder Order, Sample Sample, Patient Patient, LabTest Test);

    /// <summary>The ordered test with its live sample. 404 when the user may not see the sample (or there is none).</summary>
    private async Task<Ctx> LoadAsync(long orderItemId, bool track, CancellationToken ct)
    {
        var orgId = OrgId;
        var itemQuery = track ? db.OrderItems.AsQueryable() : db.OrderItems.AsNoTracking();
        var item = await itemQuery.SingleOrDefaultAsync(i => i.Id == orderItemId, ct) ?? throw BusinessRuleException.NotFound("Test");
        var sampleIds = await db.SampleTests.Where(st => st.OrderItemId == orderItemId).Select(st => st.SampleId).ToListAsync(ct);
        var sample = await samples.VisibleReadOnly().Where(s => sampleIds.Contains(s.Id) && s.Status != SampleStatus.Rejected && s.Status != SampleStatus.Cancelled)
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct) ?? throw BusinessRuleException.NotFound("Sample");
        var order = await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.OrganizationId == orgId && o.Id == item.OrderId, ct);
        var patient = await db.Patients.AsNoTracking().SingleAsync(p => p.Id == order.PatientId, ct);
        var test = await db.Tests.IgnoreQueryFilters().AsNoTracking().SingleAsync(t => t.OrganizationId == orgId && t.Id == item.TestId, ct);
        return new Ctx(item, order, sample, patient, test);
    }

    private int? AgeDays(Ctx c)
    {
        if (c.Patient.DateOfBirth is not DateOnly dob) return null;
        var on = DateOnly.FromDateTime(clock.ToLocal(c.Sample.CollectedAtUtc ?? c.Sample.ReceivedAtUtc ?? c.Sample.CreatedAtUtc));
        if (on < dob) return null;
        return PatientAge.Between(dob, on).TotalDays(dob, on);
    }

    private static RangeDto? ToDto(ReferenceRange? r) => r is null ? null : new RangeDto(r.Id, r.Low, r.High, r.CriticalLow, r.CriticalHigh, r.DisplayText);

    private async Task<List<TestParameter>> ParametersAsync(int testId, CancellationToken ct) =>
        await db.TestParameters.AsNoTracking().Include(p => p.Options).Include(p => p.Ranges.Where(r => r.Status == RangeStatus.Approved))
            .Where(p => p.TestId == testId && p.IsActive).OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync(ct);

    public async Task<EntryModel> GetEntryAsync(long orderItemId, CancellationToken ct)
    {
        var c = await LoadAsync(orderItemId, false, ct);
        var ps = await ParametersAsync(c.Test.Id, ct);
        var units = await UnitsAsync(ps, ct);
        var ageDays = AgeDays(c);
        var branches = await db.Branches.AsNoTracking().Where(b => b.Id == c.Sample.CurrentBranchId).Select(b => b.NameEn).FirstOrDefaultAsync(ct) ?? "";

        var versions = await Visible().AsNoTracking().Include(r => r.Values).Where(r => r.OrderItemId == orderItemId).OrderByDescending(r => r.Version).ToListAsync(ct); // VISIBILITY-GATE
        var current = versions.FirstOrDefault(r => r.Status < ResultStatus.Approved);
        var released = versions.FirstOrDefault(r => r.Status == ResultStatus.Approved);
        var shown = current ?? released;

        var userIds = versions.SelectMany(r => new int?[] { r.EnteredByUserId, r.VerifiedByUserId, r.ApprovedByUserId }).OfType<int>().ToList();
        var steps = shown is null ? [] : await db.ResultApprovals.AsNoTracking().Where(a => a.ResultId == shown.Id).OrderBy(a => a.Id).ToListAsync(ct);
        userIds.AddRange(steps.Select(s => s.UserId));
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameEn, ct);
        string? Name(int? id) => id is int i ? users.GetValueOrDefault(i) : null;

        var entryParams = ps.Select(p => new ParameterEntry(
            p.Id, p.Code, p.NameEn, p.NameUr, p.ResultType, p.UnitId is int u ? units.GetValueOrDefault(u) : null, p.DecimalPlaces, p.GroupName, p.SortOrder,
            p.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Id).Select(o => new OptionDto(o.Id, o.ValueEn, o.ValueUr, o.IsAbnormal)).ToList(),
            ToDto(ReferenceRules.Select(p.Ranges, c.Patient.Sex, ageDays)))).ToList();

        var alerts = shown is null ? [] : await AlertsForAsync(shown.Id, c, ps, units, ct);
        var settingSelf = await SettingAsync(SettingAllowSelfVerification, DefaultAllowSelfVerification, ct);
        return new EntryModel(
            c.Item.Id, c.Order.Id, c.Order.OrderNo, c.Order.Priority, c.Item.Status,
            c.Patient.Id, c.Patient.PatientNo, c.Patient.FullName, c.Patient.FullNameUr, c.Patient.Sex, c.Patient.DateOfBirth, ageDays,
            c.Test.Id, c.Test.Code, c.Test.NameEn, c.Test.NameUr, c.Test.ReportNotes,
            c.Sample.Id, c.Sample.Barcode, c.Sample.CollectedAtUtc, c.Sample.ReceivedAtUtc ?? c.Sample.CreatedAtUtc, branches,
            entryParams, shown is null ? null : Summary(shown, Name), shown is null ? [] : ValuesOf(shown, ps),
            released is null ? null : Summary(released, Name),
            steps.Select(s => new ApprovalStepDto(s.Step, s.AtUtc, Name(s.UserId), s.Comment)).ToList(), alerts,
            ActionsFor(c, current, released, settingSelf, steps.Any(s => s.Step != ResultSteps.Amend) || (current?.Version == 1 && steps.Count > 0)));
    }

    private static ResultSummary Summary(TestResult r, Func<int?, string?> name) => new(
        r.Id, r.Version, r.Status, RowVersion.ToToken(r.RowVersion), r.Comment, name(r.EnteredByUserId), r.SubmittedAtUtc,
        name(r.VerifiedByUserId), r.VerifiedAtUtc, name(r.ApprovedByUserId), r.ApprovedAtUtc, r.AmendmentReason, r.ReplacesResultId);

    private async Task<Dictionary<int, string>> UnitsAsync(IEnumerable<TestParameter> ps, CancellationToken ct)
    {
        var ids = ps.Select(p => p.UnitId).OfType<int>().Distinct().ToList();
        return await db.Units.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Code, ct);
    }

    private static List<ValueDto> ValuesOf(TestResult r, List<TestParameter> ps) => r.Values.OrderBy(v => ps.FindIndex(p => p.Id == v.ParameterId)).Select(v =>
    {
        var places = v.DecimalPlaces;
        var display = v.ValueNumeric is decimal n ? ResultValueParser.Format(n, places) : v.ValueText;
        RangeDto? range = v.RangeId is int rid ? new RangeDto(rid, v.RangeLow, v.RangeHigh, v.RangeCriticalLow, v.RangeCriticalHigh, v.RangeText) : null;
        return new ValueDto(v.ParameterId, v.ValueNumeric, v.OptionId, v.ValueText, v.ValueTextUr, display, v.UnitText, places, v.Flag, range, v.RangeText);
    }).ToList();

    private ResultActions ActionsFor(Ctx c, TestResult? current, TestResult? released, bool allowSelf, bool hasHistory)
    {
        var ctx = db.Request;
        var me = ctx.UserId ?? 0;
        var workable = c.Item.Status != OrderItemStatus.Cancelled && c.Order.Status == OrderStatus.Active;
        var atBench = c.Sample.Status == SampleStatus.Received && ctx.ActiveBranchId == c.Sample.CurrentBranchId;
        var canEntry = ctx.Can(Permissions.ResultsEnter) && workable && atBench;
        var draft = current is { Status: ResultStatus.Draft };
        var own = current?.EnteredByUserId == me;
        var mayReview = current is not null && ResultRules.MayReview(me, current.EnteredByUserId, allowSelf);
        return new ResultActions(
            Save: canEntry && ((current is null && released is null) || draft),
            Submit: canEntry && draft && current!.Values.Count > 0,
            Discard: canEntry && draft && !hasHistory,
            Verify: ctx.Can(Permissions.ResultsVerify) && current is { Status: ResultStatus.Submitted } && mayReview,
            Return: (ctx.Can(Permissions.ResultsVerify) || ctx.Can(Permissions.ResultsApprove)) && current is { Status: ResultStatus.Submitted or ResultStatus.Verified },
            Approve: ctx.Can(Permissions.ResultsApprove) && current is { Status: ResultStatus.Verified } && mayReview,
            Amend: ctx.Can(Permissions.ResultsAmend) && released is not null && current is null && workable,
            IsOwnEntry: own);
    }

    // ================= Draft =================

    public async Task<EntryModel> SaveDraftAsync(long orderItemId, SaveDraftRequest req, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var c = await LoadAsync(orderItemId, true, ct);
        RequireBench(c);

        var versions = await Visible().Include(r => r.Values).Where(r => r.OrderItemId == orderItemId && r.Status != ResultStatus.Superseded).ToListAsync(ct); // VISIBILITY-GATE
        var result = versions.FirstOrDefault(r => r.Status < ResultStatus.Approved);
        if (result is null)
        {
            if (versions.Any(r => r.Status == ResultStatus.Approved))
                throw BusinessRuleException.Conflict("result_already_released", "This result is released. Use Amend to change it.");
            result = new TestResult
            {
                OrganizationId = OrgId, OrderItemId = c.Item.Id, OrderId = c.Order.Id, SampleId = c.Sample.Id, PatientId = c.Patient.Id,
                OrderBranchId = c.Order.BranchId, LabBranchId = c.Sample.CurrentBranchId, Version = 1, Status = ResultStatus.Draft,
            };
            db.TestResults.Add(result);
        }
        else
        {
            if (result.Status != ResultStatus.Draft) throw WrongState(result);
            db.Entry(result).Property(r => r.RowVersion).OriginalValue = RowVersion.FromToken(req.RowVersion);
            db.Entry(result).Property(r => r.Comment).IsModified = true;   // forces the version check even when only values change
        }
        result.Comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim();

        var ps = await ParametersAsync(c.Test.Id, ct);
        var units = await UnitsAsync(ps, ct);
        var ageDays = AgeDays(c);
        var seen = new HashSet<int>();
        foreach (var input in req.Values)
        {
            if (!seen.Add(input.ParameterId)) throw new BusinessRuleException("result_duplicate_parameter", "A parameter appears twice.");
            var p = ps.FirstOrDefault(x => x.Id == input.ParameterId) ?? throw new BusinessRuleException("result_parameter_invalid", "This parameter does not belong to the test.");
            var existing = result.Values.FirstOrDefault(v => v.ParameterId == p.Id);
            var parsed = Parse(p, input);
            if (parsed is null)
            {
                if (existing is not null) { result.Values.Remove(existing); db.TestResultValues.Remove(existing); }
                continue;
            }
            var value = existing ?? new TestResultValue { OrganizationId = OrgId, ParameterId = p.Id };
            Apply(value, p, parsed, ReferenceRules.Select(p.Ranges, c.Patient.Sex, ageDays), p.UnitId is int u ? units.GetValueOrDefault(u) : null);
            if (existing is null) result.Values.Add(value);
        }

        var item = await db.OrderItems.SingleAsync(i => i.Id == orderItemId, ct);
        if (item.Status == OrderItemStatus.Received) item.Status = ResultRules.ItemStatusFor(ResultStatus.Draft);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetEntryAsync(orderItemId, ct);
    }

    private sealed record Parsed(decimal? Number, ParameterOption? Option, string? Text);

    /// <summary>Validates one typed value for its parameter type. Null = blank (clears the value).</summary>
    private static Parsed? Parse(TestParameter p, ValueInput input)
    {
        switch (p.ResultType)
        {
            case ResultType.Numeric:
                if (string.IsNullOrWhiteSpace(input.Value)) return null;
                return ResultValueParser.TryParseNumeric(input.Value, out var n) switch
                {
                    ResultValueParser.ParseError.None => new Parsed(n, null, null),
                    ResultValueParser.ParseError.TooManyDecimals => throw new BusinessRuleException("result_value_decimals", "Use at most 6 decimal places.", 400, new { parameterId = p.Id }),
                    ResultValueParser.ParseError.OutOfRange => throw new BusinessRuleException("result_value_range", "This number is too large.", 400, new { parameterId = p.Id }),
                    _ => throw new BusinessRuleException("result_value_invalid", "Enter a plain number, e.g. 12.5.", 400, new { parameterId = p.Id }),
                };
            case ResultType.Option:
                if (input.OptionId is null) return null;
                var option = p.Options.FirstOrDefault(o => o.Id == input.OptionId) ?? throw new BusinessRuleException("result_option_invalid", "Choose one of the listed options.", 400, new { parameterId = p.Id });
                return new Parsed(null, option, null);
            case ResultType.Text:
            case ResultType.Narrative:
                if (string.IsNullOrWhiteSpace(input.Value)) return null;
                var text = input.Value.Trim();
                var max = p.ResultType == ResultType.Text ? 500 : 4000;
                if (text.Length > max) throw new BusinessRuleException("result_text_too_long", $"At most {max} characters.", 400, new { parameterId = p.Id, max });
                return new Parsed(null, null, text);
            default:
                throw new BusinessRuleException("result_parameter_invalid", "Unsupported parameter type.");
        }
    }

    /// <summary>Stores the value with a snapshot of unit, decimals and the approved range used. Flags come only from approved configuration.</summary>
    private static void Apply(TestResultValue v, TestParameter p, Parsed parsed, ReferenceRange? range, string? unit)
    {
        v.ValueNumeric = parsed.Number;
        v.OptionId = parsed.Option?.Id;
        v.ValueText = parsed.Option?.ValueEn ?? parsed.Text;
        v.ValueTextUr = parsed.Option?.ValueUr;
        v.UnitText = unit;
        v.DecimalPlaces = p.DecimalPlaces;
        ApplyRange(v, p, range);
    }

    private static void ApplyRange(TestResultValue v, TestParameter p, ReferenceRange? range)
    {
        if (v.ValueNumeric is decimal n && p.ResultType == ResultType.Numeric)
        {
            v.Flag = ReferenceRules.Evaluate(n, range);
            v.RangeId = range?.Id; v.RangeLow = range?.Low; v.RangeHigh = range?.High;
            v.RangeCriticalLow = range?.CriticalLow; v.RangeCriticalHigh = range?.CriticalHigh; v.RangeText = range?.DisplayText;
        }
        else if (p.ResultType == ResultType.Option)
        {
            var opt = p.Options.FirstOrDefault(o => o.Id == v.OptionId);
            // Only mark "normal" when the laboratory has configured at least one abnormal option; otherwise stay unflagged.
            v.Flag = opt is null ? ResultFlag.None : opt.IsAbnormal ? ResultFlag.Abnormal : p.Options.Any(o => o.IsAbnormal) ? ResultFlag.Normal : ResultFlag.None;
            v.RangeId = null; v.RangeLow = v.RangeHigh = v.RangeCriticalLow = v.RangeCriticalHigh = null; v.RangeText = null;
        }
        else
        {
            v.Flag = ResultFlag.None;
            v.RangeId = null; v.RangeLow = v.RangeHigh = v.RangeCriticalLow = v.RangeCriticalHigh = null; v.RangeText = null;
        }
    }

    private void RequireBench(Ctx c)
    {
        if (c.Item.Status == OrderItemStatus.Cancelled || c.Order.Status != OrderStatus.Active)
            throw BusinessRuleException.Conflict("result_item_cancelled", "This test or order was cancelled.");
        if (c.Sample.Status != SampleStatus.Received)
            throw BusinessRuleException.Conflict("result_sample_not_ready", "The sample must be received before results can be entered.");
        if (c.Sample.CurrentBranchId != ActiveBranch)
            throw new BusinessRuleException("sample_not_at_branch", "This sample is held at another branch.", 409, new { branchId = c.Sample.CurrentBranchId });
    }

    private static BusinessRuleException WrongState(TestResult r) =>
        new("result_invalid_state", "This result is no longer in the right state for that action.", 409, new { status = r.Status });

    public async Task DiscardDraftAsync(long orderItemId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var c = await LoadAsync(orderItemId, true, ct);
        RequireBench(c);
        var r = await Visible().Include(x => x.Values).Where(x => x.OrderItemId == orderItemId && x.Status == ResultStatus.Draft).SingleOrDefaultAsync(ct) // VISIBILITY-GATE
                ?? throw BusinessRuleException.NotFound("Draft");
        // A draft that was handed over at least once keeps its history. An amendment that was only started (never submitted) can be abandoned.
        var steps = await db.ResultApprovals.Where(a => a.ResultId == r.Id).ToListAsync(ct);
        var onlyStarted = r.Version > 1 && steps.All(s => s.Step == ResultSteps.Amend);
        if (steps.Count > 0 && !onlyStarted)
            throw BusinessRuleException.Conflict("result_has_history", "This result has already been submitted once, so it cannot be discarded.");
        if (onlyStarted)
        {
            db.ResultApprovals.RemoveRange(steps);
            db.AddBusinessAudit("Result.AmendmentDiscarded", nameof(TestResult), r.Id, new { r.OrderItemId, r.Version, r.AmendmentReason });
        }
        if (await db.CriticalValueAlerts.AnyAsync(a => a.ResultId == r.Id, ct))
            throw BusinessRuleException.Conflict("result_has_alerts", "A critical alert exists for this result, so it cannot be discarded.");
        db.TestResultValues.RemoveRange(r.Values);
        db.TestResults.Remove(r); // VISIBILITY-GATE (loaded through Visible above)
        var item = await db.OrderItems.SingleAsync(i => i.Id == orderItemId, ct);
        if (r.Version == 1 && item.Status == OrderItemStatus.InProgress) item.Status = OrderItemStatus.Received;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // ================= Workflow =================

    private async Task<TestResult> LoadForChangeAsync(long id, string rowVersion, CancellationToken ct)
    {
        var r = await Visible().Include(x => x.Values).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw BusinessRuleException.NotFound("Result"); // VISIBILITY-GATE
        db.Entry(r).Property(x => x.RowVersion).OriginalValue = RowVersion.FromToken(rowVersion);
        // The caller must have looked at this exact version: a change in between (returned, edited, resubmitted) is a 409.
        db.Entry(r).Property(x => x.Comment).IsModified = true;
        return r;
    }

    private void Step(TestResult r, string step, string? comment) =>
        db.ResultApprovals.Add(new ResultApproval { OrganizationId = r.OrganizationId, ResultId = r.Id, Step = step, UserId = UserId, AtUtc = clock.UtcNow, Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim() });

    public async Task<EntryModel> SubmitAsync(long id, SubmitRequest req, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var r = await LoadForChangeAsync(id, req.RowVersion, ct);
        if (r.Status != ResultStatus.Draft) throw WrongState(r);
        var c = await LoadAsync(r.OrderItemId, false, ct);
        RequireBench(c);

        var ps = await ParametersAsync(c.Test.Id, ct);
        var missing = ps.Where(p => r.Values.All(v => v.ParameterId != p.Id)).Select(p => p.Id).ToList();
        if (missing.Count > 0 || ps.Count == 0)
            throw new BusinessRuleException("result_incomplete", "Every parameter needs a value before the result can be submitted.", 400, new { parameterIds = missing });

        // Flags are recomputed from the ranges approved right now, so a range approved after the draft was saved is honoured.
        var ageDays = AgeDays(c);
        foreach (var v in r.Values)
        {
            var p = ps.Single(x => x.Id == v.ParameterId);
            ApplyRange(v, p, ReferenceRules.Select(p.Ranges, c.Patient.Sex, ageDays));
        }

        r.Status = ResultStatus.Submitted;
        r.EnteredByUserId = UserId;
        r.SubmittedAtUtc = clock.UtcNow;
        Step(r, ResultSteps.Submit, null);
        await RaiseAlertsAsync(r, ps, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await ChargeStockAsync(c, r, ct);
        return await GetEntryAsync(r.OrderItemId, ct);
    }

    /// <summary>
    /// Charges the reagents this test uses to stock. Done after the result is saved and never allowed to fail it: a stock problem must not stop a patient's result.
    /// It is charged once per ordered test however often the result is returned and submitted again (see <see cref="Inventory.InventoryService.ConsumeForTestAsync"/>).
    /// </summary>
    private async Task ChargeStockAsync(Ctx c, TestResult r, CancellationToken ct)
    {
        try { await inventory.ConsumeForTestAsync(c.Item.Id, c.Test.Id, r.LabBranchId, c.Order.OrderNo, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            db.ChangeTracker.Clear();
            log.LogError(ex, "Stock was not charged for order item {OrderItemId} (test {TestId}); the result itself was saved.", c.Item.Id, c.Test.Id);
        }
    }

    /// <summary>One alert per critical parameter. Open alerts are rebuilt from the current values; a notification already recorded for the same value stays.</summary>
    private async Task RaiseAlertsAsync(TestResult r, List<TestParameter> ps, CancellationToken ct)
    {
        var existing = await db.CriticalValueAlerts.Where(a => a.ResultId == r.Id).ToListAsync(ct);
        db.CriticalValueAlerts.RemoveRange(existing.Where(a => a.Status == AlertStatus.Open));
        foreach (var v in r.Values.Where(v => ResultRules.IsCritical(v.Flag)))
        {
            var p = ps.Single(x => x.Id == v.ParameterId);
            var high = v.Flag == ResultFlag.CriticalHigh;
            var text = v.ValueNumeric is decimal n ? ResultValueParser.Format(n, Math.Max(p.DecimalPlaces, 0)) : v.ValueText ?? "";
            if (existing.Any(a => a.Status == AlertStatus.Notified && a.ParameterId == v.ParameterId && a.IsHigh == high && a.ValueText == text)) continue;
            db.CriticalValueAlerts.Add(new CriticalValueAlert
            {
                OrganizationId = r.OrganizationId, ResultId = r.Id, OrderItemId = r.OrderItemId, PatientId = r.PatientId, LabBranchId = r.LabBranchId,
                ParameterId = v.ParameterId, ValueText = text, Limit = high ? v.RangeCriticalHigh : v.RangeCriticalLow, IsHigh = high, Status = AlertStatus.Open,
            });
        }
    }

    public async Task<EntryModel> VerifyAsync(long id, StepRequest req, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var r = await LoadForChangeAsync(id, req.RowVersion, ct);
        if (r.Status != ResultStatus.Submitted) throw WrongState(r);
        await RequireReviewerAsync(r, ct);
        r.Status = ResultStatus.Verified;
        r.VerifiedByUserId = UserId;
        r.VerifiedAtUtc = clock.UtcNow;
        Step(r, ResultSteps.Verify, req.Comment);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetEntryAsync(r.OrderItemId, ct);
    }

    private async Task RequireReviewerAsync(TestResult r, CancellationToken ct)
    {
        var allowSelf = await SettingAsync(SettingAllowSelfVerification, DefaultAllowSelfVerification, ct);
        if (!ResultRules.MayReview(UserId, r.EnteredByUserId, allowSelf))
            throw new BusinessRuleException("result_self_review", "You entered this result, so someone else must verify and approve it.", 403);
    }

    public async Task<EntryModel> ReturnAsync(long id, ReasonRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Reason)) throw new BusinessRuleException("reason_required", "A reason is required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var r = await LoadForChangeAsync(id, req.RowVersion, ct);
        if (r.Status is not (ResultStatus.Submitted or ResultStatus.Verified)) throw WrongState(r);
        r.Status = ResultStatus.Draft;
        r.VerifiedByUserId = null; r.VerifiedAtUtc = null; r.EnteredByUserId = null; r.SubmittedAtUtc = null;
        Step(r, ResultSteps.Return, req.Reason);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetEntryAsync(r.OrderItemId, ct);
    }

    public async Task<EntryModel> ApproveAsync(long id, StepRequest req, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var r = await LoadForChangeAsync(id, req.RowVersion, ct);
        if (r.Status != ResultStatus.Verified) throw WrongState(r);
        await RequireReviewerAsync(r, ct);
        if (await SettingAsync(SettingBlockApproval, DefaultBlockApproval, ct) && await db.CriticalValueAlerts.AnyAsync(a => a.ResultId == r.Id && a.Status == AlertStatus.Open, ct))
            throw BusinessRuleException.Conflict("critical_alert_open", "A critical value must be reported (and read back) before this result can be approved.");

        // An amendment replaces the released version first, so there is never more than one released version per test.
        if (r.ReplacesResultId is long prevId)
        {
            var prev = await Visible().SingleAsync(x => x.Id == prevId, ct); // VISIBILITY-GATE
            if (prev.Status != ResultStatus.Approved) throw WrongState(prev);
            prev.Status = ResultStatus.Superseded;
            await db.SaveChangesAsync(ct);
        }
        r.Status = ResultStatus.Approved;
        r.ApprovedByUserId = UserId;
        r.ApprovedAtUtc = clock.UtcNow;
        Step(r, ResultSteps.Approve, req.Comment);
        var item = await db.OrderItems.SingleAsync(i => i.Id == r.OrderItemId, ct);
        item.Status = ResultRules.ItemStatusFor(ResultStatus.Approved);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetEntryAsync(r.OrderItemId, ct);
    }

    /// <summary>Starts the next version of a released result as a draft. The released version stays valid until the amendment is approved.</summary>
    public async Task<EntryModel> AmendAsync(long id, ReasonRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Reason)) throw new BusinessRuleException("reason_required", "A reason is required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var r = await LoadForChangeAsync(id, req.RowVersion, ct);
        if (r.Status != ResultStatus.Approved) throw new BusinessRuleException("result_not_released", "Only a released result can be amended.", 409, new { status = r.Status });
        if (await Visible().AnyAsync(x => x.OrderItemId == r.OrderItemId && x.Status < ResultStatus.Approved, ct)) // VISIBILITY-GATE
            throw BusinessRuleException.Conflict("amendment_in_progress", "An amendment of this result is already in progress.");
        var item = await db.OrderItems.AsNoTracking().SingleAsync(i => i.Id == r.OrderItemId, ct);
        if (item.Status == OrderItemStatus.Cancelled) throw BusinessRuleException.Conflict("result_item_cancelled", "This test or order was cancelled.");

        var next = new TestResult
        {
            OrganizationId = r.OrganizationId, OrderItemId = r.OrderItemId, OrderId = r.OrderId, SampleId = r.SampleId, PatientId = r.PatientId,
            OrderBranchId = r.OrderBranchId, LabBranchId = r.LabBranchId, Version = r.Version + 1, Status = ResultStatus.Draft,
            AmendmentReason = req.Reason.Trim(), ReplacesResultId = r.Id, Comment = r.Comment,
        };
        foreach (var v in r.Values)
            next.Values.Add(new TestResultValue
            {
                OrganizationId = v.OrganizationId, ParameterId = v.ParameterId, ValueNumeric = v.ValueNumeric, OptionId = v.OptionId, ValueText = v.ValueText, ValueTextUr = v.ValueTextUr,
                UnitText = v.UnitText, DecimalPlaces = v.DecimalPlaces, Flag = v.Flag, RangeId = v.RangeId, RangeLow = v.RangeLow, RangeHigh = v.RangeHigh,
                RangeCriticalLow = v.RangeCriticalLow, RangeCriticalHigh = v.RangeCriticalHigh, RangeText = v.RangeText,
            });
        db.TestResults.Add(next);
        await db.SaveChangesAsync(ct);
        Step(next, ResultSteps.Amend, req.Reason);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetEntryAsync(next.OrderItemId, ct);
    }

    // ================= Dashboard =================

    public sealed record ResultSlice(QueueItem Item, DateTime? AtUtc);
    public sealed record ResultDashboard(int InProgress, int Departments, int AwaitingApproval, DateTime? OldestApprovalWaitUtc, int Completed, IReadOnlyList<ResultSlice> Approvals, IReadOnlyList<ResultSlice> Released);

    /// <summary>
    /// Figures for the dashboard, for the working branch (or every branch the caller may see when none is selected). "In progress" is a result that is
    /// drafted, submitted or verified but not yet released. All reads go through <see cref="Visible"/>.
    /// </summary>
    public async Task<ResultDashboard> DashboardAsync(DateTime sinceUtc, CancellationToken ct)
    {
        var branch = db.Request.ActiveBranchId;
        IQueryable<TestResult> Scope() { var q = Visible().AsNoTracking(); return branch is int b ? q.Where(r => r.LabBranchId == b) : q; } // VISIBILITY-GATE
        ResultStatus[] open = [ResultStatus.Draft, ResultStatus.Submitted, ResultStatus.Verified];

        var inProgress = await Scope().CountAsync(r => open.Contains(r.Status), ct); // VISIBILITY-GATE
        var departments = await (from r in Scope().Where(r => open.Contains(r.Status)) // VISIBILITY-GATE
                                 join i in db.OrderItems.AsNoTracking() on r.OrderItemId equals i.Id
                                 join t in db.Tests.AsNoTracking() on i.TestId equals t.Id
                                 select t.DepartmentId).Distinct().CountAsync(ct);
        var awaiting = Scope().Where(r => r.Status == ResultStatus.Verified); // VISIBILITY-GATE
        var awaitingCount = await awaiting.CountAsync(ct);
        var oldest = await awaiting.MinAsync(r => r.VerifiedAtUtc, ct);
        var completed = await Scope().CountAsync(r => r.Status == ResultStatus.Approved && r.ApprovedAtUtc >= sinceUtc, ct); // VISIBILITY-GATE

        var waiting = await awaiting.OrderBy(r => r.VerifiedAtUtc).ThenBy(r => r.Id).Take(6).ToListAsync(ct);
        var released = await Scope().Where(r => r.Status == ResultStatus.Approved).OrderByDescending(r => r.ApprovedAtUtc).ThenByDescending(r => r.Id).Take(4).ToListAsync(ct); // VISIBILITY-GATE
        var hydrated = (await HydrateQueueAsync(waiting.Concat(released).ToList(), ct)).ToDictionary(x => x.ResultId);
        return new ResultDashboard(inProgress, departments, awaitingCount, oldest, completed,
            waiting.Select(r => new ResultSlice(hydrated[r.Id], r.VerifiedAtUtc)).ToList(),
            released.Select(r => new ResultSlice(hydrated[r.Id], r.ApprovedAtUtc)).ToList());
    }

    // ================= Queues and lookups =================

    public async Task<PagedResult<QueueItem>> QueueAsync(QueueQuery q, CancellationToken ct)
    {
        var need = q.Stage == "approval" ? Permissions.ResultsApprove : Permissions.ResultsVerify;
        if (!db.Request.Can(need)) throw new BusinessRuleException("forbidden", "You do not have access to this queue.", 403);
        var branch = ActiveBranch;
        var orgId = OrgId;
        var status = q.Stage == "approval" ? ResultStatus.Verified : ResultStatus.Submitted;
        var query = Visible().AsNoTracking().Where(r => r.Status == status && r.LabBranchId == branch); // VISIBILITY-GATE
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            var norm = Code128.NormalizeScan(term);
            var patientIds = await db.Patients.AsNoTracking().Where(p => p.PatientNo == norm || p.FullName.Contains(term) || (p.FullNameUr != null && p.FullNameUr.Contains(term))).Select(p => p.Id).Take(500).ToListAsync(ct);
            var orderIds = await db.Orders.IgnoreQueryFilters().Where(o => o.OrganizationId == orgId && o.OrderNo == norm).Select(o => o.Id).ToListAsync(ct);
            var sampleIds = await samples.VisibleReadOnly().Where(s => s.Barcode == norm).Select(s => s.Id).ToListAsync(ct);
            query = query.Where(r => patientIds.Contains(r.PatientId) || orderIds.Contains(r.OrderId) || sampleIds.Contains(r.SampleId));
        }
        var total = await query.CountAsync(ct);
        var page = await query.OrderBy(r => r.SubmittedAtUtc).ThenBy(r => r.Id).Skip(q.Skip).Take(q.PageSize).ToListAsync(ct);
        return new PagedResult<QueueItem>(await HydrateQueueAsync(page, ct), total, q.Page, q.PageSize);
    }

    private async Task<List<QueueItem>> HydrateQueueAsync(List<TestResult> page, CancellationToken ct)
    {
        if (page.Count == 0) return [];
        var orgId = OrgId;
        var me = UserId;
        var itemIds = page.Select(r => r.OrderItemId).ToList();
        var items = await db.OrderItems.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var orderIds = page.Select(r => r.OrderId).Distinct().ToList();
        var orders = await db.Orders.IgnoreQueryFilters().AsNoTracking().Where(o => o.OrganizationId == orgId && orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OrderNo, ct);
        var patientIds = page.Select(r => r.PatientId).Distinct().ToList();
        var patients = await db.Patients.AsNoTracking().Where(p => patientIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var sampleIds = page.Select(r => r.SampleId).Distinct().ToList();
        var sampleInfo = await samples.VisibleReadOnly().Where(s => sampleIds.Contains(s.Id)).Select(s => new { s.Id, s.Barcode, s.Priority }).ToDictionaryAsync(s => s.Id, ct);
        var resultIds = page.Select(r => r.Id).ToList();
        var flagCounts = await db.TestResultValues.AsNoTracking().Where(v => resultIds.Contains(v.ResultId) && v.Flag != ResultFlag.None && v.Flag != ResultFlag.Normal)
            .Select(v => new { v.ResultId, v.Flag }).ToListAsync(ct);
        var openAlerts = await db.CriticalValueAlerts.AsNoTracking().Where(a => resultIds.Contains(a.ResultId) && a.Status == AlertStatus.Open).Select(a => a.ResultId).Distinct().ToListAsync(ct);
        var userIds = page.SelectMany(r => new int?[] { r.EnteredByUserId, r.VerifiedByUserId }).OfType<int>().Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameEn, ct);
        string? Name(int? id) => id is int i ? users.GetValueOrDefault(i) : null;

        return page.Select(r =>
        {
            var i = items.GetValueOrDefault(r.OrderItemId);
            var p = patients.GetValueOrDefault(r.PatientId);
            var s = sampleInfo.GetValueOrDefault(r.SampleId);
            var flags = flagCounts.Where(f => f.ResultId == r.Id).ToList();
            return new QueueItem(
                r.Id, r.Version, r.Status, r.OrderItemId, orders.GetValueOrDefault(r.OrderId) ?? "", r.PatientId, p?.PatientNo ?? "", p?.FullName ?? "", p?.FullNameUr,
                i?.TestCode ?? "", i?.TestNameEn ?? "", i?.TestNameUr, s?.Barcode ?? "", s?.Priority ?? OrderPriority.Routine, Name(r.EnteredByUserId), r.SubmittedAtUtc,
                Name(r.VerifiedByUserId), flags.Count(f => ResultRules.IsCritical(f.Flag)), flags.Count(f => !ResultRules.IsCritical(f.Flag)),
                openAlerts.Contains(r.Id), r.Version > 1, r.EnteredByUserId == me);
        }).ToList();
    }

    public async Task<List<OrderResultDto>> ForOrderAsync(long orderId, CancellationToken ct)
    {
        // The order itself is branch-filtered: if the caller cannot see it, nothing is returned.
        if (!await db.Orders.AnyAsync(o => o.Id == orderId, ct)) throw BusinessRuleException.NotFound("Order");
        var items = await db.OrderItems.AsNoTracking().Where(i => i.OrderId == orderId).OrderBy(i => i.Id).ToListAsync(ct);
        var results = await Visible().AsNoTracking().Where(r => r.OrderId == orderId && r.Status != ResultStatus.Superseded).Select(r => new { r.Id, r.OrderItemId, r.Status, r.Version }).ToListAsync(ct); // VISIBILITY-GATE
        return items.Select(i =>
        {
            var mine = results.Where(r => r.OrderItemId == i.Id).ToList();
            var current = mine.OrderByDescending(r => r.Version).FirstOrDefault();
            return new OrderResultDto(i.Id, i.TestCode, i.TestNameEn, i.TestNameUr, i.Status, current?.Id, current?.Status, current?.Version, mine.Any(r => r.Status == ResultStatus.Approved));
        }).ToList();
    }

    public async Task<List<ApprovalStepDto>> HistoryAsync(long resultId, CancellationToken ct)
    {
        if (!await Visible().AnyAsync(r => r.Id == resultId, ct)) throw BusinessRuleException.NotFound("Result"); // VISIBILITY-GATE
        var steps = await db.ResultApprovals.AsNoTracking().Where(a => a.ResultId == resultId).OrderBy(a => a.Id).ToListAsync(ct);
        var ids = steps.Select(s => s.UserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameEn, ct);
        return steps.Select(s => new ApprovalStepDto(s.Step, s.AtUtc, users.GetValueOrDefault(s.UserId), s.Comment)).ToList();
    }

    public async Task<PatientHistory> PatientHistoryAsync(int patientId, int? months, CancellationToken ct)
    {
        if (!await db.Patients.AsNoTracking().AnyAsync(p => p.Id == patientId, ct))
            throw BusinessRuleException.NotFound("Patient");

        var fromUtc = months is int m && m > 0
            ? clock.UtcNow.AddMonths(-Math.Min(m, 120))
            : (DateTime?)null;

        var query =
            from r in Visible().AsNoTracking() // VISIBILITY-GATE
            join v in db.TestResultValues.AsNoTracking() on r.Id equals v.ResultId
            join i in db.OrderItems.AsNoTracking() on r.OrderItemId equals i.Id
            join p in db.TestParameters.AsNoTracking() on v.ParameterId equals p.Id
            where r.PatientId == patientId
                  && r.Status == ResultStatus.Approved
                  && r.ApprovedAtUtc != null
                  && v.ValueNumeric != null
                  && (fromUtc == null || r.ApprovedAtUtc >= fromUtc)
            orderby r.ApprovedAtUtc, p.SortOrder
            select new HistoryPoint(
                r.ApprovedAtUtc!.Value, r.Id, i.TestId, i.TestCode, i.TestNameEn, i.TestNameUr,
                p.Id, p.Code, p.NameEn, p.NameUr, v.ValueNumeric!.Value, v.UnitText,
                v.RangeLow, v.RangeHigh, v.Flag);

        return new PatientHistory(patientId, months, await query.ToListAsync(ct));
    }

    /// <summary>Entry model for a result id (verification screens open results, not order items).</summary>
    public async Task<EntryModel> GetByResultAsync(long resultId, CancellationToken ct)
    {
        var itemId = await Visible().Where(r => r.Id == resultId).Select(r => (long?)r.OrderItemId).SingleOrDefaultAsync(ct) // VISIBILITY-GATE
                     ?? throw BusinessRuleException.NotFound("Result");
        return await GetEntryAsync(itemId, ct);
    }

    private async Task<List<AlertDto>> AlertsForAsync(long resultId, Ctx c, List<TestParameter> ps, Dictionary<int, string> units, CancellationToken ct)
    {
        var alerts = await db.CriticalValueAlerts.AsNoTracking().Where(a => a.ResultId == resultId).OrderBy(a => a.Id).ToListAsync(ct);
        var ids = alerts.Select(a => a.NotifiedByUserId).OfType<int>().Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayNameEn, ct);
        return alerts.Select(a =>
        {
            var p = ps.FirstOrDefault(x => x.Id == a.ParameterId);
            return new AlertDto(a.Id, a.ResultId, a.OrderItemId, a.PatientId, c.Patient.PatientNo, c.Patient.FullName, c.Patient.FullNameUr, c.Patient.Mobile,
                c.Test.Code, c.Test.NameEn, c.Test.NameUr, a.ParameterId, p?.NameEn ?? "", p?.NameUr, a.ValueText, p?.UnitId is int u ? units.GetValueOrDefault(u) : null,
                a.Limit, a.IsHigh, a.Status, a.CreatedAtUtc, a.NotifiedTo, a.Method, a.ReadBackConfirmed,
                a.NotifiedByUserId is int n ? users.GetValueOrDefault(n) : null, a.NotifiedAtUtc, a.Comment);
        }).ToList();
    }
}
