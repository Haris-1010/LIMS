using System.ComponentModel.DataAnnotations;

namespace Lims.Application.Settings;

/// <summary>Kind of value a setting holds: a yes/no switch or a whole number within limits.</summary>
public enum SettingKind { Boolean = 1, Integer = 2 }

public sealed record SettingDto(
    string Key, string Group, SettingKind Kind, string Value, string DefaultValue, bool IsDefault, int? Min, int? Max,
    string DescriptionEn, string DescriptionUr, string RowVersion);

public sealed record SettingUpdate
{
    [Required, MaxLength(200)] public string Value { get; init; } = "";
    /// <summary>The version the caller saw; empty only while the setting has never been changed.</summary>
    public string? RowVersion { get; init; }
}
