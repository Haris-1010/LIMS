using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Lims.Application.Licensing;
using Microsoft.Win32;

namespace Lims.Api.Licensing;

/// <summary>How the licence check runs. Built once at start-up (Program.cs); tests replace it.</summary>
public sealed class LicenseSettings
{
    /// <summary>Off only in the Development and Testing environments. There is deliberately no configuration switch for it.</summary>
    public bool Enforce { get; init; } = true;
    /// <summary>Public half of the signing key. Built into the code so it cannot be swapped by replacing a file.</summary>
    public string PublicKeyXml { get; init; } = LicenseService.BuiltInPublicKeyXml;
    /// <summary>Folder holding license.json; default %ProgramData%\HARTLIMS. Must be outside the application folder.</summary>
    public string? LicenseDirectory { get; init; }
    /// <summary>The application folder (the licence may not live inside it).</summary>
    public string ContentRoot { get; init; } = AppContext.BaseDirectory;
    /// <summary>How long a verified result is reused before the file is read and checked again.</summary>
    public TimeSpan CacheFor { get; init; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Verifies the signed offline licence for this server and installs an accepted file. Same format and keys as the HART POS
/// licence (made by the HART licence generator): a JSON envelope {payload, signature}, RSA-3072 PKCS#1 v1.5 over SHA-256, the
/// payload naming the product ("HART LIMS"), the customer, this server's installation ID and an optional expiry date (UTC).
/// No network is used. It is a deterrent, not tamper-proof: an administrator of the server can change its files or clock.
/// </summary>
public sealed class LicenseService(LicenseSettings settings)
{
    public const string ProductName = "HART LIMS";
    public const int SchemaVersion = 1;
    public const int MaximumLicenseFileBytes = 16 * 1024;
    private const string LicenseFileName = "license.json";

    /// <summary>Same key pair as HART POS (the generator signs both products).</summary>
    public const string BuiltInPublicKeyXml = "<RSAKeyValue><Modulus>yO4/1soi0MytcS5YZ0N15kpJihoy4MHl7lgK3yLwu9ByB/b61UwgBOTnuCxpUjuuO9yOehoBFbb9xpS8Mwd8oqkhCn+ksKG7BzQ8iiDVq9OXRx3rBuWnUboVheZfEgji3VxlPJee6DJ3gO/msVIgYR6V7TJK3Xp90EXYOa8PrCjOvSUtO3PQDy+pvYS/YonHgGsBwtx6tQvHXU5Bds0b16pNW24NBeWMSFmf1s78VtUFfF6b0A7hgg85F2WwZBKyp8s98tFsAL1IUGrMtr1X1rpQ+dg437ivqj7AZJDqKEltZbPF0BLXrZSue2EMMCcPxLbQLq4+ucnX9EvkLTdmkbnmOB3x5yQxVSpoEkLR5Q3tq61/YJldiwe/+b22YiNmnC05IxpleBGrFlJ8TNd4D+w0LjiNfxm6HEsAuaO0NJlj80EeTtc2s1ZM/jZEcI8UiMib1IRAfG8f8q3vIekm++YcHcthOJ7skWAFZotAdOnIsv3Nx4gGDxvw6A+jr65B</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly object _installLock = new();
    private (LicenseStatus Status, DateTime AtUtc)? _cached;

    public bool IsEnforced => settings.Enforce;

    /// <summary>The licence state, reused for <see cref="LicenseSettings.CacheFor"/> (the middleware asks on every API call).</summary>
    public LicenseStatus GetStatus(bool fresh = false)
    {
        var cached = _cached;
        if (!fresh && cached is { } c && DateTime.UtcNow - c.AtUtc < settings.CacheFor) return c.Status;
        var status = Check();
        _cached = (status, DateTime.UtcNow);
        return status;
    }

    private LicenseStatus Check()
    {
        if (!TryGetInstallationId(out var installationId, out var error))
            return Invalid("identity", "", "Could not read the server identity: " + error);
        if (!settings.Enforce)
            return new LicenseStatus(true, "not_enforced", installationId, null, null, null, "License checks are off in this environment.");

        byte[] bytes;
        try
        {
            using var stream = new FileStream(LicensePath(), FileMode.Open, FileAccess.Read, FileShare.Read);
            bytes = ReadBounded(stream);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Invalid("missing", installationId, "No license is installed on this server.");
        }
        catch (InvalidDataException)
        {
            return Invalid("too_large", installationId, "The license file exceeds the 16 KB limit.");
        }
        catch (Exception ex) when (IsFileProblem(ex) || ex is InvalidOperationException)
        {
            return Invalid("folder", installationId, "The server cannot read the license folder. Check its location and permissions.");
        }
        return Validate(bytes, installationId);
    }

    /// <summary>Verifies the uploaded file and, when it is valid for this server, replaces the installed licence with it.</summary>
    public LicenseStatus Install(Stream upload)
    {
        if (!TryGetInstallationId(out var installationId, out var error))
            return Invalid("identity", "", "Could not read the server identity: " + error);

        byte[] bytes;
        try { bytes = ReadBounded(upload); }
        catch (InvalidDataException) { return Invalid("too_large", installationId, "The license file exceeds the 16 KB limit."); }

        var status = Validate(bytes, installationId);
        if (!status.IsValid) return status;
        try
        {
            WriteAtomically(LicensePath(), bytes);
        }
        catch (Exception ex) when (IsFileProblem(ex) || ex is InvalidOperationException)
        {
            return Invalid("save_failed", installationId, "The license is valid, but the server could not save it. Check the license folder permissions.");
        }
        _cached = null;
        return GetStatus(fresh: true);
    }

    /// <summary>
    /// SHA-256 of "HART LIMS|" + the Windows machine GUID, as 64 upper-case hex characters. Differs from the POS ID of the same
    /// server, so a POS licence never unlocks LIMS. Reinstalling Windows or moving to another server changes it.
    /// </summary>
    public static bool TryGetInstallationId(out string installationId, out string? error)
    {
        installationId = "";
        error = null;
        if (!OperatingSystem.IsWindows())
        {
            error = "Licensing needs a Windows server.";
            return false;
        }
        try
        {
            var view = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32;
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var crypto = machine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", false);
            if (crypto?.GetValue("MachineGuid") is not string guid || string.IsNullOrWhiteSpace(guid))
            {
                error = "The Windows machine identity is unavailable.";
                return false;
            }
            installationId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ProductName + "|" + guid.Trim().ToLowerInvariant())));
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            error = "Windows did not allow the server to read the machine identity.";
            return false;
        }
    }

    private LicenseStatus Validate(byte[] bytes, string installationId)
    {
        if (bytes.Length == 0) return Invalid("format", installationId, "The license file is empty.");
        try
        {
            using var envelope = JsonDocument.Parse(StrictUtf8.GetString(bytes), new JsonDocumentOptions { MaxDepth = 8 });
            var root = envelope.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("payload", out var p) || p.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("signature", out var s) || s.ValueKind != JsonValueKind.String)
                return Invalid("format", installationId, "The license file has an invalid format.");

            var payloadBytes = Convert.FromBase64String(p.GetString()!);
            var signature = Convert.FromBase64String(s.GetString()!);
            if (payloadBytes.Length == 0 || payloadBytes.Length > MaximumLicenseFileBytes)
                return Invalid("format", installationId, "The license payload has an invalid size.");
            if (!VerifySignature(payloadBytes, signature))
                return Invalid("signature", installationId, "The license signature is not valid.");

            using var doc = JsonDocument.Parse(StrictUtf8.GetString(payloadBytes), new JsonDocumentOptions { MaxDepth = 8 });
            var payload = doc.RootElement;
            if (payload.ValueKind != JsonValueKind.Object
                || !payload.TryGetProperty("schemaVersion", out var v) || v.ValueKind != JsonValueKind.Number
                || !v.TryGetInt32(out var version) || version != SchemaVersion)
                return Invalid("unsupported", installationId, "This license format is not supported.");

            var product = Str(payload, "product");
            var customer = Str(payload, "customerName");
            var licenseId = Str(payload, "licenseId");
            var licensedId = Str(payload, "installationId");
            var issued = Str(payload, "issuedUtc");
            if (!string.Equals(product, ProductName, StringComparison.Ordinal))
                return Invalid("product", installationId, "This license is for another product (" + (product ?? "none") + "), not HART LIMS.");
            if (string.IsNullOrWhiteSpace(customer) || string.IsNullOrWhiteSpace(licenseId) || string.IsNullOrWhiteSpace(issued))
                return Invalid("format", installationId, "The license is missing its customer details.");
            if (!string.Equals(licensedId, installationId, StringComparison.OrdinalIgnoreCase))
                return Invalid("server", installationId, "This license was issued for a different server.");
            if (!DateTime.TryParseExact(issued, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var issuedAt) || issuedAt.Kind != DateTimeKind.Utc)
                return Invalid("issued", installationId, "The license issue date is invalid.");

            DateTime? expires = null;
            if (payload.TryGetProperty("expiresOnUtc", out var e) && e.ValueKind != JsonValueKind.Null)
            {
                if (e.ValueKind != JsonValueKind.String
                    || !DateTime.TryParseExact(e.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                    return Invalid("format", installationId, "The license expiry date is invalid.");
                expires = DateTime.SpecifyKind(day.Date, DateTimeKind.Utc);
                // Valid through the whole expiry day (UTC).
                if (DateTime.UtcNow.Date > expires.Value)
                    return new LicenseStatus(false, "expired", installationId, customer, licenseId, expires, "This license expired on " + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " (UTC).");
            }

            return new LicenseStatus(true, "ok", installationId, customer, licenseId, expires,
                expires is { } x ? "License is valid through " + x.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " (UTC)." : "Perpetual license is valid.");
        }
        catch (Exception ex) when (ex is JsonException or FormatException or DecoderFallbackException or ArgumentException or CryptographicException)
        {
            return Invalid("format", installationId, "The license file is malformed or could not be verified.");
        }
    }

    private bool VerifySignature(byte[] payload, byte[] signature)
    {
        var root = XDocument.Parse(settings.PublicKeyXml).Element("RSAKeyValue") ?? throw new CryptographicException("The license public key is invalid.");
        var key = new RSAParameters
        {
            Modulus = Convert.FromBase64String(root.Element("Modulus")?.Value ?? ""),
            Exponent = Convert.FromBase64String(root.Element("Exponent")?.Value ?? ""),
        };
        if (key.Modulus.Length * 8 != 3072) throw new CryptographicException("The license public key must be 3072 bits.");
        if (signature.Length != key.Modulus.Length) return false;
        using var rsa = RSA.Create();
        rsa.ImportParameters(key);
        return rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private static string? Str(JsonElement o, string name) => o.TryGetProperty(name, out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;

    private static LicenseStatus Invalid(string reason, string installationId, string message) => new(false, reason, installationId, null, null, null, message);

    /// <summary>license.json in the configured folder, refusing any folder inside the application (the web server could serve it).</summary>
    private string LicensePath()
    {
        var dir = string.IsNullOrWhiteSpace(settings.LicenseDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "HARTLIMS")
            : Environment.ExpandEnvironmentVariables(settings.LicenseDirectory.Trim());
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir));
        var app = Path.TrimEndingDirectorySeparator(Path.GetFullPath(settings.ContentRoot));
        if (string.Equals(full, app, StringComparison.OrdinalIgnoreCase) || full.StartsWith(app + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The license folder must be outside the application folder.");
        return Path.Combine(full, LicenseFileName);
    }

    private static byte[] ReadBounded(Stream stream)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > MaximumLicenseFileBytes) throw new InvalidDataException("The license file exceeds the 16 KB limit.");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private void WriteAtomically(string path, byte[] bytes)
    {
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var temp = Path.Combine(dir, "license-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var s = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                s.Write(bytes);
                s.Flush(true);
            }
            lock (_installLock)
            {
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static bool IsFileProblem(Exception ex) => ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException;
}
