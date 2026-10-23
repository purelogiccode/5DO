using System;
using System.IO;
using ThreeDo.Core;
using ThreeDo.Diagnostics;
using ThreeDo.Host;
using ThreeDo.Machine;
using ThreeDo.Tests.TestUtilities;
using ThreeDo.Video;

namespace ThreeDo.Tests.HostIo;

public sealed class StateInspectorTests
{
    [Fact]
    public void Inspect_ReadsHeaderAndSections()
    {
        var machine = CreateMachine();
        machine.RunFrames(3);
        using var stream = new MemoryStream();
        machine.SaveState(stream, "disc-hash");

        stream.Position = 0;
        var info = StateInspector.Read(stream);

        Assert.Equal(StateFormat.Version, info.Version);
        Assert.Equal(ThreeDoVersion.Version, info.EmulatorVersion);
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
    public void Inspect_RejectsInvalidFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"threedo-invalid-{Guid.NewGuid():N}.tdos");
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
        var path = Path.Combine(Path.GetTempPath(), $"threedo-frame-{Guid.NewGuid():N}.bmp");
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

    private static ThreeDoMachine CreateMachine()
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

        var machine = new ThreeDoMachine();
        machine.LoadBios(rom);
        machine.Reset();
        return machine;
    }
}
