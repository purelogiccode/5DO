using System;
using System.IO;

namespace ThreeDo.Tests.Cli;

[CollectionDefinition("Console", DisableParallelization = true)]
public sealed class CliConsoleGroup
{
}

[Collection("Console")]
public sealed class CliProcessTests
{
    [Fact]
    public void Version_ReturnsZeroAndPrintsProductName()
    {
        var result = Invoke("--version");

        Assert.Equal(0, result.Code);
        Assert.Contains("3DOEmulator", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void NoArguments_PrintsUsage()
    {
        var result = Invoke();

        Assert.Equal(0, result.Code);
        Assert.Contains("Usage: 3do", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownCommand_ReturnsTwo()
    {
        var result = Invoke("not-a-command");

        Assert.Equal(2, result.Code);
        Assert.Contains("Unknown command", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownCommand_JsonReturnsStructuredError()
    {
        var result = Invoke("not-a-command", "--json");

        Assert.Equal(2, result.Code);
        Assert.Contains("\"ok\": false", result.Output, StringComparison.Ordinal);
        Assert.Contains("Unknown command", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void RunWithoutBios_ReturnsOne()
    {
        var result = Invoke("run");

        Assert.Equal(1, result.Code);
        Assert.Contains("--bios", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void StateInspectMissingFile_ReturnsOne()
    {
        var result = Invoke("state", "inspect", Path.Combine(Path.GetTempPath(), "missing-threedo-state.tdos"));

        Assert.Equal(1, result.Code);
        Assert.Contains("error:", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Bench_ReturnsZero()
    {
        var result = Invoke("bench", "--frames", "2");

        Assert.Equal(0, result.Code);
        Assert.Contains("frames:", result.Output, StringComparison.Ordinal);
        Assert.Contains("fps:", result.Output, StringComparison.Ordinal);
    }

    private static CliResult Invoke(params string[] args)
    {
        var previousOut = Console.Out;
        var previousError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var code = global::ThreeDo.Cli.Program.Main(args);
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
