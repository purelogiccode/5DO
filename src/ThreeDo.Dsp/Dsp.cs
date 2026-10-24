using System;
using System.Collections.Generic;
using ThreeDo.Core;

namespace ThreeDo.Dsp;

/// <summary>
/// The 3DO audio DSP: a 16-bit fixed-point processor with a 1024-word data memory,
/// a 2048-word program memory, register windows, FIFO ports, and the ARM semaphore.
/// </summary>
/// <remarks>
/// <para>
/// This is a direct port of FreeDOCore's <c>DSP.cpp</c>. The reference keeps nearly all state
/// in the file-scope <c>dsp</c> struct; this class owns one copy so multiple machines and tests
/// remain independent.
/// </para>
/// <para>
/// Deliberate deviations:
/// </para>
/// <list type="bullet">
/// <item>Instruction fetches mask the program counter to 10 bits. The reference indexes
/// <c>NMem[PC++]</c> without a mask and relies on valid DSP programs; masking keeps malformed
/// programs deterministic instead of throwing.</item>
/// <item>Debug logging from <c>_Arithmetic_Debug</c> and FIFO-direct writes is omitted; the
/// arithmetic itself is unchanged.</item>
/// <item>Where the reference calls <c>fastrand()</c> twice in one expression with unspecified
/// evaluation order, the two calls are made left to right and the low halfword is returned,
/// matching the observed MSVC result.</item>
/// </list>
/// </remarks>
public sealed class Dsp : IDsp, IStateSection
{
    private const uint TopBit = 0x80000000u;

    // Request bits, RQFTAG: BS=1, ALU2=2, ALU1=4, MULT2=8, MULT1=16.
    private const byte RequestBs = 0x01;
    private const byte RequestAlu2 = 0x02;
    private const byte RequestAlu1 = 0x04;
    private const byte RequestMult2 = 0x08;
    private const byte RequestMult1 = 0x10;

    private readonly byte[] _requestTable = new byte[0x8000];
    private readonly byte[] _bsTable = new byte[0x8000];
    private readonly ushort[,] _regConv = new ushort[8, 16];
    private readonly bool[,] _branchCond = new bool[32, 32];
    private readonly ushort[] _nmem = new ushort[2048];
    private readonly ushort[] _imem = new ushort[1024];
    private readonly bool[] _cpuSupply = new bool[16];

    // REGSTAG.
    private uint _pc;
    private ushort _noise;
    private ushort _audioOutStatus;
    private ushort _sema4Status;
    private ushort _sema4Data;
    private short _dspPcnt;
    private short _dspPrld;
    private short _audCnt;
    private ushort _interrupt;

    // INTAG.
    private short _mult1;
    private short _mult2;
    private short _alu1;
    private short _alu2;
    private int _bs;
    private ushort _opMask;
    private ushort _writeback;
    private byte _requests;
    private bool _running;
    private bool _genFiq;
    private long _instructionsExecuted;
    private readonly Queue<DspTraceEntry> _dspTrace = new();

    private uint _rbaseX4;
    private int _regi;
    private uint _seed;

    /// <summary>Initializes a new instance of the <see cref="Dsp"/> class and precomputes tables.</summary>
    public Dsp()
    {
        Initialize();
    }

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Dsp;

    /// <summary>Gets the save-state section version.</summary>
    public ushort Version => 1;

    /// <summary>Gets or sets the CLIO host used for FIFOs and FIQs. May be null in unit tests.</summary>
    public IDspHost? Host { get; set; }

    /// <summary>Gets a value indicating whether the DSP is running.</summary>
    public bool IsRunning => _running;

    /// <summary>Gets the current PRNG seed.</summary>
    public uint Seed => _seed;

    /// <summary>Gets the program counter.</summary>
    public uint ProgramCounter => _pc;

    /// <summary>Gets the semaphore status/acknowledge latch.</summary>
    public ushort SemaphoreStatus => _sema4Status;

    /// <summary>Gets the semaphore data latch.</summary>
    public ushort SemaphoreData => _sema4Data;

    /// <summary>Gets the audio reload count.</summary>
    public short PeriodReload => _dspPrld;

    /// <summary>Gets the audio counter.</summary>
    public short AudioCounter => _audCnt;

    /// <summary>Gets the number of DSP instructions executed since startup (diagnostics).</summary>
    public long InstructionsExecuted => _instructionsExecuted;

    /// <summary>Gets or sets a value indicating whether DSP instructions are recorded.</summary>
    public bool DspTraceEnabled { get; set; }

    /// <summary>Gets or sets the DSP trace ring capacity.</summary>
    public int DspTraceCapacity { get; set; } = 4096;

    /// <summary>Gets the recorded DSP instructions, oldest first.</summary>
    public IReadOnlyCollection<DspTraceEntry> DspTrace => _dspTrace;

    /// <summary>Clears the DSP trace ring.</summary>
    public void ClearDspTrace() => _dspTrace.Clear();

    /// <summary>One recorded DSP instruction.</summary>
    /// <param name="Pc">Program counter before execution.</param>
    /// <param name="Instruction">Raw instruction word.</param>
    /// <param name="SemaphoreStatus">Sema4 status before execution.</param>
    /// <param name="SemaphoreData">Sema4 data before execution.</param>
    /// <param name="NextPc">Program counter after the instruction fetch.</param>
    public readonly record struct DspTraceEntry(
        uint Pc,
        ushort Instruction,
        ushort SemaphoreStatus,
        ushort SemaphoreData,
        uint NextPc);

    /// <summary>Writes the DSP save-state payload.</summary>
    /// <param name="writer">Destination writer.</param>
    public void Save(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteUInt32(_pc);
        writer.WriteUInt16(_noise);
        writer.WriteUInt16(_audioOutStatus);
        writer.WriteUInt16(_sema4Status);
        writer.WriteUInt16(_sema4Data);
        writer.WriteUInt16(unchecked((ushort)_dspPcnt));
        writer.WriteUInt16(unchecked((ushort)_dspPrld));
        writer.WriteUInt16(unchecked((ushort)_audCnt));
        writer.WriteUInt16(_interrupt);
        writer.WriteUInt16(unchecked((ushort)_mult1));
        writer.WriteUInt16(unchecked((ushort)_mult2));
        writer.WriteUInt16(unchecked((ushort)_alu1));
        writer.WriteUInt16(unchecked((ushort)_alu2));
        writer.WriteInt32(_bs);
        writer.WriteUInt16(_opMask);
        writer.WriteUInt16(_writeback);
        writer.WriteByte(_requests);
        writer.WriteBoolean(_running);
        writer.WriteBoolean(_genFiq);
        writer.WriteUInt32(_rbaseX4);
        writer.WriteInt32(_regi);
        writer.WriteUInt32(_seed);
        foreach (var supply in _cpuSupply)
        {
            writer.WriteBoolean(supply);
        }

        writer.WriteBytes(ToStateBytes(_nmem));
        writer.WriteBytes(ToStateBytes(_imem));
    }

    /// <summary>Reads the DSP save-state payload.</summary>
    /// <param name="reader">Source reader.</param>
    public void Load(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _pc = reader.ReadUInt32();
        _noise = reader.ReadUInt16();
        _audioOutStatus = reader.ReadUInt16();
        _sema4Status = reader.ReadUInt16();
        _sema4Data = reader.ReadUInt16();
        _dspPcnt = unchecked((short)reader.ReadUInt16());
        _dspPrld = unchecked((short)reader.ReadUInt16());
        _audCnt = unchecked((short)reader.ReadUInt16());
        _interrupt = reader.ReadUInt16();
        _mult1 = unchecked((short)reader.ReadUInt16());
        _mult2 = unchecked((short)reader.ReadUInt16());
        _alu1 = unchecked((short)reader.ReadUInt16());
        _alu2 = unchecked((short)reader.ReadUInt16());
        _bs = reader.ReadInt32();
        _opMask = reader.ReadUInt16();
        _writeback = reader.ReadUInt16();
        _requests = reader.ReadByte();
        _running = reader.ReadBoolean();
        _genFiq = reader.ReadBoolean();
        _rbaseX4 = reader.ReadUInt32();
        _regi = reader.ReadInt32();
        _seed = reader.ReadUInt32();
        for (var i = 0; i < _cpuSupply.Length; i++)
        {
            _cpuSupply[i] = reader.ReadBoolean();
        }

        reader.ReadBytes(ToStateBytes(_nmem));
        reader.ReadBytes(ToStateBytes(_imem));
    }

    private static Span<byte> ToStateBytes(ushort[] words)
    {
        if (!BitConverter.IsLittleEndian)
        {
            throw new PlatformNotSupportedException("Save states require a little-endian host.");
        }

        return System.Runtime.InteropServices.MemoryMarshal.AsBytes(words.AsSpan());
    }

    /// <inheritdoc />
    public void Reset()
    {
        // FreeDO: _dsp_Reset.
        _dspPcnt = _dspPrld;
        _pc = 0;
        _rbaseX4 = 0;
        _regi = 0;
        _opMask = 0xffff;
    }

    /// <inheritdoc />
    public void SetRunning(bool running) => _running = running;

    /// <inheritdoc />
    public void WriteMemory(int address, ushort value) => _nmem[address & 0x3ff] = value;

    /// <summary>Reads one word of DSP data memory (diagnostics and tests).</summary>
    /// <param name="address">Data-memory address; masked to 11 bits.</param>
    /// <returns>The data word.</returns>
    public ushort ReadProgramMemory(int address) => _nmem[address & 0x7ff];

    /// <summary>Copies the DSP instruction memory for diagnostics.</summary>
    /// <param name="destination">Destination of at least 1024 words.</param>
    public void CopyInstructionMemory(Span<ushort> destination) => _imem.AsSpan().CopyTo(destination);

    /// <summary>Copies the DSP data memory for diagnostics.</summary>
    /// <param name="destination">Destination of at least 2048 words.</param>
    public void CopyDataMemory(Span<ushort> destination) => _nmem.AsSpan().CopyTo(destination);

    /// <inheritdoc />
    public void WriteInstructionMemory(int address, ushort value)
    {
        // FreeDO: _dsp_WriteIMem. The 0x70-0x7C range is the ARM-direct coefficient window.
        if (address is >= 0x70 and <= 0x7c)
        {
            _cpuSupply[address - 0x70] = true;
            _imem[address & 0x7f] = value;
            return;
        }

        if ((address & 0x80) == 0)
        {
            _imem[address & 0x7f] = value;
        }
    }

    /// <inheritdoc />
    public ushort ReadInstructionMemory(int address)
    {
        // FreeDO: _dsp_ReadIMem.
        return address switch
        {
            0x3eb => _audioOutStatus,
            0x3ec => _sema4Status,
            0x3ed => _sema4Data,
            0x3ee => _interrupt,
            0x3ef => unchecked((ushort)_dspPrld),
            _ => _imem[address],
        };
    }

    /// <inheritdoc />
    public void ArmWriteSemaphore(uint value)
    {
        // FreeDO: _dsp_ARMwrite2sema4.
        _sema4Data = (ushort)(value & 0xffff);
        _sema4Status = 0x8;
    }

    /// <inheritdoc />
    public uint ArmReadSemaphore()
    {
        // FreeDO: _dsp_ARMread2sema4.
        return ((uint)_sema4Status << 16) | _sema4Data;
    }

    /// <inheritdoc />
    public uint FastRandom()
    {
        _seed = unchecked((69069 * _seed) + 1);
        return _seed & 0xffff;
    }

    /// <inheritdoc />
    public uint Loop()
    {
        if (!_running)
        {
            return ((uint)_imem[0x3ff] << 16) | _imem[0x3fe];
        }

        Reset();

        var flags = new DspFlags();
        var exact = false;
        uint aop = 0;
        uint bop = 0;
        var y = 0u;
        uint returnAddress = 0;

        var work = true;
        do
        {
            var pc = _pc;
            var raw = _nmem[_pc++ & 0x3ff];
            _instructionsExecuted++;
            if (DspTraceEnabled)
            {
                if (DspTraceCapacity > 0 && _dspTrace.Count >= DspTraceCapacity)
                {
                    _dspTrace.Dequeue();
                }

                _dspTrace.Enqueue(new DspTraceEntry(pc, raw, _sema4Status, _sema4Data, _pc));
            }

            var inst = new DspInstruction(raw);

            if (inst.AifPad)
            {
                var op = (raw >> 7) & 0xff;
                switch (op)
                {
                    case 0: // NOP
                        break;
                    case 1: // branch accumulator
                        _pc = (y >> 16) & 0x3ff;
                        break;
                    case 2: // set rbase
                        _rbaseX4 = (uint)(inst.CifBchAddr & 0x3f) << 2;
                        break;
                    case 3: // set rmap
                        _regi = inst.CifBchAddr & 7;
                        break;
                    case 4: // RTS
                        _pc = returnAddress;
                        break;
                    case 5: // set op_mask
                        _opMask = (ushort)~(inst.CifBchAddr & 0x1f);
                        break;
                    case 6: // not used
                        break;
                    case 7: // SLEEP
                        work = false;
                        break;
                    case >= 8 and <= 15: // jump
                        _pc = (uint)inst.CifBchAddr;
                        break;
                    case >= 16 and <= 23: // JSR
                        returnAddress = _pc;
                        _pc = (uint)inst.CifBchAddr;
                        break;
                    case >= 24 and <= 31: // branch if was branched
                        _pc = (uint)inst.CifBchAddr;
                        break;
                    case >= 32 and <= 47: // MOVEREG
                        {
                            var operand = OperandLoaderNoWriteback();
                            var register = _regConv[_regi, inst.R1] ^ _rbaseX4;
                            if (inst.R1Di != 0)
                            {
                                WriteData(ReadData(register), (ushort)operand);
                            }
                            else
                            {
                                WriteData(register, (ushort)operand);
                            }
                        }

                        break;
                    case >= 48 and <= 63: // MOVE
                        {
                            var operand = OperandLoaderNoWriteback();
                            if (inst.NrofDi != 0)
                            {
                                WriteData(ReadData((uint)inst.CifBchAddr), (ushort)operand);
                            }
                            else
                            {
                                WriteData((uint)inst.CifBchAddr, (ushort)operand);
                            }
                        }

                        break;
                    default: // conditional branch
                        if (_branchCond[inst.BranchBits, (exact ? 1 : 0) | FlagsIndex(flags)])
                        {
                            _pc = (uint)inst.CifBchAddr;
                        }

                        break;
                }

                continue;
            }

            // ALU instruction.
            _requests = _requestTable[raw];
            _bs = _bsTable[raw];

            OperandLoader(inst.AifNumOps, flags);

            switch (inst.AifMuxA)
            {
                case 3:
                    if (inst.AifM2Sel == 0)
                    {
                        if (inst.AifAlu is 3 or 5)
                        {
                            aop = flags.Carry ? (uint)(_mult1 << 16) : 0;
                        }
                        else
                        {
                            aop = unchecked((uint)(_mult1 * (((int)y >> 15) & ~1)));
                        }
                    }
                    else
                    {
                        aop = unchecked((uint)(_mult1 * _mult2 * 2));
                    }

                    break;
                case 1:
                    aop = unchecked((uint)(_alu1 << 16));
                    break;
                case 0:
                    aop = y;
                    break;
                case 2:
                    aop = unchecked((uint)(_alu2 << 16));
                    break;
            }

            if (inst.AifAlu is 3 or 5)
            {
                bop = flags.Carry ? 0x10000u : 0u;
            }
            else
            {
                switch (inst.AifMuxB)
                {
                    case 0:
                        bop = y;
                        break;
                    case 1:
                        bop = unchecked((uint)(_alu1 << 16));
                        break;
                    case 2:
                        bop = unchecked((uint)(_alu2 << 16));
                        break;
                    case 3:
                        if (inst.AifM2Sel == 0)
                        {
                            bop = unchecked((uint)(_mult1 * (((int)y >> 15) & ~1)));
                        }
                        else
                        {
                            bop = unchecked((uint)(_mult1 * _mult2 * 2));
                        }

                        break;
                }
            }

            flags.Over = false;
            flags.Carry = false;

            switch (inst.AifAlu)
            {
                case 0:
                    y = aop;
                    break;
                case 1:
                    y = unchecked(0 - bop);
                    flags.Carry = SubCFlag(0, bop, y);
                    flags.Over = SubVFlag(0, bop, y);
                    break;
                case 2:
                case 3:
                    y = unchecked(aop + bop);
                    flags.Carry = AddCFlag(aop, bop, y);
                    flags.Over = AddVFlag(aop, bop, y);
                    break;
                case 4:
                case 5:
                    y = unchecked(aop - bop);
                    flags.Carry = SubCFlag(aop, bop, y);
                    flags.Over = SubVFlag(aop, bop, y);
                    break;
                case 6:
                    y = unchecked(aop + 0x1000);
                    flags.Carry = AddCFlag(aop, 0x1000, y);
                    flags.Over = AddVFlag(aop, 0x1000, y);
                    break;
                case 7:
                    y = unchecked(aop - 0x1000);
                    flags.Carry = SubCFlag(aop, 0x1000, y);
                    flags.Over = SubVFlag(aop, 0x1000, y);
                    break;
                case 8:
                    y = aop;
                    break;
                case 9:
                    y = aop ^ 0xffffffffu;
                    break;
                case 10:
                    y = aop & bop;
                    break;
                case 11:
                    y = (aop & bop) ^ 0xffffffffu;
                    break;
                case 12:
                    y = aop | bop;
                    break;
                case 13:
                    y = (aop | bop) ^ 0xffffffffu;
                    break;
                case 14:
                    y = aop ^ bop;
                    break;
                case 15:
                    y = aop ^ bop ^ 0xffffffffu;
                    break;
            }

            flags.Zero = (y & 0xffff0000) == 0;
            flags.Nega = (y >> 31) != 0;
            exact = (y & 0x0000f000) == 0;

            switch (_bs)
            {
                case 1:
                case 17:
                    y <<= 1;
                    break;
                case 2:
                case 18:
                    y <<= 2;
                    break;
                case 3:
                case 19:
                    y <<= 3;
                    break;
                case 4:
                case 20:
                    y <<= 4;
                    break;
                case 5:
                case 21:
                    y <<= 5;
                    break;
                case 6:
                case 22:
                    y <<= 8;
                    break;
                case 9:
                    y = unchecked((uint)((int)y >> 16));
                    break;
                case 10:
                    y = unchecked((uint)((int)y >> 8));
                    break;
                case 11:
                    y = unchecked((uint)((int)y >> 5));
                    break;
                case 12:
                    y = unchecked((uint)((int)y >> 4));
                    break;
                case 13:
                    y = unchecked((uint)((int)y >> 3));
                    break;
                case 14:
                    y = unchecked((uint)((int)y >> 2));
                    break;
                case 15:
                    y = unchecked((uint)((int)y >> 1));
                    break;
                case 7:
                case 23:
                    if (flags.Over)
                    {
                        y = flags.Nega ? 0x7ffff000u : 0x80000000u;
                    }

                    break;
                case 8:
                case 24:
                    flags.Carry = (int)y < 0;
                    y = ((y << 1) & 0xfffe0000u) | (flags.Carry ? 1u << 16 : 0) | (y & 0xf000u);
                    break;
                case 25:
                    y >>= 16;
                    break;
                case 26:
                    y >>= 8;
                    break;
                case 27:
                    y >>= 5;
                    break;
                case 28:
                    y >>= 4;
                    break;
                case 29:
                    y >>= 3;
                    break;
                case 30:
                    y >>= 2;
                    break;
                case 31:
                    y >>= 1;
                    break;
            }

            if (_writeback != 0)
            {
                // The reference writes the arithmetic-shifted high halfword.
                WriteData(_writeback, unchecked((ushort)((int)y >> 16)));
            }
        }
        while (work);

        if (_genFiq)
        {
            _genFiq = false;
            Host?.GenerateFiq(0x800, 0);
        }

        _dspPcnt = unchecked((short)(_dspPcnt - 567));
        if (_dspPcnt <= 0)
        {
            _dspPcnt = unchecked((short)(_dspPcnt + _dspPrld));
        }

        return ((uint)_imem[0x3ff] << 16) | _imem[0x3fe];
    }

    private static int FlagsIndex(DspFlags flags)
    {
        // Flags.raw * 0x10080402 >> 24 with little-endian byte order packs as Z,N,C,O.
        return (flags.Zero ? 16 : 0)
            | (flags.Nega ? 8 : 0)
            | (flags.Carry ? 4 : 0)
            | (flags.Over ? 2 : 0);
    }

    private void Initialize()
    {
        _seed = 0xa5a5a5a5;

        for (var rmap = 0; rmap < 8; rmap++)
        {
            for (var reg = 0; reg < 16; reg++)
            {
                _regConv[rmap, reg] = RegBase(reg, (ushort)rmap);
            }
        }

        for (var raw = 0; raw < 0x8000; raw++)
        {
            var inst = new DspInstruction((ushort)raw);
            byte requests = 0;

            if (inst.AifBs == 0x8)
            {
                requests |= RequestBs;
            }

            switch (inst.AifMuxA)
            {
                case 3:
                    requests |= RequestMult1;
                    if (inst.AifM2Sel != 0)
                    {
                        requests |= RequestMult2;
                    }

                    break;
                case 1:
                    requests |= RequestAlu1;
                    break;
                case 2:
                    requests |= RequestAlu2;
                    break;
            }

            switch (inst.AifMuxB)
            {
                case 1:
                    requests |= RequestAlu1;
                    break;
                case 2:
                    requests |= RequestAlu2;
                    break;
                case 3:
                    requests |= RequestMult1;
                    if (inst.AifM2Sel != 0)
                    {
                        requests |= RequestMult2;
                    }

                    break;
            }

            _requestTable[raw] = requests;
            _bsTable[raw] = (byte)(inst.AifBs | ((inst.AifAlu & 8) << 1));
        }

        BuildBranchTable();

        _running = false;
        _genFiq = false;
        _dspPrld = 567;
        _audCnt = 567;

        Reset();

        _sema4Status = 0;
        Array.Fill(_nmem, (ushort)0x8380);
        Array.Clear(_cpuSupply);
    }

    private void BuildBranchTable()
    {
        // FreeDO: _dsp_Init's BRCONDTAB precompute. The reference loops over 24 base opcodes
        // (0xA000..0xFC00 step 0x400), which covers branch bit values 8..31, and over all
        // 16 flag combinations times the exact flag.
        for (var raw = 0xA000; raw <= 0xFFFF; raw += 1024)
        {
            var inst = new DspInstruction((ushort)raw);
            for (var flagBits = 0; flagBits < 16; flagBits++)
            {
                var zero = (flagBits & 1) != 0;
                var nega = (flagBits & 2) != 0;
                var carry = (flagBits & 4) != 0;
                var over = (flagBits & 8) != 0;

                var mode0 = inst.BranchMode0 != 0;
                var mode1 = inst.BranchMode1 != 0;
                var md1 = !mode1 && mode0;
                var md2 = mode1 && !mode0;
                var md3 = mode1 && mode0;

                var stat0 = inst.BranchFlagSel != 0 ? carry : nega;
                var stat1 = inst.BranchFlagSel != 0 ? zero : over;
                var nstat0 = stat0 != md2;
                var nstat1 = stat1 != md2;

                var tdCare1 = inst.BranchFlagM1 == 0 || nstat0;
                var tdCare0 = inst.BranchFlagM0 == 0 || nstat1;
                var rdCare = inst.BranchFlagM1 == 0 && inst.BranchFlagM0 == 0;

                var md12s = tdCare1 && tdCare0 && (mode1 != mode0) && !rdCare;
                var super0 = md1 && inst.BranchFlagSel == 0 && rdCare;
                var super1 = md1 && inst.BranchFlagSel != 0 && rdCare;

                var nvTest = (((nega != over) || (zero && inst.BranchFlagM0 != 0))
                    != (inst.BranchFlagM1 != 0)) && inst.BranchFlagSel == 0;
                var tmpCs = carry && !zero;
                var czTest = (tmpCs != (inst.BranchFlagM0 != 0))
                    && inst.BranchFlagSel != 0
                    && inst.BranchFlagM1 == 0;

                var flagsPacked = (zero ? 16 : 0)
                    | (nega ? 8 : 0)
                    | (carry ? 4 : 0)
                    | (over ? 2 : 0);

                for (var exact = 0; exact < 2; exact++)
                {
                    var allZero = super0 && zero && exact != 0;
                    var nAllZero = super1 && !(zero && exact != 0);
                    var sds = allZero || nAllZero;
                    var xactTest = ((exact != 0) != (inst.BranchFlagM0 != 0))
                        && inst.BranchFlagSel != 0
                        && inst.BranchFlagM1 != 0;
                    var md3s = (nvTest || czTest || xactTest) && md3;

                    _branchCond[inst.BranchBits, exact | flagsPacked] = md12s || md3s || sds;
                }
            }
        }
    }

    private void OperandLoader(int requests, DspFlags flags)
    {
        _writeback = 0;

        if (requests == 0)
        {
            if (_requests != 0)
            {
                requests = 4;
            }
            else
            {
                return;
            }
        }

        Span<ushort> pool = stackalloc ushort[6];
        var operands = 0;
        ushort globalWriteback = 0;

        do
        {
            var inst = new DspInstruction(_nmem[_pc++ & 0x3ff]);

            if (inst.NrofType == 4)
            {
                // Non-register format: the operand word is an address.
                ushort address;
                if (inst.NrofDi != 0)
                {
                    address = unchecked((ushort)ReadData((uint)inst.NrofOpAddr));
                }
                else
                {
                    address = unchecked((ushort)inst.NrofOpAddr);
                }

                _writeback = address;
                pool[operands++] = ReadData(address);

                if (inst.NrofWb1 != 0)
                {
                    globalWriteback = _writeback;
                }
            }
            else if ((inst.NrofType & 6) == 6)
            {
                // Immediate format. The reference shifts by the raw one-bit JUSTIFY field:
                // 0 or 3 (the field is sign-extended to 0/-1, then masked).
                var shift = inst.IofJustify & 3;
                pool[operands] = unchecked((ushort)(inst.IofImmediate << shift));
                _writeback = pool[operands++];
            }
            else if ((inst.NrofType & 4) == 0)
            {
                // Three-register format: R3, R2, then R1 (the only write-back register).
                pool[operands++] = ReadRegister(inst.R3, inst.R3Di);
                pool[operands++] = ReadRegister(inst.R2, inst.R2Di);
                pool[operands++] = ReadRegisterWithWriteback(inst.R1, inst.R1Di);
            }
            else
            {
                // One- or two-register format.
                if (inst.R2NumRegs != 0)
                {
                    pool[operands++] = ReadRegisterWithWriteback(inst.R2, inst.R2Di);
                    if (inst.R2Wb2 != 0)
                    {
                        globalWriteback = _writeback;
                    }
                }

                pool[operands++] = ReadRegisterWithWriteback(inst.R1, inst.R1Di);
                if (inst.R2Wb1 != 0)
                {
                    globalWriteback = _writeback;
                }
            }
        }
        while (operands < requests);

        _requests &= (byte)_opMask;

        var pointer = 0;
        if ((_requests & RequestMult1) != 0)
        {
            _mult1 = unchecked((short)pool[pointer++]);
        }

        if ((_requests & RequestMult2) != 0)
        {
            _mult2 = unchecked((short)pool[pointer++]);
        }

        if ((_requests & RequestAlu1) != 0)
        {
            _alu1 = unchecked((short)pool[pointer++]);
        }

        if ((_requests & RequestAlu2) != 0)
        {
            _alu2 = unchecked((short)pool[pointer++]);
        }

        if ((_requests & RequestBs) != 0)
        {
            _bs = pool[pointer++];
        }

        if (operands != pointer)
        {
            if (globalWriteback != 0)
            {
                _writeback = globalWriteback;
            }
        }
        else
        {
            _writeback = globalWriteback;
        }
    }

    private int OperandLoaderNoWriteback()
    {
        // FreeDO: OperandLoaderNWB. Returns an uninitialized value for unreachable/immediate
        // r2 cases in the reference; zero is used here for determinism.
        var inst = new DspInstruction(_nmem[_pc++ & 0x3ff]);

        if (inst.NrofType == 4)
        {
            return inst.NrofDi != 0
                ? ReadData(unchecked((ushort)ReadData((uint)inst.NrofOpAddr)))
                : ReadData((uint)inst.NrofOpAddr);
        }

        if ((inst.NrofType & 4) == 0)
        {
            return ReadRegister(inst.R3, inst.R3Di);
        }

        if ((inst.NrofType & 6) == 6)
        {
            var shift = inst.IofJustify & 3;
            return inst.IofImmediate << shift;
        }

        if (inst.NrofType == 5)
        {
            return ReadRegister(inst.R1, inst.R1Di);
        }

        return 0;
    }

    private ushort RegisterAddress(int register) => (ushort)(_regConv[_regi, register] ^ _rbaseX4);

    private ushort ReadRegister(int register, int indirect)
    {
        var address = RegisterAddress(register);
        return indirect != 0
            ? ReadData(unchecked((ushort)ReadData(address)))
            : ReadData(address);
    }

    private ushort ReadRegisterWithWriteback(int register, int indirect)
    {
        var address = RegisterAddress(register);
        if (indirect != 0)
        {
            var content = ReadData(address);
            _writeback = content;
            return ReadData(content);
        }

        _writeback = address;
        return ReadData(address);
    }

    private ushort ReadData(uint address)
    {
        // FreeDO: ireadh.
        switch (address)
        {
            case 0xea:
                _noise = unchecked((ushort)FastRandom());
                return _noise;
            case 0xeb:
                return _audioOutStatus;
            case 0xec:
                return _sema4Status;
            case 0xed:
                return _sema4Data;
            case 0xee:
                return unchecked((ushort)_pc);
            case 0xef:
                return unchecked((ushort)_dspPcnt);
            case >= 0xf0 and <= 0xfc:
                if (_cpuSupply[address - 0xf0])
                {
                    _cpuSupply[address - 0xf0] = false;
                    _ = FastRandom();
                    return unchecked((ushort)FastRandom());
                }

                return Host?.ReadInputFifo((ushort)(address & 0x0f)) ?? 0;
            case >= 0x70 and <= 0x7c:
                if (_cpuSupply[address - 0x70])
                {
                    _cpuSupply[address - 0x70] = false;
                    return _imem[address];
                }

                return Host?.ReadInputFifoNoIncrement((ushort)(address & 0x0f)) ?? 0;
            case >= 0xd0 and <= 0xde:
                if (_cpuSupply[address & 0xf])
                {
                    return 2;
                }

                return Host?.GetInputFifoStatus((byte)(address & 0xf)) ?? 0;
            case >= 0xe0 and <= 0xe3:
                return Host?.GetOutputFifoStatus((byte)(address & 0xf)) ?? 0;
            default:
                {
                    var offset = address - 0x100;
                    return offset < 0x200 ? _imem[offset | 0x100] : _imem[offset & 0x7f];
                }
        }
    }

    private void WriteData(uint address, ushort value)
    {
        // FreeDO: iwriteh.
        address &= 0x3ff;
        switch (address)
        {
            case 0x3eb:
                _audioOutStatus = value;
                break;
            case 0x3ec:
                _sema4Status |= 0x1;
                break;
            case 0x3ed:
                _sema4Data = value;
                _sema4Status = 0x4;
                break;
            case 0x3ee:
                _interrupt = value;
                _genFiq = true;
                break;
            case 0x3ef:
                _dspPrld = unchecked((short)value);
                break;
            case >= 0x3f0 and <= 0x3f3:
                Host?.WriteOutputFifo((ushort)(address & 0x0f), value);
                break;
            case 0x3fd:
                break;
            case 0x3fe:
            case 0x3ff:
                _imem[address] = value;
                break;
            default:
                {
                    if (address < 0x100)
                    {
                        return;
                    }

                    var offset = address - 0x100;
                    _imem[offset < 0x200 ? offset | 0x100 : offset + 0x100] = value;
                    break;
                }
        }
    }

    private static ushort RegBase(int register, ushort rmap)
    {
        // FreeDO: RegBase.
        register &= 0xf;
        var x = (register >> 2) & 1;
        var y = (register >> 3) & 1;

        var twi = rmap switch
        {
            0 or 1 or 2 or 3 => x,
            4 => y,
            5 => 1 - y,
            6 => x & y,
            7 => x | y,
            _ => 0,
        };

        var result = register & 7;
        result |= twi << 8;
        result |= (register >> 3) << 9;
        return (ushort)result;
    }

    private static bool AddCFlag(uint a, uint b, uint result)
    {
        return ((a & b & TopBit) != 0)
            || ((a & ~result & TopBit) != 0)
            || ((b & ~result & TopBit) != 0)
            ? true : false;
    }

    private static bool SubCFlag(uint a, uint b, uint result)
    {
        return ((a & ~b & TopBit) != 0)
            || ((a & ~result & TopBit) != 0)
            || ((~b & ~result & TopBit) != 0)
            ? true : false;
    }

    private static bool AddVFlag(uint a, uint b, uint result)
    {
        return ((a & b & ~result & TopBit) != 0)
            || ((~a & ~b & result & TopBit) != 0)
            ? true : false;
    }

    private static bool SubVFlag(uint a, uint b, uint result)
    {
        return ((a & ~b & ~result & TopBit) != 0)
            || ((~a & b & result & TopBit) != 0)
            ? true : false;
    }

    /// <summary>The four DSP condition flags plus the last ALU result state.</summary>
    private struct DspFlags
    {
        internal bool Zero;
        internal bool Nega;
        internal bool Carry;
        internal bool Over;
    }
}


