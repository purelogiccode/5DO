using System.IO;
using FiveDo.Core;
using FiveDo.Machine;
using FiveDo.Memory;
using FiveDo.Video;

namespace FiveDo.Tests.Machine;

public sealed class VideoRegionTests
{
    private const uint VramOffset = 2 * 1024 * 1024;

    [Fact]
    public void Profiles_ExposeTheOperaGeometryAndTiming()
    {
        Assert.Equal(320, VideoRegionProfile.Ntsc.Width);
        Assert.Equal(240, VideoRegionProfile.Ntsc.Height);
        Assert.Equal(60, VideoRegionProfile.Ntsc.FieldRate);
        Assert.Equal(15_780, VideoRegionProfile.Ntsc.VdlClock);

        Assert.Equal(320, VideoRegionProfile.Pal1.Width);
        Assert.Equal(288, VideoRegionProfile.Pal1.Height);
        Assert.Equal(21, VideoRegionProfile.Pal1.StartScanline);
        Assert.Equal(50, VideoRegionProfile.Pal1.FieldRate);
        Assert.Equal(15_600, VideoRegionProfile.Pal1.VdlClock);
        Assert.Equal(624, VideoRegionProfile.Pal1.FrameLines);

        Assert.Equal(384, VideoRegionProfile.Pal2.Width);
        Assert.Equal(288, VideoRegionProfile.Pal2.Height);
        Assert.Equal(22, VideoRegionProfile.Pal2.StartScanline);
        Assert.True(VideoRegionProfile.Pal2.IsPal);
        Assert.Equal(250_000, VideoRegionProfile.Pal1.GetFrameCycles(EmulatorConstants.ArmClock));
        Assert.Equal(208_333, VideoRegionProfile.Ntsc.GetFrameCycles(EmulatorConstants.ArmClock));
    }

    [Fact]
    public void Quartz_UsesPalFieldLinesAndVdlClock()
    {
        var quartz = new Quartz(new EmulatorTuning { Region = VideoRegion.Pal1 });

        Assert.Equal(312, quartz.VdlFieldLines);
        Assert.Equal(624, quartz.VdlFrameLineCount);
        Assert.Equal(15_600, quartz.VdlEventClock);

        for (var i = 0; i < 4_000; i++)
        {
            quartz.PushArmCycles(250);
            while (quartz.QueueVdl())
            {
            }

            Assert.InRange(quartz.VdCurrOverline, 0, 623);
            Assert.InRange(quartz.VdCurrLine, 0, 311);
        }
    }

    [Fact]
    public void Vdlp_MapsPalLinesFromTheRegionStart()
    {
        var memory = new FiveDoMemory();
        var vdlp = new Vdlp(memory.Ram) { Region = VideoRegionProfile.Pal1 };
        var frame = new VdlFrame();
        const uint head = 0x1000;
        const uint bitmap = 0x8000;
        var header = 1u | (3u << 9) | (1u << 15) | (1u << 16) | (1u << 21);

        WriteVdl(memory, head, header, bitmap, bitmap, 0);
        WriteVdl(memory, head + 16, 0x01112233, 0xe0010203, 0xc001002c);

        vdlp.ProcessVdl(head);
        vdlp.DoLine(0, frame);
        vdlp.DoLine(20, frame);
        Assert.Equal(0u, frame[0].Background);
        vdlp.DoLine(21, frame);
        Assert.Equal(0x030201u, frame[0].Background);
        Assert.Equal(0xc001002cu, frame[0].OutControl);
        vdlp.DoLine(308, frame);
        Assert.Equal(0x030201u, frame[287].Background);
        Assert.Equal(0xc001002cu, frame[287].OutControl);
    }

    [Fact]
    public void Machine_PalRunsAtFiftyFieldsAndShows288Lines()
    {
        var machine = new FiveDoMachine(new EmulatorTuning { Region = VideoRegion.Pal1 });

        Assert.Equal(VideoRegion.Pal1, machine.RegionProfile.Region);
        Assert.Equal(50, machine.FramesPerSecond);
        machine.RunFrame();

        Assert.InRange(machine.LastFrameCycles, 250_000, 250_100);
        Assert.Equal(320, machine.Frame.SourceWidth);
        Assert.Equal(288, machine.Frame.SourceHeight);
    }

    [Fact]
    public void Machine_CanSwitchRegionBetweenFrames()
    {
        var machine = new FiveDoMachine();
        machine.RunFrame();
        Assert.Equal(VideoRegion.Ntsc, machine.RegionProfile.Region);

        machine.SetVideoRegion(VideoRegion.Pal1);
        machine.RunFrame();

        Assert.Equal(VideoRegion.Pal1, machine.RegionProfile.Region);
        Assert.Equal(50, machine.FramesPerSecond);
        Assert.InRange(machine.LastFrameCycles, 250_000, 250_100);
        Assert.Equal(288, machine.Frame.SourceHeight);

        machine.SetVideoRegion(VideoRegion.Ntsc);
        machine.RunFrame();

        Assert.Equal(VideoRegion.Ntsc, machine.RegionProfile.Region);
        Assert.InRange(machine.LastFrameCycles, 208_333, 208_400);
        Assert.Equal(240, machine.Frame.SourceHeight);
    }

    [Fact]
    public void StateWarnsWhenTheVideoRegionDiffers()
    {
        var pal = new FiveDoMachine(new EmulatorTuning { Region = VideoRegion.Pal1 });
        pal.RunFrame();
        using var state = new MemoryStream();
        pal.SaveState(state);

        var ntsc = new FiveDoMachine();
        state.Position = 0;
        ntsc.LoadState(state);

        Assert.Contains(
            ntsc.StateWarnings,
            warning => warning.Contains("video region", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Pal2UsesTheWiderProfile()
    {
        var machine = new FiveDoMachine(new EmulatorTuning { Region = VideoRegion.Pal2 });

        machine.RunFrame();

        Assert.Equal(384, machine.Frame.SourceWidth);
        Assert.Equal(288, machine.Frame.SourceHeight);
        Assert.Equal(576, VdlFrame.MaxLines);
    }

    private static void WriteVdl(FiveDoMemory memory, uint address, params uint[] words)
    {
        for (var i = 0; i < words.Length; i++)
        {
            memory.Ram.Write32(address + ((uint)i * 4) + VramOffset, words[i]);
        }
    }
}
