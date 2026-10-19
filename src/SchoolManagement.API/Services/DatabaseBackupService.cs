using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.API.Services;

public record BackupSchedule(bool Enabled = false, string SchoolTime = "18:00");
public record BackupEntry(Guid Id, DateTime CreatedUtc, string Kind, string Actor, string Status,
    string Database, string Server, string[] Migrations, long Bytes = 0, string Sha256 = "", string? Error = null);
public record RestoreEntry(Guid Id, Guid BackupId, Guid? SafetyBackupId, DateTime CreatedUtc, string Actor, string Status, string Reason);

// Files and manifests deliberately live outside the database being recovered.
public sealed class DatabaseBackupService
{
    private readonly string root;
    private readonly string connectionString;
    private readonly IServiceScopeFactory scopes;
    private readonly ILogger<DatabaseBackupService> logger;
    private readonly IConfiguration configuration;
    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile bool restoring;
    public bool IsRestoring => restoring;
    public string StorageDirectory => root;
    public DatabaseBackupService(IConfiguration config, IWebHostEnvironment environment, IServiceScopeFactory scopes, ILogger<DatabaseBackupService> logger)
    {
        this.scopes = scopes; this.logger = logger; configuration = config;
        connectionString = config.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Database is not configured.");
        var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        if (string.IsNullOrWhiteSpace(database) || new[] { "master", "model", "msdb", "tempdb" }.Contains(database, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Backup and restore requires a school database, not a system database.");
        root = Path.GetFullPath(config["Backups:Directory"] ?? Path.Combine(environment.ContentRootPath, "..", "..", ".local-data", "backups"));
        if (environment.WebRootPath != null && root.StartsWith(Path.GetFullPath(environment.WebRootPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Backups must be outside the public web directory.");
    }
    private string EntryPath(Guid id) => Path.Combine(root, id.ToString("N") + ".json");
    public string BackupPath(Guid id) => Path.Combine(root, id.ToString("N") + ".bak");
    private async Task Write<T>(string path, T value)
    {
        Directory.CreateDirectory(root);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(value));
        File.Move(temp, path, true);
    }
    public async Task<BackupSchedule> Schedule()
    {
        var path = Path.Combine(root, "schedule.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<BackupSchedule>(await File.ReadAllTextAsync(path))! : new();
    }
    public async Task SaveSchedule(BackupSchedule schedule)
    {
        if (!TimeOnly.TryParseExact(schedule.SchoolTime, "HH:mm", out _)) throw new ArgumentException("Choose a valid daily time.");
        await gate.WaitAsync();
        try { await Write(Path.Combine(root, "schedule.json"), schedule); }
        finally { gate.Release(); }
    }
    public async Task<List<BackupEntry>> History()
    {
        if (!Directory.Exists(root)) return [];
        var result = new List<BackupEntry>();
        foreach (var file in Directory.EnumerateFiles(root, "*.json"))
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out _)) continue;
            result.Add(JsonSerializer.Deserialize<BackupEntry>(await File.ReadAllTextAsync(file))!);
        }
        return result.OrderByDescending(x => x.CreatedUtc).ToList();
    }
    public async Task<List<RestoreEntry>> Restores()
    {
        if (!Directory.Exists(root)) return [];
        var result = new List<RestoreEntry>();
        foreach (var file in Directory.EnumerateFiles(root, "restore-*.json")) result.Add(JsonSerializer.Deserialize<RestoreEntry>(await File.ReadAllTextAsync(file))!);
        return result.OrderByDescending(x => x.CreatedUtc).ToList();
    }
    private SqlConnection Master()
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master", Pooling = false };
        return new SqlConnection(builder.ConnectionString);
    }
    private string Database => new SqlConnectionStringBuilder(connectionString).InitialCatalog;
    private string Server => new SqlConnectionStringBuilder(connectionString).DataSource;
    private string QuotedDatabase => "[" + Database.Replace("]", "]]") + "]";
    private async Task<string> TargetIdentity()
    {
        // LocalDB pipe addresses change on restart. The SQL host and data-file location do not.
        await using var master = Master(); await master.OpenAsync();
        await using var command = new SqlCommand("SELECT CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')) + N'|' + physical_name FROM sys.master_files WHERE database_id=DB_ID(@database) AND file_id=1;", master);
        command.Parameters.AddWithValue("@database", Database);
        return (string?)await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("The school database could not be identified.");
    }
    private async Task Sql(SqlConnection connection, string sql, string? path = null)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 600 };
        if (path != null) command.Parameters.AddWithValue("@file", path);
        await command.ExecuteNonQueryAsync();
    }
    private async Task<string[]> Migrations()
    {
        using var scope = scopes.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.GetAppliedMigrationsAsync()).ToArray();
    }
    public async Task<BackupEntry> Create(string actor, string kind = "Manual")
    {
        if (!await gate.WaitAsync(0)) throw new InvalidOperationException("Another backup or restore is in progress. Try again after it finishes.");
        try { return await CreateCore(actor, kind); }
        finally { gate.Release(); }
    }
    private async Task<BackupEntry> CreateCore(string actor, string kind)
    {
        Directory.CreateDirectory(root);
        var entry = new BackupEntry(Guid.NewGuid(), DateTime.UtcNow, kind, actor, "Running", Database, Server, []);
        await Write(EntryPath(entry.Id), entry);
        try {
            entry = entry with { Migrations = await Migrations(), Server = await TargetIdentity() };
            await using var master = Master(); await master.OpenAsync();
            await Sql(master, $"BACKUP DATABASE {QuotedDatabase} TO DISK=@file WITH COPY_ONLY, CHECKSUM, INIT;", BackupPath(entry.Id));
            await Sql(master, "RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM;", BackupPath(entry.Id));
            await using var stream = File.OpenRead(BackupPath(entry.Id));
            entry = entry with { Status = "Verified", Bytes = stream.Length, Sha256 = Convert.ToHexString(await SHA256.HashDataAsync(stream)) };
            await Write(EntryPath(entry.Id), entry);
            return entry;
        } catch (Exception error) {
            logger.LogError(error, "Database backup {BackupId} failed", entry.Id);
            await Write(EntryPath(entry.Id), entry with { Status = "Failed", Error = "Backup failed. Check server logs, available disk space and SQL Server backup-folder permissions." });
            throw new InvalidOperationException("Backup failed. Check server logs, disk space and SQL Server backup-folder permissions.");
        }
    }
    public async Task<BackupEntry> VerifiedEntry(Guid id)
    {
        if (!File.Exists(EntryPath(id))) throw new ArgumentException("Backup not found.");
        var entry = JsonSerializer.Deserialize<BackupEntry>(await File.ReadAllTextAsync(EntryPath(id)))!;
        if (entry.Status != "Verified" || entry.Database != Database || entry.Server != await TargetIdentity() || !File.Exists(BackupPath(id))) throw new ArgumentException("This backup is unavailable or belongs to another database/server.");
        await using var stream = File.OpenRead(BackupPath(id));
        if (entry.Sha256 != Convert.ToHexString(await SHA256.HashDataAsync(stream))) throw new ArgumentException("Backup verification failed. Do not restore this file.");
        return entry;
    }
    public async Task<RestoreEntry> Restore(Guid id, string actor, string reason)
    {
        if (!await gate.WaitAsync(0)) throw new InvalidOperationException("Another backup or restore is in progress.");
        var log = new RestoreEntry(Guid.NewGuid(), id, null, DateTime.UtcNow, actor, "Preparing", reason);
        try {
            var entry = await VerifiedEntry(id);
            var currentMigrations = await Migrations();
            if (!entry.Migrations.SequenceEqual(currentMigrations)) throw new ArgumentException("This backup uses a different database version. Ask your administrator to restore it with the matching application version.");
            await using var master = Master(); await master.OpenAsync();
            await Sql(master, "RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM;", BackupPath(id));
            restoring = true;
            var safety = await CreateCore(actor, "Before restore");
            log = log with { SafetyBackupId = safety.Id, Status = "Restoring" };
            await Write(Path.Combine(root, $"restore-{log.Id:N}.json"), log);
            SqlConnection.ClearAllPools();
            try {
                // One master connection retains the single-user slot for the restore batch.
                await Sql(master, $"ALTER DATABASE {QuotedDatabase} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE {QuotedDatabase} FROM DISK=@file WITH REPLACE, CHECKSUM, RECOVERY; ALTER DATABASE {QuotedDatabase} SET MULTI_USER;", BackupPath(id));
            } finally {
                await Sql(master, $"ALTER DATABASE {QuotedDatabase} SET MULTI_USER WITH ROLLBACK IMMEDIATE;");
                SqlConnection.ClearAllPools();
            }
            await Sql(master, $"UPDATE {QuotedDatabase}.dbo.AspNetUsers SET SecurityStamp=CONVERT(nvarchar(36),NEWID());");
            using (var scope = scopes.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var profile = await db.SchoolProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1);
                var zone = profile != null ? TimeZoneInfo.FindSystemTimeZoneById(profile.TimeZoneId) : SchoolClock.DefaultZone;
                SchoolClock.SetZone(zone);
                configuration["School:TimeZoneId"] = zone.Id;
            }
            log = log with { Status = "Completed" };
            await Write(Path.Combine(root, $"restore-{log.Id:N}.json"), log);
            return log;
        } catch (Exception error) {
            logger.LogError(error, "Database restore {RestoreId} failed", log.Id);
            await Write(Path.Combine(root, $"restore-{log.Id:N}.json"), log with { Status = "Failed" });
            throw;
        } finally { restoring = false; gate.Release(); }
    }
    public async Task RunScheduled()
    {
        var schedule = await Schedule();
        if (!schedule.Enabled || !TimeOnly.TryParseExact(schedule.SchoolTime, "HH:mm", out var time)) return;
        var now = SchoolClock.Now;
        if (TimeOnly.FromDateTime(now) < time) return;
        var history = await History();
        if (history.Any(x => x.Kind == "Automatic" && TimeZoneInfo.ConvertTimeFromUtc(x.CreatedUtc, SchoolClock.Zone).Date == now.Date)) return;
        await Create("Automatic schedule", "Automatic");
    }
}

public sealed class DatabaseBackupWorker(DatabaseBackupService backups, ILogger<DatabaseBackupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do {
            try { await backups.RunScheduled(); }
            catch (Exception error) { logger.LogError(error, "Automatic database backup failed; review Backup and Restore history."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
