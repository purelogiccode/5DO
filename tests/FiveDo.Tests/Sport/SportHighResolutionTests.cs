using FiveDo.Core;
using FiveDo.Memory;
using SportChip = FiveDo.Sport.Sport;

namespace FiveDo.Tests.Sport;

public sealed class SportHighResolutionTests
{
    private const uint VramOffset = 2 * 1024 * 1024;
    private const uint PlaneSize = EmulatorConstants.VramSize;

    private static SportChip CreateHighResolutionSport(FiveDoMemory memory)
    {
        return new SportChip(
            memory.Ram,
            new CompatibilityState
            {
                HighResolution = true,
                TrueHighResolution = true,
            });
    }

    [Fact]
    public void PresentationHighResolution_DoesNotMirrorSportPages()
    {
        var memory = new FiveDoMemory();
        var sport = new SportChip(
            memory.Ram,
            new CompatibilityState { HighResolution = true });

        sport.WriteAccess(0x2000, 0x11223344);
        sport.WriteAccess(0x4000, 0xffffffff);

        Assert.Equal(0x11223344u, memory.Ram.Read32(VramOffset));
        for (var plane = 1u; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
        {
            Assert.Equal(0u, memory.Ram.Read32(VramOffset + (plane * PlaneSize)));
        }
    }

    [Fact]
    public void FlashFill_MirrorsTheFilledPageIntoEveryPlane()
    {
        var memory = new FiveDoMemory();
        var sport = CreateHighResolutionSport(memory);

        sport.WriteAccess(0x2000, 0x11223344);
        sport.WriteAccess(0x4000, 0xffffffff);

        for (var plane = 0u; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
        {
            for (var i = 0u; i < 512; i++)
            {
                Assert.Equal(
                    0x11223344u,
                    memory.Ram.Read32(VramOffset + (plane * PlaneSize) + (i << 2)));
            }
        }
    }

    [Fact]
    public void MaskedFill_BlendsEachPlaneIndependently()
    {
        var memory = new FiveDoMemory();
        var sport = CreateHighResolutionSport(memory);
        for (var plane = 0u; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
        {
            memory.Ram.Write32(VramOffset + (plane * PlaneSize), 0xaaaaaaaa);
        }

        sport.WriteAccess(0x2000, 0x0000ffff);
        sport.WriteAccess(0x4000, 0x0000ffff);

        for (var plane = 0u; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
        {
            Assert.Equal(
                0x0000aaaau,
                memory.Ram.Read32(VramOffset + (plane * PlaneSize)));
        }
    }

    [Fact]
    public void PageCopy_CopiesEachSourcePlaneIntoTheMatchingDestinationPlane()
    {
        var memory = new FiveDoMemory();
        var sport = CreateHighResolutionSport(memory);
        for (var plane = 0u; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
        {
            for (var i = 0u; i < 512; i++)
            {
                memory.Ram.Write32(
                    VramOffset + (plane * PlaneSize) + ((512 + i) << 2),
                    (plane << 16) | (i + 1));
            }
        }

        sport.SetSource(4);
        sport.WriteAccess(8, 0xffffffff);

        for (var plane = 0u; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
        {
            for (var i = 0u; i < 512; i++)
            {
                Assert.Equal(
                    (plane << 16) | (i + 1),
                    memory.Ram.Read32(
                        VramOffset + (plane * PlaneSize) + ((1024 + i) << 2)));
            }
        }
    }

    [Fact]
    public void MaskedCopy_BlendsTheSourcePageIntoEachDestinationPlane()
    {
        var memory = new FiveDoMemory();
        var sport = CreateHighResolutionSport(memory);
        memory.Ram.Write32(VramOffset + (512 << 2), 0x0000ffff);
        for (var plane = 0u; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
        {
            memory.Ram.Write32(
                VramOffset + (plane * PlaneSize) + (1024 << 2),
                0xaaaaaaaa);
        }

        sport.SetSource(4);
        sport.WriteAccess(8, 0x0000ffff);

        for (var plane = 0u; plane < EmulatorConstants.HighResolutionVramPlanes; plane++)
        {
            Assert.Equal(
                0x0000aaaau,
                memory.Ram.Read32(VramOffset + (plane * PlaneSize) + (1024 << 2)));
        }
    }

    [Fact]
    public void MaskedFill_WithZeroMaskWritesTheColor()
    {
        // FreeDO's XOR blend treats a zero mask as "replace". Opera's
        // (current & ~mask) | (color & mask) keeps the current word. The FreeDO
        // behavior is the shipped default; lock it here so a future parity mode
        // has an explicit baseline.
        var memory = new FiveDoMemory();
        var sport = new SportChip(memory.Ram);
        memory.Ram.Write32(VramOffset, 0xaaaaaaaa);

        sport.WriteAccess(0x2000, 0x11223344);
        sport.WriteAccess(0x4000, 0);

        Assert.Equal(0x11223344u, memory.Ram.Read32(VramOffset));
    }

    [Fact]
    public void UnknownWriteOffset_IsIgnored()
    {
        var memory = new FiveDoMemory();
        var sport = new SportChip(memory.Ram);
        memory.Ram.Write32(VramOffset, 0xaaaaaaaa);

        sport.WriteAccess(0x6000, 0xffffffff);

        Assert.Equal(0xaaaaaaaau, memory.Ram.Read32(VramOffset));
        Assert.Equal(0u, sport.Color);
    }
}
