using System;

namespace ThreeDo.Core;

/// <summary>
/// Per-game compatibility fixes. Values match FreeDOCore's <c>FIX_BIT_*</c> masks.
/// </summary>
/// <remarks>
/// These flags are applied through <see cref="EmulatorTuning.FixMode"/> and are preserved
/// exactly as the reference defines them. Do not renumber them; game databases and save
/// states may persist the raw mask.
/// </remarks>
[Flags]
public enum FixMode
{
    /// <summary>No compatibility fixes.</summary>
    None = 0,

    /// <summary>FreeDO <c>FIX_BIT_TIMING_1</c>: Crash 'n Burn LDM value/PC fixes.</summary>
    BitTiming1 = 0x00000001,

    /// <summary>FreeDO <c>FIX_BIT_TIMING_2</c>: reserved.</summary>
    BitTiming2 = 0x00000002,

    /// <summary>FreeDO <c>FIX_BIT_TIMING_3</c>: Dinopark Tycoon scale Y * 5.</summary>
    BitTiming3 = 0x00000004,

    /// <summary>FreeDO <c>FIX_BIT_TIMING_4</c>: reserved.</summary>
    BitTiming4 = 0x00000008,

    /// <summary>FreeDO <c>FIX_BIT_TIMING_5</c>: Microcosm VDL bitmap pointer swap.</summary>
    BitTiming5 = 0x00000010,

    /// <summary>FreeDO <c>FIX_BIT_TIMING_6</c>: Alone in the Dark literal CEL Y step.</summary>
    BitTiming6 = 0x00000020,

    /// <summary>FreeDO <c>FIX_BIT_TIMING_7</c>: reserved.</summary>
    BitTiming7 = 0x00000040,

    /// <summary>FreeDO <c>FIX_BIT_GRAPHICS_STEP_Y</c>: Samurai Shodown CEL Y step.</summary>
    GraphicsStepY = 0x00080000,
}
