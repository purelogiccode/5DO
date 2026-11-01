using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Audio;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Tests.Cli;

[CollectionDefinition("Console", DisableParallelization = true)]
public sealed class CliConsoleGroup;

[Collection("Console")]
public sealed class CliProcessTests
{
    [Fact]
    public void Version_ReturnsZeroAndPrintsProductName()
    {
        CliResult result = Invoke("--version");

        Assert.Equal(0, result.Code);
        Assert.Contains("5DO", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void NoArguments_PrintsUsage()
    {
        CliResult result = Invoke();

        Assert.Equal(0, result.Code);
        Assert.Contains("Usage: 5do", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownCommand_ReturnsTwo()
    {
        CliResult result = Invoke("not-a-command");

        Assert.Equal(2, result.Code);
        Assert.Contains("Unknown command", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownCommand_JsonReturnsStructuredError()
    {
        CliResult result = Invoke("not-a-command", "--json");

        Assert.Equal(2, result.Code);
        Assert.Contains("\"ok\": false", result.Output, StringComparison.Ordinal);
        Assert.Contains("Unknown command", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void RunWithoutBios_ReturnsOne()
    {
        CliResult result = Invoke("run");

        Assert.Equal(1, result.Code);
        Assert.Contains("--bios", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ArmTimingOption_IsReportedInJson()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--arm-timing",
                "arm60",
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"armTiming\": \"Arm60Datasheet\"", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_ClioTimersOption_IsReportedInJson()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--clio-timers",
                "opera",
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"clioTimers\": \"OperaScheduler\"", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_CelTimingOption_IsReportedInJson()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--cel-timing",
                "opera",
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"celTiming\": \"OperaBusy\"", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_InvalidCelTiming_ReturnsOne()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--cel-timing",
                "bogus");

            Assert.Equal(1, result.Code);
            Assert.Contains("--cel-timing", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_CdromTimingOption_IsReportedInJson()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--cd-timing",
                "opera",
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"cdromTiming\": \"OperaCycleBased\"", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_InvalidCdromTiming_ReturnsOne()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--cd-timing",
                "bogus");

            Assert.Equal(1, result.Code);
            Assert.Contains("--cd-timing", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_CdSpeedOption_IsReportedInJson()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--cd-timing",
                "opera",
                "--cd-speed",
                "8x",
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"cdSpeed\": \"X8\"", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_InvalidCdSpeed_ReturnsOne()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--cd-speed",
                "3x");

            Assert.Equal(1, result.Code);
            Assert.Contains("--cd-speed", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_InputDevices_AreReportedInJson()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--input-device",
                "1=mouse,3=lightgun,4=arcade-lightgun,5=flightstick,6=orbatak-trackball",
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains(
                "\"inputDevices\": \"1=mouse,2=joypad,3=lightgun,4=arcade-lightgun,5=flightstick,6=orbatak-trackball\"",
                result.Output,
                StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_InvalidInputDevice_ReturnsOne()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--input-device",
                "1=banana");

            Assert.Equal(1, result.Code);
            Assert.Contains("--input-device", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_InvalidPbusFormat_ReturnsOne()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--pbus-format",
                "banana");

            Assert.Equal(1, result.Code);
            Assert.Contains("--pbus-format", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_AccuracyProfile_IsReportedAndSetsTheTimingOptions()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--profile",
                "accuracy",
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"profile\": \"Accuracy\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"armTiming\": \"Arm60Datasheet\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"clioTimers\": \"OperaScheduler\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"celTiming\": \"OperaBusy\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"cdromTiming\": \"OperaCycleBased\"", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_ExplicitTimingOverridesTheProfile()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--profile",
                "accuracy",
                "--arm-timing",
                "freedo",
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"profile\": \"Custom\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"armTiming\": \"FreeDoCompatible\"", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_InvalidProfile_ReturnsOne()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--profile",
                "turbo");

            Assert.Equal(1, result.Code);
            Assert.Contains("--profile", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_InvalidClioTimers_ReturnsOne()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--clio-timers",
                "bogus");

            Assert.Equal(1, result.Code);
            Assert.Contains("--clio-timers", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_CpuClockOption_IsReportedInJson()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--cpu-clock",
                "2x",
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"cpuClock\": 25000000", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_GameProfileOption_AppliesTimingAndRegion()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        var profilePath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-profile-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            File.WriteAllText(
                profilePath,
                """
                {
                  "Profile": "Accuracy",
                  "Region": "Pal1",
                  "ArmTiming": "Arm60Datasheet",
                  "ClioTimers": "OperaScheduler",
                  "CelTiming": "OperaBusy",
                  "CdromTiming": "OperaCycleBased",
                  "CdSpeed": "X8",
                  "CpuClockMultiplier": 1.5,
                  "CdAudioVolume": 0.8,
                  "CdAudioMuted": false
                }
                """);

            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--game-profile",
                profilePath,
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"armTiming\": \"Arm60Datasheet\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"clioTimers\": \"OperaScheduler\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"cdSpeed\": \"X8\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"cpuClock\": 18750000", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"region\": \"Pal1\"", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
            File.Delete(profilePath);
        }
    }

    [Fact]
    public void Run_RandomSeedOption_IsAppliedToTheDsp()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--random-seed",
                "0x1234",
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"dspSeed\": 4660", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_InvalidRandomSeed_ReturnsOne()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--random-seed",
                "not-a-seed");

            Assert.Equal(1, result.Code);
            Assert.Contains("--random-seed", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_InvalidCpuClock_ReturnsOne()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--cpu-clock",
                "8x");

            Assert.Equal(1, result.Code);
            Assert.Contains("--cpu-clock", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_InvalidArmTiming_ReturnsOne()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--arm-timing",
                "bogus");

            Assert.Equal(1, result.Code);
            Assert.Contains("--arm-timing", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void StateInspectMissingFile_ReturnsOne()
    {
        CliResult result = Invoke("state", "inspect", Path.Combine(Path.GetTempPath(), "missing-fivedo-state.tdos"));

        Assert.Equal(1, result.Code);
        Assert.Contains("error:", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AudioAnalyze_ReportsPcmStatistics()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-cli-audio-{Guid.NewGuid():N}.wav");
        try
        {
            using (var sink = new WavAudioSink(path))
            {
                sink.PushSample(new AudioSample(100, -100).Packed);
                sink.PushSample(new AudioSample(0, 0).Packed);
            }

            CliResult result = Invoke("audio", "analyze", path, "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"frames\": 2", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"peak\": 100", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"silentFrames\": 1", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Bench_ReturnsZero()
    {
        CliResult result = Invoke("bench", "--frames", "2");

        Assert.Equal(0, result.Code);
        Assert.Contains("frames:", result.Output, StringComparison.Ordinal);
        Assert.Contains("fps:", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscPlaylist_ListsEntriesInOrder()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cli-playlist-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var playlist = Path.Combine(directory, "game.m3u");
            File.WriteAllText(
                playlist,
                "#EXTM3U\n#EXTINF:0,First Disc\ndisc1.chd\n# comment\n#EXTINF:0,Second Disc\ndisc2.chd\n");

            CliResult result = Invoke("disc", "playlist", playlist, "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"count\": 2", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"displayName\": \"First Disc\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"displayName\": \"Second Disc\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("disc2.chd", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DiscTracks_ReportsAnIsoImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-cli-{Guid.NewGuid():N}.iso");
        try
        {
            File.WriteAllBytes(path, new byte[3 * 2048]);

            CliResult result = Invoke("disc", "tracks", path);

            Assert.Equal(0, result.Code);
            Assert.Contains("track 1:", result.Output, StringComparison.Ordinal);
            Assert.Contains("format=iso", result.Output, StringComparison.Ordinal);
            Assert.Contains("dataSize=6144", result.Output, StringComparison.Ordinal);
            Assert.Contains("frames=3", result.Output, StringComparison.Ordinal);

            CliResult json = Invoke("disc", "tracks", path, "--json");
            Assert.Equal(0, json.Code);
            Assert.Contains("\"tracks\"", json.Output, StringComparison.Ordinal);
            Assert.Contains("\"dataSize\": 2048", json.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FsListAndExtract_ReadAnIsoImage()
    {
        var discPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-fs-{Guid.NewGuid():N}.iso");
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"fivedo-cli-fs-out-{Guid.NewGuid():N}");
        try
        {
            var image = SyntheticOperaDisc.Build(
                [
                    new SyntheticOperaDisc.EntrySpec("HELLO.TXT", IsDirectory: false, Lba: 2, LengthBytes: 5),
                    new SyntheticOperaDisc.EntrySpec("SUB", IsDirectory: true, Lba: 3, LengthBytes: 2048),
                ],
                blockCount: 5,
                label: "TESTDISC",
                directories: new Dictionary<uint, IReadOnlyList<SyntheticOperaDisc.EntrySpec>>
                {
                    [3] = [new SyntheticOperaDisc.EntrySpec("NESTED.BIN", IsDirectory: false, Lba: 4, LengthBytes: 4)],
                });
            SyntheticOperaDisc.WriteFile(image, 2, "hello"u8);
            SyntheticOperaDisc.WriteFile(image, 4, "nest"u8);
            File.WriteAllBytes(discPath, image);

            CliResult list = Invoke("fs", "list", discPath);
            Assert.Equal(0, list.Code);
            Assert.Contains("volume=TESTDISC entries=4", list.Output, StringComparison.Ordinal);
            Assert.Contains("[F] HELLO.TXT 5 lba=2", list.Output, StringComparison.Ordinal);
            Assert.Contains("[D] SUB 2048 lba=3", list.Output, StringComparison.Ordinal);

            CliResult json = Invoke("fs", "list", discPath, "--json");
            Assert.Equal(0, json.Code);
            Assert.Contains("\"ok\": true", json.Output, StringComparison.Ordinal);
            Assert.Contains("\"path\": \"\\\\HELLO.TXT\"", json.Output, StringComparison.Ordinal);

            CliResult extract = Invoke("fs", "extract", discPath, "--out", outputDirectory, "--json");
            Assert.Equal(0, extract.Code);
            Assert.Contains("\"extracted\": 2", extract.Output, StringComparison.Ordinal);
            Assert.Equal("hello", File.ReadAllText(Path.Combine(outputDirectory, "HELLO.TXT")));
            Assert.Equal("nest", File.ReadAllText(Path.Combine(outputDirectory, "SUB", "NESTED.BIN")));
        }
        finally
        {
            File.Delete(discPath);
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void FsList_ReadsRawBinAndCueImages()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cli-fs-bin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var image = SyntheticOperaDisc.Build(
                [new SyntheticOperaDisc.EntrySpec("HELLO.TXT", IsDirectory: false, Lba: 2, LengthBytes: 5)],
                blockCount: 3);
            SyntheticOperaDisc.WriteFile(image, 2, "hello"u8);
            var binPath = Path.Combine(directory, "disc.bin");
            var cuePath = Path.Combine(directory, "disc.cue");
            File.WriteAllBytes(binPath, ToRawBin(image));
            File.WriteAllText(
                cuePath,
                $"FILE \"{Path.GetFileName(binPath)}\" BINARY\n  TRACK 01 MODE1/2352\n    INDEX 01 00:00:00\n");

            CliResult bin = Invoke("fs", "list", binPath);
            Assert.Equal(0, bin.Code);
            Assert.Contains("entries=2", bin.Output, StringComparison.Ordinal);

            CliResult cue = Invoke("fs", "list", cuePath);
            Assert.Equal(0, cue.Code);
            Assert.Contains("entries=2", cue.Output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DiscTracks_ReportsARawBinImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-cli-{Guid.NewGuid():N}.bin");
        try
        {
            var image = new byte[2 * 2352];
            image[0] = 0x00;
            for (var i = 1; i <= 10; i++)
            {
                image[i] = 0xFF;
            }

            image[11] = 0x00;
            image[15] = 1;
            File.WriteAllBytes(path, image);

            CliResult result = Invoke("disc", "tracks", path);

            Assert.Equal(0, result.Code);
            Assert.Contains("format=bin", result.Output, StringComparison.Ordinal);
            Assert.Contains("frames=2", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] ToRawBin(byte[] iso)
    {
        const int rawSectorSize = 2352;
        var raw = new byte[(iso.Length / 2048) * rawSectorSize];
        for (var sector = 0; sector < iso.Length / 2048; sector++)
        {
            var offset = sector * rawSectorSize;
            raw[offset] = 0;
            for (var i = 1; i <= 10; i++)
            {
                raw[offset + i] = 0xFF;
            }

            raw[offset + 11] = 0;
            raw[offset + 15] = 1;
            iso.AsSpan(sector * 2048, 2048).CopyTo(raw.AsSpan(offset + 16));
        }

        return raw;
    }

    [Fact]
    public void BiosScan_ReportsPrimarySecondaryAndErrors()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "fz1.bin"), new byte[1_048_576]);
            File.WriteAllBytes(Path.Combine(folder, "kanji.bin"), new byte[512]);
            File.WriteAllBytes(Path.Combine(folder, "bad.bin"), new byte[1_048_577]);

            CliResult result = Invoke("bios", "scan", folder, "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"primaryCount\": 1", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"secondaryCount\": 1", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"errorCount\": 1", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"kind\": \"Primary\"", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void BiosInfo_ReportsRegionMetadata()
    {
        var path = Path.Combine(Path.GetTempPath(), $"panafz1-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(path, new byte[1_048_576]);

            CliResult result = Invoke("bios", "info", path, "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"kind\": \"Primary\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"territory\": \"Usa\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"region\": \"USA (NTSC)\"", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Run_BiosAuto_SelectsFromFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-auto-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "panafz1.bin"), new byte[1_048_576]);

            CliResult result = Invoke(
                "run",
                "--bios-auto",
                folder,
                "--frames",
                "0",
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"biosSource\": \"auto\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"biosName\": \"panafz1\"", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Run_Bios2_ReportsTheSecondaryHash()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var primary = new byte[1_048_576];
            var secondary = new byte[512];
            var primaryPath = Path.Combine(folder, "panafz1.bin");
            var secondaryPath = Path.Combine(folder, "kanji.bin");
            File.WriteAllBytes(primaryPath, primary);
            File.WriteAllBytes(secondaryPath, secondary);
            var expected = BiosImage.FromBytes(primary, secondary).Md5Bios2;

            CliResult result = Invoke(
                "run",
                "--bios",
                primaryPath,
                "--bios2",
                secondaryPath,
                "--frames",
                "0",
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains($"\"bios2Md5\": \"{expected}\"", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Run_BiosAndBiosAuto_ReturnsOne()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-conflict-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "panafz1.bin");
            File.WriteAllBytes(path, new byte[1_048_576]);

            CliResult result = Invoke("run", "--bios", path, "--bios-auto", folder, "--frames", "0");

            Assert.Equal(1, result.Code);
            Assert.Contains("--bios", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void BiosSelect_ChoosesAnImageForASyntheticDisc()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-select-{Guid.NewGuid():N}");
        var discPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-select-{Guid.NewGuid():N}.iso");
        Directory.CreateDirectory(folder);
        try
        {
            var primary = new byte[1_048_576];
            var europe = new byte[1_048_576];
            europe[28] = 234;
            File.WriteAllBytes(Path.Combine(folder, "panafz1.bin"), primary);
            File.WriteAllBytes(Path.Combine(folder, "panafz10e-anvil.bin"), europe);
            File.WriteAllBytes(discPath, new byte[4096]);
            var expected = BiosImage.FromBytes(europe).Md5;

            CliResult result = Invoke(
                "bios",
                "select",
                discPath,
                "--folder",
                folder,
                "--md5",
                expected,
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"ok\": true", result.Output, StringComparison.Ordinal);
            Assert.Contains($"\"md5\": \"{expected}\"", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
            File.Delete(discPath);
        }
    }

    [Fact]
    public void NvramPath_GlobalWithoutDiscResolvesTheLegacyFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cli-nvram-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            CliResult result = Invoke("nvram", "path", "--dir", directory, "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"mode\": \"global\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("nvram.ram", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"exists\": false", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void NvramPath_PerGameUsesTheDiscGameIdAndVersion()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cli-nvram-{Guid.NewGuid():N}");
        var discPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-nvram-{Guid.NewGuid():N}.iso");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(discPath, new byte[4096]);
            CliResult result = Invoke(
                "nvram",
                "path",
                discPath,
                "--mode",
                "per-game",
                "--version",
                "2",
                "--dir",
                directory,
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"mode\": \"per-game\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"version\": 2", result.Output, StringComparison.Ordinal);
            Assert.Contains("per-game", result.Output, StringComparison.Ordinal);
            Assert.Contains(".2.ram", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            File.Delete(discPath);
        }
    }

    [Fact]
    public void NvramImportExportAndResetRoundtripA32KImage()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cli-nvram-{Guid.NewGuid():N}");
        var input = Path.Combine(Path.GetTempPath(), $"fivedo-cli-nvram-{Guid.NewGuid():N}.ram");
        var output = Path.Combine(Path.GetTempPath(), $"fivedo-cli-nvram-{Guid.NewGuid():N}.ram");
        Directory.CreateDirectory(directory);
        try
        {
            var bytes = NvramImage.CreateFresh().ToArray();
            bytes[321] = 0x5A;
            File.WriteAllBytes(input, bytes);

            CliResult import = Invoke("nvram", "import", "--in", input, "--dir", directory, "--json");
            Assert.Equal(0, import.Code);
            Assert.Contains("\"action\": \"import\"", import.Output, StringComparison.Ordinal);

            CliResult list = Invoke("nvram", "list", "--dir", directory, "--json");
            Assert.Equal(0, list.Code);
            Assert.Contains("nvram.ram", list.Output, StringComparison.Ordinal);

            CliResult export = Invoke("nvram", "export", "--out", output, "--dir", directory, "--json");
            Assert.Equal(0, export.Code);
            Assert.Equal(bytes, File.ReadAllBytes(output));

            CliResult reset = Invoke("nvram", "reset", "--dir", directory, "--json");
            Assert.Equal(0, reset.Code);
            var resetBytes = File.ReadAllBytes(Path.Combine(directory, "nvram.ram"));
            Assert.Equal(EmulatorConstants.NvramSize, resetBytes.Length);
            Assert.Equal(0, resetBytes[321]);
            Assert.Equal((byte)'n', resetBytes[40]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            File.Delete(input);
            File.Delete(output);
        }
    }

    [Fact]
    public void NvramPerGameImportExportUsesTheGameIdPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cli-nvram-{Guid.NewGuid():N}");
        var discPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-nvram-{Guid.NewGuid():N}.iso");
        var input = Path.Combine(Path.GetTempPath(), $"fivedo-cli-nvram-{Guid.NewGuid():N}.ram");
        var output = Path.Combine(Path.GetTempPath(), $"fivedo-cli-nvram-{Guid.NewGuid():N}.ram");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(discPath, new byte[4096]);
            var bytes = NvramImage.CreateFresh().ToArray();
            bytes[456] = 0x33;
            File.WriteAllBytes(input, bytes);

            CliResult import = Invoke(
                "nvram",
                "import",
                discPath,
                "--in",
                input,
                "--mode",
                "per-game",
                "--version",
                "1",
                "--dir",
                directory,
                "--json");
            Assert.Equal(0, import.Code);

            var files = Directory.GetFiles(directory, "*.1.ram", SearchOption.AllDirectories);
            Assert.Single(files);
            Assert.Contains("per-game", files[0], StringComparison.OrdinalIgnoreCase);
            Assert.Equal(bytes, File.ReadAllBytes(files[0]));

            CliResult export = Invoke(
                "nvram",
                "export",
                discPath,
                "--out",
                output,
                "--mode",
                "per-game",
                "--version",
                "1",
                "--dir",
                directory,
                "--json");
            Assert.Equal(0, export.Code);
            Assert.Equal(bytes, File.ReadAllBytes(output));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            File.Delete(discPath);
            File.Delete(input);
            File.Delete(output);
        }
    }

    [Fact]
    public void Run_PerGameNvramWithoutDiscReturnsOne()
    {
        CliResult result = Invoke("run", "--bios", "unused.bin", "--frames", "0", "--nvram-mode", "per-game");

        Assert.Equal(1, result.Code);
        Assert.Contains("per-game", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_NvramLayoutOptionsCannotBeCombinedWithExplicitPath()
    {
        CliResult result = Invoke(
            "run",
            "--bios",
            "unused.bin",
            "--frames",
            "0",
            "--nvram",
            "custom.ram",
            "--nvram-version",
            "1");

        Assert.Equal(1, result.Code);
        Assert.Contains("--nvram", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_GlobalNvramVersionIsReportedAndSaved()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cli-nvram-{Guid.NewGuid():N}");
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-cli-bios-{Guid.NewGuid():N}.bin");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "0",
                "--nvram-mode",
                "global",
                "--nvram-version",
                "3",
                "--nvram-dir",
                directory,
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"nvramMode\": \"global\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"nvramVersion\": 3", result.Output, StringComparison.Ordinal);
            Assert.Contains("nvram.3.ram", result.Output, StringComparison.Ordinal);
            var path = Path.Combine(directory, "nvram.3.ram");
            Assert.True(File.Exists(path));
            Assert.Equal(EmulatorConstants.NvramSize, new FileInfo(path).Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Nvram_InvalidVersionReturnsOne()
    {
        CliResult result = Invoke("nvram", "path", "--version", "10");

        Assert.Equal(1, result.Code);
        Assert.Contains("NVRAM version", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Cheat_AddListDisableRemove_Roundtrips()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cli-cheats-{Guid.NewGuid():N}");
        try
        {
            CliResult add = Invoke(
                "cheat",
                "add",
                "--game",
                "ABCD1234",
                "--dir",
                directory,
                "--name",
                "Infinite Health",
                "--code",
                "write8 0x2000 = 0x64",
                "--json");
            Assert.Equal(0, add.Code);
            Assert.Contains("\"ok\": true", add.Output, StringComparison.Ordinal);
            Assert.Contains("\"name\": \"Infinite Health\"", add.Output, StringComparison.Ordinal);

            CliResult list = Invoke(
                "cheat",
                "list",
                "--game",
                "ABCD1234",
                "--dir",
                directory,
                "--json");
            Assert.Equal(0, list.Code);
            Assert.Contains("\"name\": \"Infinite Health\"", list.Output, StringComparison.Ordinal);
            Assert.Contains("\"enabled\": true", list.Output, StringComparison.Ordinal);

            CliResult disable = Invoke(
                "cheat",
                "disable",
                "--game",
                "ABCD1234",
                "--dir",
                directory,
                "--name",
                "Infinite Health",
                "--json");
            Assert.Equal(0, disable.Code);

            CliResult afterDisable = Invoke(
                "cheat",
                "list",
                "--game",
                "ABCD1234",
                "--dir",
                directory,
                "--json");
            Assert.Contains("\"enabled\": false", afterDisable.Output, StringComparison.Ordinal);

            CliResult remove = Invoke(
                "cheat",
                "remove",
                "--game",
                "ABCD1234",
                "--dir",
                directory,
                "--name",
                "Infinite Health",
                "--json");
            Assert.Equal(0, remove.Code);
            Assert.Contains("\"count\": 0", remove.Output, StringComparison.Ordinal);
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
    public void Cheat_AddMalformedCode_ReturnsOne()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cli-cheats-{Guid.NewGuid():N}");
        try
        {
            CliResult result = Invoke(
                "cheat",
                "add",
                "--game",
                "ABCD1234",
                "--dir",
                directory,
                "--name",
                "Broken",
                "--code",
                "write8 0x2000 =");

            Assert.Equal(1, result.Code);
            Assert.Contains("Expected", result.Error, StringComparison.OrdinalIgnoreCase);
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
    public void MovieInfo_ReportsHeaderFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-cli-movie-{Guid.NewGuid():N}.5domovie");
        try
        {
            var movie = new FiveDo.Tas.Movie(
                new FiveDo.Tas.MovieHeader
                {
                    GameName = "CLI Movie",
                    GameId = "ABCD1234",
                    StartFrame = 12,
                },
                [[0x00, 0x48, 0x01, 0x80]]);
            FiveDo.Tas.MovieFile.Save(path, movie);

            CliResult result = Invoke("movie", "info", path, "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"gameName\": \"CLI Movie\"", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"frameCount\": 1", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"startFrame\": 12", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MovieRecordAndVerify_RoundtripFromState()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cli-movie-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var biosPath = Path.Combine(directory, "bios.bin");
        var statePath = Path.Combine(directory, "start.state");
        var moviePath = Path.Combine(directory, "recorded.5domovie");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult start = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "2",
                "--state-out",
                statePath,
                "--json");
            Assert.Equal(0, start.Code);
            Assert.True(File.Exists(statePath));

            CliResult record = Invoke(
                "movie",
                "record",
                "--state-in",
                statePath,
                "--frames",
                "3",
                "--out",
                moviePath,
                "--json");
            Assert.Equal(0, record.Code);
            Assert.True(File.Exists(moviePath));

            CliResult verify = Invoke("movie", "verify", moviePath, "--json");
            Assert.Equal(0, verify.Code);
            Assert.Contains("\"movieVerification\": \"pass\"", verify.Output, StringComparison.Ordinal);
            Assert.Contains("\"movieFrames\": 3", verify.Output, StringComparison.Ordinal);
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
    public void MovieBranch_KeepsThePrefix()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cli-movie-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var sourcePath = Path.Combine(directory, "source.5domovie");
        var branchPath = Path.Combine(directory, "branch.5domovie");
        try
        {
            var movie = new FiveDo.Tas.Movie(
                new FiveDo.Tas.MovieHeader { GameName = "Branch" },
                [
                    new byte[] { 0x01 },
                    new byte[] { 0x02 },
                    new byte[] { 0x03 },
                ]);
            FiveDo.Tas.MovieFile.Save(sourcePath, movie);

            CliResult result = Invoke(
                "movie",
                "branch",
                sourcePath,
                "--frame",
                "2",
                "--out",
                branchPath,
                "--json");

            Assert.Equal(0, result.Code);
            FiveDo.Tas.Movie branch = FiveDo.Tas.MovieFile.Load(branchPath);
            Assert.Equal(2, branch.FrameCount);
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
    public void MovieVerify_MismatchedEvidence_ReturnsTwo()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cli-movie-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var biosPath = Path.Combine(directory, "bios.bin");
        var statePath = Path.Combine(directory, "start.state");
        var moviePath = Path.Combine(directory, "bad.5domovie");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult start = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "1",
                "--state-out",
                statePath,
                "--json");
            Assert.Equal(0, start.Code);

            var movie = new FiveDo.Tas.Movie(
                new FiveDo.Tas.MovieHeader
                {
                    GameName = "Bad Evidence",
                    StartFrame = 1,
                    FinalFrameHash = 0x12345678,
                    FinalProgramCounter = 0,
                },
                [
                    new byte[16],
                    new byte[16],
                ],
                File.ReadAllBytes(statePath));
            FiveDo.Tas.MovieFile.Save(moviePath, movie);

            CliResult verify = Invoke("movie", "verify", moviePath, "--json");

            Assert.Equal(2, verify.Code);
            Assert.Contains("\"movieVerification\": \"fail\"", verify.Output, StringComparison.Ordinal);
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
    public void SettingsValidateAndMigrate_ReportTheSchemaVersion()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-settings-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"PreferOpenGl":false}""");

            CliResult validate = Invoke("settings", "validate", path, "--json");
            Assert.Equal(0, validate.Code);
            Assert.Contains("\"valid\": true", validate.Output, StringComparison.Ordinal);
            Assert.Contains("\"version\": 0", validate.Output, StringComparison.Ordinal);

            CliResult migrate = Invoke("settings", "migrate", path);
            Assert.Equal(0, migrate.Code);
            Assert.Contains($"toVersion:   {FiveDo.Host.AppSettings.CurrentVersion}", migrate.Output, StringComparison.Ordinal);

            CliResult after = Invoke("settings", "validate", path);
            Assert.Equal(0, after.Code);
            Assert.Contains($"version: {FiveDo.Host.AppSettings.CurrentVersion}", after.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SettingsValidate_ReturnsOneForMalformedJson()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-settings-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ broken");
            CliResult result = Invoke("settings", "validate", path, "--json");
            Assert.Equal(1, result.Code);
            Assert.Contains("\"valid\": false", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Run_SwapsDiscsAtTheRequestedFrame()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-cli-swap-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var first = Path.Combine(directory, "first.iso");
            var second = Path.Combine(directory, "second.iso");
            var bios = Path.Combine(directory, "bios.bin");
            File.WriteAllBytes(
                first,
                SyntheticOperaDisc.Build(
                    [new SyntheticOperaDisc.EntrySpec("A.TXT", IsDirectory: false, Lba: 2, LengthBytes: 1)],
                    blockCount: 4,
                    label: "FIRST"));
            File.WriteAllBytes(
                second,
                SyntheticOperaDisc.Build(
                    [new SyntheticOperaDisc.EntrySpec("B.TXT", IsDirectory: false, Lba: 2, LengthBytes: 1)],
                    blockCount: 4,
                    label: "SECOND"));
            File.WriteAllBytes(bios, new byte[1_048_576]);

            CliResult result = Invoke(
                "run",
                first,
                "--bios",
                bios,
                "--frames",
                "2",
                "--swap-at",
                "0",
                "--swap-disc",
                second,
                "--audio",
                "null",
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("swapped disc at frame 0", result.Error, StringComparison.Ordinal);
            Assert.Contains("\"ok\": true", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static CliResult Invoke(params string[] args)
    {
        TextWriter previousOut = Console.Out;
        TextWriter previousError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var code = global::FiveDo.Cli.Program.Main(args);
            return new CliResult(code, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }

    private readonly record struct CliResult(int Code, string Output, string Error);
}
