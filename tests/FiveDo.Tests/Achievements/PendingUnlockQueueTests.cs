using System;
using System.IO;
using System.Text;
using FiveDo.Achievements;

namespace FiveDo.Tests.Achievements;

public sealed class PendingUnlockQueueTests
{
    [Fact]
    public void RoundTripsEntriesThroughDisk()
    {
        string directory = CreateDirectory();
        string path = Path.Combine(directory, "pending.json");
        try
        {
            var queue = new PendingUnlockQueue(path);
            var entry = new PendingUnlock(
                579967,
                1009,
                "f51d8d468bb19f621777fedd3e025365",
                "player",
                false,
                DateTimeOffset.UtcNow.AddSeconds(-5),
                "Unfinished Dance",
                1);

            Assert.True(queue.Add(entry));
            Assert.True(queue.Contains(579967, "player", hardcore: false));
            Assert.Equal(1, queue.Count);

            var reloaded = new PendingUnlockQueue(path);
            Assert.Equal(1, reloaded.Count);
            PendingUnlock loaded = Assert.Single(reloaded.Snapshot());
            Assert.Equal(entry.AchievementId, loaded.AchievementId);
            Assert.Equal(entry.GameHash, loaded.GameHash);
            Assert.Equal(entry.Username, loaded.Username);
            Assert.Equal(entry.Hardcore, loaded.Hardcore);
            Assert.Equal(entry.Title, loaded.Title);
            Assert.Equal(entry.Points, loaded.Points);

            Assert.True(reloaded.Remove(579967, "player", hardcore: false));
            Assert.Equal(0, reloaded.Count);
            Assert.Equal(0, new PendingUnlockQueue(path).Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DeduplicatesSameAchievementAndMode()
    {
        string directory = CreateDirectory();
        string path = Path.Combine(directory, "pending.json");
        try
        {
            var queue = new PendingUnlockQueue(path);
            PendingUnlock entry = Entry(hardcore: false);

            Assert.True(queue.Add(entry));
            Assert.False(queue.Add(entry));
            Assert.True(queue.Add(Entry(hardcore: true)));
            Assert.Equal(2, queue.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RejectsWarningAchievements()
    {
        string directory = CreateDirectory();
        string path = Path.Combine(directory, "pending.json");
        try
        {
            var queue = new PendingUnlockQueue(path);
            Assert.False(queue.Add(Entry(achievementId: 101000001, hardcore: false)));
            Assert.Equal(0, queue.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void QuarantinesInvalidChecksum()
    {
        string directory = CreateDirectory();
        string path = Path.Combine(directory, "pending.json");
        try
        {
            File.WriteAllText(
                path,
                """{"payload":"eyJlbnRyaWVzIjpbXX0=","sha256":"0000000000000000000000000000000000000000000000000000000000000000"}""",
                Encoding.UTF8);

            var queue = new PendingUnlockQueue(path);
            Assert.Equal(0, queue.Count);
            Assert.False(File.Exists(path));
            Assert.True(File.Exists(path + ".bad"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static PendingUnlock Entry(bool hardcore, uint achievementId = 579967) => new(
        achievementId,
        1009,
        "f51d8d468bb19f621777fedd3e025365",
        "player",
        hardcore,
        DateTimeOffset.UtcNow,
        "Title",
        1);

    private static string CreateDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "5do-ra-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
