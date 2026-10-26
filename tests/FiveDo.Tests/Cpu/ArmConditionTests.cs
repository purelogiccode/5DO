using FiveDo.Cpu;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Cpu;

public sealed class ArmConditionTests
{
    [Theory]
    [InlineData(0x0u, 0x00000000u, false)]
    [InlineData(0x0u, 0x40000000u, true)]
    [InlineData(0x1u, 0x40000000u, false)]
    [InlineData(0x1u, 0x00000000u, true)]
    [InlineData(0x2u, 0x20000000u, true)]
    [InlineData(0x2u, 0x00000000u, false)]
    [InlineData(0x3u, 0x20000000u, false)]
    [InlineData(0x3u, 0x00000000u, true)]
    [InlineData(0x4u, 0x80000000u, true)]
    [InlineData(0x4u, 0x00000000u, false)]
    [InlineData(0x5u, 0x80000000u, false)]
    [InlineData(0x5u, 0x00000000u, true)]
    [InlineData(0x6u, 0x10000000u, true)]
    [InlineData(0x6u, 0x00000000u, false)]
    [InlineData(0x7u, 0x10000000u, false)]
    [InlineData(0x7u, 0x00000000u, true)]
    [InlineData(0x8u, 0x20000000u, true)]
    [InlineData(0x8u, 0x60000000u, false)]
    [InlineData(0x9u, 0x60000000u, true)]
    [InlineData(0x9u, 0x20000000u, false)]
    [InlineData(0xau, 0x80000000u, false)]
    [InlineData(0xau, 0x00000000u, true)]
    [InlineData(0xbu, 0x80000000u, true)]
    [InlineData(0xbu, 0x00000000u, false)]
    [InlineData(0xcu, 0x00000000u, true)]
    [InlineData(0xcu, 0x40000000u, false)]
    [InlineData(0xcu, 0x80000000u, false)]
    [InlineData(0xdu, 0x40000000u, true)]
    [InlineData(0xdu, 0x00000000u, false)]
    [InlineData(0xdu, 0x80000000u, true)]
    [InlineData(0xeu, 0x00000000u, true)]
    [InlineData(0xfu, 0xf0000000u, false)]
    public void ConditionCodes_MatchArmDefinitions(uint condition, uint cpsr, bool expected)
    {
        Assert.Equal(expected, ArmDecoder.ConditionPassed(cpsr, condition));
    }

    [Fact]
    public void FailedCondition_DoesNotExecuteInstruction()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.CmpImmediate(0, 1),
            ArmTestAssembler.Mov(0, 0x42, condition: 0x0),
            ArmTestAssembler.Nop());

        rig.Cpu.Step();
        rig.Cpu.Step();

        Assert.Equal(0u, rig.Cpu.ReadRegister(0));
        Assert.Equal(0x1008u, rig.Cpu.ProgramCounter);
    }

    [Fact]
    public void PassedCondition_ExecutesInstruction()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.CmpImmediate(0, 0),
            ArmTestAssembler.Mov(0, 0x42, condition: 0x0),
            ArmTestAssembler.Nop());

        rig.Cpu.Step();
        rig.Cpu.Step();

        Assert.Equal(0x42u, rig.Cpu.ReadRegister(0));
    }
}
