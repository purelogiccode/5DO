using System;
using System.IO;

namespace FiveDo.Tests.Cli;

[Collection("Console")]
public sealed class CliCheckpointTests
{
    [Fact]
    public void Run_CheckpointsEmitSamplesAndAudioHash()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-checkpoint-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "10",
                "--checkpoints",
                "5,10",
                "--state-hash",
                "--json");

            Assert.Equal(0, result.Code);
            Assert.Contains("\"frame\": 5", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"frame\": 10", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"stateHash\":", result.Output, StringComparison.Ordinal);
            Assert.Contains("\"audioHash\":", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
        }
    }

    [Fact]
    public void Run_InvalidCheckpointFailsWithClearError()
    {
        var biosPath = Path.Combine(Path.GetTempPath(), $"fivedo-checkpoint-bios-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllBytes(biosPath, new byte[1_048_576]);
            CliResult result = Invoke(
                "run",
                "--bios",
                biosPath,
                "--frames",
                "10",
                "--checkpoints",
                "0",
                "--json");

            Assert.Equal(1, result.Code);
            Assert.Contains("Invalid checkpoint", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(biosPath);
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
            return new CliResult(code, output.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }

    private readonly record struct CliResult(int Code, string Output);
}
