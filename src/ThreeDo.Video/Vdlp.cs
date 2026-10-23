using System;
using ThreeDo.Core;

namespace ThreeDo.Video;

/// <summary>
/// The VDLP display-list processor. FreeDO: <c>vdlp.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// Parses the display list in VRAM and fills one <see cref="VdlLine"/> per scanline with raw
/// indexed pixels, the per-line CLUT, the output-control word, and the background color.
/// </para>
/// <para>
/// The reference reads VRAM through <c>_mem_read32((addr &amp; 0xFFFFF) + 2 MB)</c>; this
/// class does the same through <see cref="IRam"/>. High-resolution (<c>RESSCALE</c>) is not
/// part of M3 and is left for M11.
/// </para>
/// </remarks>
public sealed class Vdlp : IVdlp, IStateSection
{
    private const int VramOffset = 2 * 1024 * 1024;
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
    private uint _modulo = 320;
    private uint _currentVdl;
    private uint _currentBitmap;
    private uint _previousBitmap;
    private uint _outControl;
    private uint _clutDma;
    private int _lineDelay;

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

    /// <summary>Gets the current display-list head. FreeDO: <c>HEADVDL</c>.</summary>
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

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Vdlp;

    /// <summary>Gets the save-state section version.</summary>
    public ushort Version => 1;

    /// <summary>Resets the VDLP and writes the startup display list. FreeDO: <c>_vdl_Init</c>.</summary>
    public void Reset()
    {
        _background = 0;
        _headVdl = 0xb0000;
        _modulo = 320;
        _currentVdl = 0;
        _currentBitmap = 0;
        _previousBitmap = 0;
        _outControl = 0;
        _clutDma = 0;
        _lineDelay = 0;

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
            _lineDelay = 0;
            _currentVdl = _headVdl;
            ExecuteVdl();
        }

        var y = line - 16;
        if (_lineDelay == 0)
        {
            ExecuteVdl();
        }

        if (y is >= 0 and < 240)
        {
            var target = frame.Lines[y];
            if ((_clutDma & (1u << 21)) != 0)
            {
                var destination = target.PixelBuffer;
                var source = ((_previousBitmap ^ 2) & 0xfffff) + VramOffset;
                for (var i = 0; i < 320; i++)
                {
                    destination[i] = _ram.Read16(source);
                    source += 2;
                }

                target.Width = 320;
                _clutB.AsSpan().CopyTo(target.ClutB);
                _clutG.AsSpan().CopyTo(target.ClutG);
                _clutR.AsSpan().CopyTo(target.ClutR);
            }

            target.OutControl = _outControl;
            target.ClutDma = _clutDma;
            target.Background = _background;
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

    /// <summary>Writes the VDLP save-state payload. FreeDO: <c>_vdl_Save</c>.</summary>
    /// <param name="writer">Destination writer.</param>
    public void Save(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteBytes(_clutB);
        writer.WriteBytes(_clutG);
        writer.WriteBytes(_clutR);
        writer.WriteUInt32(_background);
        writer.WriteUInt32(_headVdl);
        writer.WriteUInt32(_modulo);
        writer.WriteUInt32(_currentVdl);
        writer.WriteUInt32(_currentBitmap);
        writer.WriteUInt32(_previousBitmap);
        writer.WriteUInt32(_outControl);
        writer.WriteUInt32(_clutDma);
        writer.WriteInt32(_lineDelay);
    }

    /// <summary>Reads the VDLP save-state payload. FreeDO: <c>_vdl_Load</c>.</summary>
    /// <param name="reader">Source reader.</param>
    public void Load(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        reader.ReadBytes(_clutB);
        reader.ReadBytes(_clutG);
        reader.ReadBytes(_clutR);
        _background = reader.ReadUInt32();
        _headVdl = reader.ReadUInt32();
        _modulo = reader.ReadUInt32();
        _currentVdl = reader.ReadUInt32();
        _currentBitmap = reader.ReadUInt32();
        _previousBitmap = reader.ReadUInt32();
        _outControl = reader.ReadUInt32();
        _clutDma = reader.ReadUInt32();
        _lineDelay = reader.ReadInt32();
    }

    private uint ReadVram(uint address) => _ram.Read32((address & 0xfffff) + VramOffset);

    private void ExecuteVdl()
    {
        var entry = ReadVram(_currentVdl);
        if (entry == 0)
        {
            _lineDelay = 511;
            return;
        }

        _clutDma = entry;

        if ((entry & (1u << 16)) != 0)
        {
            _currentBitmap = ReadVram(_currentVdl + ((_compatibility.FixMode & FixMode.BitTiming5) != 0 ? 8u : 4u));
        }

        if ((entry & (1u << 15)) != 0)
        {
            _previousBitmap = ReadVram(_currentVdl + ((_compatibility.FixMode & FixMode.BitTiming5) != 0 ? 4u : 8u));
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

                _background = ((command & 0xff) << 16) |
                    (command & 0xff00) |
                    ((command >> 16) & 0xff);
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

        _currentVdl = nextVdl;
        _modulo = PixelsPerLine[(entry >> 23) & 7];
        _lineDelay = (int)(entry & 0x1ff);
    }
}
