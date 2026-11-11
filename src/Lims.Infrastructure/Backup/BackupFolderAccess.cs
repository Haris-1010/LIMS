using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Data.SqlClient;

namespace Lims.Infrastructure.Backup;

/// <summary>
/// Makes a backup folder usable without manual steps: creates it and gives the SQL Server service (which writes the
/// backup files, not the application) Modify rights on it. The application can change the folder's permissions when it
/// created the folder itself (the creator owns it), or when it runs as an administrator (deployment, development).
/// </summary>
public static class BackupFolderAccess
{
    public sealed record Result(bool Ok, string? SqlServiceAccount, string? Note);

    /// <summary>The Windows accounts the SQL Server service runs as (the per-service SID, and the configured account if visible).</summary>
    public static async Task<(bool IsLocal, List<string> Accounts)> SqlServiceAccountsAsync(SqlConnection open, CancellationToken ct)
    {
        var accounts = new List<string>();
        string? machine = null, service = null;
        await using (var cmd = open.CreateCommand())
        {
            cmd.CommandText = "SELECT CAST(SERVERPROPERTY('MachineName') AS nvarchar(256)), @@SERVICENAME";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct)) { machine = r.IsDBNull(0) ? null : r.GetString(0); service = r.IsDBNull(1) ? null : r.GetString(1); }
        }
        if (!string.IsNullOrEmpty(service))
            accounts.Add(service.Equals("MSSQLSERVER", StringComparison.OrdinalIgnoreCase) ? @"NT SERVICE\MSSQLSERVER" : @"NT SERVICE\MSSQL$" + service);
        try
        {
            // Needs VIEW SERVER STATE; the per-service SID above is enough when this is not allowed.
            await using var cmd = open.CreateCommand();
            cmd.CommandText = "SELECT service_account FROM sys.dm_server_services WHERE servicename LIKE N'SQL Server (%'";
            if (await cmd.ExecuteScalarAsync(ct) is string acct && acct.Length > 0 && !accounts.Contains(acct, StringComparer.OrdinalIgnoreCase))
                accounts.Add(acct);
        }
        catch (SqlException) { }
        var isLocal = machine is null || machine.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);
        return (isLocal, accounts);
    }

    /// <summary>Creates <paramref name="folder"/> and grants the SQL Server service Modify on it. Never throws for permission problems; says what to do instead.</summary>
    public static Result Prepare(string folder, bool sqlIsLocal, IReadOnlyList<string> sqlAccounts)
    {
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new Result(false, sqlAccounts.FirstOrDefault(), $"The folder {folder} could not be created: {ex.Message}");
        }

        if (!OperatingSystem.IsWindows() || !sqlIsLocal || sqlAccounts.Count == 0)
            return new Result(true, sqlAccounts.FirstOrDefault(), sqlIsLocal ? null
                : "SQL Server runs on another computer: the folder must exist on that computer and be writable by its SQL Server service.");

        string? note = null;
        foreach (var account in sqlAccounts) note = Grant(folder, account) ?? note;
        return new Result(true, sqlAccounts.FirstOrDefault(), note);
    }

    /// <summary>Gives <paramref name="account"/> Modify on the folder (inherited by files). Returns what to do by hand if Windows refuses.</summary>
    [SupportedOSPlatform("windows")]
    private static string? Grant(string folder, string account)
    {
        SecurityIdentifier sid;
        try { sid = (SecurityIdentifier)new NTAccount(account).Translate(typeof(SecurityIdentifier)); }
        catch (Exception) { return null; }   // e.g. LocalSystem written differently; the service SID covers it
        if (sid.IsWellKnown(WellKnownSidType.LocalSystemSid)) return null;
        try
        {
            var dir = new DirectoryInfo(folder);
            var acl = dir.GetAccessControl();
            var has = acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>()
                .Any(r => r.IdentityReference == sid && r.AccessControlType == AccessControlType.Allow
                    && (r.FileSystemRights & FileSystemRights.Modify) == FileSystemRights.Modify
                    && r.InheritanceFlags.HasFlag(InheritanceFlags.ObjectInherit));
            if (has) return null;
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.Modify,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            dir.SetAccessControl(acl);
            return null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or IOException or PrivilegeNotHeldException)
        {
            return $"Could not give {account} write access to {folder} automatically. On the server, as administrator, run: icacls \"{folder}\" /grant \"{account}:(OI)(CI)M\"";
        }
    }
}
