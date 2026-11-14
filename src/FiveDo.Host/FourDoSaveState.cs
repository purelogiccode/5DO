using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace FiveDo.Host;

/// <summary>
/// Parses the raw <c>.4dosav</c> save-state container written by 4DO/FreeDOCore.
/// </summary>
/// <remarks>
/// <para>
/// The file is the exact byte image produced by <c>_3do_Save</c>. The first 64 bytes are
/// an index table: word 0 is the magic <c>0x97970101</c>, word 1 is 64, and words 2-9
/// are the starts of the ARM, VDL, DSP, CLIO, Quartz, SPORT, MADAM, and XBUS records.
/// Word 9 is also the end of the file because XBUS is the last record.
/// </para>
/// <para>
/// Each component is a raw C structure copy. 4DO is a 32-bit application, but the ARM
/// record size reveals whether the producing build used 32-bit or 64-bit pointers;
/// pointer fields are ignored on load by 4DO itself.
/// </para>
/// </remarks>
public sealed class FourDoSaveState
{
    /// <summary>Magic value at the start of a 4DO state file.</summary>
    public const uint Magic = 0x97970101;

    /// <summary>Size of the index table at the start of the file.</summary>
    public const int HeaderSize = 64;

    /// <summary>Size of the 3 MB DRAM/VRAM image stored by FreeDOCore.</summary>
    public const int ArmRamSize = 3 * 1024 * 1024;

    /// <summary>Size of the two 1 MB BIOS banks stored by FreeDOCore.</summary>
    public const int ArmRomSize = 2 * 1024 * 1024;

    /// <summary>Size of the NVRAM image stored by FreeDOCore.</summary>
    public const int ArmNvramSize = 32 * 1024;

    /// <summary>Size of the fixed ARM register area that follows the pointer fields.</summary>
    public const int ArmRegisterSize = 183;

    /// <summary>Size of the VDLP record in current 4DO builds.</summary>
    public const int VdlSize = 132;

    /// <summary>Size of the fixed DSP tail that follows the instruction cache.</summary>
    public const int DspTailSize = 7489;

    /// <summary>Size of the CLIO record in current 4DO builds.</summary>
    public const int ClioSize = 262_496;

    /// <summary>Size of the Quartz record in current 4DO builds.</summary>
    public const int QuartzSize = 32;

    /// <summary>Size of the SPORT record in current 4DO builds.</summary>
    public const int SportSize = 12;

    /// <summary>Size of the MADAM record in current 4DO builds.</summary>
    public const int MadamSize = 8_544;

    /// <summary>Size of the XBUS structure written by FreeDOCore.</summary>
    public const int XbusStructSize = 268;

    /// <summary>Size of the XBUS device-offset table that follows the structure.</summary>
    public const int XbusDeviceTableSize = 16 * sizeof(int);

    /// <summary>Minimum size of the XBUS record including the device table.</summary>
    public const int XbusSize = XbusStructSize + XbusDeviceTableSize;

    private readonly byte[] _data;
    private readonly int[] _offsets;
    private readonly int _armStructSize;
    private readonly List<string> _warnings = [];

    private FourDoSaveState(byte[] data, int[] offsets, int pointerSize, int armStructSize)
    {
        _data = data;
        _offsets = offsets;
        PointerSize = pointerSize;
        _armStructSize = armStructSize;
    }

    /// <summary>Gets the total size of the three ARM pointer fields: 12 or 24 bytes.</summary>
    public int PointerSize { get; }

    /// <summary>Gets the ARM structure size, including its pointer fields.</summary>
    public int ArmStructSize => _armStructSize;

    /// <summary>Gets warnings produced while parsing the container.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Gets the total length of the state image.</summary>
    public int Length => _data.Length;

    /// <summary>Gets the fixed ARM registers, without the ignored pointer fields.</summary>
    public ReadOnlySpan<byte> ArmRegisters =>
        _data.AsSpan(_offsets[1] + PointerSize, ArmRegisterSize);

    /// <summary>Gets the 3 MB DRAM/VRAM image.</summary>
    public ReadOnlySpan<byte> ArmRam =>
        _data.AsSpan(GetArmRamOffset(), ArmRamSize);

    /// <summary>Gets the two 1 MB BIOS banks.</summary>
    public ReadOnlySpan<byte> ArmRom =>
        _data.AsSpan(GetArmRamOffset() + ArmRamSize, ArmRomSize);

    /// <summary>Gets the 32 KB NVRAM image.</summary>
    public ReadOnlySpan<byte> ArmNvram =>
        _data.AsSpan(GetArmRamOffset() + ArmRamSize + ArmRomSize, ArmNvramSize);

    /// <summary>Gets the VDLP record.</summary>
    public ReadOnlySpan<byte> Vdl => _data.AsSpan(_offsets[2], Math.Min(VdlSize, _offsets[3] - _offsets[2]));

    /// <summary>Gets the DSP record.</summary>
    public ReadOnlySpan<byte> Dsp => _data.AsSpan(_offsets[3], _offsets[4] - _offsets[3]);

    /// <summary>Gets the CLIO record.</summary>
    public ReadOnlySpan<byte> Clio => _data.AsSpan(_offsets[4], _offsets[5] - _offsets[4]);

    /// <summary>Gets the Quartz record.</summary>
    public ReadOnlySpan<byte> Quartz => _data.AsSpan(_offsets[5], Math.Min(QuartzSize, _offsets[6] - _offsets[5]));

    /// <summary>Gets the SPORT record.</summary>
    public ReadOnlySpan<byte> Sport => _data.AsSpan(_offsets[6], Math.Min(SportSize, _offsets[7] - _offsets[6]));

    /// <summary>Gets the MADAM record.</summary>
    public ReadOnlySpan<byte> Madam => _data.AsSpan(_offsets[7], Math.Min(MadamSize, _offsets[8] - _offsets[7]));

    /// <summary>Gets the XBUS record.</summary>
    public ReadOnlySpan<byte> Xbus => _data.AsSpan(_offsets[8], _offsets[9] - _offsets[8]);

    /// <summary>Parses a 4DO state image.</summary>
    /// <param name="data">Complete <c>.4dosav</c> bytes.</param>
    /// <returns>The parsed state.</returns>
    /// <exception cref="InvalidDataException">Thrown when the container or a known record is malformed.</exception>
    public static FourDoSaveState Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
        {
            throw new InvalidDataException(
                $"A 4DO save state must be at least {HeaderSize} bytes; the file is {data.Length}.");
        }

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (magic != Magic)
        {
            throw new InvalidDataException(
                $"Not a 4DO save state: expected magic 0x{Magic:X8}, found 0x{magic:X8}.");
        }

        var offsets = new int[10];
        for (var i = 0; i < offsets.Length; i++)
        {
            offsets[i] = BinaryPrimitives.ReadInt32LittleEndian(data[(i * sizeof(int))..]);
        }

        if (offsets[1] < HeaderSize || offsets[1] > data.Length)
        {
            throw new InvalidDataException($"The 4DO ARM record starts at invalid offset {offsets[1]}.");
        }

        for (var i = 2; i < offsets.Length; i++)
        {
            if (offsets[i] < offsets[i - 1] || offsets[i] > data.Length)
            {
                throw new InvalidDataException(
                    $"The 4DO record table is not monotonic at index {i} "
                    + $"({offsets[i - 1]} then {offsets[i]}, file length {data.Length}).");
            }
        }

        var armPayload = offsets[2] - offsets[1];
        var armStructSize = armPayload - ArmRamSize - ArmRomSize - ArmNvramSize;
        var pointerSize = armStructSize switch
        {
            ArmRegisterSize + 12 => 12,
            ArmRegisterSize + 24 => 24,
            _ => throw new InvalidDataException(
                $"The 4DO ARM record is {armPayload} bytes; its structure size {armStructSize} "
                + $"is not a 32-bit ({ArmRegisterSize + 12}) or 64-bit "
                + $"({ArmRegisterSize + 24}) layout."),
        };

        var state = new FourDoSaveState(data.ToArray(), offsets, pointerSize, armStructSize);
        state.ValidateRecords();
        if (offsets[9] != data.Length)
        {
            state._warnings.Add(
                $"The 4DO state declares {offsets[9]} bytes but the file has {data.Length}; "
                + "trailing bytes were ignored.");
        }

        return state;
    }

    private int GetArmRamOffset()
    {
        return _offsets[1] + ArmStructSize;
    }

    private void ValidateRecords()
    {
        RequireAtLeast(_offsets[3] - _offsets[2], VdlSize, "VDL");
        RequireAtLeast(_offsets[4] - _offsets[3], DspTailSize, "DSP");
        RequireAtLeast(_offsets[5] - _offsets[4], ClioSize, "CLIO");
        RequireAtLeast(_offsets[6] - _offsets[5], QuartzSize, "Quartz");
        RequireAtLeast(_offsets[7] - _offsets[6], SportSize, "SPORT");
        RequireAtLeast(_offsets[8] - _offsets[7], MadamSize, "MADAM");
        RequireAtLeast(_offsets[9] - _offsets[8], XbusSize, "XBUS");

        WarnIfLarger(_offsets[3] - _offsets[2], VdlSize, "VDL");
        WarnIfLarger(_offsets[5] - _offsets[4], ClioSize, "CLIO");
        WarnIfLarger(_offsets[6] - _offsets[5], QuartzSize, "Quartz");
        WarnIfLarger(_offsets[7] - _offsets[6], SportSize, "SPORT");
        WarnIfLarger(_offsets[8] - _offsets[7], MadamSize, "MADAM");
        WarnIfLarger(_offsets[9] - _offsets[8], XbusSize, "XBUS");
    }

    private void RequireAtLeast(int actual, int minimum, string name)
    {
        if (actual < minimum)
        {
            throw new InvalidDataException(
                $"The 4DO {name} record is {actual} bytes; at least {minimum} are required.");
        }
    }

    private void WarnIfLarger(int actual, int known, string name)
    {
        if (actual > known)
        {
            _warnings.Add(
                $"The 4DO {name} record has {actual - known} trailing bytes beyond the known {known}; "
                + "they were ignored.");
        }
    }
}
