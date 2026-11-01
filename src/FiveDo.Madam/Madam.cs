using System;
using System.Buffers.Binary;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Madam;

/// <summary>
/// The MADAM chip register file, CEL state machine, matrix engine, and PBUS DMA.
/// </summary>
/// <remarks>
/// <para>
/// Ported from FreeDOCore's <c>Madam.cpp</c>. M3 implements the register map, the CEL
/// status/FSM, the VDL head hook, the matrix engine, and PBUS DMA. The CEL rasterizer,
/// pixel decoder/processor/projector, and geometry arrive in M4.
/// </para>
/// <para>
/// The register file is <c>uint[2048 + 64]</c>, matching the reference's <c>mregs</c>.
/// The CLIO FIFO window (<c>0x400-0x53F</c>) is forwarded through <see cref="IClioFifo"/>.
/// </para>
/// </remarks>
public sealed class Madam : IMadamBus, IStateSection
{
    /// <summary>Number of MADAM registers. FreeDO: <c>mregs[2048 + 64]</c>.</summary>
    public const int RegisterCount = 2048 + 64;

    private const uint SpriteStart = 0x100;
    private const uint SpriteStop = 0x104;
    private const uint SpriteContinue = 0x108;
    private const uint SpritePause = 0x10c;

    // Opera Specification, CEL engine chapter: the engine renders about nine million
    // pixels per second, and each list entry adds a small fixed fetch/decode cost.
    private const ulong CelPixelsPerSecond = 9_000_000;
    private const ulong CelSetupCycles = 64;

    private readonly uint[] _registers = new uint[RegisterCount];
    private readonly ushort[] _plut = new ushort[32];
    private readonly byte[] _pbusQueue = new byte[20];

    private CelEngine? _cel;
    private IRam? _celRam;
    private CelTimingMode _celTiming = CelTimingMode.FreeDoCompatible;
    private int _armClock = EmulatorConstants.ArmClock;
    private bool _celDrawPending;
    private ulong _celBusyCyclesRemaining;
    private ulong _celBusyCyclesTotal;
    private bool _mctlWritten;

    private const uint MctlClutXen = 0x00002000;
    private const uint MctlVsctXen = 0x00004000;
    private const uint MctlPlayXen = 0x00008000;
    private long _celPixelsDrawn;
    private long _celTexelsWalked;
    private int _celCelsProcessed;
    private int _readModulo;
    private int _writeModulo;
    private CelFsmState _fsm;
    private double _result0Temp;
    private double _result1Temp;
    private double _result2Temp;
    private double _result3Temp;

    /// <summary>Initializes a new instance of the <see cref="Madam"/> class.</summary>
    public Madam()
    {
        Reset();
    }

    /// <summary>Gets or sets the VDLP display-list processor.</summary>
    public IVdlp? Vdlp { get; set; }

    /// <summary>Gets or sets the FIQ controller.</summary>
    public IInterruptSink? Interrupts { get; set; }

    /// <summary>Gets or sets the CLIO FIFO window.</summary>
    public IClioFifo? Fifo { get; set; }

    /// <summary>Gets or sets the raw RAM accessor used by PLUT loads.</summary>
    public IRam? Ram { get; set; }

    /// <summary>Gets or sets the CPU bus used by PBUS DMA writes.</summary>
    public IAddressBus? Bus { get; set; }

    /// <summary>Gets or sets the CEL engine state. FreeDO: <c>Get_madam_FSM</c>/<c>Set_madam_FSM</c>.</summary>
    public CelFsmState Fsm
    {
        get => _fsm;
        set => _fsm = value;
    }

    /// <summary>
    /// Gets or sets the CEL timing model. The default is the synchronous FreeDO model;
    /// <see cref="CelTimingMode.OperaBusy"/> defers the draw to the next CPU tick and keeps
    /// the CEL status busy until the estimated work deadline.
    /// </summary>
    public CelTimingMode CelTiming
    {
        get => _celTiming;
        set
        {
            if (value is not CelTimingMode.FreeDoCompatible and not CelTimingMode.OperaBusy)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown CEL timing mode.");
            }

            _celTiming = value;
        }
    }

    /// <summary>Gets or sets the ARM clock used to convert CEL work into busy cycles.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the clock is not positive.</exception>
    public int ArmClock
    {
        get => _armClock;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The ARM clock must be positive.");
            }

            _armClock = value;
        }
    }

    /// <summary>Gets a value indicating whether a CEL list is waiting for the next CPU tick.</summary>
    public bool CelDrawPending => _celDrawPending;

    /// <summary>Gets the ARM cycles left in the current CEL busy period.</summary>
    public ulong CelBusyCyclesRemaining => _celBusyCyclesRemaining;

    /// <summary>Gets the total ARM cycles estimated for the most recent CEL list.</summary>
    public ulong CelBusyCyclesTotal => _celBusyCyclesTotal;

    /// <summary>Gets a value indicating whether CLUT DMA is enabled in MCTL.</summary>
    public bool ClutDmaEnabled => !_mctlWritten || (_registers[0x008] & MctlClutXen) != 0;

    /// <summary>Gets a value indicating whether VDL/video DMA is enabled in MCTL.</summary>
    public bool VideoDmaEnabled => !_mctlWritten || (_registers[0x008] & MctlVsctXen) != 0;

    /// <summary>
    /// Gets the pixels drawn by the most recent CEL list when
    /// <see cref="CelTimingMode.OperaBusy"/> tracking is active.
    /// </summary>
    public long CelPixelsDrawn => _celPixelsDrawn;

    /// <summary>
    /// Gets the source texels walked by the most recent CEL list when
    /// <see cref="CelTimingMode.OperaBusy"/> tracking is active.
    /// </summary>
    public long CelTexelsWalked => _celTexelsWalked;

    /// <summary>Gets the number of list entries processed by the most recent CEL list.</summary>
    public int CelCelsProcessed => _celCelsProcessed;

    /// <summary>
    /// Gets or sets the shared compatibility state used by the CEL timing heuristics.
    /// FreeDO: <c>fixmode</c>, <c>speedfixes</c>, <c>unknownflag11</c>, <c>sdf</c>.
    /// </summary>
    public CompatibilityState Compatibility { get; set; } = new();

    /// <summary>
    /// Gets or sets a diagnostic sink for kernel debug characters written to MADAM register 0.
    /// FreeDO: <c>EXT_KPRINT</c>. The value is the character code in the low byte.
    /// </summary>
    public Action<uint>? KPrint { get; set; }

    /// <summary>Gets the read modulo value. FreeDO: <c>RMOD</c>.</summary>
    public int ReadModulo => _readModulo;

    /// <summary>Gets the write modulo value. FreeDO: <c>WMOD</c>.</summary>
    public int WriteModulo => _writeModulo;

    /// <summary>Gets the palette lookup table. FreeDO: <c>PLUT</c>.</summary>
    public Span<ushort> Plut => _plut;

    /// <summary>Gets the backing PLUT array for the CEL decoder hot path.</summary>
    internal ushort[] PlutEntries => _plut;

    /// <summary>Gets the last PBUS controller packet. FreeDO: <c>PBUSQueue</c>.</summary>
    public ReadOnlySpan<byte> PbusQueue => _pbusQueue;

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Madam;

    /// <summary>Gets the save-state section version.</summary>
    /// <remarks>Version 2 adds the CEL busy deadline and pending-draw flag.</remarks>
    public ushort Version => 2;

    /// <summary>Resets the MADAM register file and CEL state. FreeDO: <c>_madam_Init</c>.</summary>
    public void Reset()
    {
        Array.Clear(_registers);
        Array.Clear(_plut);
        Array.Clear(_pbusQueue);
        _readModulo = 0;
        _writeModulo = 0;
        _fsm = CelFsmState.Idle;
        _celDrawPending = false;
        _celBusyCyclesRemaining = 0;
        _celBusyCyclesTotal = 0;
        _celPixelsDrawn = 0;
        _celTexelsWalked = 0;
        _celCelsProcessed = 0;
        _result0Temp = 0;
        _result1Temp = 0;
        _result2Temp = 0;
        _result3Temp = 0;

        _registers[0x004] = 0x29;
        _registers[0x574] = 0xfffffffc;
        _registers[0x000] = 0x01020000;
    }

    /// <summary>Stops the CEL engine without clearing registers. Opera: <c>opera_madam_soft_reset</c>.</summary>
    public void SoftReset()
    {
        _registers[0x004] &= ~0xF1u;
        _fsm = CelFsmState.Idle;
        _celDrawPending = false;
        _celBusyCyclesRemaining = 0;
        _celBusyCyclesTotal = 0;
    }

    /// <summary>Reads a MADAM register. FreeDO: <c>_madam_Peek</c>.</summary>
    /// <param name="offset">Byte offset within the MADAM window.</param>
    /// <returns>The register value.</returns>
    public uint Peek(uint offset)
    {
        var address = offset & 0x7ff;

        if (address is >= 0x400 and <= 0x53f)
        {
            return Fifo?.FifoStruct(address) ?? 0;
        }

        if (address == 0x28)
        {
            return _fsm switch
            {
                CelFsmState.Idle => 0x0,
                CelFsmState.Suspended => 0x30,
                CelFsmState.InProcess => 0x10,
                _ => 0,
            };
        }

        return _registers[address];
    }

    /// <summary>Writes a MADAM register. FreeDO: <c>_madam_Poke</c>.</summary>
    /// <param name="offset">Byte offset within the MADAM window.</param>
    /// <param name="value">Register value.</param>
    public void Poke(uint offset, uint value)
    {
        var address = offset & 0x7ff;

        if (address is >= 0x400 and <= 0x53f)
        {
            Fifo?.SetFifo(address, value);
            return;
        }

        switch (address)
        {
            case 0x004:
                _registers[address] = 0x29;
                return;
            case 0x008:
                _registers[address] = value;
                _mctlWritten = true;
                HandleDma8();
                return;
            case 0x580:
                Vdlp?.ProcessVdl(value);
                return;
            case 0x584:
            case 0x588:
            case 0x58c:
            case 0x590:
            case 0x594:
            case 0x598:
            case 0x59c:
                _registers[address] = value;
                return;
            case 0x000:
                KPrint?.Invoke(value);
                return;
            case SpriteStart:
                if (_fsm == CelFsmState.Idle)
                {
                    _fsm = CelFsmState.InProcess;
                }

                if (_celTiming == CelTimingMode.OperaBusy)
                {
                    // Opera replaces the pending busy period when software starts a new
                    // list before the old deadline expires.
                    _celDrawPending = true;
                }

                return;
            case SpriteStop:
                _fsm = CelFsmState.Idle;
                _celDrawPending = false;
                _celBusyCyclesRemaining = 0;
                _celBusyCyclesTotal = 0;
                _registers[0x5a4] = 0;
                return;
            case SpriteContinue:
                if (_fsm == CelFsmState.Suspended)
                {
                    _fsm = CelFsmState.InProcess;
                }

                return;
            case SpritePause:
                if (_fsm == CelFsmState.InProcess)
                {
                    _fsm = CelFsmState.Suspended;
                }

                return;
            case 0x7fc:
                ExecuteMatrixCommand(value);
                return;
            case 0x130:
                _registers[address] = value;
                _readModulo = DecodeRegCtlMod((int)(value & 0xff));
                _writeModulo = DecodeRegCtlMod((int)((value >> 8) & 0xff));
                return;
            default:
                _registers[address] = value;
                return;
        }
    }

    /// <summary>Copies a PBUS controller packet. FreeDO: <c>_madam_KeyPressed</c>.</summary>
    /// <param name="data">Controller packet.</param>
    public void KeyPressed(ReadOnlySpan<byte> data)
    {
        var count = Math.Min(data.Length, _pbusQueue.Length);
        data[..count].CopyTo(_pbusQueue);
        _pbusQueue.AsSpan(count).Fill(0xff);
    }

    /// <summary>Loads PLUT entries from VRAM. FreeDO: <c>Madam.cpp: LoadPLUT</c>.</summary>
    /// <param name="address">PLUT byte address in VRAM.</param>
    /// <param name="count">Entry count: 2, 4, 16, or 32 in the reference.</param>
    public void LoadPlut(uint address, int count)
    {
        if (Ram is null)
        {
            return;
        }

        var entries = Math.Min(count, _plut.Length);
        for (var i = 0; i < entries; i++)
        {
            _plut[i] = Ram.Read16((((address >> 1) + (uint)i) ^ 1) << 1);
        }
    }

    /// <summary>Handles queued CEL work. FreeDO: <c>_madam_HandleCEL</c>.</summary>
    /// <returns>CEL cycles consumed.</returns>
    public int HandleCel()
    {
        if (Ram is null)
        {
            _fsm = CelFsmState.Idle;
            _celDrawPending = false;
            _celBusyCyclesRemaining = 0;
            _celBusyCyclesTotal = 0;
            return 0;
        }

        if (_cel is null || !ReferenceEquals(_celRam, Ram))
        {
            _cel = new CelEngine(this, Ram);
            _celRam = Ram;
        }

        _cel.TraceWriter = CelTraceWriter;
        _cel.TrackWork = _celTiming == CelTimingMode.OperaBusy;
        var cycles = _cel.HandleCel();
        _celPixelsDrawn = _cel.PixelsDrawn;
        _celTexelsWalked = _cel.TexelsWalked;
        _celCelsProcessed = _cel.CelsProcessed;

        if (_celTiming == CelTimingMode.FreeDoCompatible)
        {
            _fsm = CelFsmState.Idle;
            _celDrawPending = false;
            _celBusyCyclesRemaining = 0;
            _celBusyCyclesTotal = 0;
        }
        else
        {
            StartCelBusy();
        }

        return cycles;
    }

    /// <summary>
    /// Advances the CEL state machine before a CPU instruction. FreeDO: no equivalent;
    /// Opera: <c>opera_madam_cel_tick</c>.
    /// </summary>
    /// <param name="elapsedCycles">ARM cycles executed since the previous tick.</param>
    /// <returns><see langword="true"/> when a pending CEL list was drawn.</returns>
    public bool TickCel(uint elapsedCycles)
    {
        if (_celTiming != CelTimingMode.OperaBusy || _fsm != CelFsmState.InProcess)
        {
            return false;
        }

        if (_celDrawPending)
        {
            HandleCel();
            return true;
        }

        if (_celBusyCyclesRemaining > elapsedCycles)
        {
            _celBusyCyclesRemaining -= elapsedCycles;
            return false;
        }

        _celBusyCyclesRemaining = 0;
        _fsm = CelFsmState.Idle;
        return false;
    }

    private void StartCelBusy()
    {
        _celDrawPending = false;

        // Opera paces the engine by the larger of rendered pixels and walked source
        // texels, plus one fixed setup cost per list entry.
        var work = Math.Max(_celPixelsDrawn, _celTexelsWalked);
        var busy = work <= 0
            ? 0
            : ((ulong)work * (uint)_armClock) / CelPixelsPerSecond;
        busy += (ulong)Math.Max(_celCelsProcessed, 0) * CelSetupCycles;

        _celBusyCyclesRemaining = busy;
        _celBusyCyclesTotal = busy;

        if (busy == 0)
        {
            _fsm = CelFsmState.Idle;
        }
        else
        {
            _fsm = CelFsmState.InProcess;
        }

        CelTraceWriter?.WriteLine(
            $"cel-list cels={_celCelsProcessed} pixels={_celPixelsDrawn} "
            + $"texels={_celTexelsWalked} busy={busy}");
    }

    /// <summary>Gets or sets an optional diagnostic writer for CEL commands.</summary>
    public TextWriter? CelTraceWriter { get; set; }

    /// <summary>Writes the MADAM save-state payload. FreeDO: <c>_madam_Save</c>.</summary>
    /// <param name="writer">Destination writer.</param>
    public void Save(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        for (var i = 0; i < _registers.Length; i++)
        {
            writer.WriteUInt32(_registers[i]);
        }

        for (var i = 0; i < _plut.Length; i++)
        {
            writer.WriteUInt16(_plut[i]);
        }

        writer.WriteBytes(_pbusQueue);
        writer.WriteInt32(_readModulo);
        writer.WriteInt32(_writeModulo);
        writer.WriteUInt32((uint)_fsm);
        writer.WriteDouble(_result0Temp);
        writer.WriteDouble(_result1Temp);
        writer.WriteDouble(_result2Temp);
        writer.WriteDouble(_result3Temp);
        writer.WriteUInt32(_celDrawPending ? 1u : 0u);
        writer.WriteUInt64(_celBusyCyclesRemaining);
        writer.WriteUInt64(_celBusyCyclesTotal);
    }

    /// <summary>Reads the MADAM save-state payload. FreeDO: <c>_madam_Load</c>.</summary>
    /// <param name="reader">Source reader.</param>
    public void Load(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        for (var i = 0; i < _registers.Length; i++)
        {
            _registers[i] = reader.ReadUInt32();
        }

        for (var i = 0; i < _plut.Length; i++)
        {
            _plut[i] = reader.ReadUInt16();
        }

        reader.ReadBytes(_pbusQueue);
        _readModulo = reader.ReadInt32();
        _writeModulo = reader.ReadInt32();
        _fsm = (CelFsmState)reader.ReadUInt32();
        _result0Temp = reader.ReadDouble();
        _result1Temp = reader.ReadDouble();
        _result2Temp = reader.ReadDouble();
        _result3Temp = reader.ReadDouble();
        _celPixelsDrawn = 0;
        _celTexelsWalked = 0;
        _celCelsProcessed = 0;

        if (reader is StateSectionReader { Version: 1 })
        {
            // Version-1 states have no timing fields. A running or suspended FSM implies
            // that the list has not been drawn yet.
            _celDrawPending = _fsm is CelFsmState.InProcess or CelFsmState.Suspended;
            _celBusyCyclesRemaining = 0;
            _celBusyCyclesTotal = 0;
            return;
        }

        var pending = reader.ReadUInt32();
        if (pending > 1)
        {
            throw new InvalidStateException($"Invalid CEL pending-draw flag {pending}.");
        }

        var busyRemaining = reader.ReadUInt64();
        var busyTotal = reader.ReadUInt64();
        if (busyRemaining > busyTotal)
        {
            throw new InvalidStateException(
                $"Invalid CEL busy deadline: {busyRemaining} of {busyTotal} cycles remain.");
        }

        _celDrawPending = pending != 0;
        _celBusyCyclesRemaining = busyRemaining;
        _celBusyCyclesTotal = busyTotal;
    }

    /// <summary>
    /// Reads the raw register file without the CPU-facing status overlays.
    /// FreeDO: direct <c>mregs[]</c> access inside the CEL engine.
    /// </summary>
    /// <param name="offset">Register offset.</param>
    /// <returns>The raw register value.</returns>
    internal uint ReadRegister(uint offset)
    {
        return _registers[offset & 0x7ff];
    }

    /// <summary>
    /// Writes the raw register file without CPU-facing side effects.
    /// FreeDO: direct <c>mregs[]</c> access inside the CEL engine.
    /// </summary>
    /// <param name="offset">Register offset.</param>
    /// <param name="value">Register value.</param>
    internal void WriteRegister(uint offset, uint value)
    {
        _registers[offset & 0x7ff] = value;
    }

    private void HandleDma8()
    {
        if ((_registers[0x008] & MctlPlayXen) == 0)
        {
            return;
        }

        DmaPbus();
        _registers[0x008] &= ~MctlPlayXen;
        Interrupts?.GenerateFiq(0, 1);
    }

    private void DmaPbus()
    {
        if (unchecked((int)_registers[0x574]) < 0)
        {
            return;
        }

        _registers[0x574] -= 4;
        _registers[0x570] += 4;
        _registers[0x578] += 4;

        var index = 0;
        while (unchecked((int)_registers[0x574]) > 0)
        {
            var value = index < 5
                ? BinaryPrimitives.ReadUInt32LittleEndian(_pbusQueue.AsSpan(index * 4, 4))
                : 0xffffffffu;
            Bus?.Write32(_registers[0x570], value);
            _registers[0x574] -= 4;
            _registers[0x570] += 4;
            _registers[0x578] += 4;
            index++;
        }

        _registers[0x574] = 0xfffffffc;
    }

    private void ExecuteMatrixCommand(uint command)
    {
        _registers[0x7fc] = 0;

        switch (command)
        {
            case 0:
                StoreMatrixResults();
                return;
            case 1:
                StoreMatrixResults();
                _result0Temp = (M(0x600) * V(0x640) + M(0x604) * V(0x644) + M(0x608) * V(0x648) + M(0x60c) * V(0x64c)) / 65536.0;
                _result1Temp = (M(0x610) * V(0x640) + M(0x614) * V(0x644) + M(0x618) * V(0x648) + M(0x61c) * V(0x64c)) / 65536.0;
                _result2Temp = (M(0x620) * V(0x640) + M(0x624) * V(0x644) + M(0x628) * V(0x648) + M(0x62c) * V(0x64c)) / 65536.0;
                _result3Temp = (M(0x630) * V(0x640) + M(0x634) * V(0x644) + M(0x638) * V(0x648) + M(0x63c) * V(0x64c)) / 65536.0;
                return;
            case 2:
                StoreMatrixResults();
                _result0Temp = (M(0x600) * V(0x640) + M(0x604) * V(0x644) + M(0x608) * V(0x648)) / 65536.0;
                _result1Temp = (M(0x610) * V(0x640) + M(0x614) * V(0x644) + M(0x618) * V(0x648)) / 65536.0;
                _result2Temp = (M(0x620) * V(0x640) + M(0x624) * V(0x644) + M(0x628) * V(0x648)) / 65536.0;
                return;
            case 3:
                StoreMatrixResults();
                _result2Temp = (int)((M(0x620) * V(0x640) + M(0x624) * V(0x644) + M(0x628) * V(0x648)) / 65536.0);

                double scale;
                if (_result2Temp != 0)
                {
                    scale = Nfrac16() / _result2Temp;
                }
                else
                {
                    scale = Nfrac16();
                }

                _result0Temp = (int)((M(0x600) * V(0x640) + M(0x604) * V(0x644) + M(0x608) * V(0x648)) / 65536.0);
                _result1Temp = (int)((M(0x610) * V(0x640) + M(0x614) * V(0x644) + M(0x618) * V(0x648)) / 65536.0);
                _result0Temp = _result0Temp * scale / 65536.0 / 65536.0;
                _result1Temp = _result1Temp * scale / 65536.0 / 65536.0;
                return;
            default:
                return;
        }
    }

    private void StoreMatrixResults()
    {
        _registers[0x660] = ToRegister(_result0Temp);
        _registers[0x664] = ToRegister(_result1Temp);
        _registers[0x668] = ToRegister(_result2Temp);
        _registers[0x66c] = ToRegister(_result3Temp);
    }

    private static int DecodeRegCtlMod(int value)
    {
        // Opera: REGCTL MOD bits encode pixel-width groups; XY2OFF needs a byte
        // stride. The old FreeDO formula mixed bit positions and produced the
        // wrong row stride for several widths (DinoPark Tycoon).
        var mod = 0;
        if ((value & 0x01) != 0)
        {
            mod += 32 << 2;
        }

        if ((value & 0x02) != 0)
        {
            mod += 512 << 2;
        }

        if ((value & 0x04) != 0)
        {
            mod += 256 << 2;
        }

        if ((value & 0x08) != 0)
        {
            mod += 1024 << 2;
        }

        if ((value & 0x10) != 0)
        {
            mod += 64 << 2;
        }

        if ((value & 0x20) != 0)
        {
            mod += 128 << 2;
        }

        if ((value & 0x40) != 0)
        {
            mod += 256 << 2;
        }

        if ((value & 0x80) != 0)
        {
            mod += 1024 << 2;
        }

        return mod;
    }

    private double M(int offset)
    {
        return unchecked((int)_registers[offset]);
    }

    private double V(int offset)
    {
        return unchecked((int)_registers[offset]);
    }

    private long Nfrac16()
    {
        return unchecked(((long)_registers[0x680] << 32) | _registers[0x684]);
    }

    /// <summary>
    /// Converts a matrix result to a register value. FreeDO assigns a <c>double</c> directly
    /// to an <c>unsigned int</c>; the C++ behaviour is undefined for out-of-range values.
    /// The port truncates through <see cref="long"/> and wraps two's-complement, and maps
    /// NaN/out-of-range values to zero or all ones deterministically.
    /// </summary>
    private static uint ToRegister(double value)
    {
        if (double.IsNaN(value))
        {
            return 0;
        }

        if (value >= 9.2233720368547758e18)
        {
            return 0xffffffff;
        }

        if (value <= -9.2233720368547758e18)
        {
            return 0;
        }

        return unchecked((uint)(long)value);
    }
}
