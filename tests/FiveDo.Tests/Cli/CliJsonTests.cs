using System;
using System.Text.Json;
using FiveDo.Cli;
using FiveDo.Host;

namespace FiveDo.Tests.Cli;

public sealed class CliJsonTests
{
    [Fact]
    public void RunResult_UsesTheDocumentedCamelCaseContract()
    {
        var result = new CliRunResult(
            Ok: true,
            Frames: 120,
            ElapsedSeconds: 2.0,
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
            Profile: "Accuracy",
            ArmTiming: "Arm60Datasheet",
            ClioTimers: "OperaScheduler",
            CelTiming: "OperaBusy",
            CelBusyCycles: 1234,
            CdromTiming: "OperaCycleBased",
            CdSpeed: "X8",
            CpuClock: 25_000_000,
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
            AudioDurationSeconds: 10.0,
            AudioPeak: 12345,
            AudioRms: 1234.5,
            AudioClippedFrames: 0,
            AudioSilentFrames: 0,
            AudioDcOffsetLeft: 0.1,
            AudioDcOffsetRight: -0.1,
            AudioDroppedSamples: null,
            AudioUnderrunBuffers: null,
            AudioQueuedBuffers: null,
            AudioBufferedFrames: null,
            AudioLatencyMilliseconds: null,
            Warnings: []);

        var json = JsonSerializer.Serialize(result, CliJsonContext.Default.CliRunResult);

        Assert.Contains("\"ok\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"sourceWidth\": 320", json, StringComparison.Ordinal);
        Assert.Contains("\"vdlModulo\": 320", json, StringComparison.Ordinal);
        Assert.Contains("\"dspSema\": 524289", json, StringComparison.Ordinal);
        Assert.Contains("\"profile\": \"Accuracy\"", json, StringComparison.Ordinal);
        Assert.Contains("\"armTiming\": \"Arm60Datasheet\"", json, StringComparison.Ordinal);
        Assert.Contains("\"clioTimers\": \"OperaScheduler\"", json, StringComparison.Ordinal);
        Assert.Contains("\"celTiming\": \"OperaBusy\"", json, StringComparison.Ordinal);
        Assert.Contains("\"celBusyCycles\": 1234", json, StringComparison.Ordinal);
        Assert.Contains("\"cdromTiming\": \"OperaCycleBased\"", json, StringComparison.Ordinal);
        Assert.Contains("\"cdSpeed\": \"X8\"", json, StringComparison.Ordinal);
        Assert.Contains("\"cpuClock\": 25000000", json, StringComparison.Ordinal);
        Assert.Contains("\"autopilotLookahead\": null", json, StringComparison.Ordinal);
        Assert.Contains("\"audioDurationSeconds\": 10", json, StringComparison.Ordinal);
        Assert.Contains("\"audioPeak\": 12345", json, StringComparison.Ordinal);
        Assert.Contains("\"audioRms\": 1234.5", json, StringComparison.Ordinal);
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
        CliSelfTestResult? loaded = JsonSerializer.Deserialize(json, CliJsonContext.Default.CliSelfTestResult);

        Assert.NotNull(loaded);
        Assert.True(loaded.Ok);
        Assert.Equal(2, loaded.Checks.Count);
        Assert.Equal("deterministic frame", loaded.Checks[0].Name);
        Assert.True(loaded.Checks[0].Ok);
        Assert.Equal("state roundtrip", loaded.Checks[1].Name);
        Assert.False(loaded.Checks[1].Ok);
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
