using System;

namespace FiveDo.Madam;

/// <summary>
/// CEL second preamble word flags. FreeDO: the <c>PRE1_*</c> defines in <c>Madam.cpp</c>.
/// </summary>
/// <remarks>
/// Only the flags consumed by the pixel projector and the LR-CEL path are named here.
/// The remaining fields are masks and counts; callers use the raw <see cref="CcbHeader.Pre1"/>
/// value for those.
/// </remarks>
[Flags]
public enum CcbPreamble1 : uint
{
    /// <summary>No flags.</summary>
    None = 0,

    /// <summary>Disable the projector's H/V swap. FreeDO: <c>PRE1_NOSWAP</c>.</summary>
    NoSwap = 0x00004000,

    /// <summary>LR-form CEL data. FreeDO: <c>PRE1_LRFORM</c>.</summary>
    LrForm = 0x00000800,
}
