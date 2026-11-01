using System;
using System.IO;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class ScreenshotLibraryTests
{
    [Fact]
    public void Parse_ReadsGameStampFrameAndNativeSuffix()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-shots-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "Gex (USA)-20261101-120000-f001234.png");
            File.WriteAllBytes(path, [1, 2, 3]);

            ScreenshotEntry entry = ScreenshotLibrary.Parse(path);

            Assert.Equal("Gex (USA)", entry.GameName);
            Assert.Equal(1234, entry.FrameNumber);
            Assert.False(entry.Native);
            Assert.Equal(new DateTime(2026, 11, 1, 12, 0, 0), entry.CapturedAt.DateTime);

            var native = Path.Combine(directory, "Gex (USA)-20261101-120000-f001234-native.png");
            File.WriteAllBytes(native, [1]);
            ScreenshotEntry nativeEntry = ScreenshotLibrary.Parse(native);
            Assert.True(nativeEntry.Native);
            Assert.Equal("Gex (USA)", nativeEntry.GameName);
            Assert.Equal(1234, nativeEntry.FrameNumber);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void List_SortsNewestFirstAndKeepsUnparsedNames()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-shots-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string old = Path.Combine(directory, "Road Rash (USA)-20261031-090000-f000010.png");
            string recent = Path.Combine(directory, "Gex (USA)-20261101-120000-f001234.png");
            string manual = Path.Combine(directory, "Manual Screenshot.png");
            File.WriteAllBytes(old, [1]);
            File.WriteAllBytes(recent, [1]);
            File.WriteAllBytes(manual, [1]);
            File.SetLastWriteTimeUtc(old, new DateTime(2026, 10, 31, 9, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(recent, new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(manual, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

            var entries = ScreenshotLibrary.List(directory);

            Assert.Equal(3, entries.Count);
            Assert.Equal("Gex (USA)", entries[0].GameName);
            Assert.Equal("Road Rash (USA)", entries[1].GameName);
            Assert.Equal("Manual Screenshot", entries[2].GameName);
            Assert.Equal(-1, entries[2].FrameNumber);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Delete_RemovesTheFileAndListHandlesMissingDirectories()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-shots-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "Gex (USA)-20261101-120000-f000001.png");
            File.WriteAllBytes(path, [1, 2]);

            Assert.True(ScreenshotLibrary.Delete(path));
            Assert.False(ScreenshotLibrary.Delete(path));
            Assert.False(File.Exists(path));
            Assert.Empty(ScreenshotLibrary.List(Path.Combine(directory, "missing")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
