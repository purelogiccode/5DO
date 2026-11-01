using System;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Madam;

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

    /// <summary>Loads the CEL origin from the XYPOSH/XYPOSL registers. Opera: <c>LoadXYPosFromRegisters</c>.</summary>
    internal void LoadXYPosFromRegisters()
    {
        var high = _madam.ReadRegister(0x140);
        var low = _madam.ReadRegister(0x144);
        _geometry.XPosition1616 = unchecked((int)((high & 0xffff0000u) | ((low >> 16) & 0xffffu)));
        _geometry.YPosition1616 = unchecked((int)(((high & 0xffffu) << 16) | (low & 0xffffu)));
    }

    /// <summary>Stores the CEL origin into XYPOSH/XYPOSL. Opera: <c>StoreXYPosToRegisters</c>.</summary>
    internal void StoreXYPosToRegisters(int x, int y)
    {
        var high = ((uint)x & 0xffff0000u) | (((uint)y >> 16) & 0xffffu);
        var low = (((uint)x & 0xffffu) << 16) | ((uint)y & 0xffffu);
        _madam.WriteRegister(0x140, high);
        _madam.WriteRegister(0x144, low);
        _geometry.XPosition1616 = x;
        _geometry.YPosition1616 = y;
    }

    /// <summary>
    /// Advances the CEL origin by the vertical delta and sprite height. Opera:
    /// <c>StoreXYPosFromDrawHeight</c>.
    /// </summary>
    internal void StoreXYPosFromDrawHeight(int x, int y, int vdx, int vdy, int height)
    {
        StoreXYPosToRegisters(
            unchecked(x + (vdx * height)),
            unchecked(y + (vdy * height)));
    }

    /// <summary>Gets or sets an optional diagnostic writer for CEL commands.</summary>
    internal TextWriter? TraceWriter { get; set; }

    /// <summary>
    /// Gets the pixels written by the most recent list while work tracking is enabled.
    /// Opera: <c>g_CEL_PIXELS_DRAWN</c>.
    /// </summary>
    internal long PixelsDrawn { get; private set; }

    /// <summary>
    /// Gets the source texels walked by the most recent list while work tracking is
    /// enabled. Opera: <c>g_CEL_TEXELS_WALKED</c>.
    /// </summary>
    internal long TexelsWalked { get; private set; }

    /// <summary>Gets the list entries processed by the most recent list. Opera: <c>g_CEL_LIST_CELS</c>.</summary>
    internal int CelsProcessed { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rasterizer should count work for the
    /// CEL busy estimate. The default FreeDO path leaves this off to keep its hot loops
    /// unchanged.
    /// </summary>
    internal bool TrackWork { get; set; }

    /// <summary>Counts rendered pixels toward the CEL busy estimate.</summary>
    /// <param name="count">Pixels written by a rasterizer span or loop.</param>
    internal void AddPixels(int count)
    {
        if (TrackWork && count > 0)
        {
            PixelsDrawn += count;
        }
    }

    /// <summary>Counts one source texel walked by an arbitrary-quad CEL.</summary>
    internal void AddTexel()
    {
        if (TrackWork)
        {
            TexelsWalked++;
        }
    }

    private long _celNumber;
    private uint _currentCcb;
    private int _arbitraryTraceCount;

    /// <summary>Writes a CEL progress marker to the diagnostic trace, when enabled.</summary>
    internal void TraceProgress(long texels, int row, int column)
    {
        TraceWriter?.WriteLine(
            $"cel {_celNumber - 1} progress texels={texels} row={row} col={column}");
    }

    /// <summary>Writes the first few arbitrary-quad inputs of the current CEL.</summary>
    internal void TraceArbitrary(int xA, int yA, int xB, int yB, int xC, int yC, int xD, int yD)
    {
        if (TraceWriter is null || _arbitraryTraceCount++ >= 8)
        {
            return;
        }

        TraceWriter.WriteLine(
            $"cel {_celNumber - 1} arb A=({xA >> 16},{yA >> 16}) B=({xB >> 16},{yB >> 16}) "
            + $"C=({xC >> 16},{yC >> 16}) D=({xD >> 16},{yD >> 16}) raw="
            + $"({xA:x8},{yA:x8},{xB:x8},{yB:x8},{xC:x8},{yC:x8},{xD:x8},{yD:x8})");
    }

    /// <summary>Writes one CEL command to the diagnostic trace, when enabled.</summary>
    internal void TraceCel(string kind, int width, int height, int bpp)
    {
        if (TraceWriter is null)
        {
            return;
        }

        CelGeometry g = _geometry;
        _arbitraryTraceCount = 0;
        TraceWriter.WriteLine(
            $"cel {_celNumber++} {kind} ccb={_currentCcb:x8} next={_nextCcb:x8} flags={(uint)_flags:x8} "
            + $"pre0={_pre0:x8} pre1={_pre1:x8} data={_dataAddress:x8} bpp={bpp} "
            + $"size={width}x{height} pos=({g.XPosition1616:x8},{g.YPosition1616:x8}) "
            + $"h=({g.Hdx1616:x8},{g.Hdy1616:x8}) v=({g.Vdx1616:x8},{g.Vdy1616:x8}) "
            + $"hd=({g.Hddx1616:x8},{g.Hddy1616:x8}) clip=({ClipX},{ClipY}) "
            + $"src={PixelSource:x8} dst={FrameBufferTarget:x8}");
        if ((long)width * height > 100_000)
        {
            var words = new System.Text.StringBuilder();
            for (var offset = 0; offset < 64; offset += 4)
            {
                words.Append($"{_memory.ReadWord(unchecked(_currentCcb + (uint)offset)):x8} ");
            }

            TraceWriter.WriteLine($"cel {_celNumber - 1} ccbdata={words}");
        }
    }

    /// <summary>
    /// Runs every queued CCB until the list ends. FreeDO: <c>_madam_HandleCEL</c>.
    /// </summary>
    /// <returns>The number of CEL cycles consumed.</returns>
    internal int HandleCel()
    {
        // FreeDO: __smallcicles=CELCYCLES=0.
        _ = _memory.TakeCycles();
        PixelsDrawn = 0;
        TexelsWalked = 0;
        CelsProcessed = 0;

        _nextCcb = _madam.ReadRegister(NextCcbRegister);
        _madam.WriteRegister(CcbStatus, _madam.ReadRegister(CcbStatus) | 0x10u);

        var stop = false;
        while (_nextCcb != 0 && !stop)
        {
            CelsProcessed++;
            _currentCcb = _nextCcb;
            CcbHeader header = _parser.Parse(_nextCcb & 0xfffffcu);
            _flags = header.Flags;
            _nextCcb = header.NextCcb;

            if (header.NextIsLast)
            {
                _flags |= CcbControl.Last;
            }

            if ((_flags & CcbControl.Last) != CcbControl.None)
            {
                stop = true;
                _nextCcb = 0;
            }

            if ((_flags & CcbControl.HasPosition) != CcbControl.None)
            {
                // Opera: YOXY set means the header supplies a new origin. When
                // it is clear, the CEL continues from the hardware XY position
                // registers, which the previous CEL advanced by VDX/VDY * height.
                StoreXYPosToRegisters(header.XPosition, header.YPosition);
            }
            else
            {
                LoadXYPosFromRegisters();
            }

            // FreeDO: CEL_ORIGIN_VH_VALUE = (XPOS1616 & 1) | ((YPOS1616 & 1) << 15).
            _celOriginVh = ((uint)_geometry.XPosition1616 & 1u)
                | (((uint)_geometry.YPosition1616 & 1u) << 15);

            if ((_flags & CcbControl.LoadSize) != CcbControl.None)
            {
                _geometry.Hdx1616 = header.Hdx;
                _geometry.Hdy1616 = header.Hdy;
                _geometry.Vdx1616 = header.Vdx;
                _geometry.Vdy1616 = header.Vdy;
            }

            if ((_flags & CcbControl.LoadPerspective) != CcbControl.None)
            {
                _geometry.Hddx1616 = header.Hddx;
                _geometry.Hddy1616 = header.Hddy;
            }

            if ((_flags & CcbControl.LoadPixelProcessor) != CcbControl.None)
            {
                _pixc = header.PixelControl;
            }

            _pre0 = header.Pre0;
            _pre1 = header.Pre1;

            var bppIndex = (int)(_pre0 & 0x7u);

            // Opera: BPP 0 is part of the 1-bpp low-depth group, not a no-op.
            // Only BPP 7 remains reserved.
            if (bppIndex == 7)
            {
                continue;
            }

            if ((_flags & CcbControl.LoadPlut) != CcbControl.None && !header.PlutDataInvalid)
            {
                var count = bppIndex switch
                {
                    0 or 1 => 2,
                    2 => 4,
                    3 => 16,
                    _ => 32,
                };
                _madam.LoadPlut(header.PlutData, count);
            }

            if ((_flags & CcbControl.Skip) != CcbControl.None || header.PDataInvalid)
            {
                continue;
            }

            _dataAddress = (_flags & CcbControl.CcbPreamble) != CcbControl.None
                ? header.PData
                : header.PDataCursor;

            _geometry.Flags = _flags;
            _geometry.ClipX = ClipX;
            _geometry.ClipY = ClipY;

            var started = TraceWriter is null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
            if ((_flags & CcbControl.Packed) != CcbControl.None)
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

            if (TraceWriter is not null)
            {
                TimeSpan elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);
                TraceWriter.WriteLine(
                    $"cel {_celNumber - 1} done ms={elapsed.TotalMilliseconds:F3}");
            }
        }

        _madam.WriteRegister(NextCcbRegister, _nextCcb);
        return _memory.TakeCycles();
    }
}
