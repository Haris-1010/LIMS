using Lims.Application.Common;

namespace Lims.Application.Security;

/// <summary>
/// Password rules for staff accounts: at least 8 characters, a letter and a digit, and not containing
/// the username. Length beats complexity, so no symbol requirement; 128 max bounds hashing cost.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static string? Validate(string? password, string? username)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength) return "password_too_short";
        if (password.Length > MaxLength) return "password_too_long";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "password_needs_letter_and_digit";
        if (!string.IsNullOrWhiteSpace(username) && password.Contains(username.Trim(), StringComparison.OrdinalIgnoreCase))
            return "password_contains_username";
        return null;
    }

    public static void Enforce(string? password, string? username)
    {
        var code = Validate(password, username);
        if (code is not null)
            throw new BusinessRuleException(code, code switch
            {
                "password_too_short" => $"Password must be at least {MinLength} characters.",
                "password_too_long" => $"Password must be at most {MaxLength} characters.",
                "password_needs_letter_and_digit" => "Password must contain at least one letter and one digit.",
                _ => "Password must not contain the username.",
            });
    }

    /// <summary>Random temporary password (unambiguous characters) for first-admin setup and resets.</summary>
    public static string GenerateTemporary()
    {
        const string letters = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digits = "23456789";
        var chars = new char[12];
        for (var i = 0; i < chars.Length; i++)
        {
            var set = i % 4 == 3 ? digits : letters;
            chars[i] = set[System.Security.Cryptography.RandomNumberGenerator.GetInt32(set.Length)];
        }
        return new string(chars);
    }
}
