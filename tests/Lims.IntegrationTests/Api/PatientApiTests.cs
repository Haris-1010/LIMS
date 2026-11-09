using System.Net;
using Microsoft.EntityFrameworkCore;

namespace Lims.IntegrationTests.Api;

[Collection(DatabaseCollection.Name)]
public sealed class PatientApiTests(DatabaseFixture fx)
{
    private ApiFactory Api => fx.Api;

    private async Task<ApiClient> SignedIn(int org, params string[] roles)
    {
        var c = await Api.ClientAsync();
        ApiClient.Ok(await c.LoginAsync(await Api.CreateUserAsync(org, roles)));
        return c;
    }

    private static string UniqueMobile() => "0300" + Random.Shared.Next(1000000, 9999999);

    private static object NewPatient(string name, string? mobile = null, string? cnic = null, int? ageYears = 30, string? dob = null, bool confirm = false) => new
    {
        fullName = name, sex = 2, mobile, cnic, ageYears = dob is null ? ageYears : null, dateOfBirth = dob,
        email = "patient@example.com", address = "House 1, Street 2", confirmNotDuplicate = confirm,
    };

    [Fact]
    public async Task Registration_assigns_a_number_and_estimates_birth_date_from_age()
    {
        var c = await SignedIn(fx.OrgA, "RECEPTION");
        var res = await c.PostAsync("/api/patients", NewPatient("Ayesha Siddiqui", UniqueMobile(), ageYears: 34));
        ApiClient.Ok(res, HttpStatusCode.Created);
        var p = await ApiClient.Body(res);
        Assert.Matches(@"^P-\d{6}$", p.GetProperty("patientNo").GetString());
        Assert.True(p.GetProperty("isDateOfBirthEstimated").GetBoolean());
        var dob = DateOnly.Parse(p.GetProperty("dateOfBirth").GetString()!);
        Assert.InRange(DateTime.Today.Year - dob.Year, 33, 35);
    }

    [Fact]
    public async Task Age_or_date_of_birth_is_required_and_validated()
    {
        var c = await SignedIn(fx.OrgA, "RECEPTION");
        var noAge = await c.PostAsync("/api/patients", NewPatient("No Age", ageYears: null));
        Assert.Equal("age_required", await ApiClient.Code(noAge));

        var future = await c.PostAsync("/api/patients", NewPatient("Future Born", dob: DateTime.Today.AddDays(3).ToString("yyyy-MM-dd")));
        Assert.Equal("dob_in_future", await ApiClient.Code(future));

        var badCnic = await c.PostAsync("/api/patients", NewPatient("Bad Cnic", cnic: "12345"));
        Assert.Equal("cnic_invalid", await ApiClient.Code(badCnic));
    }

    [Fact]
    public async Task Possible_duplicate_is_flagged_until_confirmed()
    {
        var c = await SignedIn(fx.OrgA, "RECEPTION");
        var mobile = UniqueMobile();
        ApiClient.Ok(await c.PostAsync("/api/patients", NewPatient("Muhammad Bilal", mobile)), HttpStatusCode.Created);

        var dup = await c.PostAsync("/api/patients", NewPatient("Muhammad Bilal Khan", mobile));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("possible_duplicate", await ApiClient.Code(dup));
        var details = (await ApiClient.Body(dup)).GetProperty("details");
        Assert.Contains("same_mobile_and_name", details[0].GetProperty("reasons").EnumerateArray().Select(r => r.GetString()));

        // Live check used by the form returns the same match.
        var live = await ApiClient.Body(await c.GetAsync($"/api/patients/duplicates?fullName=Muhammad%20Bilal&mobile={mobile}"));
        Assert.True(live.GetArrayLength() >= 1);

        // Different person sharing a family phone: staff confirm and registration proceeds.
        ApiClient.Ok(await c.PostAsync("/api/patients", NewPatient("Muhammad Bilal Khan", mobile, confirm: true)), HttpStatusCode.Created);
    }

    [Fact]
    public async Task Same_cnic_is_flagged_as_duplicate()
    {
        var c = await SignedIn(fx.OrgA, "RECEPTION");
        var cnic = "35202" + Random.Shared.Next(1000000, 9999999) + "1";
        ApiClient.Ok(await c.PostAsync("/api/patients", NewPatient("Fatima Noor", cnic: cnic)), HttpStatusCode.Created);
        var dup = await c.PostAsync("/api/patients", NewPatient("Fatima N.", cnic: cnic));
        Assert.Equal("possible_duplicate", await ApiClient.Code(dup));
    }

    [Fact]
    public async Task Identity_details_are_masked_without_permission_and_access_is_logged()
    {
        var reception = await SignedIn(fx.OrgA, "RECEPTION");          // no patients.viewSensitive
        var pathologist = await SignedIn(fx.OrgA, "PATHOLOGIST");      // has patients.viewSensitive
        var cnic = "61101" + Random.Shared.Next(1000000, 9999999) + "3";
        var created = await ApiClient.Body(await reception.PostAsync("/api/patients", NewPatient("Zainab Ali", cnic: cnic)));
        var id = created.GetProperty("id").GetInt32();

        var masked = await ApiClient.Body(await reception.GetAsync($"/api/patients/{id}"));
        Assert.False(masked.GetProperty("sensitiveVisible").GetBoolean());
        Assert.StartsWith("*****-", masked.GetProperty("cnic").GetString());
        Assert.DoesNotContain("example.com", masked.GetProperty("email").GetString());

        var full = await ApiClient.Body(await pathologist.GetAsync($"/api/patients/{id}"));
        Assert.True(full.GetProperty("sensitiveVisible").GetBoolean());
        Assert.Equal($"{cnic[..5]}-{cnic[5..12]}-{cnic[12]}", full.GetProperty("cnic").GetString());

        await using var db = fx.System();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "Patient.ViewSensitive" && a.EntityId == id.ToString()));
        // The audit trail of the registration itself never stores the CNIC.
        var insert = await db.AuditLogs.SingleAsync(a => a.EntityType == "Patient" && a.EntityId == id.ToString() && a.Action == "Insert");
        Assert.DoesNotContain(cnic[5..12], insert.Changes);
    }

    [Fact]
    public async Task Editing_without_sensitive_permission_keeps_hidden_details()
    {
        var reception = await SignedIn(fx.OrgA, "RECEPTION");
        var cnic = "42101" + Random.Shared.Next(1000000, 9999999) + "5";
        var created = await ApiClient.Body(await reception.PostAsync("/api/patients", NewPatient("Hamza Tariq", cnic: cnic)));
        var id = created.GetProperty("id").GetInt32();

        // Reception sends back the masked values it was shown, with a new mobile number.
        ApiClient.Ok(await reception.PutAsync($"/api/patients/{id}", new
        {
            fullName = "Hamza Tariq", sex = 1, ageYears = 30, mobile = "03219876543",
            cnic = created.GetProperty("cnic").GetString(), email = created.GetProperty("email").GetString(),
            rowVersion = created.GetProperty("rowVersion").GetString(),
        }));

        await using var db = fx.System();
        var saved = await db.Patients.SingleAsync(p => p.Id == id);
        Assert.Equal($"{cnic[..5]}-{cnic[5..12]}-{cnic[12]}", saved.Cnic);
        Assert.Equal("patient@example.com", saved.Email);
        Assert.Equal("03219876543", saved.Mobile);
    }

    [Fact]
    public async Task Search_finds_by_number_name_mobile_and_cnic()
    {
        var c = await SignedIn(fx.OrgA, "RECEPTION");
        var mobile = UniqueMobile();
        var cnic = "37405" + Random.Shared.Next(1000000, 9999999) + "7";
        var p = await ApiClient.Body(await c.PostAsync("/api/patients", NewPatient("Usman Farooq Qureshi", mobile, cnic)));
        var no = p.GetProperty("patientNo").GetString()!;

        foreach (var term in new[] { no, "Farooq Qur", mobile[^7..], cnic })
        {
            var list = await ApiClient.Body(await c.GetAsync($"/api/patients?search={Uri.EscapeDataString(term)}"));
            Assert.Contains(list.GetProperty("items").EnumerateArray(), x => x.GetProperty("patientNo").GetString() == no);
        }
    }

    [Fact]
    public async Task Other_organisations_cannot_see_patients_and_roles_without_permission_get_403()
    {
        var a = await SignedIn(fx.OrgA, "RECEPTION");
        var created = await ApiClient.Body(await a.PostAsync("/api/patients", NewPatient("Org A Patient", UniqueMobile())));
        var id = created.GetProperty("id").GetInt32();

        var b = await SignedIn(fx.OrgB, "SYSADMIN");
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/patients/{id}")).StatusCode);
        var listB = await ApiClient.Body(await b.GetAsync("/api/patients?search=Org%20A%20Patient"));
        Assert.Equal(0, listB.GetProperty("total").GetInt32());

        var phleb = await SignedIn(fx.OrgA, "PHLEBOTOMY"); // can view but not register
        Assert.Equal(HttpStatusCode.OK, (await phleb.GetAsync($"/api/patients/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await phleb.PostAsync("/api/patients", NewPatient("X", UniqueMobile()))).StatusCode);
        var inventory = await SignedIn(fx.OrgA, "INVENTORY");
        Assert.Equal(HttpStatusCode.Forbidden, (await inventory.GetAsync($"/api/patients/{id}")).StatusCode);
    }

    [Fact]
    public async Task Doctors_get_automatic_codes_and_only_managers_can_edit()
    {
        var admin = await SignedIn(fx.OrgA, "SYSADMIN");
        var res = await admin.PostAsync("/api/doctors", new { fullName = "Dr. Kamran Shah", specialty = "Cardiology", hospital = "City Hospital" });
        ApiClient.Ok(res, HttpStatusCode.Created);
        var doc = await ApiClient.Body(res);
        Assert.Matches(@"^D\d{4}$", doc.GetProperty("code").GetString());

        var reception = await SignedIn(fx.OrgA, "RECEPTION");
        var options = await ApiClient.Body(await reception.GetAsync("/api/doctors/options?search=Kamran"));
        Assert.True(options.GetArrayLength() >= 1);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsync("/api/doctors", new { fullName = "Dr. X" })).StatusCode);
    }
}
