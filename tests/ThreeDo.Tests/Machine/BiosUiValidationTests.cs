using System;
using System.IO;
using ThreeDo.Core;
using ThreeDo.Machine;
using ThreeDo.Tests.TestUtilities;
using ThreeDo.Video;
using Xunit.Abstractions;

namespace ThreeDo.Tests.Machine;

/// <summary>
/// Optional real-BIOS validation for the M4/M5 exit criteria. Set <c>THREEDO_BIOS</c> to a
/// 1 MB BIOS image to enable these tests; set <c>THREEDO_FRAME_DUMP</c> to a directory to write
/// PNG snapshots for manual comparison with 4DO.
/// </summary>
/// <remarks>
/// The CD-ROM/XBUS device is attached by default, so the BIOS passes its device poll and runs
/// its boot animation. The interactive menu still depends on a valid game disc, so this test
/// asserts the boot frame and DSP startup rather than menu contents.
/// </remarks>
public sealed class BiosUiValidationTests
{
    private readonly ITestOutputHelper _output;

    public BiosUiValidationTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void RealBios_RendersStableBootFrameWhenProvided()
    {
        var path = GetBiosPath();
        if (path is null)
        {
            return;
        }

        var first = RunBios(path);
        var second = RunBios(path);

        Assert.True(first.NonBlackPixels > 0, "The BIOS produced an empty frame.");
        Assert.Equal(first.Hash, second.Hash);
        Assert.True(first.DistinctColors > 16, $"The BIOS frame has only {first.DistinctColors} distinct colors.");

        _output.WriteLine($"frame hash={first.Hash:x8} distinct={first.DistinctColors} pc={first.ProgramCounter:x8}");

        var dumpDirectory = Environment.GetEnvironmentVariable("THREEDO_FRAME_DUMP");
        if (!string.IsNullOrWhiteSpace(dumpDirectory))
        {
            Directory.CreateDirectory(dumpDirectory);
            PngWriter.Write(first.Frame, Path.Combine(dumpDirectory, "bios-boot.png"));
        }
    }

    [Fact]
    public void RealBios_StartsDspAndProducesSamplesWhenProvided()
    {
        var path = GetBiosPath();
        if (path is null)
        {
            return;
        }

        var machine = new ThreeDoMachine();
        var sink = new CountingAudioSink();
        machine.AudioSink = sink;
        machine.LoadBios(File.ReadAllBytes(path));
        machine.Reset();
        machine.RunFrames(1200);

        Assert.True(sink.Samples > 0, "The DSP produced no samples.");
        Assert.True(machine.Dsp.IsRunning, "The BIOS did not start the DSP.");

        _output.WriteLine($"dsp samples={sink.Samples} nonzero={sink.NonZero} pc={machine.Cpu.ProgramCounter:x8}");
    }

    private static string? GetBiosPath()
    {
        var path = Environment.GetEnvironmentVariable("THREEDO_BIOS");
        return string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? null : path;
    }

    private static BiosFrame RunBios(string path)
    {
        var machine = new ThreeDoMachine();
        machine.LoadBios(File.ReadAllBytes(path));
        machine.Reset();
        machine.RunFrames(120);

        var frame = new FrameBuffer(EmulatorConstants.VideoWidth, EmulatorConstants.VideoHeight);
        FrameAssembler.Assemble(machine.Frame, frame, frame.Width, frame.Height);

        var hash = 2166136261u;
        var nonBlack = 0;
        var colors = new System.Collections.Generic.HashSet<uint>();
        for (var i = 0; i < frame.PixelSpan.Length; i += 4)
        {
            var b = frame.PixelSpan[i];
            var g = frame.PixelSpan[i + 1];
            var r = frame.PixelSpan[i + 2];
            hash = unchecked((hash ^ b) * 16777619);
            hash = unchecked((hash ^ g) * 16777619);
            hash = unchecked((hash ^ r) * 16777619);
            var color = (uint)((r << 16) | (g << 8) | b);
            colors.Add(color);
            if (color != 0)
            {
                nonBlack++;
            }
        }

        return new BiosFrame(hash, nonBlack, colors.Count, machine.Cpu.ProgramCounter, frame);
    }

    private sealed record BiosFrame(uint Hash, int NonBlackPixels, int DistinctColors, uint ProgramCounter, FrameBuffer Frame);

    private sealed class CountingAudioSink : IAudioSink
    {
        public long Samples { get; private set; }

        public long NonZero { get; private set; }

        public void PushSample(uint packedSample)
        {
            Samples++;
            if (packedSample != 0)
            {
                NonZero++;
            }
        }
    }
}
