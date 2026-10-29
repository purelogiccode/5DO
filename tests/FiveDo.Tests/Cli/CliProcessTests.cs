using System;
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
