using System;
using ThreeDo.Core;

namespace ThreeDo.Sport;

/// <summary>
/// The SPORT VRAM fill/copy engine. FreeDO: <c>SPORT.cpp</c>.
/// </summary>
/// <remarks>
/// A SPORT write selects one of three operations by the address bits:
/// a 512-word flash fill (<c>0x4000-0x5FFF</c>), a 512-word page copy
/// (<c>0x0000-0x1FFF</c>), or a color-register write (<c>0x2000-0x3FFF</c>).
/// </remarks>
public sealed class Sport : ISportBus, IStateSection
{
    private const uint VramOffset = EmulatorConstants.VramBase;
    private const int VramPlaneSize = EmulatorConstants.VramSize;

    private readonly IRam _ram;
    private readonly CompatibilityState? _compatibility;

    private uint _color;
    private uint _source;
    private uint _destination;

    /// <summary>Initializes a new instance of the <see cref="Sport"/> class.</summary>
    /// <param name="ram">Raw VRAM accessor.</param>
    /// <param name="compatibility">Optional shared compatibility state for high-resolution mode.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ram"/> is null.</exception>
    public Sport(IRam ram, CompatibilityState? compatibility = null)
    {
        ArgumentNullException.ThrowIfNull(ram);
        _ram = ram;
        _compatibility = compatibility;
    }

    private bool HighResolution => _compatibility?.HighResolution == true;

    /// <summary>Gets the current fill color. FreeDO: <c>gSPORTCOLOR</c>.</summary>
    public uint Color => _color;

    /// <summary>Gets the current source page word index. FreeDO: <c>gSPORTSOURCE</c>.</summary>
    public uint Source => _source;

    /// <summary>Gets the current destination page word index. FreeDO: <c>gSPORTDESTINATION</c>.</summary>
    public uint Destination => _destination;

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Sport;

    /// <summary>Gets the save-state section version.</summary>
    public ushort Version => 1;

    /// <summary>Resets the SPORT registers.</summary>
    public void Reset()
    {
        _color = 0;
        _source = 0;
        _destination = 0;
    }

    /// <summary>Selects the source page. FreeDO: <c>_sport_SetSource</c>.</summary>
    /// <param name="offset">Byte offset within the SPORT window.</param>
    /// <returns>Always zero.</returns>
    public uint SetSource(uint offset)
    {
        _source = unchecked(offset << 7);
        return 0;
    }

    /// <summary>Performs a SPORT fill, copy, or color write. FreeDO: <c>_sport_WriteAccess</c>.</summary>
    /// <param name="offset">Byte offset within the SPORT window.</param>
    /// <param name="value">Value written by the CPU.</param>
    public void WriteAccess(uint offset, uint value)
    {
        if ((offset & ~0x1fffu) == 0x4000)
        {
            var page = (offset & 0x7ffu) << 7;
            if (value == 0xffffffff)
            {
                for (var i = 0u; i < 512; i++)
                {
                    _ram.Write32(VramOffset + ((page + i) << 2), _color);
                }

                if (HighResolution)
                {
                    for (var plane = 1; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
                    {
                        CopyPage(page, page, plane * VramPlaneSize);
                    }
                }
            }
            else
            {
                for (var i = 0u; i < 512; i++)
                {
                    var offsetBytes = VramOffset + ((page + i) << 2);
                    var current = _ram.Read32(offsetBytes);
                    _ram.Write32(offsetBytes, ((current ^ _color) & value) ^ _color);
                }

                if (HighResolution)
                {
                    for (var plane = 1; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
                    {
                        for (var i = 0u; i < 512; i++)
                        {
                            var offsetBytes = VramOffset + (uint)(plane * VramPlaneSize) + ((page + i) << 2);
                            var current = _ram.Read32(offsetBytes);
                            _ram.Write32(offsetBytes, ((current ^ _color) & value) ^ _color);
                        }
                    }
                }
            }

            return;
        }

        if ((offset & ~0x1fffu) == 0)
        {
            _destination = (offset & 0x7ffu) << 7;
            if (value == 0xffffffff)
            {
                CopyPage(_destination, _source, 0);
                if (HighResolution)
                {
                    for (var plane = 1; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
                    {
                        CopyPage(_destination, _destination, plane * VramPlaneSize);
                    }
                }
            }
            else
            {
                for (var i = 0u; i < 512; i++)
                {
                    var destinationBytes = VramOffset + ((_destination + i) << 2);
                    var sourceValue = _ram.Read32(VramOffset + ((_source + i) << 2));
                    var current = _ram.Read32(destinationBytes);
                    _ram.Write32(destinationBytes, ((current ^ sourceValue) & value) ^ sourceValue);
                }

                if (HighResolution)
                {
                    for (var plane = 1; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
                    {
                        for (var i = 0u; i < 512; i++)
                        {
                            var destinationBytes = VramOffset + (uint)(plane * VramPlaneSize) + ((_destination + i) << 2);
                            var sourceValue = _ram.Read32(VramOffset + ((_source + i) << 2));
                            var current = _ram.Read32(destinationBytes);
                            _ram.Write32(destinationBytes, ((current ^ sourceValue) & value) ^ sourceValue);
                        }
                    }
                }
            }

            return;
        }

        if ((offset & ~0x1fffu) == 0x2000)
        {
            _color = value;
        }
    }

    private void CopyPage(uint destinationPage, uint sourcePage, int destinationPlaneOffset)
    {
        for (var i = 0u; i < 512; i++)
        {
            var source = _ram.Read32(VramOffset + ((sourcePage + i) << 2));
            _ram.Write32(VramOffset + (uint)destinationPlaneOffset + ((destinationPage + i) << 2), source);
        }
    }

    /// <summary>Writes the SPORT save-state payload. FreeDO: <c>_sport_Save</c>.</summary>
    /// <param name="writer">Destination writer.</param>
    public void Save(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteUInt32(_color);
        writer.WriteUInt32(_source);
        writer.WriteUInt32(_destination);
    }

    /// <summary>Reads the SPORT save-state payload. FreeDO: <c>_sport_Load</c>.</summary>
    /// <param name="reader">Source reader.</param>
    public void Load(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _color = reader.ReadUInt32();
        _source = reader.ReadUInt32();
        _destination = reader.ReadUInt32();
    }
}
