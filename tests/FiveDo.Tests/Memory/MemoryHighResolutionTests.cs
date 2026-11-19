using FiveDo.Core;
using FiveDo.Memory;

namespace FiveDo.Tests.Memory;

public sealed class MemoryHighResolutionTests
{
    private const uint VramOffset = 2 * 1024 * 1024;
    private const uint PlaneSize = EmulatorConstants.VramSize;

    [Fact]
    public void CpuWrite16_MirrorsVramIntoEveryHighResolutionPlane()
    {
        var compatibility = new CompatibilityState
        {
            HighResolution = true,
            TrueHighResolution = true,
        };
        var memory = new FiveDoMemory(compatibility: compatibility);

        memory.Write16(VramOffset + 0x100, 0x1234);

        for (var plane = 0u; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
        {
            Assert.Equal(
                0x1234,
                memory.Ram.Read16(VramOffset + (0x100 ^ 2) + (plane * PlaneSize)));
        }
    }

    [Fact]
    public void CpuWrite8_MirrorsVramIntoEveryHighResolutionPlane()
    {
        var compatibility = new CompatibilityState
        {
            HighResolution = true,
            TrueHighResolution = true,
        };
        var memory = new FiveDoMemory(compatibility: compatibility);

        memory.Write8(VramOffset + 0x101, 0xa5);

        for (var plane = 0u; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
        {
            Assert.Equal(
                0xa5,
                memory.Ram.Read8(VramOffset + (0x101 ^ 3) + (plane * PlaneSize)));
        }
    }

    [Fact]
    public void CpuWrite32_MirrorsVramIntoEveryHighResolutionPlane()
    {
        var compatibility = new CompatibilityState
        {
            HighResolution = true,
            TrueHighResolution = true,
        };
        var memory = new FiveDoMemory(compatibility: compatibility);

        memory.Write32(VramOffset + 0x200, 0x11223344);

        for (var plane = 0u; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
        {
            Assert.Equal(
                0x11223344u,
                memory.Ram.Read32(VramOffset + 0x200 + (plane * PlaneSize)));
        }
    }

    [Fact]
    public void CpuWrite_PresentationHighResolutionDoesNotTouchExtraPlanes()
    {
        var compatibility = new CompatibilityState { HighResolution = true };
        var memory = new FiveDoMemory(compatibility: compatibility);

        memory.Write32(VramOffset + 0x200, 0x11223344);

        Assert.Equal(0x11223344u, memory.Ram.Read32(VramOffset + 0x200));
        for (var plane = 1u; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
        {
            Assert.Equal(0u, memory.Ram.Read32(VramOffset + (plane * PlaneSize) + 0x200));
        }
    }

    [Fact]
    public void InternalRamWrite_IsNotMirrored()
    {
        var compatibility = new CompatibilityState
        {
            HighResolution = true,
            TrueHighResolution = true,
        };
        var memory = new FiveDoMemory(compatibility: compatibility);

        // CEL and SPORT write planes explicitly through IRam; mirroring those writes
        // would corrupt planes 2/3 with a second copy.
        memory.Ram.Write16(VramOffset + 0x300, 0x5678);

        Assert.Equal(0x5678, memory.Ram.Read16(VramOffset + 0x300));
        for (var plane = 1u; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
        {
            Assert.Equal(0u, memory.Ram.Read16(VramOffset + (plane * PlaneSize) + 0x300));
        }
    }
}
