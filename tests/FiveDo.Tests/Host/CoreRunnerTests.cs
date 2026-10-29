using System;
using System.Threading;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Machine;

namespace FiveDo.Tests.Host;

/// <summary>Threading and pacing tests for the dedicated core runner.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "AccessToDisposedClosure",
    Justification = "Callbacks complete before the runner is disposed at the end of each test.")]
public sealed class CoreRunnerTests
{
    [Fact]
    public void Start_RunsFramesAndPauseStopsAdvancing()
    {
        FiveDoMachine machine = CreateMachine();
        using var runner = new CoreRunner(machine);
        var faults = 0;
        runner.Faulted += (_, _) => Interlocked.Increment(ref faults);

        runner.Start();
        Assert.True(SpinWait.SpinUntil(() => runner.Frames >= 3, TimeSpan.FromSeconds(2)));
        runner.Pause();

        var paused = runner.Frames;
        Thread.Sleep(150);
        Assert.Equal(paused, runner.Frames);

        runner.Resume();
        Assert.True(SpinWait.SpinUntil(() => runner.Frames > paused, TimeSpan.FromSeconds(2)));
        runner.Stop();

        Assert.Equal(0, faults);
    }

    [Fact]
    public void Invoke_PausesTheLoopWhileTheActionRuns()
    {
        FiveDoMachine machine = CreateMachine();
        using var runner = new CoreRunner(machine);
        runner.Start();
        Assert.True(SpinWait.SpinUntil(() => runner.Frames >= 3, TimeSpan.FromSeconds(2)));

        var before = 0L;
        var during = 0L;
        runner.Invoke(() =>
        {
            before = runner.Frames;
            Thread.Sleep(100);
            during = runner.Frames;
        });

        Assert.Equal(before, during);
        Assert.True(SpinWait.SpinUntil(() => runner.Frames > during, TimeSpan.FromSeconds(2)));
        runner.Stop();
    }

    [Fact]
    public void PauseAndResume_PreserveThePauseStateAcrossInvoke()
    {
        FiveDoMachine machine = CreateMachine();
        using var runner = new CoreRunner(machine);
        runner.Start();
        Assert.True(SpinWait.SpinUntil(() => runner.Frames >= 3, TimeSpan.FromSeconds(2)));

        runner.Pause();
        var paused = runner.Frames;
        runner.Invoke(() => Thread.Sleep(20));
        Thread.Sleep(80);
        Assert.Equal(paused, runner.Frames);

        runner.Resume();
        Assert.True(SpinWait.SpinUntil(() => runner.Frames > paused, TimeSpan.FromSeconds(2)));
        runner.Stop();
    }

    [Fact]
    public void StepFrame_AdvancesExactlyOneFrameWhilePaused()
    {
        FiveDoMachine machine = CreateMachine();
        using var runner = new CoreRunner(machine);
        runner.Start();
        Assert.True(SpinWait.SpinUntil(() => runner.Frames >= 3, TimeSpan.FromSeconds(2)));
        runner.Pause();

        var runnerFrames = runner.Frames;
        var machineFrames = machine.FramesExecuted;
        runner.StepFrame();

        Assert.Equal(runnerFrames + 1, runner.Frames);
        Assert.Equal(machineFrames + 1, machine.FramesExecuted);
        Thread.Sleep(80);
        Assert.Equal(runnerFrames + 1, runner.Frames);

        runner.Stop();
    }

    [Fact]
    public void StepFrame_ReportsTheNewFrameToTheSink()
    {
        FiveDoMachine machine = CreateMachine();
        VdlFrame? last = null;
        using var runner = new CoreRunner(machine, frameSink: frame => last = frame);
        runner.Pause();
        runner.StepFrame();

        Assert.NotNull(last);
        Assert.Equal(1, runner.Frames);
        Assert.Equal(1, machine.FramesExecuted);
    }

    [Fact]
    public void Status_ReportsTheAudioDerivedTargetFrameRate()
    {
        FiveDoMachine machine = CreateMachine();
        CoreStatus? last = null;
        using var signal = new ManualResetEventSlim();
        using var runner = new CoreRunner(
            machine,
            statusSink: status =>
            {
                last = status;
                if (status.TargetFramesPerSecond > 0)
                {
                    signal.Set();
                }
            });

        runner.Start();
        Assert.True(signal.Wait(TimeSpan.FromSeconds(3)));
        runner.Stop();

        Assert.NotNull(last);
        Assert.InRange(last.Value.TargetFramesPerSecond, 59.5, 60.5);
    }

    private static FiveDoMachine CreateMachine()
    {
        var machine = new FiveDoMachine();
        machine.LoadBios(new byte[EmulatorConstants.RomBankSize]);
        machine.Reset();
        return machine;
    }
}
