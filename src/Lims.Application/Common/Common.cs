namespace Lims.Application.Common;

/// <summary>
/// A business rule was broken (e.g. duplicate username). <see cref="Code"/> is a stable, machine-readable
/// key the frontend translates; <see cref="Message"/> is an English fallback for logs and API users.
/// </summary>
public class BusinessRuleException(string code, string message, int status = 400, object? details = null) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    /// <summary>Optional structured data for the client (e.g. possible duplicate patients).</summary>
    public object? Details { get; } = details;

    public static BusinessRuleException NotFound(string what) => new("not_found", $"{what} was not found.", 404);
    public static BusinessRuleException Conflict(string code, string message) => new(code, message, 409);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record PageQuery
{
    private int _page = 1;
    private int _pageSize = 25;

    public string? Search { get; init; }
    public string? Sort { get; init; }
    public bool Desc { get; init; }
    public int Page { get => _page; init => _page = Math.Max(1, value); }
    /// <summary>Clamped to 1–100 so a client cannot request the whole table.</summary>
    public int PageSize { get => _pageSize; init => _pageSize = Math.Clamp(value, 1, 100); }
    public int Skip => (Page - 1) * PageSize;
}

public static class RowVersion
{
    public static string ToToken(byte[] value) => Convert.ToBase64String(value);

    public static byte[] FromToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new BusinessRuleException("row_version_required", "The record version is required for updates.");
        try { return Convert.FromBase64String(token); }
        catch (FormatException) { throw new BusinessRuleException("row_version_invalid", "The record version is invalid."); }
    }
}
