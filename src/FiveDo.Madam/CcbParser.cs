using System;
using FiveDo.Core;

namespace FiveDo.Madam;

/// <summary>
/// Parses a CEL control block header from VRAM. FreeDO: the CCB section of
/// <c>Madam.cpp: _madam_HandleCEL</c>.
/// </summary>
/// <remarks>
/// Pointers are masked to 24 bits and validated against the active RAM/VRAM window. Relative
/// pointers are added to the address of the pointer word plus four, matching the reference.
/// </remarks>
public sealed class CcbParser
{
    private const uint PointerMask = 0xffffff;

    private readonly IRam _ram;
    private readonly CelMemory? _celMemory;

    private uint MaximumVramAddress => (uint)(_ram.Layout.RamSize - 1);

    /// <summary>Initializes a new instance of the <see cref="CcbParser"/> class.</summary>
    /// <param name="ram">Raw RAM/VRAM accessor.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ram"/> is null.</exception>
    public CcbParser(IRam ram)
        : this(ram, null)
    {
    }

    /// <summary>
    /// Initializes a parser that routes header reads through CEL cycle accounting.
    /// FreeDO: <c>mread</c> inside <c>_madam_HandleCEL</c>.
    /// </summary>
    /// <param name="ram">Raw RAM/VRAM accessor.</param>
    /// <param name="celMemory">CEL memory accumulator used for cycle counts.</param>
    internal CcbParser(IRam ram, CelMemory? celMemory)
    {
        ArgumentNullException.ThrowIfNull(ram);
        _ram = ram;
        _celMemory = celMemory;
    }

    /// <summary>Parses one CCB header.</summary>
    /// <param name="address">CCB byte address in VRAM.</param>
    /// <returns>The parsed header.</returns>
    public CcbHeader Parse(uint address)
    {
        var header = new CcbHeader
        {
            Address = address,
            Flags = (CcbControl)ReadWord(address),
        };

        var current = address + 4;

        header.NextCcb = ReadWord(current) & ~3u;
        if ((header.Flags & CcbControl.NextPointerAbsolute) == CcbControl.None)
        {
            header.NextCcb = unchecked(header.NextCcb + current + 4) & PointerMask;
        }

        header.NextIsLast = header.NextCcb == 0 || header.NextCcb > MaximumVramAddress;
        current += 4;

        header.PData = ReadWord(current) & ~3u;
        if ((header.Flags & CcbControl.SourcePointerAbsolute) == CcbControl.None)
        {
            header.PData = unchecked(header.PData + current + 4) & PointerMask;
        }

        header.PDataInvalid = header.PData > MaximumVramAddress;
        current += 4;

        if ((header.Flags & CcbControl.LoadPlut) != CcbControl.None)
        {
            header.PlutData = ReadWord(current) & ~3u;
            if ((header.Flags & CcbControl.PlutPointerAbsolute) == CcbControl.None)
            {
                header.PlutData = unchecked(header.PlutData + current + 4) & PointerMask;
            }

            header.PlutDataInvalid = header.PlutData > MaximumVramAddress;
        }

        current += 4;

        if ((header.Flags & CcbControl.HasPosition) != CcbControl.None)
        {
            header.XPosition = unchecked((int)ReadWord(current));
            current += 4;
            header.YPosition = unchecked((int)ReadWord(current));
            current += 4;
        }
        else
        {
            current += 8;
        }

        if ((header.Flags & CcbControl.LoadSize) != CcbControl.None)
        {
            header.Hdx = unchecked((int)ReadWord(current)) >> 4;
            current += 4;
            header.Hdy = unchecked((int)ReadWord(current)) >> 4;
            current += 4;
            header.Vdx = unchecked((int)ReadWord(current));
            current += 4;
            header.Vdy = unchecked((int)ReadWord(current));
            current += 4;
        }

        if ((header.Flags & CcbControl.LoadPerspective) != CcbControl.None)
        {
            header.Hddx = unchecked((int)ReadWord(current)) >> 4;
            current += 4;
            header.Hddy = unchecked((int)ReadWord(current)) >> 4;
            current += 4;
        }

        if ((header.Flags & CcbControl.LoadPixelProcessor) != CcbControl.None)
        {
            header.PixelControl = ReadWord(current);
            current += 4;
        }

        if ((header.Flags & CcbControl.CcbPreamble) != CcbControl.None)
        {
            header.Pre0 = ReadWord(current);
            current += 4;
            if ((header.Flags & CcbControl.Packed) == CcbControl.None)
            {
                header.Pre1 = ReadWord(current);
                current += 4;
            }
        }
        else if (!header.PDataInvalid)
        {
            header.PDataCursor = header.PData;
            header.Pre0 = ReadWord(header.PDataCursor);
            header.PDataCursor += 4;
            if ((header.Flags & CcbControl.Packed) == CcbControl.None)
            {
                header.Pre1 = ReadWord(header.PDataCursor);
                header.PDataCursor += 4;
            }
        }

        header.BodyAddress = current;
        return header;
    }

    private uint ReadWord(uint address)
    {
        return _celMemory?.ReadWord(address) ?? _ram.Read32(address);
    }
}
