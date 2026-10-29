using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Diagnostics;
using FiveDo.Host;
using FiveDo.Machine;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Host;

public sealed class StateInspectorTests
{
    [Fact]
    public void Inspect_ReadsHeaderAndSections()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(3);
        using var stream = new MemoryStream();
        machine.SaveState(stream, "disc-hash");

        stream.Position = 0;
        StateInfo info = StateInspector.Read(stream);

        Assert.Equal(StateFormat.Version, info.Version);
        Assert.Equal(FiveDoVersion.Version, info.EmulatorVersion);
        Assert.Equal(EmulatorConstants.ArmClock, info.ArmClock);
        Assert.Equal("disc-hash", info.DiscHash);
        Assert.Equal(3, info.FramesExecuted);
        Assert.Contains(info.Sections, section => section.Id == StateSectionIds.Cpu);
        Assert.Contains(info.Sections, section => section.Id == StateSectionIds.Memory);
        Assert.Contains(info.Sections, section => section.Id == StateSectionIds.Dsp);
        Assert.Contains(info.Sections, section => section.Id == StateSectionIds.Cdrom);
        Assert.Contains(info.Sections, section => section.Id == StateSectionIds.Machine);
        Assert.All(info.Sections, section => Assert.True(section.PayloadLength > 0));
    }

    [Fact]
    public void ReadHeader_MatchesTheFullReader()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(3);
        using var stream = new MemoryStream();
        machine.SaveState(stream, "disc-hash");

        stream.Position = 0;
        StateInfo full = StateInspector.Read(stream);
        stream.Position = 0;
        StateInfo header = StateInspector.ReadHeader(stream);

        Assert.Equal(full.Version, header.Version);
        Assert.Equal(full.EmulatorVersion, header.EmulatorVersion);
        Assert.Equal(full.ArmClock, header.ArmClock);
        Assert.Equal(full.FixMode, header.FixMode);
        Assert.Equal(full.BiosHash, header.BiosHash);
        Assert.Equal(full.DiscHash, header.DiscHash);
        Assert.Equal(full.FramesExecuted, header.FramesExecuted);
        Assert.Equal(full.Sections, header.Sections);
    }

    [Fact]
    public void Inspect_ReportsCompressedSections()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(3);
        using var stream = new MemoryStream();
        machine.SaveState(stream);
        var bytes = stream.ToArray();

        StateInfo info = StateInspector.Read(new MemoryStream(bytes));

        Assert.True(info.Compressed);
        Assert.Equal(bytes.Length, info.StoredLength);
        Assert.Contains(
            info.Sections,
            section => section.Compressed && section.StoredLength < section.PayloadLength);
    }

    [Fact]
    public void Inspect_AcceptsVersionOneStates()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(3);
        using var stream = new MemoryStream();
        machine.SaveState(stream);
        var bytes = stream.ToArray();
        bytes[4] = 1;
        bytes[5] = 0;

        StateInfo info = StateInspector.Read(new MemoryStream(bytes));

        Assert.Equal(1, info.Version);
    }

    [Fact]
    public void ReadHeader_AcceptsVersionOneUncompressedState()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(3);
        using var stream = new MemoryStream();
        machine.SaveState(stream, compress: false);
        var bytes = stream.ToArray();
        bytes[4] = 1;
        bytes[5] = 0;

        StateInfo info = StateInspector.ReadHeader(new MemoryStream(bytes));

        Assert.Equal(1, info.Version);
        Assert.False(info.Compressed);
        Assert.All(info.Sections, section => Assert.False(section.Compressed));
    }

    [Fact]
    public void Inspect_RejectsInvalidFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-invalid-{Guid.NewGuid():N}.tdos");
        try
        {
            File.WriteAllBytes(path, new byte[64]);
            Assert.Throws<InvalidStateException>(() => StateInspector.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FrameDumper_WritesBmpHeader()
    {
        var frame = new FrameBuffer(4, 2);
        frame.PixelSpan[0] = 1;
        frame.PixelSpan[1] = 2;
        frame.PixelSpan[2] = 3;
        frame.PixelSpan[3] = 255;
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-frame-{Guid.NewGuid():N}.bmp");
        try
        {
            FrameDumper.WriteBmp(frame, path);
            var bytes = File.ReadAllBytes(path);
            Assert.Equal((byte)'B', bytes[0]);
            Assert.Equal((byte)'M', bytes[1]);
            Assert.Equal(14 + 40 + (4 * 2 * 4), bytes.Length);
            Assert.Equal(4, BitConverter.ToInt32(bytes, 18));
            Assert.Equal(2, BitConverter.ToInt32(bytes, 22));
            Assert.Equal(32, BitConverter.ToInt16(bytes, 28));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FrameDumper_WritesPackedBgraSpan()
    {
        var pixels = new byte[4 * 2 * 4];
        pixels[0] = 10;
        pixels[1] = 20;
        pixels[2] = 30;
        pixels[3] = 255;
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-frame-{Guid.NewGuid():N}.bmp");
        try
        {
            FrameDumper.WriteBmp(pixels, 4, 2, path);
            var bytes = File.ReadAllBytes(path);
            Assert.Equal((byte)'B', bytes[0]);
            Assert.Equal((byte)'M', bytes[1]);
            Assert.Equal(14 + 40 + (4 * 2 * 4), bytes.Length);
            Assert.Equal(4, BitConverter.ToInt32(bytes, 18));
            Assert.Equal(2, BitConverter.ToInt32(bytes, 22));
            Assert.Equal(10, bytes[54 + (4 * 4)]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PngDumper_WritesPngSignature()
    {
        var frame = new FrameBuffer(2, 2);
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-frame-{Guid.NewGuid():N}.png");
        try
        {
            PngDumper.Write(frame, path);
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4e, 0x47 }, bytes[..4]);
            Assert.Equal("IEND"u8.ToArray(), bytes[^8..^4]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static FiveDoMachine CreateMachine()
    {
        var program = new[]
        {
            ArmTestAssembler.Mov(0, 0x03300000),
            ArmTestAssembler.Mov(1, 0xb0000),
            ArmTestAssembler.StrImmediate(1, 0, 0x580),
            ArmTestAssembler.Branch(0x0300000c, 0x0300000c),
        };
        var rom = new byte[EmulatorConstants.RomBankSize];
        for (var i = 0; i < program.Length; i++)
        {
            var word = program[i];
            rom[i * 4] = (byte)(word >> 24);
            rom[(i * 4) + 1] = (byte)(word >> 16);
            rom[(i * 4) + 2] = (byte)(word >> 8);
            rom[(i * 4) + 3] = (byte)word;
        }

        var machine = new FiveDoMachine();
        machine.LoadBios(rom);
        machine.Reset();
        return machine;
    }
}
