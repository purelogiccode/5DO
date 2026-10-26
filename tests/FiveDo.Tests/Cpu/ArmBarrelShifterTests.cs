using FiveDo.Cpu;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Cpu;

public sealed class ArmBarrelShifterTests
{
    [Theory]
    [InlineData(0u, 0u, 0u)]
    [InlineData(1u, 1u, 2u)]
    [InlineData(0x80000001u, 1u, 2u)]
    [InlineData(0x80000000u, 1u, 0u)]
    [InlineData(0x12345678u, 4u, 0x23456780u)]
    [InlineData(0x12345678u, 31u, 0u)]
    [InlineData(0x12345678u, 32u, 0u)]
    [InlineData(0x12345678u, 33u, 0u)]
    public void Lsl_ReturnsExpectedValue(uint value, byte shift, uint expected)
    {
        var rig = new ArmTestRig();

        var result = ArmBarrelShifter.ShiftNoCarry(rig.Cpu.State, value, shift, 0, out _);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1u, 0u, 1u)]
    [InlineData(2u, 1u, 1u)]
    [InlineData(0x80000000u, 1u, 0x40000000u)]
    [InlineData(0x80000000u, 31u, 1u)]
    [InlineData(0xFFFFFFFFu, 32u, 0u)]
    [InlineData(0xFFFFFFFFu, 33u, 0u)]
    public void Lsr_ReturnsExpectedValue(uint value, byte shift, uint expected)
    {
        var rig = new ArmTestRig();

        var result = ArmBarrelShifter.ShiftNoCarry(rig.Cpu.State, value, shift, 1, out _);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0x80000000u, 1u, 0xC0000000u)]
    [InlineData(0x80000000u, 31u, 0xFFFFFFFFu)]
    [InlineData(0x80000000u, 32u, 0xFFFFFFFFu)]
    [InlineData(0x80000000u, 33u, 0xFFFFFFFFu)]
    [InlineData(0x40000000u, 2u, 0x10000000u)]
    public void Asr_ReturnsExpectedValue(uint value, byte shift, uint expected)
    {
        var rig = new ArmTestRig();

        var result = ArmBarrelShifter.ShiftNoCarry(rig.Cpu.State, value, shift, 2, out _);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1u, 1u, 0x80000000u)]
    [InlineData(0x80000000u, 1u, 0x40000000u)]
    [InlineData(0x12345678u, 8u, 0x78123456u)]
    [InlineData(0x12345678u, 32u, 0x12345678u)]
    [InlineData(0x12345678u, 33u, 0x091A2B3Cu)]
    public void Ror_ReturnsExpectedValue(uint value, byte shift, uint expected)
    {
        var rig = new ArmTestRig();

        var result = ArmBarrelShifter.ShiftNoCarry(rig.Cpu.State, value, shift, 3, out _);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Rrx_UsesCarryIn()
    {
        var rig = new ArmTestRig();
        rig.Cpu.Cpsr = 0x20000013;

        var result = ArmBarrelShifter.ShiftNoCarry(rig.Cpu.State, 2, 0, 4, out var carry);

        Assert.Equal(0x80000001u, result);
        Assert.Equal(0u, carry);
    }

    [Fact]
    public void ShiftSetCarry_UpdatesCpsrCarry()
    {
        var rig = new ArmTestRig();
        rig.Cpu.Cpsr = 0x00000013;

        var result = ArmBarrelShifter.ShiftSetCarry(rig.Cpu.State, 0x80000000u, 1, 0);

        Assert.Equal(0u, result);
        Assert.Equal(1u, (rig.Cpu.Cpsr >> 29) & 1);
    }

    [Fact]
    public void ShiftNoCarry_ZeroShiftPreservesOldCarry()
    {
        var rig = new ArmTestRig();
        rig.Cpu.Cpsr = 0x20000013;

        var result = ArmBarrelShifter.ShiftNoCarry(rig.Cpu.State, 0x12345678u, 0, 0, out var carry);

        Assert.Equal(0x12345678u, result);
        Assert.Equal(1u, carry);
    }
}
