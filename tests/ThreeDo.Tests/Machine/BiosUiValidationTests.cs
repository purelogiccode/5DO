using System;
using System.IO;
using ThreeDo.Core;
using ThreeDo.Machine;
using ThreeDo.Tests.TestUtilities;
using ThreeDo.Video;

namespace ThreeDo.Tests.Machine;

/// <summary>
/// Optional real-BIOS visual validation for M4.15. Set <c>THREEDO_BIOS</c> to a 1 MB BIOS
/// image to enable it; set <c>THREEDO_FRAME_DUMP</c> to a directory to write PNG snapshots
/// for manual comparison with 4DO.
/// </summary>
/// <remarks>
/// The BIOS boot splash renders before the CD/XBUS device exists. The interactive BIOS menu
/// additionally requires the M6 CD-ROM device, so this test deliberately asserts only that
/// the boot frame is stable and non-empty.
/// </remarks>
public sealed class BiosUiValidationTests
{
    [Fact]
    public void RealBios_RendersStableBootFrameWhenProvided()
    {
        var path = Environment.GetEnvironmentVariable("THREEDO_BIOS");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var first = RunBios(path);
        var second = RunBios(path);

        Assert.True(first.NonBlackPixels > 0, "The BIOS produced an empty frame.");
        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal(first.Width, second.Width);

        var dumpDirectory = Environment.GetEnvironmentVariable("THREEDO_FRAME_DUMP");
        if (!string.IsNullOrWhiteSpace(dumpDirectory))
        {
            Directory.CreateDirectory(dumpDirectory);
            PngWriter.Write(first.Frame, Path.Combine(dumpDirectory, "bios-boot.png"));
        }
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
        for (var i = 0; i < frame.PixelSpan.Length; i += 4)
        {
            var b = frame.PixelSpan[i];
            var g = frame.PixelSpan[i + 1];
            var r = frame.PixelSpan[i + 2];
            hash = unchecked((hash ^ b) * 16777619);
            hash = unchecked((hash ^ g) * 16777619);
            hash = unchecked((hash ^ r) * 16777619);
            if ((b | g | r) != 0)
            {
                nonBlack++;
            }
        }

        return new BiosFrame(hash, nonBlack, frame.Width, frame);
    }

    private sealed record BiosFrame(uint Hash, int NonBlackPixels, int Width, FrameBuffer Frame);
}
