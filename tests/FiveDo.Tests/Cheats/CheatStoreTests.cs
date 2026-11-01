using System;
using System.IO;
using FiveDo.Cheats;

namespace FiveDo.Tests.Cheats;

public sealed class CheatStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundtripsEntriesAndReportsInvalidCode()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cheats-{Guid.NewGuid():N}");
        var path = CheatStore.ResolvePath(directory, "ABCD1234");
        try
        {
            var document = new CheatDocument
            {
                GameId = "ABCD1234",
                Cheats =
                [
                    new CheatEntry
                    {
                        Name = "Infinite health",
                        Description = "Keeps health at 100.",
                        Code = "write8 0x2000 = 0x64",
                        Enabled = true,
                    },
                    new CheatEntry
                    {
                        Name = "Broken",
                        Code = "write8 0x2000 =",
                        Enabled = false,
                    },
                ],
            };

            CheatStore.Save(path, document);
            CheatDocument loaded = CheatStore.Load(path, "ABCD1234");
            CheatSet set = CheatSet.FromDocument(loaded);

            Assert.Equal(2, loaded.Cheats.Count);
            Assert.Equal("ABCD1234", loaded.GameId);
            Assert.Equal("Infinite health", loaded.Cheats[0].Name);
            Assert.Equal(2, set.Count);
            Assert.Equal(1, set.ActiveCount);
            Assert.True(set.Entries[1].HasError);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_MissingFileReturnsEmptyDocument()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cheats-{Guid.NewGuid():N}");
        var path = CheatStore.ResolvePath(directory, "deadbeef");

        CheatDocument document = CheatStore.Load(path, "deadbeef");

        Assert.Empty(document.Cheats);
        Assert.Equal("DEADBEEF", document.GameId);
    }

    [Fact]
    public void Load_RejectsUnsupportedVersionAndMalformedJson()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cheats-{Guid.NewGuid():N}");
        var versioned = Path.Combine(directory, "versioned.json");
        var malformed = Path.Combine(directory, "malformed.json");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(versioned, """{"Version":2,"GameId":"ABCD1234","Cheats":[]}""");
            File.WriteAllText(malformed, "{ not json");

            Assert.Throws<InvalidDataException>(() => CheatStore.Load(versioned, "ABCD1234"));
            Assert.Throws<InvalidDataException>(() => CheatStore.Load(malformed, "ABCD1234"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolvePath_NormalizesGameIdAndUsesGlobalFallback()
    {
        var directory = Path.Combine(Path.GetTempPath(), "fivedo-cheats-tests");

        Assert.Equal(
            Path.Combine(Path.GetFullPath(directory), "global.json"),
            CheatStore.ResolvePath(directory, null));
        Assert.Equal(
            Path.Combine(Path.GetFullPath(directory), "ABCD1234.json"),
            CheatStore.ResolvePath(directory, " abcd1234 "));
    }
}
