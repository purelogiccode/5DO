using System;
using System.Buffers.Binary;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>
/// A 32 KB 3DO NVRAM image with fresh-volume-header initialization.
/// </summary>
/// <remarks>
/// FreeDO maps one NVRAM byte per 32-bit word, so the backing store is exactly 32 KB.
/// The fresh image reproduces 4DO's <c>EmulationHelper.InitializeNvram</c>, including the
/// big-endian volume-header fields and the initial directory words.
/// </remarks>
public sealed class NvramImage
{
    private const int VolumeHeaderSize = 132;

    private readonly byte[] _data;

    private NvramImage(byte[] data)
    {
        _data = data;
    }

    /// <summary>Gets the NVRAM bytes.</summary>
    public ReadOnlyMemory<byte> Data => _data;

    /// <summary>Creates a fresh NVRAM image with an initialized volume header.</summary>
    /// <returns>A new 32 KB image.</returns>
    public static NvramImage CreateFresh()
    {
        var data = new byte[EmulatorConstants.NvramSize];
        InitializeVolumeHeader(data);
        return new NvramImage(data);
    }

    /// <summary>Loads an NVRAM image from a file.</summary>
    /// <param name="path">Path to a 32 KB image.</param>
    /// <returns>The validated image.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is empty.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not exactly 32 KB.</exception>
    public static NvramImage Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return FromBytes(File.ReadAllBytes(path));
    }

    /// <summary>Validates a 32 KB NVRAM image.</summary>
    /// <param name="data">NVRAM bytes.</param>
    /// <returns>The validated image.</returns>
    /// <exception cref="InvalidDataException">Thrown when the size is not exactly 32 KB.</exception>
    public static NvramImage FromBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length != EmulatorConstants.NvramSize)
        {
            throw new InvalidDataException(
                $"NVRAM must be exactly {EmulatorConstants.NvramSize} bytes; got {data.Length}.");
        }

        return new NvramImage(data.ToArray());
    }

    /// <summary>Copies the NVRAM bytes into a destination span.</summary>
    /// <param name="destination">Destination of at least 32 KB.</param>
    public void CopyTo(Span<byte> destination)
    {
        _data.CopyTo(destination);
    }

    /// <summary>Saves the image to a file.</summary>
    /// <param name="path">Destination path.</param>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllBytes(path, _data);
    }

    /// <summary>Returns a copy of the NVRAM bytes.</summary>
    /// <returns>A 32 KB array.</returns>
    public byte[] ToArray()
    {
        return (byte[])_data.Clone();
    }

    private static void InitializeVolumeHeader(byte[] data)
    {
        data[0] = 0x01;
        data[1] = (byte)'Z';
        data[2] = (byte)'Z';
        data[3] = (byte)'Z';
        data[4] = (byte)'Z';
        data[5] = (byte)'Z';
        data[6] = 0x02;
        data[7] = 0x00;

        var label = "nvram"u8;
        label.CopyTo(data.AsSpan(40));

        WriteBigEndian(data, 72, 0xffffffff);
        WriteBigEndian(data, 76, 0x00000001);
        WriteBigEndian(data, 80, 0x00008000);
        WriteBigEndian(data, 84, 0xfffffffe);
        WriteBigEndian(data, 88, 0x00000000);
        WriteBigEndian(data, 92, 0x00000001);
        WriteBigEndian(data, 96, 0x00000000);
        WriteBigEndian(data, 100, 0x00000084);
        for (var i = 1; i < 8; i++)
        {
            WriteBigEndian(data, 100 + (i * 4), 0);
        }

        var offset = VolumeHeaderSize;
        offset = WriteBigEndian(data, offset, 0x855a02b6);
        offset = WriteBigEndian(data, offset, 0x00000098);
        offset = WriteBigEndian(data, offset, 0x00000098);
        offset = WriteBigEndian(data, offset, 0x00000014);
        offset = WriteBigEndian(data, offset, 0x00000014);
        offset = WriteBigEndian(data, offset, 0x7aa565bd);
        offset = WriteBigEndian(data, offset, 0x00000084);
        offset = WriteBigEndian(data, offset, 0x00000084);
        offset = WriteBigEndian(data, offset, 0x00007668);
        WriteBigEndian(data, offset, 0x00000014);
    }

    private static int WriteBigEndian(byte[] data, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset, 4), value);
        return offset + 4;
    }
}
