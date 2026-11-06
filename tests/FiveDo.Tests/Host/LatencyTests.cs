using System;
using System.Collections.Generic;
using System.Threading;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Machine;
using FiveDo.Tests.TestUtilities;
using FiveDo.Video;

namespace FiveDo.Tests.Host;

/// <summary>Run-ahead, frame-delay, snapshot, and latency-telemetry tests.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "AccessToDisposedClosure",
    Justification = "The runner is stopped before the collected frame lists are read.")]
public sealed class LatencyTests
{
    [Fact]
    public void MachineStateSnapshot_RoundtripsFrameAndContinuesDeterministically()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(5);
        uint captured = FrameHasher.Hash(machine.Frame);

        var snapshot = new MachineStateSnapshot();
        snapshot.Capture(machine);
        Assert.True(snapshot.IsValid);
        Assert.True(snapshot.Length > 0);

        machine.RunFrames(5);
        snapshot.Restore(machine);
        Assert.Equal(captured, FrameHasher.Hash(machine.Frame));

        machine.RunFrames(5);
        uint continued = FrameHasher.Hash(machine.Frame);

        // A fresh run to the same point must match the restored timeline.
        FiveDoMachine reference = CreateMachine();
        reference.RunFrames(10);
        Assert.Equal(continued, FrameHasher.Hash(reference.Frame));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void RunAhead_PresentsTheNextFramesAndAdvancesOneLogicalFramePerDisplay(int depth)
    {
        FiveDoMachine baseline = CreateMachine();
        var baselineHashes = new List<uint>();
        for (var i = 0; i < 40; i++)
        {
            baseline.RunFrame();
            baselineHashes.Add(FrameHasher.Hash(baseline.Frame));
        }

        FiveDoMachine machine = CreateMachine();
        var presented = new List<uint>();
        using var runner = new CoreRunner(machine, frame => presented.Add(FrameHasher.Hash(frame)));
        runner.RunAhead = depth;

        runner.Start();
        Assert.True(SpinWait.SpinUntil(() => presented.Count >= 10, TimeSpan.FromSeconds(5)));
        runner.Pause();

        long displayed = runner.Frames;
        long machineFrame = 0;
        runner.Invoke(() => machineFrame = machine.FramesExecuted);
        runner.Stop();

        Assert.Equal(displayed, presented.Count);
        Assert.Equal(displayed + depth, machineFrame);
        for (var i = 0; i < Math.Min(presented.Count, baselineHashes.Count - depth); i++)
        {
            Assert.Equal(baselineHashes[i + depth], presented[i]);
        }
    }

    [Fact]
    public void RunAhead_EmitsOnlyTheVisibleFrameAudio()
    {
        FiveDoMachine machine = CreateMachine();
        var audio = new RecordingAudioSink();
        machine.AudioSink = audio;
        using var runner = new CoreRunner(machine);
        runner.RunAhead = 1;

        runner.Start();
        Assert.True(SpinWait.SpinUntil(() => runner.PresentedFrames >= 10, TimeSpan.FromSeconds(5)));
        runner.Pause();
        long displayed = runner.Frames;
        runner.Stop();

        Assert.True(displayed >= 10);
        // A visible frame produces one DSP frame (~735 samples). The hidden
        // speculative frame must not add a second copy to the output.
        Assert.InRange(audio.SampleCount, (int)displayed * 100, (int)displayed * 900);
    }

    [Fact]
    public void FrameDelay_HoldsTheConfiguredNumberOfFrames()
    {
        FiveDoMachine machine = CreateMachine();
        var presented = 0;
        using var runner = new CoreRunner(machine, _ => presented++);
        runner.FrameDelay = 2;

        for (var i = 0; i < 5; i++)
        {
            runner.StepFrame();
        }

        Assert.Equal(3, presented);
        Assert.Equal(5, runner.PresentedFrames);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void RunAhead_RejectsDepthsOutsideTheSupportedRange(int depth)
    {
        FiveDoMachine machine = CreateMachine();
        using var runner = new CoreRunner(machine);
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.RunAhead = depth);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    public void FrameDelay_RejectsValuesOutsideTheSupportedRange(int delay)
    {
        FiveDoMachine machine = CreateMachine();
        using var runner = new CoreRunner(machine);
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.FrameDelay = delay);
    }

    [Fact]
    public void LatencyTelemetry_ReportsRollingMeanAndMaximum()
    {
        var telemetry = new LatencyTelemetry();
        telemetry.Record(0, 5, frequency: 1000);
        telemetry.Record(10, 25, frequency: 1000);

        Assert.Equal(2, telemetry.Samples);
        Assert.Equal(10, telemetry.MeanMilliseconds, 3);
        Assert.Equal(15, telemetry.MaxMilliseconds, 3);
        Assert.Equal(15, telemetry.LastMilliseconds, 3);

        telemetry.Reset();
        Assert.Equal(0, telemetry.Samples);
        Assert.Equal(0, telemetry.MeanMilliseconds);
    }

    private static FiveDoMachine CreateMachine()
    {
        var machine = new FiveDoMachine();
        machine.LoadBios(new byte[EmulatorConstants.RomBankSize]);
        machine.Reset();
        return machine;
    }
}
