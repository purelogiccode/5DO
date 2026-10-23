using System;
using ThreeDo.Core;

namespace ThreeDo.Madam;

/// <summary>
/// The CEL control-block walker and rasterizer dispatcher. FreeDO: <c>Madam.cpp: _madam_HandleCEL</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps the parsed CCB fields, the geometry, the bit reader, and the pixel
/// pipeline in file-scope globals. This class owns one copy of that state so multiple
/// machines and tests remain independent.
/// </para>
/// <para>
/// Register writes use the raw MADAM register file rather than <see cref="Madam.Poke"/> so
/// the CEL engine does not trigger the CPU-facing side effects of the sprite registers.
/// </para>
/// </remarks>
internal sealed class CelEngine
{
    private const uint CcbStatus = 0x28;
    private const uint CcbControl0 = 0x110;
    private const uint ClipRegister = 0x134;
    private const uint PixelSourceRegister = 0x138;
    private const uint FrameBufferTargetRegister = 0x13c;
    private const uint NextCcbRegister = 0x5a4;

    private static readonly int[] BppTable = { 1, 1, 2, 4, 6, 8, 16, 1 };

    private readonly Madam _madam;
    private readonly IRam _ram;
    private readonly CelMemory _memory;
    private readonly CelBitReader _bitReader;
    private readonly CcbParser _parser;
    private readonly PixelDecoder _decoder = new();
    private readonly CelGeometry _geometry = new();
    private readonly CelRasterizer _rasterizer;

    private uint _nextCcb;
    private uint _pre0;
    private uint _pre1;
    private uint _pixc;
    private CcbControl _flags;
    private uint _celOriginVh;
    private uint _dataAddress;

    internal CelEngine(Madam madam, IRam ram)
    {
        ArgumentNullException.ThrowIfNull(madam);
        ArgumentNullException.ThrowIfNull(ram);

        _madam = madam;
        _ram = ram;
        _memory = new CelMemory(ram);
        _bitReader = new CelBitReader(ram);
        _parser = new CcbParser(ram, _memory);
        _rasterizer = new CelRasterizer(this);
    }

    internal Madam Madam => _madam;

    internal IRam Ram => _ram;

    internal CelMemory Memory => _memory;

    internal CelBitReader BitReader => _bitReader;

    internal PixelDecoder Decoder => _decoder;

    internal CelGeometry Geometry => _geometry;

    internal CompatibilityState Compatibility => _madam.Compatibility;

    internal uint Pre0 => _pre0;

    internal uint Pre1 => _pre1;

    internal uint PixelControl => _pixc;

    internal CcbControl Flags => _flags;

    internal uint CelOriginVh => _celOriginVh;

    internal uint DataAddress => _dataAddress;

    internal uint Control0 => _madam.ReadRegister(CcbControl0);

    internal uint PixelSource => _madam.ReadRegister(PixelSourceRegister);

    internal uint FrameBufferTarget => _madam.ReadRegister(FrameBufferTargetRegister);

    internal int ReadModulo => _madam.ReadModulo;

    internal int WriteModulo => _madam.WriteModulo;

    internal int ClipX => (int)(_madam.ReadRegister(ClipRegister) & 0x3ff);

    internal int ClipY => (int)((_madam.ReadRegister(ClipRegister) >> 16) & 0x3ff);

    /// <summary>
    /// Runs every queued CCB until the list ends. FreeDO: <c>_madam_HandleCEL</c>.
    /// </summary>
    /// <returns>The number of CEL cycles consumed.</returns>
    internal int HandleCel()
    {
        // FreeDO: __smallcicles=CELCYCLES=0.
        _ = _memory.TakeCycles();

        _nextCcb = _madam.ReadRegister(NextCcbRegister);
        _madam.WriteRegister(CcbStatus, _madam.ReadRegister(CcbStatus) | 0x10u);

        var stop = false;
        while (_nextCcb != 0 && !stop)
        {
            var header = _parser.Parse(_nextCcb & 0xfffffcu);
            _flags = header.Flags;
            _nextCcb = header.NextCcb;

            if (header.NextIsLast)
            {
                _flags |= CcbControl.Last;
            }

            if ((_flags & CcbControl.Last) != 0)
            {
                stop = true;
                _nextCcb = 0;
            }

            var current = header.BodyAddress;
            if ((_flags & CcbControl.HasPosition) != 0)
            {
                _geometry.XPosition1616 = unchecked((int)ReadWord(ref current));
                _geometry.YPosition1616 = unchecked((int)ReadWord(ref current));
            }
            else
            {
                current += 8;
            }

            // FreeDO: CEL_ORIGIN_VH_VALUE = (XPOS1616 & 1) | ((YPOS1616 & 1) << 15).
            _celOriginVh = ((uint)_geometry.XPosition1616 & 1u)
                | (((uint)_geometry.YPosition1616 & 1u) << 15);

            if ((_flags & CcbControl.LoadSize) != 0)
            {
                _geometry.Hdx1616 = unchecked((int)ReadWord(ref current)) >> 4;
                _geometry.Hdy1616 = unchecked((int)ReadWord(ref current)) >> 4;
                _geometry.Vdx1616 = unchecked((int)ReadWord(ref current));
                _geometry.Vdy1616 = unchecked((int)ReadWord(ref current));
            }

            if ((_flags & CcbControl.LoadPerspective) != 0)
            {
                _geometry.Hddx1616 = unchecked((int)ReadWord(ref current)) >> 4;
                _geometry.Hddy1616 = unchecked((int)ReadWord(ref current)) >> 4;
            }

            if ((_flags & CcbControl.LoadPixelProcessor) != 0)
            {
                _pixc = ReadWord(ref current);
            }

            _pre0 = header.Pre0;
            _pre1 = header.Pre1;

            var bppIndex = (int)(_pre0 & 0x7u);
            if (bppIndex is 0 or 7)
            {
                continue;
            }

            if ((_flags & CcbControl.LoadPlut) != 0 && !header.PlutDataInvalid)
            {
                var count = bppIndex switch
                {
                    1 => 2,
                    2 => 4,
                    3 => 16,
                    _ => 32,
                };
                _madam.LoadPlut(header.PlutData, count);
            }

            if ((_flags & CcbControl.Skip) != 0 || header.PDataInvalid)
            {
                continue;
            }

            _dataAddress = (_flags & CcbControl.CcbPreamble) != 0
                ? header.PData
                : header.PDataCursor;

            _geometry.Flags = _flags;
            _geometry.ClipX = ClipX;
            _geometry.ClipY = ClipY;

            if ((_flags & CcbControl.Packed) != 0)
            {
                _rasterizer.DrawPackedCel();
            }
            else if ((_pre1 & (uint)CcbPreamble1.LrForm) != 0
                && BppTable[bppIndex] == 16)
            {
                _rasterizer.DrawLrCel();
            }
            else
            {
                _rasterizer.DrawLiteralCel();
            }
        }

        _madam.WriteRegister(NextCcbRegister, _nextCcb);
        _madam.Fsm = CelFsmState.Idle;
        return _memory.TakeCycles();
    }

    private uint ReadWord(ref uint address)
    {
        var value = _memory.ReadWord(address);
        address += 4;
        return value;
    }
}
