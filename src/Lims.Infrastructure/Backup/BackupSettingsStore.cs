using System.Text.Json;
using Lims.Application.Backup;
using Lims.Application.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Lims.Infrastructure.Backup;

/// <summary>
/// Keeps the backup schedule in backup-settings.json in the server's data folder, so it survives restarts and updates.
/// Data folder: <c>Backup:DataFolder</c>, else the parent of <c>DataProtection:KeysPath</c> (D:\LimsData in production),
/// else App_Data under the content root. With no file yet, automatic backups are ON with safe defaults
/// (folder <c>Backup:Folder</c> or &lt;data folder&gt;\backups, full daily at 22:00, log every 15 minutes, 14 days).
/// </summary>
public sealed class BackupSettingsStore(IConfiguration config)
{
    private const string FileName = "backup-settings.json";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly object sync = new();

    public string DataFolder
    {
        get
        {
            if (config["Backup:DataFolder"] is { Length: > 0 } explicitFolder) return explicitFolder;
            if (config["DataProtection:KeysPath"] is { Length: > 0 } keys && Directory.GetParent(Path.GetFullPath(keys)) is { } parent) return parent.FullName;
            return Path.Combine(config["contentRoot"] is { Length: > 0 } root ? root : AppContext.BaseDirectory, "App_Data");
        }
    }

    public string SettingsPath => Path.Combine(DataFolder, FileName);

    public string DefaultFolder => config["Backup:Folder"] is { Length: > 0 } f ? f : Path.Combine(DataFolder, "backups");

    /// <summary>The database being backed up: <c>Backup:Database</c>, else the database in the connection string.</summary>
    public string Database
    {
        get
        {
            if (config["Backup:Database"] is { Length: > 0 } d) return d;
            var cs = config.GetConnectionString("Lims");
            var name = string.IsNullOrEmpty(cs) ? null : new SqlConnectionStringBuilder(cs).InitialCatalog;
            return string.IsNullOrEmpty(name) ? "LIMS" : name;
        }
    }

    /// <summary>False when <c>Backup:Automatic</c> is false (tests, or a server where IT runs backups another way).</summary>
    public bool AutomaticAllowed => !bool.TryParse(config["Backup:Automatic"], out var on) || on;

    public BackupSettings Load()
    {
        lock (sync)
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var saved = JsonSerializer.Deserialize<BackupSettings>(File.ReadAllText(SettingsPath), Json);
                    if (saved is not null && !string.IsNullOrWhiteSpace(saved.Folder)) return saved;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // A damaged or unreadable file must not stop backups: fall back to the defaults.
            }
            return new BackupSettings(true, DefaultFolder, null);
        }
    }

    public void Save(BackupSettings settings)
    {
        lock (sync)
        {
            try
            {
                Directory.CreateDirectory(DataFolder);
                var temp = SettingsPath + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
                File.Move(temp, SettingsPath, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new BusinessRuleException("backup_settings_unwritable",
                    $"The server could not save the backup settings in {DataFolder}: {ex.Message}");
            }
        }
    }
}
