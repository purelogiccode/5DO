// Ported from rcheevos (MIT) - src/rcheevos/rc_internal.h (rc_eval_state_t).
namespace FiveDo.Achievements.Rcheevos;

/// <summary>Mutable state shared by condition sets while a trigger is evaluated.</summary>
internal sealed class RcEvalState
{
    public RcPeek? Peek;

    public object? PeekUserData;

    public RcTypedValue MeasuredValue;

    public int AddHits;

    public bool IsTrue;

    public bool IsPrimed;

    public bool IsPaused;

    public bool CanMeasure;

    public bool MeasuredFromHits;

    public bool AndNext = true;

    public bool OrNext;

    public bool ResetNext;

    public bool StopProcessing;

    public bool WasReset;

    public bool WasCondReset;

    public bool HasHits;
}
