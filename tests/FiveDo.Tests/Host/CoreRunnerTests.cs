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
        var machine = CreateMachine();
        using var runner = new CoreRunner(machine);
        var faults = 0;
        runner.Faulted += _ => Interlocked.Increment(ref faults);

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
        var machine = CreateMachine();
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
        var machine = CreateMachine();
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

    private static FiveDoMachine CreateMachine()
    {
        var machine = new FiveDoMachine();
        machine.LoadBios(new byte[EmulatorConstants.RomBankSize]);
        machine.Reset();
        return machine;
    }
}
