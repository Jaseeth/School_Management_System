using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.API.Services;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.IntegrationTests;

public partial class TeacherAssignmentTests
{
    private sealed class BackupEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SchoolManagement.API";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = root;
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task DatabaseBackupRestoresOnlyItsIsolatedDatabaseAndKeepsSafetyHistory()
    {
        await using var db = await TestDatabase.CreateAsync();
        var root = Path.Combine(Path.GetTempPath(), "SchoolBackupTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:DefaultConnection"] = db.Context.Database.GetConnectionString(), ["Backups:Directory"] = root
            }).Build();
            var backups = new DatabaseBackupService(configuration, new BackupEnvironment(root), db.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<DatabaseBackupService>.Instance);
            var stampBefore = (await db.Users.FindByIdAsync(db.AdminUserId))!.SecurityStamp;
            var backup = await backups.Create("test-admin");
            Assert.Equal("Verified", backup.Status);
            Assert.True(backup.Bytes > 0);
            Assert.Equal(64, backup.Sha256.Length);
            db.Context.SchoolProfiles.Add(new SchoolProfile { Name = "After backup" }); await db.Context.SaveChangesAsync();
            Assert.Equal(1, await db.Context.SchoolProfiles.CountAsync());
            var restored = await backups.Restore(backup.Id, "test-admin", "Isolated restore verification");
            Assert.Equal("Completed", restored.Status);
            Assert.NotNull(restored.SafetyBackupId);
            db.Context.ChangeTracker.Clear();
            Assert.Empty(await db.Context.SchoolProfiles.ToListAsync());
            Assert.NotEqual(stampBefore, (await db.Users.FindByIdAsync(db.AdminUserId))!.SecurityStamp);
            Assert.Equal(2, (await backups.History()).Count);
            Assert.Single(await backups.Restores());
            Assert.False(backups.IsRestoring);
            await File.AppendAllTextAsync(backups.BackupPath(backup.Id), "tampered");
            await Assert.ThrowsAsync<ArgumentException>(() => backups.VerifiedEntry(backup.Id));
            await Assert.ThrowsAsync<ArgumentException>(() => backups.SaveSchedule(new(true, "invalid")));
            await backups.SaveSchedule(new(false, "18:00"));
            await backups.RunScheduled();
            Assert.Equal(2, (await backups.History()).Count);
            await backups.SaveSchedule(new(true, "00:00"));
            await backups.RunScheduled();
            await backups.RunScheduled();
            Assert.Equal(3, (await backups.History()).Count);
            Assert.Single(await backups.History(), x => x.Kind == "Automatic");
        } finally {
            // Only the unique directory created by this test is removed.
            var resolved = Path.GetFullPath(root);
            var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SchoolBackupTests")) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected test directory");
            Directory.Delete(resolved, true);
        }
    }
}
