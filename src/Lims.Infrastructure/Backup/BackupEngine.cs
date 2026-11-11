using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lims.Application.Backup;
using Lims.Application.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Lims.Infrastructure.Backup;

/// <summary>
/// Makes the database backups from inside the application (no scheduled tasks, no PowerShell, no administrator rights):
/// full and transaction-log backups with CHECKSUM, retention and an optional verified copy to a second folder.
/// SQL Server writes the files, so its service gets Modify on the folder (<see cref="BackupFolderAccess"/>).
/// The database login needs db_backupoperator (Grant-LimsAppPermissions.sql). Results go to backup-status.json in the
/// backup folder, in the same format the scripts in db/scripts use, so the status page and the scripts agree.
/// </summary>
public sealed class BackupEngine(IConfiguration config, BackupSettingsStore store, ILogger<BackupEngine> log)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly object StatusSync = new();
    private const string LockName = "LIMS-backup";

    public static string JobName(BackupJob job) => job.ToString();

    /// <summary>Runs one job. Throws <see cref="BusinessRuleException"/> with a clear reason when it fails (after recording the failure).</summary>
    public async Task<BackupActionResult> RunAsync(BackupJob job, BackupSettings settings, CancellationToken ct, bool waitIfBusy = false, string? folderOverride = null)
    {
        var folder = folderOverride ?? settings.Folder;
        if (!await Gate.WaitAsync(waitIfBusy ? Timeout.InfiniteTimeSpan : TimeSpan.Zero, ct))
            throw new BusinessRuleException("backup_busy", "A backup is already running. Try again in a few minutes.", 409);
        try
        {
            var result = job switch
            {
                BackupJob.Full => await BackupAsync(BackupKind.Full, settings, folder, waitIfBusy, ct),
                BackupJob.Log => await BackupAsync(BackupKind.Log, settings, folder, waitIfBusy, ct),
                BackupJob.Retention => Retention(settings, folder),
                BackupJob.Offsite => await OffsiteAsync(settings, folder, ct),
                _ => throw new ArgumentOutOfRangeException(nameof(job)),
            };
            WriteStatus(folder, JobName(job), true, result.Message);
            return result;
        }
        catch (BusinessRuleException ex) when (ex.Code != "backup_busy")
        {
            WriteStatus(folder, JobName(job), false, ex.Message);
            log.LogError("Backup job {Job} failed: {Reason}", job, ex.Message);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not BusinessRuleException)
        {
            WriteStatus(folder, JobName(job), false, ex.Message);
            log.LogError(ex, "Backup job {Job} failed", job);
            throw new BusinessRuleException("backup_failed", ex.Message);
        }
        finally { Gate.Release(); }
    }

    private SqlConnection Connection()
    {
        var cs = config.GetConnectionString("Lims") ?? throw new InvalidOperationException("ConnectionStrings:Lims is not set.");
        var b = new SqlConnectionStringBuilder(cs) { ApplicationName = "LIMS-backup" };
        if (config["Backup:Database"] is { Length: > 0 } d) b.InitialCatalog = d;
        return new SqlConnection(b.ConnectionString);
    }

    /// <summary>Opens a connection and creates/permissions the folder; returns the SQL service account for messages.</summary>
    public async Task<BackupFolderAccess.Result> PrepareFolderAsync(string folder, CancellationToken ct)
    {
        await using var c = Connection();
        await c.OpenAsync(ct);
        var (isLocal, accounts) = await BackupFolderAccess.SqlServiceAccountsAsync(c, ct);
        return BackupFolderAccess.Prepare(folder, isLocal, accounts);
    }

    private async Task<BackupActionResult> BackupAsync(BackupKind kind, BackupSettings settings, string folder, bool waitForLock, CancellationToken ct)
    {
        await using var c = Connection();
        await c.OpenAsync(ct);
        var database = c.Database;
        var (isLocal, accounts) = await BackupFolderAccess.SqlServiceAccountsAsync(c, ct);
        var access = BackupFolderAccess.Prepare(folder, isLocal, accounts);
        if (!access.Ok) throw new BusinessRuleException("backup_folder_create_failed", access.Note ?? $"The folder {folder} could not be created.");

        // One backup at a time across processes too (IIS overlapping recycle, the deployment's backup).
        await using (var lockCmd = c.CreateCommand())
        {
            lockCmd.CommandText = "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @name, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = @timeout; SELECT @r;";
            lockCmd.Parameters.AddWithValue("@name", LockName);
            lockCmd.Parameters.AddWithValue("@timeout", waitForLock ? 30 * 60 * 1000 : 0);
            lockCmd.CommandTimeout = 0;
            if (Convert.ToInt32(await lockCmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture) < 0)
                throw new BusinessRuleException("backup_busy", "A backup is already running. Try again in a few minutes.", 409);
        }

        if (kind == BackupKind.Log)
        {
            var recovery = await ScalarAsync<string>(c, "SELECT recovery_model_desc FROM sys.databases WHERE database_id = DB_ID()", ct);
            if (recovery == "SIMPLE")
            {
                // Point-in-time recovery needs the FULL recovery model. Switch it if this login may; then a full backup starts the chain.
                try
                {
                    await ExecAsync(c, $"ALTER DATABASE {Quote(database)} SET RECOVERY FULL", ct);
                    log.LogWarning("Recovery model of {Database} switched from SIMPLE to FULL for log backups", database);
                }
                catch (SqlException)
                {
                    throw new BusinessRuleException("backup_log_needs_full_recovery",
                        $"Log backups need the FULL recovery model, and this login may not change it. As a SQL administrator run: ALTER DATABASE [{database}] SET RECOVERY FULL. Or set the log interval to 0.");
                }
                var full = await BackupFileAsync(c, BackupKind.Full, database, folder, accounts, ct);
                WriteStatus(folder, JobName(BackupJob.Full), true, full.Message);
                return new BackupActionResult(true, $"Recovery model switched to FULL; full backup {full.Message}", full.File);
            }
            try
            {
                return await BackupFileAsync(c, BackupKind.Log, database, folder, accounts, ct);
            }
            catch (SqlException ex) when (ex.Errors.Cast<SqlError>().Any(e => e.Number == 4214))
            {
                // No full backup since the log chain was broken (new database, recovery model changed): take one, then go on.
                var full = await BackupFileAsync(c, BackupKind.Full, database, folder, accounts, ct);
                WriteStatus(folder, JobName(BackupJob.Full), true, full.Message);
                return await BackupFileAsync(c, BackupKind.Log, database, folder, accounts, ct);
            }
        }
        return await BackupFileAsync(c, BackupKind.Full, database, folder, accounts, ct);
    }

    private async Task<BackupActionResult> BackupFileAsync(SqlConnection c, BackupKind kind, string database, string folder, IReadOnlyList<string> sqlAccounts, CancellationToken ct)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var file = Path.Combine(folder, $"{database}_{stamp}_{(kind == BackupKind.Log ? "LOG.trn" : "FULL.bak")}");
        var verb = kind == BackupKind.Log ? "LOG" : "DATABASE";
        var options = kind == BackupKind.Log ? "CHECKSUM" : "CHECKSUM, INIT";

        async Task Run(bool compress)
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandTimeout = 0;
            cmd.CommandText = $"BACKUP {verb} @db TO DISK = @file WITH {options}{(compress ? ", COMPRESSION" : "")}, NAME = @name";
            cmd.Parameters.AddWithValue("@db", database);
            cmd.Parameters.AddWithValue("@file", file);
            cmd.Parameters.AddWithValue("@name", $"{database} {kind} backup {stamp} (LIMS)");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        try
        {
            try { await Run(compress: true); }
            catch (SqlException ex) when (ex.Errors.Cast<SqlError>().Any(e => e.Number == 1844))
            {
                await Run(compress: false);   // Express and some editions cannot compress backups
            }
        }
        catch (SqlException ex) when (!ex.Errors.Cast<SqlError>().Any(e => e.Number == 4214))
        {
            throw Explain(ex, folder, database, sqlAccounts);
        }

        var verified = await VerifyAsync(c, file, ct);
        long size = 0;
        try { size = new FileInfo(file).Length; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        var message = $"{Path.GetFileName(file)} ({Math.Round(size / 1048576.0, 1).ToString(CultureInfo.InvariantCulture)} MB){(verified ? ", verified" : "")}";
        log.LogInformation("{Kind} backup of {Database} written: {File}", kind, database, Path.GetFileName(file));
        return new BackupActionResult(true, message, Path.GetFileName(file));
    }

    /// <summary>RESTORE VERIFYONLY needs a right the application login does not have; then the CHECKSUM taken during the backup has to do.</summary>
    private static async Task<bool> VerifyAsync(SqlConnection c, string file, CancellationToken ct)
    {
        try
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandTimeout = 0;
            cmd.CommandText = "RESTORE VERIFYONLY FROM DISK = @file WITH CHECKSUM";
            cmd.Parameters.AddWithValue("@file", file);
            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }
        catch (SqlException ex) when (ex.Errors.Cast<SqlError>().Any(e => e.Number is 262 or 229 or 297 or 300 or 916 or 15247))
        {
            return false;
        }
    }

    private static BusinessRuleException Explain(SqlException ex, string folder, string database, IReadOnlyList<string> sqlAccounts)
    {
        var text = string.Join(" ", ex.Errors.Cast<SqlError>().Select(e => e.Message));
        if (text.Contains("Operating system error 5", StringComparison.OrdinalIgnoreCase) || text.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
        {
            var account = sqlAccounts.FirstOrDefault() ?? @"NT SERVICE\MSSQLSERVER";
            return new BusinessRuleException("backup_folder_denied",
                $"SQL Server ({account}) may not write to {folder}. Choose a folder the server created (for example under the LIMS data folder), or as administrator run: icacls \"{folder}\" /grant \"{account}:(OI)(CI)M\"");
        }
        if (text.Contains("Operating system error 3", StringComparison.OrdinalIgnoreCase))
            return new BusinessRuleException("backup_folder_missing", $"SQL Server cannot find the folder {folder}. If SQL Server runs on another computer, the folder must exist there.");
        if (ex.Errors.Cast<SqlError>().Any(e => e.Number is 262 or 229 or 916 or 3110))
            return new BusinessRuleException("backup_db_permission",
                $"The application's database login may not back up [{database}]. Run db\\scripts\\Grant-LimsAppPermissions.sql (Deploy-Lims.ps1 does this), which adds it to db_backupoperator.");
        return new BusinessRuleException("backup_failed", text);
    }

    private BackupActionResult Retention(BackupSettings settings, string folder)
    {
        var database = store.Database;
        var deleted = DeleteOld(folder, database, settings.RetentionDays);
        if (!string.IsNullOrWhiteSpace(settings.OffsiteFolder) && Directory.Exists(settings.OffsiteFolder))
            deleted += DeleteOld(settings.OffsiteFolder, database, settings.RetentionDays);
        return new BackupActionResult(true, deleted == 0 ? "Nothing to remove" : $"{deleted} old backup file(s) removed");
    }

    private static int DeleteOld(string folder, string database, int retentionDays)
    {
        if (!Directory.Exists(folder)) return 0;
        var files = ListBackups(folder, database);
        var count = 0;
        foreach (var f in BackupSchedule.ToDelete(DateTime.Now, files, retentionDays))
        {
            File.Delete(Path.Combine(folder, f.Name));
            count++;
        }
        return count;
    }

    public static List<BackupFile> ListBackups(string folder, string database) =>
        new DirectoryInfo(folder).EnumerateFiles().Take(20_000)
            .Select(f => BackupRules.ParseFileName(database, f.Name, f.Length)).Where(f => f is not null).Select(f => f!).ToList();

    /// <summary>Copies backup files the second folder does not have yet, and proves each copy by SHA-256.</summary>
    private async Task<BackupActionResult> OffsiteAsync(BackupSettings settings, string folder, CancellationToken ct)
    {
        var target = settings.OffsiteFolder;
        if (string.IsNullOrWhiteSpace(target)) return new BackupActionResult(true, "No offsite folder set");
        try { Directory.CreateDirectory(target); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BusinessRuleException("backup_offsite_unreachable", $"The offsite folder {target} cannot be reached: {ex.Message}");
        }
        var copied = 0;
        foreach (var f in ListBackups(folder, store.Database).OrderBy(f => f.StampLocal))
        {
            var source = Path.Combine(folder, f.Name);
            var dest = Path.Combine(target, f.Name);
            if (File.Exists(dest) && new FileInfo(dest).Length == f.SizeBytes) continue;
            var temp = dest + ".partial";
            try
            {
                await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write))
                    await input.CopyToAsync(output, ct);
                if (!(await HashAsync(source, ct)).SequenceEqual(await HashAsync(temp, ct)))
                    throw new IOException($"The copy of {f.Name} does not match the original.");
                File.Move(temp, dest, overwrite: true);
                copied++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                try { File.Delete(temp); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
                throw new BusinessRuleException("backup_offsite_unreachable", $"Copying {f.Name} to {target} failed: {ex.Message}");
            }
        }
        return new BackupActionResult(true, copied == 0 ? "Offsite copy up to date" : $"{copied} file(s) copied and checked");
    }

    private static async Task<byte[]> HashAsync(string path, CancellationToken ct)
    {
        await using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await SHA256.HashDataAsync(s, ct);
    }

    /// <summary>Records the last success/failure of a job in backup-status.json (same format as LimsBackup.Common.ps1).</summary>
    public void WriteStatus(string folder, string job, bool ok, string detail)
    {
        lock (StatusSync)
        {
            try
            {
                if (!Directory.Exists(folder)) return;
                var path = Path.Combine(folder, BackupRules.StatusFileName);
                JsonObject root;
                try { root = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject() : new JsonObject(); }
                catch (JsonException) { root = new JsonObject(); }
                var entry = root[job] as JsonObject ?? new JsonObject();
                var now = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
                if (detail.Length > 1000) detail = detail[..1000];
                if (ok) { entry["lastSuccess"] = now; entry["lastDetail"] = detail; }
                else { entry["lastFailure"] = now; entry["lastFailureDetail"] = detail; }
                root[job] = entry;
                var temp = path + ".tmp";
                File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temp, path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.LogWarning("Could not update {File} in {Folder}: {Reason}", BackupRules.StatusFileName, folder, ex.Message);
            }
        }
    }

    /// <summary>What the scheduler needs: last success and failure per job, from the status file and the backup files themselves.</summary>
    public BackupState ReadState(string folder)
    {
        var failures = new Dictionary<BackupJob, DateTime>();
        DateTime? Success(BackupJob j, List<JobStatus> s) => s.FirstOrDefault(x => x.Name == JobName(j))?.LastSuccessLocal;
        List<JobStatus> status = [];
        List<BackupFile> files = [];
        try
        {
            var path = Path.Combine(folder, BackupRules.StatusFileName);
            if (File.Exists(path))
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(fs);
                status = BackupRules.ParseStatus(reader.ReadToEnd());
            }
            if (Directory.Exists(folder)) files = ListBackups(folder, store.Database);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        foreach (var j in Enum.GetValues<BackupJob>())
            if (status.FirstOrDefault(x => x.Name == JobName(j))?.LastFailureLocal is { } f) failures[j] = f;

        static DateTime? Max(DateTime? a, DateTime? b) => a is null ? b : b is null ? a : a > b ? a : b;
        var fullFile = files.Where(f => f.Kind == BackupKind.Full).Select(f => (DateTime?)f.StampLocal).DefaultIfEmpty().Max();
        var logFile = files.Where(f => f.Kind == BackupKind.Log).Select(f => (DateTime?)f.StampLocal).DefaultIfEmpty().Max();
        return new BackupState(
            Max(Success(BackupJob.Full, status), fullFile), Max(Success(BackupJob.Log, status), logFile),
            Success(BackupJob.Retention, status), Success(BackupJob.Offsite, status), failures);
    }

    private static string Quote(string name) => "[" + name.Replace("]", "]]") + "]";

    private static async Task ExecAsync(SqlConnection c, string sql, CancellationToken ct)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 0;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<T?> ScalarAsync<T>(SqlConnection c, string sql, CancellationToken ct)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        var v = await cmd.ExecuteScalarAsync(ct);
        return v is null or DBNull ? default : (T)v;
    }
}
