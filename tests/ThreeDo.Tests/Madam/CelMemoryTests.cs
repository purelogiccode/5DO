using ThreeDo.Madam;
using ThreeDo.Memory;

namespace ThreeDo.Tests.Madam;

public sealed class CelMemoryTests
{
    [Fact]
    public void MemoryAccess_AccumulatesReferenceCycleCosts()
    {
        var memory = new ThreeDoMemory();
        var cel = new CelMemory(memory.Ram);

        cel.WriteWord(0x1000, 0x11223344);
        Assert.Equal(2, cel.Cycles);

        Assert.Equal(0x11223344u, cel.ReadWord(0x1000));
        Assert.Equal(3, cel.Cycles);

        cel.WriteHalfword(0x1000, 0x5566);
        Assert.Equal(5, cel.Cycles);

        Assert.Equal(0x5566, cel.ReadHalfword(0x1000));
        Assert.Equal(6, cel.Cycles);

        cel.ReadPlut(0x2000, 2);
        Assert.Equal(10, cel.Cycles);

        Assert.Equal(10, cel.TakeCycles());
        Assert.Equal(0, cel.Cycles);
    }

    [Fact]
    public void HalfwordAccess_UsesReferenceXorAddressing()
    {
        var memory = new ThreeDoMemory();
        var cel = new CelMemory(memory.Ram);
        memory.Ram.Write16(0x1002, 0xabcd);

        Assert.Equal(0xabcd, cel.ReadHalfword(0x1000));

        cel.WriteHalfword(0x1000, 0x1234);
        Assert.Equal(0x1234, memory.Ram.Read16(0x1002));
    }

    [Fact]
    public void PlutRead_ZeroBaseReturnsZeroButStillCostsFourCycles()
    {
        var memory = new ThreeDoMemory();
        var cel = new CelMemory(memory.Ram);

        Assert.Equal(0, cel.ReadPlut(0, 0));
        Assert.Equal(4, cel.Cycles);
    }
}
