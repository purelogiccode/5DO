namespace FiveDo.Core;

/// <summary>
/// Selects the CLIO timer scan model used by the scheduler.
/// </summary>
/// <remarks>
/// FreeDO services all sixteen timers from one 25 MHz/64 event and decrements every
/// enabled timer on each event. Opera instead walks one timer slot per 4 M25M ticks and
/// inserts a configurable slack gap after slot 15, so each timer sees a 25 MHz scan
/// cadence and cascade carry is explicit. The default remains the FreeDO-compatible
/// model so existing save states and golden frames are unchanged.
/// </remarks>
public enum ClioTimerMode
{
    /// <summary>FreeDO's all-timer scan with one event per 25 MHz/64 tick. This is the default.</summary>
    FreeDoCompatible = 0,

    /// <summary>Opera's per-slot timer scan with an explicit cascade carry.</summary>
    OperaScheduler = 1,
}
