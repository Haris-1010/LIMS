using System.Text.Json;

namespace Lims.Application.Audit;

/// <summary>Reads the JSON stored with an audit line into a list of changed fields.</summary>
public static class AuditRules
{
    /// <summary>Reads the stored JSON: {"Field":{"old":..,"new":..}} for row changes, or any object for named actions.</summary>
    public static List<AuditChange> ParseChanges(string? json)
    {
        var list = new List<AuditChange>();
        if (string.IsNullOrWhiteSpace(json)) return list;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) { list.Add(new AuditChange("value", null, Text(doc.RootElement))); return list; }
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.Object && (p.Value.TryGetProperty("old", out _) || p.Value.TryGetProperty("new", out _)))
                    list.Add(new AuditChange(p.Name, p.Value.TryGetProperty("old", out var o) ? Text(o) : null, p.Value.TryGetProperty("new", out var n) ? Text(n) : null));
                else list.Add(new AuditChange(p.Name, null, Text(p.Value)));
            }
        }
        catch (JsonException)
        {
            list.Add(new AuditChange("value", null, json));
        }
        return list;
    }

    private static string? Text(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => e.GetString(),
        _ => e.GetRawText(),
    };
}
