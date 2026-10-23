using System;

namespace ThreeDo.Dsp;

/// <summary>
/// Bit-field decode of one 16-bit DSP instruction word.
/// </summary>
/// <remarks>
/// FreeDO: the <c>AIFTAG</c>, <c>CIFTAG</c>, <c>IOFTAG</c>, <c>NROFTAG</c>, <c>R2OFTAG</c>,
/// <c>R3OFTAG</c>, <c>BRNTAG</c>, and <c>BRNBITS</c> unions in <c>DSP.cpp</c>. Fields overlap;
/// which interpretation applies depends on <see cref="AifPad"/> and the operand type bits.
/// One-bit signed fields (DI, JUSTIFY, PAD, M2SEL) are exposed as <c>-1</c>/<c>0</c> to match
/// the C bit-field semantics exactly.
/// </remarks>
internal readonly struct DspInstruction
{
    private readonly ushort _raw;

    internal DspInstruction(ushort raw) => _raw = raw;

    internal ushort Raw => _raw;

    // AIFTAG: ALU instruction fields.
    internal int AifBs => _raw & 0xf;

    internal int AifAlu => (_raw >> 4) & 0xf;

    internal int AifMuxB => (_raw >> 8) & 3;

    internal int AifMuxA => (_raw >> 10) & 3;

    internal int AifM2Sel => ((_raw >> 12) & 1) != 0 ? -1 : 0;

    internal int AifNumOps => (_raw >> 13) & 3;

    internal bool AifPad => ((_raw >> 15) & 1) != 0;

    // CIFTAG: control instruction fields.
    internal int CifBchAddr => _raw & 0x3ff;

    internal int CifFlagMask => (_raw >> 10) & 3;

    internal int CifFlagSel => (_raw >> 12) & 1;

    internal int CifMode => (_raw >> 13) & 3;

    // IOFTAG: immediate operand fields.
    internal int IofImmediate => (_raw & 0x1000) != 0 ? (_raw & 0x1fff) - 0x2000 : _raw & 0x1fff;

    internal int IofJustify => ((_raw >> 13) & 1) != 0 ? -1 : 0;

    internal int IofType => (_raw >> 14) & 3;

    // NROFTAG: non-register operand fields.
    internal int NrofOpAddr => _raw & 0x3ff;

    internal int NrofDi => ((_raw >> 10) & 1) != 0 ? -1 : 0;

    internal int NrofWb1 => (_raw >> 11) & 1;

    internal int NrofType => (_raw >> 13) & 7;

    // R3OFTAG: three-register operand fields.
    internal int R1 => _raw & 0xf;

    internal int R1Di => ((_raw >> 4) & 1) != 0 ? -1 : 0;

    internal int R2 => (_raw >> 5) & 0xf;

    internal int R2Di => ((_raw >> 9) & 1) != 0 ? -1 : 0;

    internal int R3 => (_raw >> 10) & 0xf;

    internal int R3Di => ((_raw >> 14) & 1) != 0 ? -1 : 0;

    // R2OFTAG: one- or two-register operand fields.
    internal int R2NumRegs => (_raw >> 10) & 1;

    internal int R2Wb1 => (_raw >> 11) & 1;

    internal int R2Wb2 => (_raw >> 12) & 1;

    internal int R2Type => (_raw >> 13) & 7;

    // R3OFTAG shares the low 16 bits; only the TYPE width differs.
    internal int R3Type => (_raw >> 15) & 1;

    // BRNTAG / BRNBITS: conditional branch fields.
    internal int BranchFlagM0 => (_raw >> 10) & 1;

    internal int BranchFlagM1 => (_raw >> 11) & 1;

    internal int BranchFlagSel => (_raw >> 12) & 1;

    internal int BranchMode0 => (_raw >> 13) & 1;

    internal int BranchMode1 => (_raw >> 14) & 1;

    internal int BranchAc => (_raw >> 15) & 1;

    internal int BranchBits => (_raw >> 10) & 0x1f;
}
