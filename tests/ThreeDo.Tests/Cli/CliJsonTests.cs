using System;
using System.Text.Json;
using ThreeDo.Cli;
using ThreeDo.Host;

namespace ThreeDo.Tests.Cli;

public sealed class CliJsonTests
{
    [Fact]
    public void RunResult_UsesTheDocumentedCamelCaseContract()
    {
        var result = new CliRunResult(
            Ok: true,
            Frames: 120,
            CyclesPerFrame: 208345,
            SourceWidth: 320,
            SourceHeight: 240,
            VdlModulo: 320,
            Pc: 0x1234,
            Instructions: 7,
            FrameHash: 0xabcdef01,
            Samples: 737,
            DiscHash: "abc",
            GameId: null,
            FixMode: 0x20,
            DspRunning: true,
            DspPc: 0x3ff,
            DspSema: 0x00080001,
            DspSeed: 1,
            CdStatus: 0xe1,
            CdPoll: 0x0c,
            Autopilot: null,
            AutopilotLineX: null,
            AutopilotRoadCoverage: null,
            AutopilotSteering: null,
            AutopilotObstacle: null,
            AutopilotRecovery: null,
            AutopilotCrashRestart: null,
            AutopilotMenuAdvance: null,
            AutopilotStaticFrames: null,
            AutopilotPacing: null,
            AutopilotUpset: null,
            AutopilotLookahead: null,
            Warnings: []);

        var json = JsonSerializer.Serialize(result, CliJsonContext.Default.CliRunResult);

        Assert.Contains("\"ok\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"sourceWidth\": 320", json, StringComparison.Ordinal);
        Assert.Contains("\"vdlModulo\": 320", json, StringComparison.Ordinal);
        Assert.Contains("\"dspSema\": 524289", json, StringComparison.Ordinal);
        Assert.Contains("\"autopilotLookahead\": null", json, StringComparison.Ordinal);
        Assert.Contains("\"warnings\": []", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfTestResult_RoundtripsThroughTheSourceGeneratedContext()
    {
        var result = new CliSelfTestResult(
            true,
            [
                new CliSelfTestCheck("deterministic frame", true),
                new CliSelfTestCheck("state roundtrip", false),
            ]);

        var json = JsonSerializer.Serialize(result, CliJsonContext.Default.CliSelfTestResult);
        var loaded = JsonSerializer.Deserialize(json, CliJsonContext.Default.CliSelfTestResult);

        Assert.NotNull(loaded);
        Assert.True(loaded.Ok);
        Assert.Collection(
            loaded.Checks,
            check =>
            {
                Assert.Equal("deterministic frame", check.Name);
                Assert.True(check.Ok);
            },
            check =>
            {
                Assert.Equal("state roundtrip", check.Name);
                Assert.False(check.Ok);
            });
    }

    [Fact]
    public void HostRecords_KeepTheirHistoricalPascalCaseContract()
    {
        var info = new StateInfo(
            1,
            "0.0.0",
            12_500_000,
            0,
            "bios",
            "disc",
            3,
            [new StateSectionInfo(1, 1, 4)]);

        var json = JsonSerializer.Serialize(info, CliHostJsonContext.Default.StateInfo);

        Assert.Contains("\"Version\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"EmulatorVersion\": \"0.0.0\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Sections\":", json, StringComparison.Ordinal);
    }
}
