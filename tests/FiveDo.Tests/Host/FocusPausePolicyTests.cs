using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class FocusPausePolicyTests
{
    [Fact]
    public void EnabledFocusLossPausesARunningMachine()
    {
        var policy = new FocusPausePolicy();

        Assert.True(policy.OnDeactivated(pauseOnFocusLoss: true, running: true));
        Assert.True(policy.AutoPaused);
    }

    [Fact]
    public void DisabledOrIdleFocusLossDoesNotPause()
    {
        var disabled = new FocusPausePolicy();
        Assert.False(disabled.OnDeactivated(pauseOnFocusLoss: false, running: true));
        Assert.False(disabled.AutoPaused);

        var idle = new FocusPausePolicy();
        Assert.False(idle.OnDeactivated(pauseOnFocusLoss: true, running: false));
        Assert.False(idle.AutoPaused);
    }

    [Fact]
    public void ActivationResumesOnlyAfterAnAutomaticPause()
    {
        var policy = new FocusPausePolicy();
        Assert.False(policy.OnActivated(running: false));

        policy.OnDeactivated(pauseOnFocusLoss: true, running: true);
        Assert.True(policy.OnActivated(running: false));
        Assert.False(policy.AutoPaused);
        Assert.False(policy.OnActivated(running: false));
    }

    [Fact]
    public void ManualStartOrStopClearsTheAutomaticPause()
    {
        var policy = new FocusPausePolicy();
        policy.OnDeactivated(pauseOnFocusLoss: true, running: true);

        policy.Clear();

        Assert.False(policy.AutoPaused);
        Assert.False(policy.OnActivated(running: false));
    }
}
