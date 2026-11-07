using System;
using FiveDo.Core;

namespace FiveDo.Video;

/// <summary>
/// The VDLP display-list processor. FreeDO: <c>vdlp.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// Parses the display list in VRAM and fills one <see cref="VdlLine"/> per scanline with raw
/// indexed pixels, the per-line CLUT, the output-control word, and the background color.
/// </para>
/// <para>
/// The reference reads VRAM through <c>_mem_read32((addr &amp; VRAM_SIZE_MASK) + VRAM)</c>;
/// this class does the same through <see cref="IRam"/>, using the active
/// <see cref="MemoryLayout"/> so expanded DRAM/VRAM layouts decode correctly.
/// </para>
/// </remarks>
public sealed class Vdlp : IVdlp, IStateSection
{
    private uint VramOffset => (uint)_ram.Layout.VramBase;
    private uint VramPlaneSize => (uint)_ram.Layout.VramSize;
    private uint VramMask => _ram.Layout.VramSizeMask;
    private const uint VdlControl = 0x80000000;
    private const uint VdlBackground = 0xe0000000;
    private const uint VdlRgbControlMask = 0x60000000;
    private const uint VdlPenMask = 0x1f000000;
    private const uint VdlRedMask = 0x00ff0000;
    private const uint VdlGreenMask = 0x0000ff00;
    private const uint VdlBlueMask = 0x000000ff;
    private const int VdlPenShift = 24;
    private const int VdlRedShift = 16;
    private const int VdlGreenShift = 8;

    private static readonly uint[] PixelsPerLine = { 320, 384, 512, 640, 1024, 320, 320, 320 };

    private static readonly uint[] StartupVdl =
    {
        0x00004410, 0x002c0000, 0x002c0000, 0x002b0098,
        0x00000000, 0x01080808, 0x02101010, 0x03191919,
        0x04212121, 0x05292929, 0x06313131, 0x073a3a3a,
        0x08424242, 0x094a4a4a, 0x0a525252, 0x0b5a5a5a,
        0x0c636363, 0x0d6b6b6b, 0x0e737373, 0x0f7b7b7b,
        0x10848484, 0x118c8c8c, 0x12949494, 0x139c9c9c,
        0x14a5a5a5, 0x15adadad, 0x16b5b5b5, 0x17bdbdbd,
        0x18c5c5c5, 0x19cecece, 0x1ad6d6d6, 0x1bdedede,
        0x1ce6e6e6, 0x1defefef, 0x1ef8f8f8, 0x1fffffff,
        0xe0010101, 0xc001002c, 0x002180ef, 0x002c0000,
        0x002c0000, 0x002b00a8, 0x00000000, 0x002c0000,
        0x002c0000, 0x002b0000,
    };

    private readonly IRam _ram;
    private readonly CompatibilityState _compatibility;
    private readonly byte[] _clutB = new byte[VdlLine.ClutSize];
    private readonly byte[] _clutG = new byte[VdlLine.ClutSize];
    private readonly byte[] _clutR = new byte[VdlLine.ClutSize];

    private uint _background;
    private uint _headVdl;
    private uint _nextVdl;
    private uint _modulo = 320;
    private uint _currentVdl;
    private uint _currentBitmap;
    private uint _previousBitmap;
    private uint _outControl;
    private uint _clutDma;
    private VideoRegionProfile _region = VideoRegionProfile.Ntsc;
    private int _lineDelay;
    private int _persistLength;
    private int _forceLine;

    /// <summary>Initializes a new instance of the <see cref="Vdlp"/> class.</summary>
    /// <param name="ram">Raw VRAM accessor.</param>
    /// <param name="compatibility">Compatibility flags. Defaults to a new instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ram"/> is null.</exception>
    public Vdlp(IRam ram, CompatibilityState? compatibility = null)
    {
        ArgumentNullException.ThrowIfNull(ram);
        _ram = ram;
        _compatibility = compatibility ?? new CompatibilityState();
        Reset();
    }

    /// <summary>Gets or sets the active video region profile.</summary>
    public VideoRegionProfile Region
    {
        get => _region;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _region = value;
        }
    }

    /// <summary>Gets the retained display-list head. FreeDO: <c>HEADVDL</c>; Opera: <c>head_vdl</c>.</summary>
    public uint HeadVdl => _headVdl;

    /// <summary>Gets the current display-list pointer. FreeDO: <c>CURRENTVDL</c>.</summary>
    public uint CurrentVdl => _currentVdl;

    /// <summary>Gets the current bitmap pointer. FreeDO: <c>CURRENTBMP</c>.</summary>
    public uint CurrentBitmap => _currentBitmap;

    /// <summary>Gets the previous bitmap pointer. FreeDO: <c>PREVIOUSBMP</c>.</summary>
    public uint PreviousBitmap => _previousBitmap;

    /// <summary>Gets the current line width. FreeDO: <c>MODULO</c>.</summary>
    public uint Modulo => _modulo;

    /// <summary>Gets the output-control word. FreeDO: <c>OUTCONTROLL</c>.</summary>
    public uint OutControl => _outControl;

    /// <summary>Gets the background color word. FreeDO: <c>BACKGROUND</c>.</summary>
    public uint Background => _background;

    /// <summary>Gets the raw CLUT DMA word. FreeDO: <c>CLUTDMA.raw</c>.</summary>
    public uint ClutDma => _clutDma;

    /// <summary>Gets the current line delay. FreeDO: <c>linedelay</c>.</summary>
    public int LineDelay => _lineDelay;

    /// <summary>Gets or sets the MADAM CLUT DMA enable source. Opera: <c>madam_clut_dma_enabled</c>.</summary>
    public Func<bool>? ClutDmaEnabled { get; set; }

    /// <summary>Gets or sets the MADAM video DMA enable source. Opera: <c>madam_video_dma_enabled</c>.</summary>
    public Func<bool>? VideoDmaEnabled { get; set; }

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Vdlp;

    /// <summary>Gets the save-state section version.</summary>
    /// <remarks>Version 3 separates the retained head from the active successor cursor.</remarks>
    public ushort Version => 3;

    /// <summary>Resets the VDLP and writes the startup display list. FreeDO: <c>_vdl_Init</c>.</summary>
    public void Reset()
    {
        _background = 0;
        _headVdl = 0xb0000;
        _nextVdl = 0xb0000;
        _modulo = 320;
        _currentVdl = 0;
        _currentBitmap = 0;
        _previousBitmap = 0;
        _outControl = 0;
        _clutDma = 0;
        _lineDelay = 0;
        _persistLength = 0;
        _forceLine = 0;

        for (var i = 0u; i < (uint)StartupVdl.Length; i++)
        {
            _ram.Write32(_headVdl + (i * 4) + VramOffset, StartupVdl[i]);
        }

        for (var i = 0; i < VdlLine.ClutSize; i++)
        {
            var value = (byte)(((i & 0x1f) << 3) | ((i >> 2) & 7));
            _clutB[i] = value;
            _clutG[i] = value;
            _clutR[i] = value;
        }
    }

    /// <summary>Sets the display-list head. FreeDO: <c>_vdl_ProcessVDL</c>.</summary>
    /// <param name="address">VDL address within VRAM.</param>
    public void ProcessVdl(uint address)
    {
        // Opera: retain the CPU-programmed head; the current field keeps
        // traversing its existing display list until the next force line.
        _headVdl = address;
    }

    /// <summary>Renders one scanline. FreeDO: <c>_vdl_DoLineNew</c>.</summary>
    /// <param name="line2X">Absolute VDL line counter.</param>
    /// <param name="frame">Destination frame.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="frame"/> is null.</exception>
    public void DoLine(int line2X, VdlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var line = line2X & 0x7ff;
        if (line == 0)
        {
            // A VDL programmed during the previous field becomes visible here.
            // Opera latches this boundary at scanline 5 because its visible area
            // starts at scanline 21. 5DO still starts visible output at scanline
            // 16, so keep the boundary at zero until OP-SYS-05 aligns the region
            // timing; starting at five here would shift the boot display.
            _lineDelay = 0;
            _forceLine = 0;
        }

        if (ClutDmaEnabled?.Invoke() ?? true)
        {
            if (line == _forceLine)
            {
                // Every field restarts from the retained head. The entry itself
                // is processed even when the previous field left a delay.
                _nextVdl = _headVdl;
                ExecuteVdl();
            }

            // When the current entry's persist count is exhausted, the successor
            // is processed on this same line. This second call is what keeps a
            // zero-persist head from shifting the whole field by a line.
            if (_lineDelay <= 0)
            {
                ExecuteVdl();
            }
        }

        var y = line - _region.StartScanline;

        if (y >= 0 && y < _region.Height)
        {
            if (_compatibility.HighResolution)
            {
                RenderHighResolutionLine(frame, y);
            }
            else
            {
                VdlLine target = frame.Lines[y];
                var videoDmaEnabled = (_clutDma & (1u << 21)) != 0
                    && (VideoDmaEnabled?.Invoke() ?? true);
                if (videoDmaEnabled)
                {
                    var lineWidth = (int)Math.Min(_modulo, VdlLine.MaxPixels);
                    if (lineWidth <= 0)
                    {
                        lineWidth = EmulatorConstants.VideoWidth;
                    }

                    Span<ushort> destination = target.PixelBuffer;
                    var source = ((_previousBitmap ^ 2) & VramMask) + VramOffset;

                    // A 3DO scanline stores one 16-bit pixel in each 32-bit VRAM word.
                    // The two halves of a word belong to an adjacent-line pair, so the
                    // source advances four bytes per pixel. Reading halfwords back to
                    // back mixes two scanlines and halves the horizontal resolution.
                    ReadOnlySpan<uint> words = _ram.Words;
                    var lane = (int)(source & 3);
                    if (!words.IsEmpty && lane <= 2)
                    {
                        var wordIndex = (int)(source >> 2);
                        var shift = lane * 8;
                        for (var i = 0; i < lineWidth; i++)
                        {
                            destination[i] = (ushort)(words[wordIndex++] >> shift);
                        }
                    }
                    else
                    {
                        for (var i = 0; i < lineWidth; i++)
                        {
                            destination[i] = _ram.Read16(source);
                            source += 4;
                        }
                    }

                    target.Width = lineWidth;
                    _clutB.AsSpan().CopyTo(target.ClutB);
                    _clutG.AsSpan().CopyTo(target.ClutG);
                    _clutR.AsSpan().CopyTo(target.ClutR);
                }

                target.OutControl = _outControl;
                target.ClutDma = _clutDma;
                target.Background = _background;
                if (!videoDmaEnabled)
                {
                    // Opera: a disabled bitmap DMA renders a black line. The old
                    // pixels and background must not persist from the previous field.
                    target.Pixels.Clear();
                    target.Background = 0;
                }
            }
        }

        if ((_currentBitmap & 2) != 0)
        {
            _currentBitmap = unchecked(_currentBitmap + (_modulo * 4) - 2);
        }
        else
        {
            _currentBitmap = unchecked(_currentBitmap + 2);
        }

        if ((_clutDma & (1u << 17)) == 0)
        {
            _previousBitmap = _currentBitmap;
        }
        else if ((_previousBitmap & 2) != 0)
        {
            _previousBitmap = unchecked(_previousBitmap + (_modulo * 4) - 2);
        }
        else
        {
            _previousBitmap = unchecked(_previousBitmap + 2);
        }

        _lineDelay--;
        _outControl &= ~1u;
    }

    /// <summary>
    /// Renders one low-resolution VDL line as two high-resolution lines.
    /// FreeDO: <c>_vdl_DoLineNew</c> with <c>RESSCALE</c> set.
    /// </summary>
    /// <remarks>
    /// The 3DO stores a 640x480 frame in four VRAM planes sized by the active VRAM capacity. Each low-resolution
    /// bitmap line supplies the even/odd columns of two adjacent high-resolution
    /// scanlines: planes 0/1 form the even line and planes 2/3 form the odd line.
    /// </remarks>
    private void RenderHighResolutionLine(VdlFrame frame, int y)
    {
        VdlLine target0 = frame.Lines[y * 2];
        VdlLine target1 = frame.Lines[(y * 2) + 1];

        var videoDmaEnabled = (_clutDma & (1u << 21)) != 0
            && (VideoDmaEnabled?.Invoke() ?? true);
        if (videoDmaEnabled)
        {
            var lineWidth = (int)Math.Min(_modulo, VdlLine.MaxPixels / 2);
            if (lineWidth <= 0)
            {
                lineWidth = EmulatorConstants.VideoWidth;
            }

            Span<ushort> destination0 = target0.PixelBuffer;
            Span<ushort> destination1 = target1.PixelBuffer;
            var source = ((_previousBitmap ^ 2) & VramMask) + VramOffset;
            ReadOnlySpan<uint> words = _ram.Words;
            var lane = (int)(source & 3);
            if (!words.IsEmpty && lane <= 2)
            {
                var wordIndex = (int)(source >> 2);
                var shift = lane * 8;
                var planeWords = (int)(VramPlaneSize >> 2);
                for (var i = 0; i < lineWidth; i++)
                {
                    var x = i * 2;
                    destination0[x] = (ushort)(words[wordIndex] >> shift);
                    destination0[x + 1] = (ushort)(words[wordIndex + planeWords] >> shift);
                    destination1[x] = (ushort)(words[wordIndex + (2 * planeWords)] >> shift);
                    destination1[x + 1] = (ushort)(words[wordIndex + (3 * planeWords)] >> shift);
                    wordIndex++;
                }
            }
            else
            {
                for (var i = 0; i < lineWidth; i++)
                {
                    var x = i * 2;
                    destination0[x] = _ram.Read16(source);
                    destination0[x + 1] = _ram.Read16(source + VramPlaneSize);
                    destination1[x] = _ram.Read16(source + (2 * VramPlaneSize));
                    destination1[x + 1] = _ram.Read16(source + (3 * VramPlaneSize));
                    source += 4;
                }
            }

            var width = lineWidth * 2;
            target0.Width = width;
            target1.Width = width;
            _clutB.AsSpan().CopyTo(target0.ClutB);
            _clutG.AsSpan().CopyTo(target0.ClutG);
            _clutR.AsSpan().CopyTo(target0.ClutR);
            _clutB.AsSpan().CopyTo(target1.ClutB);
            _clutG.AsSpan().CopyTo(target1.ClutG);
            _clutR.AsSpan().CopyTo(target1.ClutR);
        }

        target0.OutControl = _outControl;
        target0.ClutDma = _clutDma;
        target0.Background = _background;
        target1.OutControl = _outControl;
        target1.ClutDma = _clutDma;
        target1.Background = _background;

        if (!videoDmaEnabled)
        {
            target0.Pixels.Clear();
            target1.Pixels.Clear();
            target0.Background = 0;
            target1.Background = 0;
        }
    }

    /// <summary>Writes the VDLP save-state payload. FreeDO: <c>_vdl_Save</c>.</summary>
    /// <param name="writer">Destination writer.</param>
    public void Save(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteBytes(_clutB);
        writer.WriteBytes(_clutG);
        writer.WriteBytes(_clutR);
        writer.WriteUInt32(_background);
        writer.WriteInt32(_forceLine);
        writer.WriteUInt32(_currentVdl);
        writer.WriteUInt32(_headVdl);
        writer.WriteUInt32(_nextVdl);
        writer.WriteUInt32(_modulo);
        writer.WriteUInt32(_currentBitmap);
        writer.WriteUInt32(_previousBitmap);
        writer.WriteUInt32(_outControl);
        writer.WriteUInt32(_clutDma);
        writer.WriteInt32(_lineDelay);
        writer.WriteInt32(_persistLength);
    }

    /// <summary>Reads the VDLP save-state payload. FreeDO: <c>_vdl_Load</c>.</summary>
    /// <param name="reader">Source reader.</param>
    public void Load(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        reader.ReadBytes(_clutB);
        reader.ReadBytes(_clutG);
        reader.ReadBytes(_clutR);

        if (reader is StateSectionReader { Version: 1 })
        {
            // Version 1 stored the CPU-programmed head separately from the active
            // cursor. Opera's migration restarts traversal from the active cursor;
            // the old head may not be the entry the display was actually using.
            _background = reader.ReadUInt32();
            _ = reader.ReadUInt32(); // old head_vdl
            _modulo = reader.ReadUInt32();
            _currentVdl = reader.ReadUInt32();
            _currentBitmap = reader.ReadUInt32();
            _previousBitmap = reader.ReadUInt32();
            _outControl = reader.ReadUInt32();
            _clutDma = reader.ReadUInt32();
            _lineDelay = reader.ReadInt32();
            _headVdl = _currentVdl;
            _nextVdl = _currentVdl;
            _forceLine = 0;
            _persistLength = _lineDelay > 0 ? _lineDelay : 0;
            return;
        }

        if (reader is StateSectionReader { Version: 2 })
        {
            // Version 2 kept the retained head in the successor slot; the active
            // cursor was already past the entry body. Resume from the successor.
            _background = reader.ReadUInt32();
            _ = reader.ReadInt32(); // old force_line
            _currentVdl = reader.ReadUInt32();
            var nextVdl = reader.ReadUInt32();
            _modulo = reader.ReadUInt32();
            _currentBitmap = reader.ReadUInt32();
            _previousBitmap = reader.ReadUInt32();
            _outControl = reader.ReadUInt32();
            _clutDma = reader.ReadUInt32();
            _lineDelay = reader.ReadInt32();
            _persistLength = reader.ReadInt32();
            _headVdl = nextVdl;
            _nextVdl = nextVdl;
            _forceLine = 0;
            return;
        }

        _background = reader.ReadUInt32();
        _ = reader.ReadInt32(); // saved force_line; normalized to the current cadence
        _forceLine = 0;
        _currentVdl = reader.ReadUInt32();
        _headVdl = reader.ReadUInt32();
        _nextVdl = reader.ReadUInt32();
        _modulo = reader.ReadUInt32();
        _currentBitmap = reader.ReadUInt32();
        _previousBitmap = reader.ReadUInt32();
        _outControl = reader.ReadUInt32();
        _clutDma = reader.ReadUInt32();
        _lineDelay = reader.ReadInt32();
        _persistLength = reader.ReadInt32();
    }

    private uint ReadVram(uint address)
    {
        return _ram.Read32((address & VramMask) + VramOffset);
    }

    private void ExecuteVdl()
    {
        // Opera: a zero entry is still a valid display-list word. It may point at
        // a successor through its next-pointer field, so do not treat zero as a
        // 511-line stop. This is the Johnny Bazookatone horizontal-line fix.
        if (_nextVdl == 0)
        {
            return;
        }

        _currentVdl = _nextVdl;
        var entry = ReadVram(_currentVdl);
        _clutDma = entry;

        if ((entry & (1u << 16)) != 0)
        {
            _currentBitmap = ReadVram(_currentVdl + ((_compatibility.FixMode & FixMode.BitTiming5) != FixMode.None ? 8u : 4u));
        }

        if ((entry & (1u << 15)) != 0)
        {
            _previousBitmap = ReadVram(_currentVdl + ((_compatibility.FixMode & FixMode.BitTiming5) != FixMode.None ? 4u : 8u));
        }

        uint nextVdl;
        if ((entry & (1u << 18)) != 0)
        {
            nextVdl = unchecked(_currentVdl + ReadVram(_currentVdl + 12) + 16);
        }
        else
        {
            nextVdl = ReadVram(_currentVdl + 12);
        }

        _currentVdl += 16;

        var commandCount = (int)((entry >> 9) & 0x3f);
        var ignoreCommands = false;
        for (var i = 0; i < commandCount; i++)
        {
            var command = ReadVram(_currentVdl);
            _currentVdl += 4;

            if ((command & VdlControl) == 0)
            {
                var colorIndex = (int)((command & VdlPenMask) >> VdlPenShift);
                switch (command & VdlRgbControlMask)
                {
                    case 0x00000000:
                        _clutR[colorIndex] = (byte)((command & VdlRedMask) >> VdlRedShift);
                        _clutG[colorIndex] = (byte)((command & VdlGreenMask) >> VdlGreenShift);
                        _clutB[colorIndex] = (byte)(command & VdlBlueMask);
                        break;
                    case 0x60000000:
                        _clutR[colorIndex] = (byte)((command & VdlRedMask) >> VdlRedShift);
                        break;
                    case 0x40000000:
                        _clutG[colorIndex] = (byte)((command & VdlGreenMask) >> VdlGreenShift);
                        break;
                    case 0x20000000:
                        _clutB[colorIndex] = (byte)(command & VdlBlueMask);
                        break;
                }
            }
            else if ((command & 0xff000000) == VdlBackground)
            {
                if (ignoreCommands)
                {
                    continue;
                }

                // Opera: background_to_0RGB1555 extracts 8-bit r/g/b (bits 23-16,
                // 15-8, 7-0) and narrows each to 5 bits. The old code byte-swapped
                // the word and treated it as an 8-bit-per-channel value, which
                // swapped red/blue and corrupted the green channel.
                var red = (command >> 16) & 0xff;
                var green = (command >> 8) & 0xff;
                var blue = command & 0xff;
                _background = ((red >> 3) << 10) | ((green >> 3) << 5) | (blue >> 3);
            }
            else if ((command & 0xe0000000) == 0xc0000000)
            {
                if (ignoreCommands)
                {
                    continue;
                }

                _outControl = command;
                ignoreCommands = (_outControl & 2) != 0;
            }
            else if (command == 0xffffffff)
            {
                if (ignoreCommands)
                {
                    continue;
                }

                for (var j = 0; j < VdlLine.ClutSize; j++)
                {
                    var value = (byte)(((j & 0x1f) << 3) | ((j >> 2) & 7));
                    _clutB[j] = value;
                    _clutG[j] = value;
                    _clutR[j] = value;
                }
            }
        }

        _nextVdl = nextVdl;
        _modulo = PixelsPerLine[(entry >> 23) & 7];
        _persistLength = (int)(entry & 0x1ff);
        _lineDelay = _persistLength;
    }
}
