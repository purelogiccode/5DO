using System;
using System.Runtime.InteropServices;
using FiveDo.Core;

namespace FiveDo.Clio;

/// <summary>
/// The CLIO chip: interrupt controller, timers, FIFOs, XBUS/DSP windows, and ROM bank select.
/// </summary>
/// <remarks>
/// <para>
/// Ported from FreeDOCore's <c>Clio.cpp</c>. The register file is <c>uint[65536]</c>, matching
/// the reference's 256 KB <c>cregs</c> array. The class implements <see cref="IInterruptSink"/>
/// because CLIO is the machine's FIQ controller.
/// </para>
/// <para>
/// All mutable state is instance state. The chip talks to its neighbours through the small
/// contracts in <c>FiveDo.Core</c>; it never references another chip project directly.
/// </para>
/// </remarks>
public sealed class Clio : IClioBus, IInterruptSink, IClioFifo, IDspHost, IStateSection
{
    private const int InputFifoCount = 13;
    private const int OutputFifoCount = 4;
    private const int RegisterCount = 65536;

    private const uint Decrement = 0x1;
    private const uint Reload = 0x2;
    private const uint Cascade = 0x4;

    private const uint Dipir1Address = 0x410;
    private const uint Dipir2Address = 0x414;
    private const uint DipirCurrent = 0x8000;
    private const uint DipirBeforeReset = 0x4000;
    private const uint DipirDeviceMask = 0x00ff;
    private const uint DipirFiq = 1u << 1;

    private const uint CstatPon = 0x01;
    private const uint CstatRollover = 0x02;
    private const uint CstatWatchdogMask = 0x0c;
    private const uint CstatDipirReset = 0x40;
    private const uint CstatClearOnZeroMask = CstatPon | CstatRollover | CstatDipirReset;
    private const uint ResetSoft = 0x10;
    private const uint ResetClearDipir = 0x20;

    private readonly EmulatorTuning _tuning;
    private readonly uint[] _registers = new uint[RegisterCount];
    private readonly int[] _inputPointers = new int[InputFifoCount];
    private readonly int[] _outputPointers = new int[OutputFifoCount];
    private readonly FifoDescriptor[] _inputFifos = new FifoDescriptor[InputFifoCount];
    private readonly FifoDescriptor[] _outputFifos = new FifoDescriptor[OutputFifoCount];

    private int _dspWord1;
    private int _dspWord2;
    private int _dspAddress;
    private int _timerValue;
    private int _lineSize;
    private int _flagTime;
    private bool _timerCarry;
    private uint _prng32State = 0xDEADBEEF;

    /// <summary>Initializes a new instance of the <see cref="Clio"/> class.</summary>
    /// <param name="tuning">Machine tuning. Defaults to <see cref="EmulatorTuning.Default"/>.</param>
    public Clio(EmulatorTuning? tuning = null)
    {
        _tuning = tuning ?? EmulatorTuning.Default;
        _tuning.Validate();
        OperaDipirEnabled = _tuning.OperaDipir;
        OperaSoftResetEnabled = _tuning.OperaSoftReset;
        Reset();
    }

    /// <summary>Gets the machine tuning.</summary>
    public EmulatorTuning Tuning => _tuning;

    /// <summary>Gets or sets whether Opera's DIPIR latches and access aborts are active.</summary>
    public bool OperaDipirEnabled { get; set; }

    /// <summary>Gets or sets whether Opera's CSTAT soft-reset semantics are active.</summary>
    public bool OperaSoftResetEnabled { get; set; }

    /// <summary>Gets or sets the callback invoked for a CSTAT soft reset. The argument is clear-DIPIR.</summary>
    public Action<bool>? SoftResetRequested { get; set; }

    private bool UseOperaDipir => OperaDipirEnabled;

    /// <summary>Gets or sets the raw RAM accessor used by FIFO and DMA transfers.</summary>
    public IRam? Ram { get; set; }

    /// <summary>Gets or sets the ROM bank selector. FreeDO: <c>SelectROM</c>.</summary>
    public IRomSelector? RomSelector { get; set; }

    /// <summary>Gets or sets the XBUS controller. FreeDO: <c>_xbus_*</c>.</summary>
    public IClioXbus? Xbus { get; set; }

    /// <summary>Gets or sets the DSP. FreeDO: <c>_dsp_*</c>.</summary>
    public IDsp? Dsp { get; set; }

    /// <summary>Gets or sets the MADAM register window used by XBUS DMA.</summary>
    public IMadamBus? Madam { get; set; }

    /// <summary>Gets a value indicating whether a FIQ is currently pending. FreeDO: <c>_clio_NeedFIQ</c>.</summary>
    public bool NeedFiq =>
        (_registers[0x40] & _registers[0x48]) != 0 ||
        (_registers[0x60] & _registers[0x68]) != 0;

    /// <summary>Gets the V0 interrupt line. FreeDO: <c>_clio_v0line</c>.</summary>
    public int V0Line => (int)(_registers[8] & 0x7ff);

    /// <summary>Gets the V1 interrupt line. FreeDO: <c>_clio_v1line</c>.</summary>
    public int V1Line => (int)(_registers[12] & 0x7ff);

    /// <summary>Gets the timer delay. FreeDO: <c>_clio_GetTimerDelay</c>.</summary>
    public uint TimerDelay => _registers[0x220];

    /// <summary>Gets or sets an optional diagnostic writer for timer register traffic.</summary>
    public System.IO.TextWriter? TraceWriter { get; set; }

    /// <summary>Gets or sets the frame provider used to stamp diagnostic trace lines.</summary>
    public Func<long>? FrameProvider { get; set; }

    /// <summary>Gets or sets an optional observer for generated FIQ reason masks (diagnostics only).</summary>
    public Action<uint, uint>? FiqObserver { get; set; }

    /// <summary>Gets the timer-dependent value used by register <c>0x120</c>. FreeDO: <c>TIMER_VAL</c>.</summary>
    public int TimerValue => _timerValue;

    /// <summary>Gets the cascade carry from the previous timer slot in Opera mode. FreeDO: <c>TIMER_CARRY</c>.</summary>
    public bool TimerCarry => _timerCarry;

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Clio;

    /// <summary>Gets the save-state section version.</summary>
    /// <remarks>Version 3 adds the separate CLIO 32-bit PRNG state.</remarks>
    public ushort Version => 3;

    /// <summary>Resets the chip. FreeDO: <c>_clio_Init</c> and <c>_clio_Reset</c>.</summary>
    /// <param name="resetReason">Value stored in register <c>0x28</c>. FreeDO uses <c>0x40</c> for 3DO-CD.</param>
    public void Reset(int resetReason = 0x40)
    {
        Array.Clear(_registers);
        Array.Clear(_inputPointers);
        Array.Clear(_outputPointers);
        Array.Clear(_inputFifos);
        Array.Clear(_outputFifos);
        _dspWord1 = 0;
        _dspWord2 = 0;
        _dspAddress = 0;
        _timerValue = 0;
        _lineSize = 0;
        _flagTime = 0;
        _timerCarry = false;

        _registers[0x0028] = unchecked((uint)resetReason);
        _registers[0x0400] = 0x80;
        _registers[0x0220] = 64;
    }

    /// <summary>Decrements the one-shot timer window once per emulated frame. FreeDO: <c>_3do_Frame</c>.</summary>
    public void OnFrame()
    {
        if (_flagTime != 0)
        {
            _flagTime--;
        }
    }

    /// <summary>Sets the CLIO 32-bit PRNG state. Opera: <c>prng32_seed</c>.</summary>
    /// <param name="seed">Initial state.</param>
    public void SetRandomSeed(uint seed)
    {
        _prng32State = seed;
    }

    /// <summary>Gets the CLIO 32-bit PRNG state. Opera: <c>prng32_state_get</c>.</summary>
    public uint RandomState => _prng32State;

    private uint NextRandom32()
    {
        var z = unchecked(_prng32State += 0x9e3779b9);
        z = unchecked((z ^ (z >> 16)) * 0x85ebca6b);
        z = unchecked((z ^ (z >> 13)) * 0xc2b2ae35);
        return unchecked(z ^ (z >> 16));
    }

    /// <summary>Reads a CLIO register. FreeDO: <c>_clio_Peek</c>.</summary>
    /// <param name="offset">Byte offset within the CLIO window.</param>
    /// <returns>The register value.</returns>
    public uint Peek(uint offset)
    {
        var address = offset & 0xffff;

        if ((address & ~0x2cu) == 0x40)
        {
            address &= ~4u;
            return address switch
            {
                0x40 => _registers[0x40],
                0x48 => _registers[0x48] | 0x80000000,
                0x60 => _registers[0x60],
                0x68 => _registers[0x68],
                _ => 0,
            };
        }

        switch (address)
        {
            case 0x204:
                return _registers[0x200];
            case 0x20c:
                return _registers[0x208];
            case 0x308:
                return _registers[0x304];
            case 0x410:
            case 0x414:
                return UseOperaDipir
                    ? _registers[address]
                    : (address == 0x414 ? DipirBeforeReset : 0u);
            case 0x0:
                return 0x02020000;
            case 0x17F0:
                return NextRandom32();
            case 0x17D0:
                return Dsp?.ArmReadSemaphore() ?? 0;
        }

        if (address is >= 0x500 and < 0x540)
        {
            return Xbus?.GetResult() ?? 0;
        }

        if (address is >= 0x540 and < 0x600
            && UseOperaDipir
            && XbusAccessAborts(Xbus?.SelectedDeviceAddress ?? 0, address))
        {
            return 0;
        }

        if (address is >= 0x540 and < 0x580)
        {
            return Xbus?.GetPoll() ?? 0;
        }

        if (address is >= 0x580 and < 0x5c0)
        {
            return Xbus?.GetStatusFifo() ?? 0;
        }

        if (address is >= 0x5c0 and < 0x600)
        {
            return Xbus?.GetDataFifo() ?? 0;
        }

        if (address is >= 0x3800 and <= 0x3bff)
        {
            _dspAddress = (int)((address - 0x3800) >> 1);
            _dspAddress &= 0xff;
            _dspAddress += 0x300;
            var first = Dsp?.ReadInstructionMemory(_dspAddress) ?? 0;
            var second = Dsp?.ReadInstructionMemory(_dspAddress + 1) ?? 0;
            return ((uint)first << 16) | second;
        }

        if (address is >= 0x3c00 and <= 0x3fff)
        {
            _dspAddress = (int)((address - 0x3c00) >> 2);
            _dspAddress &= 0xff;
            _dspAddress += 0x300;
            return Dsp?.ReadInstructionMemory(_dspAddress) ?? 0;
        }

        return _registers[address];
    }

    /// <summary>Writes a CLIO register. FreeDO: <c>_clio_Poke</c>.</summary>
    /// <param name="offset">Byte offset within the CLIO window.</param>
    /// <param name="value">Register value.</param>
    /// <returns><see langword="true"/> when the write requests a four-byte PC advance.</returns>
    public bool Poke(uint offset, uint value)
    {
        var address = offset & 0xffff;

        if (TraceWriter is not null
            && (address is 0x200 or 0x204 or 0x208 or 0x20c or 0x220
                or 0x28 or 0x410 or 0x414
                || address is >= 0x100 and <= 0x17c
                || address is >= 0x540 and < 0x600
                || address == 0x304))
        {
            TraceWriter.WriteLine(
                $"clio f={FrameProvider?.Invoke() ?? -1} write {address:x3}={value:x8} delay={_registers[0x220]:x}");
        }

        if (_flagTime == 0)
        {
            _timerValue = 0;
            _lineSize = 0;
        }

        if ((address & ~0x2cu) == 0x40)
        {
            switch (address)
            {
                case 0x40:
                    _registers[0x40] |= value;
                    if (_registers[0x60] != 0)
                    {
                        _registers[0x40] |= 0x80000000;
                    }

                    return false;
                case 0x44:
                    _registers[0x40] &= ~value;
                    if (_registers[0x60] == 0)
                    {
                        _registers[0x40] &= ~0x80000000;
                    }

                    return false;
                case 0x48:
                    _registers[0x48] |= value;
                    return false;
                case 0x4c:
                    _registers[0x48] &= ~value;
                    _registers[0x48] |= 0x80000000;
                    return false;
                case 0x60:
                    _registers[0x60] |= value;
                    if (_registers[0x60] != 0)
                    {
                        _registers[0x40] |= 0x80000000;
                    }

                    return false;
                case 0x64:
                    _registers[0x60] &= ~value;
                    if (_registers[0x60] == 0)
                    {
                        _registers[0x40] &= ~0x80000000;
                    }

                    return false;
                case 0x68:
                    _registers[0x68] |= value;
                    return false;
                case 0x6c:
                    _registers[0x68] &= ~value;
                    return false;
            }
        }

        switch (address)
        {
            case 0x84:
                _registers[0x84] = value & 0xf;
                RomSelector?.SelectRom((value & 4) != 0 ? 1 : 0);
                return false;
            case 0x300:
                ClearFifos(value);
                return false;
            case 0x304:
                HandleDma(value);
                switch (value)
                {
                    case 0x100000:
                        if (_timerValue < 5800)
                        {
                            _timerValue += 0x33;
                        }

                        _lineSize += 0x33;
                        _flagTime = _tuning.ArmClock / 2_000_000;
                        break;
                    default:
                        if (_registers[0x304] == 0)
                        {
                            _timerValue = 0;
                            _lineSize = 0;
                        }

                        break;
                }

                return false;
            case 0x308:
                _registers[0x304] &= ~value;
                return false;
            case 0x400:
                if ((value & 0x800) != 0)
                {
                    return false;
                }

                _registers[0x400] = value;
                return false;
            case 0x28:
                if (!OperaSoftResetEnabled)
                {
                    _registers[address] = value;
                    return value == 0x30;
                }

                var cstat = value & ~(ResetSoft | ResetClearDipir | CstatClearOnZeroMask | CstatWatchdogMask);
                cstat |= _registers[address] & CstatWatchdogMask;
                cstat |= _registers[address] & value & CstatClearOnZeroMask;
                if ((value & (ResetSoft | ResetClearDipir)) == (ResetSoft | ResetClearDipir))
                {
                    cstat |= CstatDipirReset;
                }

                _registers[address] = cstat;
                if ((value & ResetSoft) != 0)
                {
                    // The reset handler sets the PC directly; do not also apply the
                    // normal CLIO four-byte PC adjustment for this write.
                    SoftResetRequested?.Invoke((value & ResetClearDipir) != 0);
                }

                return false;
            case 0x17e8:
                Dsp?.Reset();
                return false;
            case 0x17d0:
                Dsp?.ArmWriteSemaphore(value);
                return false;
            case 0x17fc:
                Dsp?.SetRunning(value > 0);
                return false;
            case 0x200:
                _registers[0x200] |= value;
                return false;
            case 0x204:
                _registers[0x200] &= ~value;
                return false;
            case 0x208:
                _registers[0x208] |= value;
                return false;
            case 0x20c:
                _registers[0x208] &= ~value;
                return false;
            case 0x220:
                _registers[address] = value & 0x3ff;
                return false;
            case 0x120:
                _registers[address] = _timerValue > 800
                    ? unchecked((uint)_timerValue + (value / 0x30))
                    : value;
                return false;
        }

        if (address is >= 0x500 and < 0x540)
        {
            Xbus?.SetSelect(value);
            return false;
        }

        if (address is >= 0x540 and < 0x600
            && UseOperaDipir
            && XbusAccessAborts(Xbus?.SelectedDeviceAddress ?? 0, address))
        {
            return false;
        }

        if (address is >= 0x540 and < 0x580)
        {
            Xbus?.SetPoll(value);
            return false;
        }

        if (address is >= 0x580 and < 0x5c0)
        {
            Xbus?.SetCommandFifo(value);
            return false;
        }

        if (address is >= 0x5c0 and < 0x600)
        {
            Xbus?.SetDataFifo(value);
            return false;
        }

        if (address is >= 0x1800 and <= 0x1fff)
        {
            address &= ~0x400u;
            _dspWord1 = (int)(value >> 16);
            _dspWord2 = (int)(value & 0xffff);
            _dspAddress = (int)((address - 0x1800) >> 1);
            Dsp?.WriteMemory(_dspAddress, unchecked((ushort)_dspWord1));
            Dsp?.WriteMemory(_dspAddress + 1, unchecked((ushort)_dspWord2));
            return false;
        }

        if (address is >= 0x2000 and <= 0x2fff)
        {
            address &= ~0x800u;
            _dspWord1 = (int)(value & 0xffff);
            _dspAddress = (int)((address - 0x2000) >> 2);
            Dsp?.WriteMemory(_dspAddress, unchecked((ushort)_dspWord1));
            return false;
        }

        if (address is >= 0x3000 and <= 0x33ff)
        {
            _dspAddress = (int)((address - 0x3000) >> 1);
            _dspAddress &= 0xff;
            _dspWord1 = (int)(value >> 16);
            _dspWord2 = (int)(value & 0xffff);
            Dsp?.WriteInstructionMemory(_dspAddress, unchecked((ushort)_dspWord1));
            Dsp?.WriteInstructionMemory(_dspAddress + 1, unchecked((ushort)_dspWord2));
            return false;
        }

        if (address is >= 0x3400 and <= 0x37ff)
        {
            _dspAddress = (int)((address - 0x3400) >> 2);
            _dspAddress &= 0xff;
            _dspWord1 = (int)(value & 0xffff);
            Dsp?.WriteInstructionMemory(_dspAddress, unchecked((ushort)_dspWord1));
            return false;
        }

        _registers[address] = value;
        return false;
    }

    /// <summary>Raises a FIQ. FreeDO: <c>_clio_GenerateFiq</c>.</summary>
    /// <param name="reason1">Low interrupt mask.</param>
    /// <param name="reason2">High interrupt mask.</param>
    public void GenerateFiq(uint reason1, uint reason2)
    {
        FiqObserver?.Invoke(reason1, reason2);
        _registers[0x40] |= reason1;
        _registers[0x60] |= reason2;
        if (_registers[0x60] != 0)
        {
            _registers[0x40] |= 0x80000000;
        }
    }

    /// <summary>
    /// Latches an XBUS media-access event in DIPIR1 or DIPIR2. Opera:
    /// <c>opera_clio_xbus_media_access</c>.
    /// </summary>
    /// <param name="unit">Selected XBUS device address.</param>
    public void XbusMediaAccess(uint unit)
    {
        if (!UseOperaDipir)
        {
            return;
        }

        var latch = DipirCurrent | (unit & DipirDeviceMask);
        var dipir1 = _registers[Dipir1Address];
        var dipir2 = _registers[Dipir2Address];

        if (((dipir1 & DipirCurrent) != 0
             && (dipir1 & DipirDeviceMask) == (latch & DipirDeviceMask))
            || ((dipir2 & DipirCurrent) != 0
                && (dipir2 & DipirDeviceMask) == (latch & DipirDeviceMask)))
        {
            return;
        }

        if ((dipir1 & DipirCurrent) == 0)
        {
            _registers[Dipir1Address] = latch;
            GenerateFiq(0, DipirFiq);
        }
        else if ((dipir2 & DipirCurrent) == 0)
        {
            _registers[Dipir2Address] = latch;
        }
    }

    /// <summary>
    /// Gets whether an XBUS register access is blocked by the DIPIR latches. Opera:
    /// <c>opera_clio_xbus_access_aborts</c>.
    /// </summary>
    /// <param name="unit">Selected XBUS device address.</param>
    /// <param name="address">CLIO register offset.</param>
    /// <returns><see langword="true"/> when the access must be ignored.</returns>
    public bool XbusAccessAborts(uint unit, uint address)
    {
        if (!UseOperaDipir)
        {
            return false;
        }

        var dipir1 = _registers[Dipir1Address];
        var dipir2 = _registers[Dipir2Address];
        if ((dipir1 & DipirCurrent) == 0 && (dipir2 & DipirCurrent) == 0)
        {
            return false;
        }

        var isPoll = address is >= 0x540 and < 0x580;
        unit &= DipirDeviceMask;

        if ((dipir1 & DipirCurrent) != 0 && (dipir2 & DipirCurrent) == 0)
        {
            return !isPoll && (dipir1 & DipirDeviceMask) == unit;
        }

        if ((dipir2 & DipirCurrent) != 0 && (dipir1 & DipirCurrent) == 0)
        {
            return !isPoll && (dipir2 & DipirDeviceMask) == unit;
        }

        if (!isPoll)
        {
            return true;
        }

        return (dipir1 & DipirDeviceMask) != unit
            && (dipir2 & DipirDeviceMask) != unit;
    }

    /// <summary>Converts current DIPIR latches to their before-reset form. Opera: <c>clio_dipir_latch_after_reset</c>.</summary>
    public void ClearDipirOnSoftReset()
    {
        _registers[Dipir1Address] = DipirLatchAfterReset(_registers[Dipir1Address]);
        _registers[Dipir2Address] = DipirLatchAfterReset(_registers[Dipir2Address]);
    }

    private static uint DipirLatchAfterReset(uint latch)
    {
        var result = 0u;
        for (var shift = 0; shift < 32; shift += 16)
        {
            var field = (latch >> shift) & 0xffff;
            if ((field & DipirCurrent) != 0)
            {
                result |= (DipirBeforeReset | (field & DipirDeviceMask)) << shift;
            }
        }

        return result;
    }

    /// <summary>Updates the video line counter. FreeDO: <c>_clio_UpdateVCNT</c>.</summary>
    /// <param name="line">Current line within the half-frame.</param>
    /// <param name="halfFrame">Half-frame index.</param>
    public void UpdateVcnt(int line, int halfFrame)
    {
        _registers[0x34] = ((uint)halfFrame << 11) + (uint)line;
    }

    /// <summary>Services all sixteen timers for one timer event. FreeDO: <c>_clio_DoTimers</c>.</summary>
    public void DoTimers()
    {
        var needDecrementNextTimer = true;
        for (var timer = 0; timer < 16; timer++)
        {
            var flag = _registers[timer < 8 ? 0x200 : 0x208] >> ((timer * 4) & 31);
            if ((flag & Cascade) == 0)
            {
                needDecrementNextTimer = true;
            }

            if (needDecrementNextTimer && (flag & Decrement) != 0)
            {
                var counter = unchecked((ushort)_registers[0x100 + (timer * 8)]);
                var wrapped = counter == 0;
                counter--;
                needDecrementNextTimer = wrapped;

                if (wrapped)
                {
                    if ((timer & 1) != 0)
                    {
                        GenerateFiq(1u << (10 - (timer / 2)), 0);
                    }

                    if ((flag & Reload) != 0)
                    {
                        counter = unchecked((ushort)_registers[0x100 + (timer * 8) + 4]);
                    }
                    else
                    {
                        _registers[timer < 8 ? 0x200 : 0x208] &= ~(Decrement << ((timer * 4) & 31));
                    }
                }

                _registers[0x100 + (timer * 8)] = counter;
            }
            else
            {
                needDecrementNextTimer = false;
            }
        }
    }

    /// <summary>
    /// Services one timer slot. Opera: <c>opera_clio_timer_execute</c>.
    /// </summary>
    /// <param name="timer">Timer index, 0-15.</param>
    /// <remarks>
    /// Timer 0 starts a new scan and clears the carry. A cascade timer only decrements
    /// when the previous timer wrapped in this scan. An underflowing timer raises the
    /// odd-timer FIQ, reloads or stops, and leaves the carry set for the next slot.
    /// </remarks>
    public void DoTimers(int timer)
    {
        timer &= 0x0f;
        if (timer == 0)
        {
            _timerCarry = false;
        }

        var flags = (_registers[timer < 8 ? 0x200 : 0x208] >> ((timer * 4) & 31)) & 0xfu;
        if ((flags & Decrement) == 0)
        {
            _timerCarry = false;
            return;
        }

        if ((flags & Cascade) != 0 && !_timerCarry)
        {
            _timerCarry = false;
            return;
        }

        var counterAddress = 0x100 + (timer * 8);
        var counter = unchecked((ushort)_registers[counterAddress]);
        var underflow = counter == 0;

        if (underflow)
        {
            if ((timer & 1) != 0)
            {
                GenerateFiq(1u << (10 - (timer / 2)), 0);
            }

            if ((flags & Reload) != 0)
            {
                counter = unchecked((ushort)_registers[counterAddress + 4]);
            }
            else
            {
                counter = 0xffff;
                _registers[timer < 8 ? 0x200 : 0x208] &= ~(Decrement << ((timer * 4) & 31));
            }
        }
        else
        {
            counter--;
        }

        _registers[counterAddress] = counter;
        _timerCarry = underflow;
    }

    /// <summary>Writes a FIFO descriptor. FreeDO: <c>_clio_SetFIFO</c>.</summary>
    /// <param name="address">Register offset.</param>
    /// <param name="value">Register value.</param>
    public void SetFifo(uint address, uint value)
    {
        var index = (int)((address >> 4) & 0xf);
        var field = address & 0xf;

        if ((address & 0x500) == 0x400)
        {
            if (index >= InputFifoCount)
            {
                return;
            }

            ref FifoDescriptor fifo = ref _inputFifos[index];
            switch (field)
            {
                case 0:
                    fifo.StartAddress = value;
                    fifo.NextAddress = 0;
                    _inputPointers[index] = 0;
                    break;
                case 4:
                    fifo.StartLength = value == 0 ? 0 : unchecked((int)value + 4);
                    fifo.NextLength = 0;
                    break;
                case 8:
                    fifo.NextAddress = value;
                    break;
                case 0xc:
                    fifo.NextLength = value == 0 ? 0 : unchecked((int)value + 4);
                    break;
            }

            return;
        }

        if (index >= OutputFifoCount)
        {
            return;
        }

        ref FifoDescriptor output = ref _outputFifos[index];
        switch (field)
        {
            case 0:
                output.StartAddress = value;
                break;
            case 4:
                output.StartLength = unchecked((int)value + 4);
                break;
            case 8:
                output.NextAddress = value;
                break;
            case 0xc:
                output.NextLength = unchecked((int)value + 4);
                break;
        }
    }

    /// <summary>Reads a FIFO descriptor. FreeDO: <c>_clio_FIFOStruct</c>.</summary>
    /// <param name="address">Register offset.</param>
    /// <returns>Register value.</returns>
    public uint FifoStruct(uint address)
    {
        var index = (int)((address >> 4) & 0xf);
        var field = address & 0xf;

        if ((address & 0x500) == 0x400)
        {
            if (index >= InputFifoCount)
            {
                return 0;
            }

            ref FifoDescriptor fifo = ref _inputFifos[index];
            return field switch
            {
                0 => unchecked(fifo.StartAddress + (uint)_inputPointers[index]),
                4 => unchecked((uint)(fifo.StartLength - _inputPointers[index])),
                8 => fifo.NextAddress,
                0xc => unchecked((uint)fifo.NextLength),
                _ => 0,
            };
        }

        if (index >= OutputFifoCount)
        {
            return 0;
        }

        ref FifoDescriptor output = ref _outputFifos[index];
        return field switch
        {
            0 => unchecked(output.StartAddress + (uint)_outputPointers[index]),
            4 => unchecked((uint)(output.StartLength - _outputPointers[index])),
            8 => output.NextAddress,
            0xc => unchecked((uint)output.NextLength),
            _ => 0,
        };
    }

    /// <summary>Reads one word from an input FIFO. FreeDO: <c>_clio_EIFIFO</c>.</summary>
    /// <param name="channel">FIFO channel, 0-12.</param>
    /// <returns>The FIFO word, or zero when disabled.</returns>
    public ushort ReadInputFifo(ushort channel)
    {
        if (channel >= InputFifoCount)
        {
            return 0;
        }

        ref FifoDescriptor fifo = ref _inputFifos[channel];

        // Opera: complete a restored/queued transfer before the CPU can observe
        // the end address, then read from the (possibly reloaded) current buffer.
        if (fifo.StartAddress != 0 && _inputPointers[channel] >= fifo.StartLength)
        {
            CompleteInputFifo(channel);
        }

        if (fifo.StartAddress == 0)
        {
            return 0;
        }

        var value = Ram?.Read16((fifo.StartAddress + (uint)_inputPointers[channel]) ^ 2) ?? 0;
        _inputPointers[channel] += 2;

        // Complete immediately after the final word so the next read sees the
        // reloaded buffer instead of the stale end address.
        if (_inputPointers[channel] >= fifo.StartLength)
        {
            CompleteInputFifo(channel);
        }

        return value;
    }

    private void CompleteInputFifo(ushort channel)
    {
        ref FifoDescriptor fifo = ref _inputFifos[channel];
        _inputPointers[channel] = 0;
        GenerateFiq(1u << (channel + 16), 0);

        // Opera: reload is enabled by the channel request bit. A disabled
        // channel leaves the FIFO detached even when a next descriptor exists.
        if (fifo.NextAddress != 0 && (_registers[0x304] & (1u << channel)) != 0)
        {
            fifo.StartAddress = fifo.NextAddress;
            fifo.StartLength = fifo.NextLength;
        }
        else
        {
            fifo.StartAddress = 0;
        }
    }

    /// <summary>Reads one word from an input FIFO without advancing. FreeDO: <c>_clio_EIFIFONI</c>.</summary>
    /// <param name="channel">FIFO channel, 0-12.</param>
    /// <returns>The FIFO word.</returns>
    public ushort ReadInputFifoNoIncrement(ushort channel)
    {
        if (channel >= InputFifoCount)
        {
            return 0;
        }

        ref FifoDescriptor fifo = ref _inputFifos[channel];
        return Ram?.Read16((fifo.StartAddress + (uint)_inputPointers[channel]) ^ 2) ?? 0;
    }

    /// <summary>Writes one word to an output FIFO. FreeDO: <c>_clio_EOFIFO</c>.</summary>
    /// <param name="channel">FIFO channel, 0-3.</param>
    /// <param name="value">Word value.</param>
    public void WriteOutputFifo(ushort channel, ushort value)
    {
        if (channel >= OutputFifoCount)
        {
            return;
        }

        ref FifoDescriptor fifo = ref _outputFifos[channel];
        if (fifo.StartAddress == 0)
        {
            return;
        }

        if (fifo.StartLength - _outputPointers[channel] > 0)
        {
            Ram?.Write16((fifo.StartAddress + (uint)_outputPointers[channel]) ^ 2, value);
            _outputPointers[channel] += 2;
        }
        else
        {
            _outputPointers[channel] = 0;
            GenerateFiq(1u << (channel + 12), 0);
            if (fifo.NextAddress != 0)
            {
                fifo.StartAddress = fifo.NextAddress;
                fifo.StartLength = fifo.NextLength;
            }
            else
            {
                fifo.StartAddress = 0;
            }
        }
    }

    /// <summary>Gets the input FIFO status. FreeDO: <c>_clio_GetEIFIFOStat</c>.</summary>
    /// <param name="channel">FIFO channel.</param>
    /// <returns>Status value.</returns>
    public ushort GetInputFifoStatus(byte channel)
    {
        if (channel >= InputFifoCount)
        {
            return 0;
        }

        return _inputFifos[channel].StartAddress != 0 ? (ushort)2 : (ushort)0;
    }

    /// <summary>Gets the output FIFO status. FreeDO: <c>_clio_GetEOFIFOStat</c>.</summary>
    /// <param name="channel">FIFO channel.</param>
    /// <returns>Status value.</returns>
    public ushort GetOutputFifoStatus(byte channel)
    {
        if (channel >= OutputFifoCount)
        {
            return 0;
        }

        return _outputFifos[channel].StartAddress != 0 ? (ushort)1 : (ushort)0;
    }

    /// <summary>Writes the CLIO save-state payload. FreeDO: <c>_clio_Save</c>.</summary>
    /// <param name="writer">Destination writer.</param>
    public void Save(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        for (var i = 0; i < _registers.Length; i++)
        {
            writer.WriteUInt32(_registers[i]);
        }

        writer.WriteInt32(_dspWord1);
        writer.WriteInt32(_dspWord2);
        writer.WriteInt32(_dspAddress);
        writer.WriteInt32(_timerValue);
        writer.WriteInt32(_lineSize);
        writer.WriteInt32(_flagTime);
        writer.WriteUInt32(_timerCarry ? 1u : 0u);
        writer.WriteUInt32(_prng32State);

        for (var i = 0; i < InputFifoCount; i++)
        {
            writer.WriteInt32(_inputPointers[i]);
            WriteFifo(writer, _inputFifos[i]);
        }

        for (var i = 0; i < OutputFifoCount; i++)
        {
            writer.WriteInt32(_outputPointers[i]);
            WriteFifo(writer, _outputFifos[i]);
        }
    }

    /// <summary>Reads the CLIO save-state payload. FreeDO: <c>_clio_Load</c>.</summary>
    /// <param name="reader">Source reader.</param>
    public void Load(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        for (var i = 0; i < _registers.Length; i++)
        {
            _registers[i] = reader.ReadUInt32();
        }

        _dspWord1 = reader.ReadInt32();
        _dspWord2 = reader.ReadInt32();
        _dspAddress = reader.ReadInt32();
        _timerValue = reader.ReadInt32();
        _lineSize = reader.ReadInt32();
        _flagTime = reader.ReadInt32();
        if (reader is StateSectionReader { Version: 1 })
        {
            _timerCarry = false;
        }
        else
        {
            var carry = reader.ReadUInt32();
            if (carry > 1)
            {
                throw new InvalidStateException($"Invalid CLIO timer carry {carry}.");
            }

            _timerCarry = carry != 0;
        }

        if (reader is not StateSectionReader sectionReader || sectionReader.Version >= 3)
        {
            _prng32State = reader.ReadUInt32();
        }
        else
        {
            _prng32State = 0xDEADBEEF;
        }

        for (var i = 0; i < InputFifoCount; i++)
        {
            _inputPointers[i] = reader.ReadInt32();
            _inputFifos[i] = ReadFifo(reader);
        }

        for (var i = 0; i < OutputFifoCount; i++)
        {
            _outputPointers[i] = reader.ReadInt32();
            _outputFifos[i] = ReadFifo(reader);
        }
    }

    private static void WriteFifo(IStateWriter writer, FifoDescriptor fifo)
    {
        writer.WriteUInt32(fifo.StartAddress);
        writer.WriteInt32(fifo.StartLength);
        writer.WriteUInt32(fifo.NextAddress);
        writer.WriteInt32(fifo.NextLength);
    }

    private static FifoDescriptor ReadFifo(IStateReader reader)
    {
        return new FifoDescriptor
        {
            StartAddress = reader.ReadUInt32(),
            StartLength = reader.ReadInt32(),
            NextAddress = reader.ReadUInt32(),
            NextLength = reader.ReadInt32(),
        };
    }

    private void ClearFifos(uint value)
    {
        _registers[0x304] &= ~value;

        for (var i = 0; i < InputFifoCount; i++)
        {
            if ((value & (1u << i)) == 0)
            {
                continue;
            }

            _inputFifos[i] = default;
            _inputPointers[i] = 0;
            value &= ~(1u << i);
        }

        for (var i = 0; i < OutputFifoCount; i++)
        {
            if ((value & (1u << (i + 16))) == 0)
            {
                continue;
            }

            _outputFifos[i] = default;
            _outputPointers[i] = 0;
            value &= ~(1u << (i + 16));
        }
    }

    private void HandleDma(uint value)
    {
        _registers[0x304] |= value;
        if ((value & 0x00100000) == 0)
        {
            return;
        }

        _registers[0x304] &= ~0x00100000u;
        var source = Madam?.Peek(0x540) ?? 0;
        var target = source;
        var length = unchecked((int)(Madam?.Peek(0x544) ?? 0));
        _registers[0x400] &= ~0x80u;

        var traceFirst = TraceWriter is not null;
        while (length >= 0)
        {
            var b3 = Xbus?.GetDataFifo() ?? 0;
            var b2 = Xbus?.GetDataFifo() ?? 0;
            var b1 = Xbus?.GetDataFifo() ?? 0;
            var b0 = Xbus?.GetDataFifo() ?? 0;

            if (traceFirst)
            {
                TraceWriter!.WriteLine(
                    $"clio dma f={FrameProvider?.Invoke() ?? -1} select={Xbus?.SelectedDevice ?? -1:x} "
                    + $"target={target:x8} len={length:x} first={b3:x2}{b2:x2}{b1:x2}{b0:x2}");
                traceFirst = false;
            }

            // IRam is the raw word-lane accessor, so the FIFO's little-endian
            // byte order is b0,b1,b2,b3 here. Opera's b3-first order applies to
            // its CPU-visible opera_mem_write8 accessor.
            Ram?.Write8(target, (byte)b0);
            Ram?.Write8(target + 1, (byte)b1);
            Ram?.Write8(target + 2, (byte)b2);
            Ram?.Write8(target + 3, (byte)b3);

            target += 4;
            length -= 4;
        }

        _registers[0x400] |= 0x80;
        Madam?.Poke(0x544, 0xFFFFFFFC);
        GenerateFiq(1u << 29, 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FifoDescriptor
    {
        public uint StartAddress;
        public int StartLength;
        public uint NextAddress;
        public int NextLength;
    }
}
