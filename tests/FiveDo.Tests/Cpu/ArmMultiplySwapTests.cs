using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Cpu;

public sealed class ArmMultiplySwapTests
{
    [Fact]
    public void Mul_MultipliesRmByRs()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 6),
            ArmTestAssembler.Mov(1, 7),
            ArmTestAssembler.Mul(2, 0, 1));

        rig.Run(3);

        Assert.Equal(42u, rig.Cpu.ReadRegister(2));
    }

    [Fact]
    public void Mla_AddsAccumulator()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 6),
            ArmTestAssembler.Mov(1, 7),
            ArmTestAssembler.Mov(2, 5),
            ArmTestAssembler.Mla(3, 0, 1, 2));

        rig.Run(4);

        Assert.Equal(47u, rig.Cpu.ReadRegister(3));
    }

    [Fact]
    public void Muls_SetsZeroAndNegative()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0),
            ArmTestAssembler.Mov(1, 7),
            ArmTestAssembler.Mul(2, 0, 1, setFlags: true));

        rig.Run(3);

        Assert.Equal(0u, rig.Cpu.ReadRegister(2));
        Assert.Equal(1u, (rig.Cpu.Cpsr >> 30) & 1);
        Assert.Equal(0u, (rig.Cpu.Cpsr >> 31) & 1);
    }

    [Fact]
    public void Mul_WhenDestinationEqualsMultiplier_UsesFreeDoQuirk()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 6),
            ArmTestAssembler.Mov(1, 7),
            ArmTestAssembler.Mul(0, 0, 1));

        rig.Run(3);

        Assert.Equal(0u, rig.Cpu.ReadRegister(0));
    }

    [Fact]
    public void Mla_WhenDestinationEqualsMultiplier_UsesFreeDoQuirk()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 6),
            ArmTestAssembler.Mov(1, 7),
            ArmTestAssembler.Mov(2, 5),
            ArmTestAssembler.Mla(0, 0, 1, 2));

        rig.Run(4);

        Assert.Equal(5u, rig.Cpu.ReadRegister(0));
    }

    [Fact]
    public void Swp_ExchangesWord()
    {
        var rig = new ArmTestRig();
        rig.WriteRam(0x100, 0x11223344);
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x100),
            ArmTestAssembler.Swp(2, 1, 0));

        rig.Cpu.WriteRegister(1, 0xaabbccdd);
        rig.Run(2);

        Assert.Equal(0x11223344u, rig.Cpu.ReadRegister(2));
        Assert.Equal(0xaabbccddu, rig.ReadRam(0x100));
    }

    [Fact]
    public void Swpb_ExchangesByte()
    {
        var rig = new ArmTestRig();
        rig.WriteRam(0x100, 0x11223344);
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x100),
            ArmTestAssembler.Swp(2, 1, 0, byteAccess: true));

        rig.Cpu.WriteRegister(1, 0xaabbccdd);
        rig.Run(2);

        Assert.Equal(0x11u, rig.Cpu.ReadRegister(2));
        Assert.Equal(0xdd223344u, rig.ReadRam(0x100));
    }

    [Fact]
    public void Swp_UnalignedWord_RotatesLoadedValue()
    {
        var rig = new ArmTestRig();
        rig.WriteRam(0x100, 0x11223344);
        rig.LoadProgram(
            ArmTestAssembler.Swp(2, 1, 0));

        rig.Cpu.WriteRegister(0, 0x101);
        rig.Cpu.WriteRegister(1, 0xaabbccdd);
        rig.Run(1);

        Assert.Equal(0x44112233u, rig.Cpu.ReadRegister(2));
        Assert.Equal(0xaabbccddu, rig.ReadRam(0x100));
    }
}
