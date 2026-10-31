using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Audio;
using FiveDo.Core;

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
