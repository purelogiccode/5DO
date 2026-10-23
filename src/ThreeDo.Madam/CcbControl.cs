using System;

namespace ThreeDo.Madam;

/// <summary>
/// CEL control-block flags. FreeDO: the <c>CCB_*</c> defines in <c>Madam.cpp</c>.
/// </summary>
/// <remarks>
/// Values are persisted in guest memory and must not be renumbered. The names are expanded
/// for C# readability; the FreeDO names are recorded in the comments.
/// </remarks>
[Flags]
public enum CcbControl : uint
{
    /// <summary>No flags.</summary>
    None = 0,

    /// <summary>Skip rendering this CEL. FreeDO: <c>CCB_SKIP</c>.</summary>
    Skip = 0x80000000,

    /// <summary>Last CEL in the list. FreeDO: <c>CCB_LAST</c>.</summary>
    Last = 0x40000000,

    /// <summary>Next-CCB pointer is absolute. FreeDO: <c>CCB_NPABS</c>.</summary>
    NextPointerAbsolute = 0x20000000,

    /// <summary>Source-data pointer is absolute. FreeDO: <c>CCB_SPABS</c>.</summary>
    SourcePointerAbsolute = 0x10000000,

    /// <summary>PLUT pointer is absolute. FreeDO: <c>CCB_PPABS</c>.</summary>
    PlutPointerAbsolute = 0x08000000,

    /// <summary>Load size registers. FreeDO: <c>CCB_LDSIZE</c>.</summary>
    LoadSize = 0x04000000,

    /// <summary>Load perspective registers. FreeDO: <c>CCB_LDPRS</c>.</summary>
    LoadPerspective = 0x02000000,

    /// <summary>Load pixel-processor mode. FreeDO: <c>CCB_LDPPMP</c>.</summary>
    LoadPixelProcessor = 0x01000000,

    /// <summary>Load the PLUT. FreeDO: <c>CCB_LDPLUT</c>.</summary>
    LoadPlut = 0x00800000,

    /// <summary>Preamble words are in the CCB. FreeDO: <c>CCB_CCBPRE</c>.</summary>
    CcbPreamble = 0x00400000,

    /// <summary>Position fields are present. FreeDO: <c>CCB_YOXY</c>.</summary>
    HasPosition = 0x00200000,

    /// <summary>Sub-code word is present. FreeDO: <c>CCB_ACSC</c>.</summary>
    SubCode = 0x00100000,

    /// <summary>Sub-code length is present. FreeDO: <c>CCB_ALSC</c>.</summary>
    SubCodeLength = 0x00080000,

    /// <summary>Clockwise CEL winding. FreeDO: <c>CCB_ACW</c>.</summary>
    Clockwise = 0x00040000,

    /// <summary>Counter-clockwise CEL winding. FreeDO: <c>CCB_ACCW</c>.</summary>
    CounterClockwise = 0x00020000,

    /// <summary>Two-dimensional flag. FreeDO: <c>CCB_TWD</c>.</summary>
    TwoDimensional = 0x00010000,

    /// <summary>Line-color enable. FreeDO: <c>CCB_LCE</c>.</summary>
    LineColorEnable = 0x00008000,

    /// <summary>Attribute-color enable. FreeDO: <c>CCB_ACE</c>.</summary>
    AttributeColorEnable = 0x00004000,

    /// <summary>Reserved bit 13. FreeDO: <c>CCB_reserved13</c>.</summary>
    Reserved13 = 0x00002000,

    /// <summary>Maria flag. FreeDO: <c>CCB_MARIA</c>.</summary>
    Maria = 0x00001000,

    /// <summary>Pixel XOR mode. FreeDO: <c>CCB_PXOR</c>.</summary>
    PixelXor = 0x00000800,

    /// <summary>Use the AV signal. FreeDO: <c>CCB_USEAV</c>.</summary>
    UseAv = 0x00000400,

    /// <summary>Packed CEL data. FreeDO: <c>CCB_PACKED</c>.</summary>
    Packed = 0x00000200,

    /// <summary>Pixel-over mode mask. FreeDO: <c>CCB_POVER_MASK</c>.</summary>
    PixelOverMask = 0x00000180,

    /// <summary>PLUT position. FreeDO: <c>CCB_PLUTPOS</c>.</summary>
    PlutPosition = 0x00000040,

    /// <summary>Background flag. FreeDO: <c>CCB_BGND</c>.</summary>
    Background = 0x00000020,

    /// <summary>No-blocking flag. FreeDO: <c>CCB_NOBLK</c>.</summary>
    NoBlock = 0x00000010,

    /// <summary>PLUT offset mask. FreeDO: <c>CCB_PLUTA_MASK</c>.</summary>
    PlutOffsetMask = 0x0000000f,
}
