using System;
using ThreeDo.Core;

namespace ThreeDo.Madam;

/// <summary>
/// Parses a CEL control block header from VRAM. FreeDO: the CCB section of
/// <c>Madam.cpp: _madam_HandleCEL</c>.
/// </summary>
/// <remarks>
/// Pointers are masked to 24 bits and validated against the 3 MB VRAM window. Relative
/// pointers are added to the address of the pointer word plus four, matching the reference.
/// </remarks>
public sealed class CcbParser
{
    private const uint PointerMask = 0xffffff;
    private const uint MaximumVramAddress = 0x2fffff;

    private readonly IRam _ram;

    /// <summary>Initializes a new instance of the <see cref="CcbParser"/> class.</summary>
    /// <param name="ram">Raw RAM/VRAM accessor.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ram"/> is null.</exception>
    public CcbParser(IRam ram)
    {
        ArgumentNullException.ThrowIfNull(ram);
        _ram = ram;
    }

    /// <summary>Parses one CCB header.</summary>
    /// <param name="address">CCB byte address in VRAM.</param>
    /// <returns>The parsed header.</returns>
    public CcbHeader Parse(uint address)
    {
        var header = new CcbHeader
        {
            Address = address,
            Flags = (CcbControl)_ram.Read32(address),
        };

        var current = address + 4;

        header.NextCcb = _ram.Read32(current) & ~3u;
        if ((header.Flags & CcbControl.NextPointerAbsolute) == 0)
        {
            header.NextCcb = unchecked(header.NextCcb + current + 4) & PointerMask;
        }

        header.NextIsLast = header.NextCcb == 0 || header.NextCcb > MaximumVramAddress;
        current += 4;

        header.PData = _ram.Read32(current) & ~3u;
        if ((header.Flags & CcbControl.SourcePointerAbsolute) == 0)
        {
            header.PData = unchecked(header.PData + current + 4) & PointerMask;
        }

        header.PDataInvalid = header.PData > MaximumVramAddress;
        current += 4;

        if ((header.Flags & CcbControl.LoadPlut) != 0)
        {
            header.PlutData = _ram.Read32(current) & ~3u;
            if ((header.Flags & CcbControl.PlutPointerAbsolute) == 0)
            {
                header.PlutData = unchecked(header.PlutData + current + 4) & PointerMask;
            }

            header.PlutDataInvalid = header.PlutData > MaximumVramAddress;
        }

        current += 4;

        if ((header.Flags & CcbControl.CcbPreamble) != 0)
        {
            header.Pre0 = _ram.Read32(current);
            current += 4;
            if ((header.Flags & CcbControl.Packed) == 0)
            {
                header.Pre1 = _ram.Read32(current);
                current += 4;
            }
        }
        else if (!header.PDataInvalid)
        {
            header.PDataCursor = header.PData;
            header.Pre0 = _ram.Read32(header.PDataCursor);
            header.PDataCursor += 4;
            if ((header.Flags & CcbControl.Packed) == 0)
            {
                header.Pre1 = _ram.Read32(header.PDataCursor);
                header.PDataCursor += 4;
            }
        }

        header.BodyAddress = current;
        return header;
    }
}
