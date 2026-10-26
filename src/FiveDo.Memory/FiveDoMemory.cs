using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using FiveDo.Core;

namespace FiveDo.Memory;

/// <summary>
/// The 3DO memory subsystem: DRAM/VRAM, both BIOS ROM banks, NVRAM, and the CPU address bus.
/// </summary>
/// <remarks>
/// <para>
/// RAM and ROM are stored as <see cref="uint"/> words whose numeric values match what
/// FreeDOCore's little-endian C++ build sees in <c>pRam</c>/<c>pRom</c>. Byte accesses use
/// the same lane convention as the reference.
/// </para>
/// <para>
/// The backing RAM allocation includes sixteen reserved 1 MB mirrors for the future
/// high-resolution mode. Only the first 3 MB are CPU-visible in v1.
/// </para>
/// </remarks>
public sealed class FiveDoMemory : IAddressBus, IRam, IPcAdjustSource, IRomSelector, IStateSection
{
    private readonly uint[] _ram = new uint[EmulatorConstants.RamAllocationSize / sizeof(uint)];
    private readonly uint[] _rom = new uint[EmulatorConstants.RomSize / sizeof(uint)];
    private readonly byte[] _nvram = new byte[EmulatorConstants.NvramSize];
    private int _romBank;
    private bool _pcAdjustPending;

    /// <summary>Initializes a new instance of the <see cref="FiveDoMemory"/> class.</summary>
    /// <param name="tuning">Machine tuning. Defaults to <see cref="EmulatorTuning.Default"/>.</param>
    public FiveDoMemory(EmulatorTuning? tuning = null)
    {
        Tuning = tuning ?? EmulatorTuning.Default;
        Tuning.Validate();
    }

    /// <summary>Gets the machine tuning.</summary>
    public EmulatorTuning Tuning { get; }

    /// <summary>Gets the CPU-visible RAM/VRAM size in bytes.</summary>
    public static int VisibleSize => EmulatorConstants.RamSize;

    /// <summary>Gets the total RAM backing size in bytes, including reserved mirrors.</summary>
    public int Size => _ram.Length * sizeof(uint);

    /// <summary>Gets the active ROM bank (0 or 1).</summary>
    public int RomBank => _romBank;

    /// <summary>Gets a value indicating whether a CLIO write requested a four-byte PC advance.</summary>
    public bool PcAdjustPending => _pcAdjustPending;

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Memory;

    /// <summary>Gets the save-state section version.</summary>
    public ushort Version => 1;

    /// <summary>Gets the RAM/VRAM raw accessor.</summary>
    public IRam Ram => this;

    /// <summary>Gets the CPU address bus.</summary>
    public IAddressBus Bus => this;

    /// <summary>Gets or sets the MADAM register window hook.</summary>
    public IMadamBus? Madam { get; set; }

    /// <summary>Gets or sets the CLIO register window hook.</summary>
    public IClioBus? Clio { get; set; }

    /// <summary>Gets or sets the SPORT window hook.</summary>
    public ISportBus? Sport { get; set; }

    /// <summary>Gets or sets the diagnostic port hook.</summary>
    public IDiagPort? DiagPort { get; set; }

    /// <summary>
    /// Gets or sets a developer-only observer for RAM writes.
    /// </summary>
    /// <remarks>
    /// The observer is invoked for CPU and DMA writes alike, with the physical RAM
    /// offset, the written value, and the access size in bytes. It is null in normal
    /// operation and must stay off the hot path unless a diagnostic is requested.
    /// </remarks>
    public Action<uint, uint, int>? RamWriteObserver { get; set; }

    /// <summary>Gets a read-only view of NVRAM.</summary>
    public ReadOnlySpan<byte> Nvram => _nvram;

    /// <summary>Loads one 1 MB BIOS bank from big-endian ROM bytes.</summary>
    /// <param name="bank">Bank index: 0 for BIOS1, 1 for BIOS2.</param>
    /// <param name="data">Exactly 1 MB of big-endian ROM data.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bank"/> is not 0 or 1.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is not exactly 1 MB.</exception>
    public void LoadRom(int bank, ReadOnlySpan<byte> data)
    {
        if ((uint)bank > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bank), bank, "The ROM bank must be 0 or 1.");
        }

        if (data.Length != EmulatorConstants.RomBankSize)
        {
            throw new ArgumentException($"A BIOS bank must be exactly {EmulatorConstants.RomBankSize} bytes.", nameof(data));
        }

        var wordOffset = bank * (EmulatorConstants.RomBankSize / sizeof(uint));
        for (var i = 0; i < data.Length; i += sizeof(uint))
        {
            _rom[wordOffset + (i / sizeof(uint))] = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(i, sizeof(uint)));
        }
    }

    /// <summary>Loads BIOS1.</summary>
    /// <param name="bios1">Exactly 1 MB of big-endian BIOS1 data.</param>
    public void LoadBios(ReadOnlySpan<byte> bios1) => LoadRom(0, bios1);

    /// <summary>Loads BIOS1 and BIOS2.</summary>
    /// <param name="bios1">Exactly 1 MB of big-endian BIOS1 data.</param>
    /// <param name="bios2">Exactly 1 MB of big-endian BIOS2 data.</param>
    public void LoadBios(ReadOnlySpan<byte> bios1, ReadOnlySpan<byte> bios2)
    {
        LoadRom(0, bios1);
        LoadRom(1, bios2);
    }

    /// <summary>Loads NVRAM from a 32 KB image.</summary>
    /// <param name="data">Exactly 32 KB of NVRAM data.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="data"/> is not exactly 32 KB.</exception>
    public void LoadNvram(ReadOnlySpan<byte> data)
    {
        if (data.Length != EmulatorConstants.NvramSize)
        {
            throw new ArgumentException($"NVRAM must be exactly {EmulatorConstants.NvramSize} bytes.", nameof(data));
        }

        data.CopyTo(_nvram);
    }

    /// <summary>Copies NVRAM into <paramref name="destination"/>.</summary>
    /// <param name="destination">Destination span of at least 32 KB.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small.</exception>
    public void CopyNvram(Span<byte> destination)
    {
        if (destination.Length < EmulatorConstants.NvramSize)
        {
            throw new ArgumentException($"The destination must hold at least {EmulatorConstants.NvramSize} bytes.", nameof(destination));
        }

        _nvram.CopyTo(destination);
    }

    /// <summary>Copies one ROM bank back to big-endian BIOS bytes.</summary>
    /// <param name="bank">Bank index: 0 or 1.</param>
    /// <param name="destination">Destination of exactly 1 MB.</param>
    public void CopyRomBank(int bank, Span<byte> destination)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)bank, 1u);
        if (destination.Length < EmulatorConstants.RomBankSize)
        {
            throw new ArgumentException($"The destination must hold {EmulatorConstants.RomBankSize} bytes.", nameof(destination));
        }

        var wordOffset = bank * (EmulatorConstants.RomBankSize / sizeof(uint));
        for (var i = 0; i < EmulatorConstants.RomBankSize; i += sizeof(uint))
        {
            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(i, sizeof(uint)), _rom[wordOffset + (i / sizeof(uint))]);
        }
    }

    /// <summary>Writes the memory save-state payload.</summary>
    /// <param name="writer">Destination writer.</param>
    public void Save(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteByte((byte)_romBank);
        writer.WriteBoolean(_pcAdjustPending);
        writer.WriteBytes(ToStateBytes(_ram));
        writer.WriteBytes(ToStateBytes(_rom));
        writer.WriteBytes(_nvram);
    }

    /// <summary>Reads the memory save-state payload.</summary>
    /// <param name="reader">Source reader.</param>
    public void Load(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var bank = reader.ReadByte();
        if (bank > 1)
        {
            throw new InvalidStateException($"Invalid ROM bank {bank} in memory state.");
        }

        _romBank = bank;
        _pcAdjustPending = reader.ReadBoolean();
        reader.ReadBytes(ToStateBytes(_ram));
        reader.ReadBytes(ToStateBytes(_rom));
        reader.ReadBytes(_nvram);
    }

    /// <summary>Selects the active ROM bank.</summary>
    /// <param name="bank">Bank index: 0 or 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bank"/> is not 0 or 1.</exception>
    public void SelectRomBank(int bank)
    {
        if ((uint)bank > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bank), bank, "The ROM bank must be 0 or 1.");
        }

        _romBank = bank;
    }

    /// <summary>Consumes a pending CLIO PC-advance request.</summary>
    /// <returns><see langword="true"/> when a request was pending.</returns>
    public bool ConsumePcAdjustment()
    {
        var pending = _pcAdjustPending;
        _pcAdjustPending = false;
        return pending;
    }

    private static Span<byte> ToStateBytes(uint[] words)
    {
        return MemoryMarshal.AsBytes(words.AsSpan());
    }

    /// <summary>Selects the active ROM bank through the bus contract. FreeDO: <c>SelectROM</c>.</summary>
    /// <param name="bank">Zero for BIOS1, non-zero for BIOS2.</param>
    void IRomSelector.SelectRom(int bank) => SelectRomBank(bank == 0 ? 0 : 1);

    /// <summary>Copies CPU-visible RAM bytes for diagnostics.</summary>
    /// <param name="offset">Byte offset in CPU-visible RAM.</param>
    /// <param name="destination">Destination span.</param>
    public void CopyRam(uint offset, Span<byte> destination)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = ReadRam8(offset + (uint)i);
        }
    }

    /// <summary>Clears the CPU-visible RAM and the reserved mirrors.</summary>
    public void ClearRam() => _ram.AsSpan().Clear();

    /// <summary>Resets the bus and ROM bank without clearing RAM or NVRAM.</summary>
    public void Reset()
    {
        _romBank = 0;
        _pcAdjustPending = false;
    }

    byte IRam.Read8(uint offset) => ReadRam8(offset);

    ushort IRam.Read16(uint offset) => ReadRam16(offset);

    uint IRam.Read32(uint offset) => ReadRam32(offset);

    void IRam.Write8(uint offset, byte value) => WriteRam8(offset, value);

    void IRam.Write16(uint offset, ushort value) => WriteRam16(offset, value);

    void IRam.Write32(uint offset, uint value) => WriteRam32(offset, value);

    private byte ReadRam8(uint offset)
    {
        var index = (int)(offset >> 2);
        if ((uint)index >= (uint)_ram.Length)
        {
            return 0;
        }

        return (byte)(_ram[index] >> (int)((offset & 3) * 8));
    }

    private ushort ReadRam16(uint offset)
    {
        var index = (int)(offset >> 2);
        if ((uint)index >= (uint)_ram.Length)
        {
            return 0;
        }

        var lane = (int)(offset & 3);
        if (lane <= 2)
        {
            return (ushort)(_ram[index] >> (lane * 8));
        }

        var low = (byte)(_ram[index] >> 24);
        var next = (uint)index + 1;
        var high = next < (uint)_ram.Length ? (byte)_ram[next] : (byte)0;
        return (ushort)(low | (high << 8));
    }

    private uint ReadRam32(uint offset)
    {
        var index = (int)(offset >> 2);
        if ((uint)index >= (uint)_ram.Length)
        {
            return 0;
        }

        if ((offset & 3) == 0)
        {
            return _ram[index];
        }

        return (uint)(ReadRam8(offset)
            | (ReadRam8(offset + 1) << 8)
            | (ReadRam8(offset + 2) << 16)
            | (ReadRam8(offset + 3) << 24));
    }

    private void WriteRam8(uint offset, byte value, bool notify = true)
    {
        var index = (int)(offset >> 2);
        if ((uint)index >= (uint)_ram.Length)
        {
            return;
        }

        if (notify)
        {
            RamWriteObserver?.Invoke(offset, value, 1);
        }

        var shift = (int)((offset & 3) * 8);
        var mask = 0xFFu << shift;
        _ram[index] = (_ram[index] & ~mask) | ((uint)value << shift);
    }

    private void WriteRam16(uint offset, ushort value, bool notify = true)
    {
        if ((offset & 1) == 0 && (offset & 3) != 3)
        {
            var index = (int)(offset >> 2);
            if ((uint)index >= (uint)_ram.Length)
            {
                return;
            }

            if (notify)
            {
                RamWriteObserver?.Invoke(offset, value, 2);
            }

            var shift = (int)((offset & 3) * 8);
            var mask = 0xFFFFu << shift;
            _ram[index] = (_ram[index] & ~mask) | ((uint)value << shift);
            return;
        }

        WriteRam8(offset, (byte)value, notify);
        WriteRam8(offset + 1, (byte)(value >> 8), notify);
    }

    private void WriteRam32(uint offset, uint value, bool notify = true)
    {
        var index = (int)(offset >> 2);
        if ((uint)index >= (uint)_ram.Length)
        {
            return;
        }

        if (notify)
        {
            RamWriteObserver?.Invoke(offset, value, 4);
        }

        if ((offset & 3) == 0)
        {
            _ram[index] = value;
            return;
        }

        WriteRam8(offset, (byte)value, notify: false);
        WriteRam8(offset + 1, (byte)(value >> 8), notify: false);
        WriteRam8(offset + 2, (byte)(value >> 16), notify: false);
        WriteRam8(offset + 3, (byte)(value >> 24), notify: false);
    }

    /// <inheritdoc />
    public byte Read8(uint address)
    {
        if (address < EmulatorConstants.RamSize)
        {
            return ReadRam8(address ^ 3);
        }

        var index = address ^ (EmulatorConstants.RomBase | 3);
        if ((index & ~0xFFFFFu) == 0)
        {
            return ReadRom8(index);
        }

        index = address ^ (EmulatorConstants.NvramDiagWindowBase | 3);
        if ((index & ~0xFFFFFu) == 0 && (index & 0x40000) == 0x40000)
        {
            return _nvram[(index >> 2) & 0x7FFF];
        }

        return unchecked((byte)EmulatorConstants.UnmappedValue);
    }

    /// <inheritdoc />
    public ushort Read16(uint address)
    {
        if (address < EmulatorConstants.RamSize)
        {
            return ReadRam16(address ^ 2);
        }

        if ((address & 0xFFF00000u) == EmulatorConstants.RomBase)
        {
            return ReadRom16((address & 0xFFFFFu) ^ 2);
        }

        if (address >= EmulatorConstants.NvramBase && address < EmulatorConstants.DiagBase)
        {
            return _nvram[((address - EmulatorConstants.NvramBase) >> 2) & 0x7FFF];
        }

        return unchecked((ushort)EmulatorConstants.UnmappedValue);
    }

    /// <inheritdoc />
    public uint Read32(uint address)
    {
        address &= ~3u;

        if (address < EmulatorConstants.RamSize)
        {
            return ReadRam32(address);
        }

        var index = address ^ EmulatorConstants.MadamBase;
        if ((index & ~0x7FFu) == 0)
        {
            return Madam?.Peek(index) ?? 0;
        }

        index = address ^ EmulatorConstants.ClioBase;
        if ((index & ~0xFFFFu) == 0)
        {
            return Clio?.Peek(index) ?? 0;
        }

        index = address ^ EmulatorConstants.SportBase;
        if ((index & ~0xFFFFFu) == 0)
        {
            if ((index & ~0x1FFFu) == 0)
            {
                return Sport?.SetSource(index) ?? 0;
            }

            return EmulatorConstants.UnmappedValue;
        }

        index = address ^ EmulatorConstants.RomBase;
        if ((index & ~0xFFFFFu) == 0)
        {
            return ReadRom32(index);
        }

        index = address ^ EmulatorConstants.NvramDiagWindowBase;
        if ((index & ~0xFFFFFu) == 0)
        {
            if ((index & 0x80000) != 0)
            {
                return DiagPort?.Get() ?? 0;
            }

            if ((index & 0x40000) != 0)
            {
                return _nvram[(index >> 2) & 0x7FFF];
            }
        }

        return EmulatorConstants.UnmappedValue;
    }

    /// <inheritdoc />
    public void Write8(uint address, byte value)
    {
        if (address < EmulatorConstants.RamSize)
        {
            WriteRam8(address ^ 3, value);
            return;
        }

        var index = address ^ (EmulatorConstants.NvramDiagWindowBase | 3);
        if ((index & ~0xFFFFFu) == 0 && (index & 0x40000) == 0x40000)
        {
            _nvram[(index >> 2) & 0x7FFF] = value;
        }
    }

    /// <inheritdoc />
    public void Write16(uint address, ushort value)
    {
        if (address < EmulatorConstants.RamSize)
        {
            WriteRam16(address ^ 2, value);
            return;
        }

        if (address >= EmulatorConstants.NvramBase && address < EmulatorConstants.DiagBase)
        {
            _nvram[((address - EmulatorConstants.NvramBase) >> 2) & 0x7FFF] = (byte)value;
        }
    }

    /// <inheritdoc />
    public void Write32(uint address, uint value)
    {
        address &= ~3u;

        if (address < EmulatorConstants.RamSize)
        {
            WriteRam32(address, value);
            return;
        }

        var index = address ^ EmulatorConstants.MadamBase;
        if ((index & ~0x7FFu) == 0)
        {
            Madam?.Poke(index, value);
            return;
        }

        index = address ^ EmulatorConstants.ClioBase;
        if ((index & ~0xFFFFu) == 0)
        {
            if (Clio is not null && Clio.Poke(index, value))
            {
                _pcAdjustPending = true;
            }

            return;
        }

        index = address ^ EmulatorConstants.SportBase;
        if ((index & ~0xFFFFFu) == 0)
        {
            Sport?.WriteAccess(index, value);
            return;
        }

        index = address ^ EmulatorConstants.NvramDiagWindowBase;
        if ((index & ~0xFFFFFu) == 0)
        {
            if ((index & 0x80000) != 0)
            {
                DiagPort?.Send(value);
                return;
            }

            if ((index & 0x40000) != 0)
            {
                _nvram[(index >> 2) & 0x7FFF] = (byte)value;
            }
        }
    }

    private byte ReadRom8(uint offset)
    {
        var wordIndex = (_romBank * (EmulatorConstants.RomBankSize / sizeof(uint))) + (int)((offset & 0xFFFFF) >> 2);
        return (byte)(_rom[wordIndex] >> (int)((offset & 3) * 8));
    }

    private ushort ReadRom16(uint offset)
    {
        return (ushort)(ReadRom8(offset) | (ReadRom8(offset + 1) << 8));
    }

    private uint ReadRom32(uint offset)
    {
        var wordIndex = (_romBank * (EmulatorConstants.RomBankSize / sizeof(uint))) + (int)((offset & 0xFFFFF) >> 2);
        return _rom[wordIndex];
    }
}
