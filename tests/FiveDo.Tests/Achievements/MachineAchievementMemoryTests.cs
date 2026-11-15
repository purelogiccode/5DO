using FiveDo.Host;
using FiveDo.Memory;

namespace FiveDo.Tests.Achievements;

public sealed class MachineAchievementMemoryTests
{
    [Fact]
    public void ExposesTheRawOperaDramByteOrder()
    {
        var machineMemory = new FiveDoMemory();
        machineMemory.Write32(0, 0x11223344);
        var memory = new MachineAchievementMemory(machineMemory);

        // Opera stores a 32-bit word in host order and the CPU's big-endian byte 0
        // lives at DRAM[3]. RetroArch exposes DRAM as-is, so byte 0 is the LSB.
        Assert.Equal(0x44u, memory.Peek(0, 1));
        Assert.Equal(0x33u, memory.Peek(1, 1));
        Assert.Equal(0x22u, memory.Peek(2, 1));
        Assert.Equal(0x11u, memory.Peek(3, 1));
        Assert.Equal(0x11223344u, memory.Peek(0, 4));
    }

    [Fact]
    public void ReturnsZeroOutsideTheTwoMegabyteWindow()
    {
        var memory = new MachineAchievementMemory(new FiveDoMemory());

        Assert.Equal(0u, memory.Peek(0x200000, 1));
        Assert.Equal(0u, memory.Peek(0x1FFFFF, 4));
    }
}
