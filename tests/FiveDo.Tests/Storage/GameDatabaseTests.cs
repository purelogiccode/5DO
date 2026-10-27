using System;
using System.IO;
using FiveDo.Input;
using FiveDo.Storage;

namespace FiveDo.Tests.Storage;

public sealed class GameDatabaseTests
{
    [Fact]
    public void DefaultDatabase_ContainsKnownRecords()
    {
        GameDatabase database = GameDatabase.Default;

        Assert.True(database.Count > 100);
        GameRecord? record = database.FindByChecksum("00256A21C9476CAADBBE49BDA7FE2050");
        Assert.NotNull(record);
        Assert.Equal("Out of this World", record.Name);
        Assert.Equal("00256A21", record.Id);
        Assert.Equal(record, database.FindById("00256a21"));
    }

    [Fact]
    public void UnknownChecksum_ReturnsNull()
    {
        Assert.Null(GameDatabase.Default.FindByChecksum("00000000000000000000000000000000"));
    }

    [Fact]
    public void Load_ParsesCustomXml()
    {
        const string xml = """
            <?xml version="1.0"?>
            <database>
              <game>
                <id>ABC</id>
                <checkSum>abc123</checkSum>
                <name>Test Game</name>
                <releaseYear>1994</releaseYear>
                <publisher>Publisher</publisher>
                <regions>US</regions>
              </game>
            </database>
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        var database = GameDatabase.Load(stream);

        Assert.Equal(1, database.Count);
        Assert.Equal("Test Game", database.FindByChecksum("ABC123")?.Name);
    }
}

public sealed class InputProfileTests
{
    [Fact]
    public void DefaultProfile_HasSixPlayersAndNoConflicts()
    {
        var profile = InputProfile.CreateDefault();

        Assert.Equal(6, profile.Players.Length);
        Assert.Empty(profile.FindConflicts());
        Assert.Equal(ControllerButton.A, profile.Players[0].Keyboard["Z"]);
    }

    [Fact]
    public void Profile_RoundtripsThroughJson()
    {
        var directory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"fivedo-profile-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(directory.FullName);
        try
        {
            var path = Path.Combine(directory.FullName, "input.json");
            var profile = InputProfile.CreateDefault();
            profile.Players[0].Keyboard["Q"] = ControllerButton.C;

            InputProfileStore.Save(path, profile);
            InputProfile loaded = InputProfileStore.Load(path);

            Assert.Equal(ControllerButton.C, loaded.Players[0].Keyboard["Q"]);
            Assert.Equal(profile.Hotkeys["SaveState"], loaded.Hotkeys["SaveState"]);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void Profile_DetectsDuplicateHotkeys()
    {
        var profile = InputProfile.CreateDefault();
        profile.Hotkeys["Other"] = profile.Hotkeys["SaveState"];

        Assert.NotEmpty(profile.FindConflicts());
    }

    [Fact]
    public void Load_CreatesDefaultsWhenMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-profile-{Guid.NewGuid():N}", "input.json");
        InputProfile profile = InputProfileStore.Load(path);

        Assert.Equal(6, profile.Players.Length);
        Assert.True(File.Exists(path));
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }
}
