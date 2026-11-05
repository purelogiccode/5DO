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

        Assert.Equal(0x00000001u, database.FindById("F3AF1B13")?.FixMode);
        Assert.Equal("1=lightgun", database.FindById("E8FBE61C")?.InputDevices);
        Assert.Equal("a48e6746bd7edec0f40cff078f0bb19f", database.FindById("14F7638D")?.Bios);
        Assert.Equal("20706f726c22d79d3c385e215d7428e2", database.FindById("56927592")?.Bios);
        Assert.Equal("c23fb5d5e6bb1c240d02cf968972be37", database.FindById("56927592")?.Bios2);
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
                <revision>Rev 1</revision>
                <discNumber>2</discNumber>
                <discTotal>3</discTotal>
                <bios>abc123</bios>
                <bios2>def456</bios2>
                <fixMode>0x00000001</fixMode>
                <inputDevices>1=lightgun</inputDevices>
                <nvram>per-game</nvram>
                <cheats>starter-pack</cheats>
              </game>
            </database>
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        var database = GameDatabase.Load(stream);

        Assert.Equal(1, database.Count);
        GameRecord? record = database.FindByChecksum("ABC123");
        Assert.Equal("Test Game", record?.Name);
        Assert.Equal("Rev 1", record?.Revision);
        Assert.Equal(2, record?.DiscNumber);
        Assert.Equal(3, record?.DiscTotal);
        Assert.Equal("abc123", record?.Bios);
        Assert.Equal("def456", record?.Bios2);
        Assert.Equal(1u, record?.FixMode);
        Assert.Equal("1=lightgun", record?.InputDevices);
        Assert.Equal("per-game", record?.Nvram);
        Assert.Equal("starter-pack", record?.Cheats);
    }

    [Fact]
    public void Save_RoundTripsOptionalFields()
    {
        var record = new GameRecord(
            "AABBCCDD",
            "AABBCCDD000000000000000000000000",
            "Round Trip",
            "1995",
            "Publisher",
            "US-EU")
        {
            Revision = "Beta",
            DiscNumber = 1,
            DiscTotal = 2,
            Bios = "0123456789abcdef0123456789abcdef",
            Bios2 = "fedcba9876543210fedcba9876543210",
            FixMode = 0x00000010,
            InputDevices = "1=arcade-lightgun,2=mouse",
            Nvram = "per-game",
            Cheats = "test",
        };
        GameDatabase database = GameDatabase.FromRecords([record]);
        using var stream = new MemoryStream();

        database.Save(stream);
        stream.Position = 0;
        GameDatabase loaded = GameDatabase.Load(stream);

        Assert.Equal(record, Assert.Single(loaded.Records));
    }

    [Fact]
    public void Merge_PreservesCuratedRecordsUnlessOverwriteIsSet()
    {
        var curated = new GameRecord("AABBCCDD", "AABBCCDD000000000000000000000000", "Curated", "1994", "A", "US");
        var incoming = new GameRecord("AABBCCDD", "AABBCCDD000000000000000000000000", "Incoming", "1995", "B", "EU");
        GameDatabase database = GameDatabase.FromRecords([curated]);

        GameDatabase merged = database.Merge([incoming]);
        Assert.Equal("Curated", Assert.Single(merged.Records).Name);

        GameDatabase overwritten = database.Merge([incoming], overwrite: true);
        Assert.Equal("Incoming", Assert.Single(overwritten.Records).Name);
    }

    [Theory]
    [InlineData(
        "3DO Game Guru (1995)(3DO Company)(EU-US)[!].chd",
        "3DO Game Guru",
        "1995",
        "3DO Company",
        "EU-US")]
    [InlineData(
        "Daedalus Encounter, The (1995)(Panasonic)(US)(Disc 3 of 4)[!].chd",
        "Daedalus Encounter, The",
        "1995",
        "Panasonic",
        "US")]
    [InlineData(
        "Crash 'n Burn (USA, Korea).chd",
        "Crash 'n Burn",
        "",
        "",
        "US-KR")]
    public void Describe_ParsesGoodToolsAndRedumpNames(
        string fileName,
        string expectedName,
        string expectedYear,
        string expectedPublisher,
        string expectedRegions)
    {
        GameRecord record = GameDatabaseNameParser.Describe(
            Path.Combine("discs", fileName),
            "AABBCCDD000000000000000000000000");

        Assert.Equal(expectedName, record.Name);
        Assert.Equal(expectedYear, record.ReleaseYear);
        Assert.Equal(expectedPublisher, record.Publisher);
        Assert.Equal(expectedRegions, record.Regions);
    }

    [Fact]
    public void Describe_ParsesDatesDiscNumbersAndReleaseTypes()
    {
        GameRecord record = GameDatabaseNameParser.Describe(
            @"J:\Panasonic 3DO\Foes of Ali (USA) (Beta) (1995-11-07) (Disc 2 of 3).chd",
            "AABBCCDD000000000000000000000000");

        Assert.Equal("Foes of Ali", record.Name);
        Assert.Equal("1995", record.ReleaseYear);
        Assert.Equal("US", record.Regions);
        Assert.Equal("Beta", record.Revision);
        Assert.Equal(2, record.DiscNumber);
        Assert.Equal(3, record.DiscTotal);
    }

    [Theory]
    [InlineData(@"J:\Panasonic 3DO\Foes of Ali (USA).chd")]
    [InlineData("J:/Panasonic 3DO/Foes of Ali (USA).chd")]
    [InlineData("Foes of Ali (USA).chd")]
    public void Describe_HandlesEitherPathSeparator(string path)
    {
        GameRecord record = GameDatabaseNameParser.Describe(
            path,
            "AABBCCDD000000000000000000000000");

        Assert.Equal("Foes of Ali", record.Name);
        Assert.Equal("US", record.Regions);
    }

}

public sealed class InputProfileTests
{
    [Fact]
    public void DefaultProfile_HasEightPlayersAndNoConflicts()
    {
        var profile = InputProfile.CreateDefault();

        Assert.Equal(PbusEncoder.MaxPorts, profile.Players.Length);
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
    public void AddMissingDefaultHotkeys_PreservesCustomBindings()
    {
        var profile = InputProfile.CreateDefault();
        profile.Hotkeys.Clear();
        profile.Hotkeys["SaveState"] = "F2";

        profile.AddMissingDefaultHotkeys();

        Assert.Equal("F2", profile.Hotkeys["SaveState"]);
        Assert.Equal("F6", profile.Hotkeys["SlotPrevious"]);
        Assert.Equal("F7", profile.Hotkeys["SlotNext"]);
        Assert.Equal("F9", profile.Hotkeys["FrameAdvance"]);
        Assert.Equal("R", profile.Hotkeys["Rewind"]);
        Assert.Equal("Tab", profile.Hotkeys["FastForward"]);
    }

    [Fact]
    public void Load_CreatesDefaultsWhenMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-profile-{Guid.NewGuid():N}", "input.json");
        InputProfile profile = InputProfileStore.Load(path);

        Assert.Equal(PbusEncoder.MaxPorts, profile.Players.Length);
        Assert.True(File.Exists(path));
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }
}
