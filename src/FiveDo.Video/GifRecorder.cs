using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using FiveDo.Core;

namespace FiveDo.Video;

/// <summary>
/// Records emulated frames as an animated GIF using a fixed 3-3-2 palette.
/// </summary>
/// <remarks>
/// <para>
/// The recorder is an <see cref="IFrameSink"/>: attach it to the machine frame sink to
/// receive every emulated frame.
/// Frames are reduced to the requested GIF rate by an accumulator, so a 60 Hz source
/// recorded at 30 fps stores every second frame.
/// </para>
/// <para>
/// GIF has no audio. The fixed palette keeps encoding deterministic and allocation-free
/// per frame apart from the LZW dictionary; the global color table uses 3 bits of red,
/// 3 bits of green, and 2 bits of blue.
/// </para>
/// </remarks>
public sealed class GifRecorder : IFrameSink, IDisposable
{
    private const int PaletteSize = 256;
    private const int MinCodeSize = 8;

    private readonly Stream _output;
    private readonly double _sourceFramesPerSecond;
    private readonly double _captureFramesPerSecond;
    private readonly int _maxFrames;
    private readonly byte[] _palette = new byte[PaletteSize * 3];
    private FrameBuffer? _frameBuffer;
    private double _accumulator;
    private int _capturedFrames;
    private int _frameWidth;
    private int _frameHeight;
    private int _delayCentiseconds;
    private bool _completed;
    private bool _headerWritten;
    private int _skippedFrames;

    /// <summary>Initializes a recorder that writes to a stream.</summary>
    /// <param name="output">Destination stream. The recorder does not own it.</param>
    /// <param name="sourceFramesPerSecond">Emulated frame rate (50 or 60).</param>
    /// <param name="targetFramesPerSecond">Requested GIF rate, clamped to 1-50.</param>
    /// <param name="maxFrames">Maximum captured frames, or zero for unlimited.</param>
    public GifRecorder(
        Stream output,
        double sourceFramesPerSecond,
        double targetFramesPerSecond = 30,
        int maxFrames = 0)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite)
        {
            throw new ArgumentException("The output stream must be writable.", nameof(output));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sourceFramesPerSecond, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(maxFrames);
        _output = output;
        _sourceFramesPerSecond = sourceFramesPerSecond;
        _captureFramesPerSecond = Math.Clamp(
            Math.Min(targetFramesPerSecond, sourceFramesPerSecond),
            1,
            50);
        _maxFrames = maxFrames;
        _delayCentiseconds = Math.Max(2, (int)Math.Round(100.0 / _captureFramesPerSecond));
        BuildPalette();
    }

    /// <summary>Gets the number of frames written to the GIF.</summary>
    public int CapturedFrames => _capturedFrames;

    /// <summary>Gets the number of frames rejected because their dimensions changed.</summary>
    public int SkippedFrames => _skippedFrames;

    /// <summary>Gets the GIF width, or zero before the first frame.</summary>
    public int Width => _frameWidth;

    /// <summary>Gets the GIF height, or zero before the first frame.</summary>
    public int Height => _frameHeight;

    /// <summary>Gets the effective capture rate.</summary>
    public double CaptureFramesPerSecond => _captureFramesPerSecond;

    /// <inheritdoc />
    public void OnFrameTrigger()
    {
    }

    /// <inheritdoc />
    public void OnFrame(VdlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (_completed || (_maxFrames > 0 && _capturedFrames >= _maxFrames))
        {
            return;
        }

        _accumulator += _captureFramesPerSecond;
        if (_accumulator + 1e-9 < _sourceFramesPerSecond)
        {
            return;
        }

        _accumulator -= _sourceFramesPerSecond;
        Capture(frame);
    }

    /// <summary>Writes the GIF trailer and flushes the stream. Safe to call twice.</summary>
    public void Complete()
    {
        if (_completed)
        {
            return;
        }

        if (!_headerWritten)
        {
            WriteHeader(EmulatorConstants.VideoWidth, EmulatorConstants.VideoHeight);
        }

        _output.WriteByte(0x3B);
        _output.Flush();
        _completed = true;
    }

    /// <inheritdoc />
    public void Dispose() => Complete();

    private void Capture(VdlFrame frame)
    {
        int width = Math.Max(1, frame.SourceWidth);
        int height = Math.Max(1, frame.SourceHeight);
        if (!_headerWritten)
        {
            _frameWidth = width;
            _frameHeight = height;
            WriteHeader(width, height);
        }
        else if (width != _frameWidth || height != _frameHeight)
        {
            _skippedFrames++;
            return;
        }

        _frameBuffer ??= new FrameBuffer(width, height);
        FrameBuffer buffer = _frameBuffer;
        FrameAssembler.Assemble(frame, buffer, width, height);
        var indices = new byte[width * height];
        Span<byte> pixels = buffer.PixelSpan;
        for (var i = 0; i < indices.Length; i++)
        {
            int offset = i * 4;
            byte b = pixels[offset];
            byte g = pixels[offset + 1];
            byte r = pixels[offset + 2];
            indices[i] = (byte)(((r >> 5) << 5) | ((g >> 5) << 2) | (b >> 6));
        }

        WriteGraphicControl(_delayCentiseconds);
        WriteImageDescriptor(_output, width, height);
        EncodeLzw(indices, _output);
        _capturedFrames++;
    }

    private void WriteHeader(int width, int height)
    {
        _output.Write(Encoding.ASCII.GetBytes("GIF89a"));
        WriteUInt16(_output, (ushort)width);
        WriteUInt16(_output, (ushort)height);
        _output.WriteByte(0xF7); // global table, 8-bit color, 256 entries
        _output.WriteByte(0);
        _output.WriteByte(0);
        _output.Write(_palette);

        // NETSCAPE2.0 looping extension.
        _output.Write([0x21, 0xFF, 0x0B]);
        _output.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
        _output.Write([0x03, 0x01, 0x00, 0x00, 0x00]);
        _headerWritten = true;
    }

    private void WriteGraphicControl(int delayCentiseconds)
    {
        _output.Write([0x21, 0xF9, 0x04, 0x04]);
        WriteUInt16(_output, (ushort)delayCentiseconds);
        _output.WriteByte(0);
        _output.WriteByte(0);
    }

    private static void WriteImageDescriptor(Stream output, int width, int height)
    {
        output.WriteByte(0x2C);
        WriteUInt16(output, 0);
        WriteUInt16(output, 0);
        WriteUInt16(output, (ushort)width);
        WriteUInt16(output, (ushort)height);
        output.WriteByte(0);
    }

    private void BuildPalette()
    {
        for (var index = 0; index < PaletteSize; index++)
        {
            int r = (index >> 5) & 0x07;
            int g = (index >> 2) & 0x07;
            int b = index & 0x03;
            _palette[(index * 3) + 0] = (byte)((r * 255) / 7);
            _palette[(index * 3) + 1] = (byte)((g * 255) / 7);
            _palette[(index * 3) + 2] = (byte)((b * 255) / 3);
        }
    }

    private static void EncodeLzw(ReadOnlySpan<byte> indices, Stream output)
    {
        output.WriteByte(MinCodeSize);
        var dictionary = new Dictionary<int, int>(4096);
        int clearCode = 1 << MinCodeSize;
        int endCode = clearCode + 1;
        int nextCode = endCode + 1;
        int codeSize = MinCodeSize + 1;
        int bitBuffer = 0;
        int bitCount = 0;
        var blocks = new List<byte>(indices.Length / 2);

        WriteCode(clearCode);
        int prefix = indices[0];
        for (var i = 1; i < indices.Length; i++)
        {
            int suffix = indices[i];
            int key = (prefix << 8) | suffix;
            if (dictionary.TryGetValue(key, out int code))
            {
                prefix = code;
                continue;
            }

            WriteCode(prefix);
            if (nextCode < 4096)
            {
                dictionary[key] = nextCode++;
            }
            else
            {
                WriteCode(clearCode);
                dictionary.Clear();
                codeSize = MinCodeSize + 1;
                nextCode = endCode + 1;
            }

            prefix = suffix;
        }

        WriteCode(prefix);
        WriteCode(endCode);
        if (bitCount > 0)
        {
            blocks.Add((byte)bitBuffer);
        }

        const int maxBlockSize = 255;
        for (var offset = 0; offset < blocks.Count;)
        {
            int count = Math.Min(maxBlockSize, blocks.Count - offset);
            output.WriteByte((byte)count);
            output.Write(CollectionsMarshal.AsSpan(blocks).Slice(offset, count));
            offset += count;
        }

        output.WriteByte(0);
        return;

        void WriteCode(int code)
        {
            bitBuffer |= code << bitCount;
            bitCount += codeSize;
            while (bitCount >= 8)
            {
                blocks.Add((byte)bitBuffer);
                bitBuffer >>= 8;
                bitCount -= 8;
            }

            if (nextCode > (1 << codeSize) - 1 && codeSize < 12)
            {
                codeSize++;
            }
        }
    }

    private static void WriteUInt16(Stream output, ushort value)
    {
        output.WriteByte((byte)value);
        output.WriteByte((byte)(value >> 8));
    }
}
