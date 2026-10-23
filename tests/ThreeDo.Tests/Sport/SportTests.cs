using ThreeDo.Core;
using ThreeDo.Memory;
using SportChip = ThreeDo.Sport.Sport;

namespace ThreeDo.Tests.Sport;

public sealed class SportTests
{
    private const uint VramOffset = 2 * 1024 * 1024;

    [Fact]
    public void FlashFill_WritesAllWords()
    {
        var memory = new ThreeDoMemory();
        var sport = new SportChip(memory.Ram);

        sport.WriteAccess(0x2000, 0x11223344);
        sport.WriteAccess(0x4000, 0xffffffff);

        for (var i = 0u; i < 512; i++)
        {
            Assert.Equal(0x11223344u, memory.Ram.Read32(VramOffset + (i << 2)));
        }
    }

    [Fact]
    public void FlashFill_MaskedBlendsWords()
    {
        var memory = new ThreeDoMemory();
        var sport = new SportChip(memory.Ram);

        for (var i = 0u; i < 512; i++)
        {
            memory.Ram.Write32(VramOffset + (i << 2), 0xaaaaaaaa);
        }

        sport.WriteAccess(0x2000, 0xffffffff);
        sport.WriteAccess(0x4000, 0x0000ffff);

        Assert.Equal(0xffffaaaau, memory.Ram.Read32(VramOffset));
    }

    [Fact]
    public void PageCopy_Copies512Words()
    {
        var memory = new ThreeDoMemory();
        var sport = new SportChip(memory.Ram);

        for (var i = 0u; i < 512; i++)
        {
            memory.Ram.Write32(VramOffset + ((512 + i) << 2), i + 1);
        }

        sport.SetSource(4);
        sport.WriteAccess(8, 0xffffffff);

        for (var i = 0u; i < 512; i++)
        {
            Assert.Equal(i + 1, memory.Ram.Read32(VramOffset + ((1024 + i) << 2)));
        }
    }

    [Fact]
    public void PageCopy_MaskedBlendsWords()
    {
        var memory = new ThreeDoMemory();
        var sport = new SportChip(memory.Ram);
        memory.Ram.Write32(VramOffset + (512 << 2), 0xffffffff);
        memory.Ram.Write32(VramOffset + (1024 << 2), 0xaaaaaaaa);

        sport.SetSource(4);
        sport.WriteAccess(8, 0x0000ffff);

        Assert.Equal(0xffffaaaau, memory.Ram.Read32(VramOffset + (1024 << 2)));
    }

    [Fact]
    public void SaveLoad_RoundtripsRegisters()
    {
        var memory = new ThreeDoMemory();
        var sport = new SportChip(memory.Ram);
        sport.WriteAccess(0x2000, 0x12345678);
        sport.SetSource(7);
        sport.WriteAccess(9, 0xffffffff);

        var writer = new StateWriter();
        sport.Save(writer);
        var copy = new SportChip(memory.Ram);
        copy.Load(new StateReader(writer.ToArray()));

        Assert.Equal(sport.Color, copy.Color);
        Assert.Equal(sport.Source, copy.Source);
        Assert.Equal(sport.Destination, copy.Destination);
    }
}
