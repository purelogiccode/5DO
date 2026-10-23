namespace ThreeDo.Madam;

/// <summary>
/// The MADAM CEL engine state machine. FreeDO: <c>FSM_IDLE</c>, <c>FSM_INPROCESS</c>,
/// and <c>FSM_SUSPENDED</c> in <c>Madam.h</c>.
/// </summary>
public enum CelFsmState : uint
{
    /// <summary>The CEL engine is idle.</summary>
    Idle = 1,

    /// <summary>The CEL engine has work queued.</summary>
    InProcess = 2,

    /// <summary>The CEL engine is paused.</summary>
    Suspended = 3,
}
