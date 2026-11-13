using System;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

/// <summary>
/// Covers the run/pause transitions that keep the display awake (UX-32).
/// </summary>
public sealed class DisplaySleepPolicyTests
{
    [Fact]
    public void SetRunning_AcquiresOnlyWhileActive()
    {
        var backend = new FakeBackend();
        var policy = new DisplaySleepPolicy(backend);

        policy.SetRunning(true);
        Assert.True(policy.IsInhibited);
        Assert.True(backend.IsHeld);

        policy.SetRunning(true);
        Assert.Equal(1, backend.AcquireCount);

        policy.SetPaused(true);
        Assert.False(policy.IsInhibited);
        Assert.False(backend.IsHeld);
        Assert.Equal(1, backend.ReleaseCount);

        policy.SetPaused(false);
        Assert.True(policy.IsInhibited);
        Assert.Equal(2, backend.AcquireCount);

        policy.SetRunning(false);
        Assert.False(policy.IsInhibited);
        Assert.Equal(2, backend.ReleaseCount);
    }

    [Fact]
    public void SetPausedWhileStopped_DoesNotAcquire()
    {
        var backend = new FakeBackend();
        var policy = new DisplaySleepPolicy(backend);

        policy.SetPaused(true);
        policy.SetRunning(true);

        Assert.False(policy.IsInhibited);
        Assert.Equal(0, backend.AcquireCount);

        policy.SetPaused(false);
        Assert.True(policy.IsInhibited);
        Assert.Equal(1, backend.AcquireCount);
    }

    [Fact]
    public void Release_ReleasesTheRequestAndClearsTheState()
    {
        var backend = new FakeBackend();
        var policy = new DisplaySleepPolicy(backend);
        policy.SetRunning(true);

        policy.Release();

        Assert.False(policy.IsInhibited);
        Assert.False(backend.IsHeld);
        Assert.Equal(1, backend.ReleaseCount);

        policy.Release();
        Assert.Equal(1, backend.ReleaseCount);

        policy.SetRunning(true);
        Assert.True(policy.IsInhibited);
        Assert.Equal(2, backend.AcquireCount);
    }

    [Fact]
    public void UnsupportedBackend_NeverInhibits()
    {
        var backend = new FakeBackend { IsSupported = false, AcquireResult = false };
        var policy = new DisplaySleepPolicy(backend);

        policy.SetRunning(true);

        Assert.False(policy.IsSupported);
        Assert.False(policy.IsInhibited);
        Assert.Equal(1, backend.AcquireCount);

        policy.SetRunning(false);
        Assert.Equal(0, backend.ReleaseCount);
    }

    [Fact]
    public void AcquireFailure_DoesNotMarkTheRequestAsHeld()
    {
        var backend = new FakeBackend { AcquireResult = false };
        var policy = new DisplaySleepPolicy(backend);

        policy.SetRunning(true);

        Assert.False(policy.IsInhibited);
        policy.SetRunning(false);
        Assert.Equal(0, backend.ReleaseCount);
    }

    [Fact]
    public void Constructor_RejectsANullBackend()
    {
        Assert.Throws<ArgumentNullException>(() => new DisplaySleepPolicy(null!));
    }

    [Fact]
    public void Factory_MatchesThePlatformCapability()
    {
        IDisplaySleepBackend backend = DisplaySleepBackend.Create();

        Assert.Equal(OperatingSystem.IsWindows(), backend.IsSupported);
    }

    private sealed class FakeBackend : IDisplaySleepBackend
    {
        public bool IsSupported { get; init; } = true;

        public bool AcquireResult { get; init; } = true;

        public int AcquireCount { get; private set; }

        public int ReleaseCount { get; private set; }

        public bool IsHeld { get; private set; }

        public bool Acquire()
        {
            AcquireCount++;
            if (!AcquireResult)
            {
                return false;
            }

            IsHeld = true;
            return true;
        }

        public void Release()
        {
            ReleaseCount++;
            IsHeld = false;
        }
    }
}
