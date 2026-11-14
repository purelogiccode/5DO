using FiveDo.Frontend.Avalonia;

namespace FiveDo.Tests.Frontend;

public sealed class EscapeQuitPolicyTests
{
    [Fact]
    public void FirstEscapePausesAndSecondEscapeQuits()
    {
        var policy = new EscapeQuitPolicy();

        Assert.False(policy.ShouldQuit(running: true));
        Assert.True(policy.IsArmed);
        Assert.True(policy.ShouldQuit(running: false));
        Assert.False(policy.IsArmed);
    }

    [Fact]
    public void EscapeWhileAlreadyPaused_ArmsThenQuits()
    {
        var policy = new EscapeQuitPolicy();

        Assert.False(policy.ShouldQuit(running: false));
        Assert.True(policy.IsArmed);
        Assert.True(policy.ShouldQuit(running: false));
    }

    [Fact]
    public void Disarm_RequiresAFreshPauseBeforeQuitting()
    {
        var policy = new EscapeQuitPolicy();

        Assert.False(policy.ShouldQuit(running: true));
        policy.Disarm();
        Assert.False(policy.ShouldQuit(running: false));
        Assert.True(policy.IsArmed);
        Assert.True(policy.ShouldQuit(running: false));
    }

    [Fact]
    public void EscapeWhileRunningStaysInThePauseStep()
    {
        var policy = new EscapeQuitPolicy();

        Assert.False(policy.ShouldQuit(running: true));
        Assert.False(policy.ShouldQuit(running: true));
        Assert.True(policy.IsArmed);
        Assert.True(policy.ShouldQuit(running: false));
    }
}
