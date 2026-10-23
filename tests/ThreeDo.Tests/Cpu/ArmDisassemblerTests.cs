using ThreeDo.Cpu;
using ThreeDo.Tests.TestUtilities;

namespace ThreeDo.Tests.Cpu;

public sealed class ArmDisassemblerTests
{
    [Fact]
    public void MovImmediate_FormatsOperand()
    {
        Assert.Equal("mov r0, #0x1", ArmDisassembler.Disassemble(ArmTestAssembler.Mov(0, 1)));
    }

    [Fact]
    public void AddRegister_FormatsThreeOperands()
    {
        Assert.Equal(
            "add r2, r0, r1",
            ArmDisassembler.Disassemble(ArmTestAssembler.DpRegister(4, 2, 0, 1)));
    }

    [Fact]
    public void ShiftedRegister_FormatsShift()
    {
        Assert.Equal(
            "mov r1, r0, lsl #4",
            ArmDisassembler.Disassemble(ArmTestAssembler.DpRegister(13, 1, 0, 0, shiftType: 0, shift: 4)));
    }

    [Fact]
    public void ConditionSuffix_IsAppended()
    {
        Assert.Equal(
            "movne r0, #0x1",
            ArmDisassembler.Disassemble(ArmTestAssembler.Mov(0, 1, condition: 0x1)));
    }

    [Fact]
    public void LdrImmediate_FormatsAddress()
    {
        Assert.Equal(
            "ldr r0, [r1, #0x4]",
            ArmDisassembler.Disassemble(ArmTestAssembler.LdrImmediate(0, 1, 4)));
    }

    [Fact]
    public void StrbNegativeOffset_FormatsAddress()
    {
        Assert.Equal(
            "strb r0, [r1, #-0x4]",
            ArmDisassembler.Disassemble(ArmTestAssembler.StrImmediate(0, 1, 4, add: false, byteAccess: true)));
    }

    [Fact]
    public void Branch_FormatsAbsoluteTarget()
    {
        Assert.Equal(
            "b 0x00000020",
            ArmDisassembler.Disassemble(ArmTestAssembler.Branch(0x20, 0)));
    }

    [Fact]
    public void BranchLink_FormatsAbsoluteTarget()
    {
        Assert.Equal(
            "bl 0x00000020",
            ArmDisassembler.Disassemble(ArmTestAssembler.Branch(0x20, 0, link: true)));
    }

    [Fact]
    public void Multiply_FormatsOperands()
    {
        Assert.Equal(
            "mul r2, r0, r1",
            ArmDisassembler.Disassemble(ArmTestAssembler.Mul(2, 0, 1)));
    }

    [Fact]
    public void Swap_FormatsOperands()
    {
        Assert.Equal(
            "swp r2, r1, [r0]",
            ArmDisassembler.Disassemble(ArmTestAssembler.Swp(2, 1, 0)));
    }

    [Fact]
    public void Swi_FormatsImmediate()
    {
        Assert.Equal("swi #0x1", ArmDisassembler.Disassemble(ArmTestAssembler.Swi(1)));
    }

    [Fact]
    public void Ldm_FormatsRegisterList()
    {
        Assert.Equal(
            "ldmia r0!, {r1, r2}",
            ArmDisassembler.Disassemble(ArmTestAssembler.Block(true, 0, 0x6, preIndex: false, add: true, writeback: true)));
    }

    [Fact]
    public void Undefined_FormatsWord()
    {
        Assert.Equal(".word 0xe6000010", ArmDisassembler.Disassemble(0xe6000010));
    }
}
