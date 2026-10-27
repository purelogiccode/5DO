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

    /// <summary>Initializes a new instance of the <see cref="Clio"/> class.</summary>
    /// <param name="tuning">Machine tuning. Defaults to <see cref="EmulatorTuning.Default"/>.</param>
    public Clio(EmulatorTuning? tuning = null)
    {
        _tuning = tuning ?? EmulatorTuning.Default;
        _tuning.Validate();
        Reset();
    }

    /// <summary>Gets the machine tuning.</summary>
    public EmulatorTuning Tuning => _tuning;

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

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Clio;

    /// <summary>Gets the save-state section version.</summary>
    public ushort Version => 1;

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
            case 0x414:
                return 0x4000;
            case 0x0:
                return 0x02020000;
            case 0x17F0:
                return Dsp?.FastRandom() ?? 0;
            case 0x17D0:
                return Dsp?.ArmReadSemaphore() ?? 0;
        }

        if (address is >= 0x500 and < 0x540)
        {
            return Xbus?.GetResult() ?? 0;
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
                || address is >= 0x100 and <= 0x17c
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
                _registers[address] = value;
                return value == 0x30;
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
        if (fifo.StartAddress == 0)
        {
            return 0;
        }

        ushort value;
        if (fifo.StartLength - _inputPointers[channel] > 0)
        {
            value = Ram?.Read16((fifo.StartAddress + (uint)_inputPointers[channel]) ^ 2) ?? 0;
            _inputPointers[channel] += 2;
        }
        else
        {
            _inputPointers[channel] = 0;
            GenerateFiq(1u << (channel + 16), 0);
            if (fifo.NextAddress != 0)
            {
                fifo.StartAddress = fifo.NextAddress;
                fifo.StartLength = fifo.NextLength;
                value = Ram?.Read16((fifo.StartAddress + (uint)_inputPointers[channel]) ^ 2) ?? 0;
                _inputPointers[channel] += 2;
            }
            else
            {
                fifo.StartAddress = 0;
                value = 0;
            }
        }

        return value;
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

    private static FifoDescriptor ReadFifo(IStateReader reader) => new()
    {
        StartAddress = reader.ReadUInt32(),
        StartLength = reader.ReadInt32(),
        NextAddress = reader.ReadUInt32(),
        NextLength = reader.ReadInt32(),
    };

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
