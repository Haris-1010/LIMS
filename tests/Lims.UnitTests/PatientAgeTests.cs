using Lims.Domain.Patients;

namespace Lims.UnitTests;

public sealed class PatientAgeTests
{
    [Theory]
    [InlineData("1990-11-09", "2026-11-09", 36, 0, 0)]
    [InlineData("1990-11-10", "2026-11-09", 35, 11, 30)] // day before birthday
    [InlineData("2026-10-31", "2026-11-09", 0, 0, 9)]    // newborn
    [InlineData("2024-02-29", "2025-02-28", 1, 0, 0)]    // leap-day birthday counts on 28 Feb
    [InlineData("2024-02-29", "2025-02-27", 0, 11, 29)]
    [InlineData("2026-01-31", "2026-03-01", 0, 1, 1)]
    public void Age_is_years_months_days(string dob, string on, int y, int m, int d)
    {
        var age = PatientAge.Between(DateOnly.Parse(dob), DateOnly.Parse(on));
        Assert.Equal((y, m, d), (age.Years, age.Months, age.Days));
    }

    [Fact]
    public void Future_date_of_birth_is_rejected() =>
        Assert.Throws<ArgumentException>(() => PatientAge.Between(new DateOnly(2027, 1, 1), new DateOnly(2026, 11, 9)));

    [Fact]
    public void Estimated_date_of_birth_round_trips_to_the_stated_age()
    {
        var today = new DateOnly(2026, 11, 9);
        var dob = PatientAge.Estimate(42, 3, 0, today);
        var age = PatientAge.Between(dob, today);
        Assert.Equal((42, 3, 0), (age.Years, age.Months, age.Days));
    }

    [Theory]
    [InlineData("3520212345671", "35202-1234567-1")]
    [InlineData("35202-1234567-1", "35202-1234567-1")]
    [InlineData(" 35202 1234567 1 ", "35202-1234567-1")]
    [InlineData("3520212345", null)]
    [InlineData("", null)]
    public void Cnic_is_normalised(string input, string? expected) => Assert.Equal(expected, PatientAge.NormalizeCnic(input));

    [Theory]
    [InlineData("0300-1234567", "03001234567")]
    [InlineData("+92 300 1234567", "+923001234567")]
    [InlineData("  ", null)]
    public void Phone_is_normalised(string input, string? expected) => Assert.Equal(expected, PatientAge.NormalizePhone(input));

    [Fact]
    public void Masking_keeps_only_the_last_character_and_separators()
    {
        Assert.Equal("*****-*******-1", PatientAge.Mask("35202-1234567-1"));
        Assert.Equal("****@*******.***", PatientAge.Mask("sana@example.com", 0));
        Assert.Null(PatientAge.Mask(null));
    }
}
