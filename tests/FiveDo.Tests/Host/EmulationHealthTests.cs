using System;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class EmulationHealthTests
{
    private static CoreStatus Status(
        double fps = 60,
        double target = 60,
        double presented = 60,
        bool runAheadAutoDisabled = false) =>
        new(Frames: 0, ProgramCounter: 0, Cycles: 0, FramesPerSecond: fps, TargetFramesPerSecond: target)
        {
            PresentedFramesPerSecond = presented,
            RunAheadAutoDisabled = runAheadAutoDisabled,
        };

    [Fact]
    public void IdleWhenNoGameIsRunning()
    {
        EmulationHealth health = EmulationHealthEvaluator.Evaluate(
            running: false,
            Status(fps: 0, target: 0, presented: 0),
            audioUnderruns: 5,
            audioDropped: 10,
            previousAudioUnderruns: 0,
            previousAudioDropped: 0);

        Assert.Equal(EmulationHealthState.Idle, health.State);
        Assert.Equal("Idle", health.Label);
    }

    [Fact]
    public void GoodAtTargetWithCleanAudio()
    {
        EmulationHealth health = EmulationHealthEvaluator.Evaluate(
            running: true, Status(), 0, 0, 0, 0);

        Assert.Equal(EmulationHealthState.Good, health.State);
        Assert.Equal("OK", health.Label);
    }

    [Fact]
    public void WarningBelowNinetyFivePercentOfTarget()
    {
        EmulationHealth health = EmulationHealthEvaluator.Evaluate(
            running: true, Status(fps: 54, target: 60, presented: 54), 0, 0, 0, 0);

        Assert.Equal(EmulationHealthState.Warning, health.State);
        Assert.Contains("54.0 of 60", health.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void CriticalBelowSeventyFivePercentOfTarget()
    {
        EmulationHealth health = EmulationHealthEvaluator.Evaluate(
            running: true, Status(fps: 42, target: 60, presented: 42), 0, 0, 0, 0);

        Assert.Equal(EmulationHealthState.Critical, health.State);
        Assert.Equal("Issue", health.Label);
    }

    [Fact]
    public void PalTargetUsesFiftyHertz()
    {
        EmulationHealth health = EmulationHealthEvaluator.Evaluate(
            running: true, Status(fps: 49, target: 50, presented: 49), 0, 0, 0, 0);

        Assert.Equal(EmulationHealthState.Good, health.State);
    }

    [Fact]
    public void WarningOnAudioUnderrunDelta()
    {
        EmulationHealth health = EmulationHealthEvaluator.Evaluate(
            running: true, Status(), audioUnderruns: 4, audioDropped: 3, previousAudioUnderruns: 3, previousAudioDropped: 3);

        Assert.Equal(EmulationHealthState.Warning, health.State);
        Assert.Contains("1 buffer", health.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void CriticalOnThreeUnderrunsInOneTick()
    {
        EmulationHealth health = EmulationHealthEvaluator.Evaluate(
            running: true, Status(), audioUnderruns: 10, audioDropped: 0, previousAudioUnderruns: 7, previousAudioDropped: 0);

        Assert.Equal(EmulationHealthState.Critical, health.State);
        Assert.Contains("3 underrun", health.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void HistoricUnderrunsDoNotKeepTheBadgeAmber()
    {
        EmulationHealth health = EmulationHealthEvaluator.Evaluate(
            running: true, Status(), audioUnderruns: 9, audioDropped: 12, previousAudioUnderruns: 9, previousAudioDropped: 12);

        Assert.Equal(EmulationHealthState.Good, health.State);
    }

    [Fact]
    public void WarningWhenTheDisplayFallsBehind()
    {
        EmulationHealth health = EmulationHealthEvaluator.Evaluate(
            running: true, Status(fps: 60, target: 60, presented: 40), 0, 0, 0, 0);

        Assert.Equal(EmulationHealthState.Warning, health.State);
        Assert.Contains("display shows 40.0", health.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void WarningWhenRunAheadDisabledItself()
    {
        EmulationHealth health = EmulationHealthEvaluator.Evaluate(
            running: true, Status(runAheadAutoDisabled: true), 0, 0, 0, 0);

        Assert.Equal(EmulationHealthState.Warning, health.State);
        Assert.Contains("Run-ahead", health.Details, StringComparison.Ordinal);
    }
}
