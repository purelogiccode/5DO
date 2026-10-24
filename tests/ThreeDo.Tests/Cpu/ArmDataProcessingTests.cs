using ThreeDo.Tests.TestUtilities;

namespace ThreeDo.Tests.Cpu;

public sealed class ArmDataProcessingTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(0xffu)]
    [InlineData(0x1000u)]
    [InlineData(0xff000000u)]
    public void MovImmediate_LoadsValue(uint value)
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.Mov(0, value));

        rig.Cpu.Step();

        Assert.Equal(value, rig.Cpu.ReadRegister(0));
    }

    [Fact]
    public void ArithmeticOperations_ProduceExpectedResults()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 10),
            ArmTestAssembler.Mov(1, 3),
            ArmTestAssembler.AddImmediate(2, 0, 5),
            ArmTestAssembler.SubImmediate(3, 0, 4),
            ArmTestAssembler.DpRegister(4, 4, 0, 1),
            ArmTestAssembler.DpRegister(2, 5, 0, 1),
            ArmTestAssembler.DpRegister(3, 6, 0, 1),
            ArmTestAssembler.DpRegister(0, 7, 0, 1),
            ArmTestAssembler.DpRegister(1, 8, 0, 1),
            ArmTestAssembler.DpRegister(12, 9, 0, 1),
            ArmTestAssembler.DpRegister(14, 10, 0, 1),
            ArmTestAssembler.DpRegister(15, 11, 0, 1));

        rig.Run(12);

        Assert.Equal(15u, rig.Cpu.ReadRegister(2));
        Assert.Equal(6u, rig.Cpu.ReadRegister(3));
        Assert.Equal(13u, rig.Cpu.ReadRegister(4));
        Assert.Equal(7u, rig.Cpu.ReadRegister(5));
        Assert.Equal(0xfffffff9u, rig.Cpu.ReadRegister(6));
        Assert.Equal(0x2u, rig.Cpu.ReadRegister(7));
        Assert.Equal(0x9u, rig.Cpu.ReadRegister(8));
        Assert.Equal(0xbu, rig.Cpu.ReadRegister(9));
        Assert.Equal(0x8u, rig.Cpu.ReadRegister(10));
        Assert.Equal(0xfffffffcu, rig.Cpu.ReadRegister(11));
    }

    [Fact]
    public void ImmediateShifts_ProduceExpectedResults()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x80000001),
            ArmTestAssembler.DpRegister(13, 1, 0, 0, shiftType: 0, shift: 4),
            ArmTestAssembler.DpRegister(13, 2, 0, 0, shiftType: 1, shift: 1),
            ArmTestAssembler.DpRegister(13, 3, 0, 0, shiftType: 2, shift: 1),
            ArmTestAssembler.DpRegister(13, 4, 0, 0, shiftType: 3, shift: 4));

        rig.Run(5);

        Assert.Equal(0x00000010u, rig.Cpu.ReadRegister(1));
        Assert.Equal(0x40000000u, rig.Cpu.ReadRegister(2));
        Assert.Equal(0xc0000000u, rig.Cpu.ReadRegister(3));
        Assert.Equal(0x18000000u, rig.Cpu.ReadRegister(4));
    }

    [Fact]
    public void RegisterShift_UsesLowByteOfShiftRegister()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 1),
            ArmTestAssembler.Mov(2, 4),
            ArmTestAssembler.DpRegisterShiftedByRegister(13, 1, 0, 0, 2));

        rig.Run(3);

        Assert.Equal(0x10u, rig.Cpu.ReadRegister(1));
    }

    [Fact]
    public void Adds_SetsCarryAndZeroOnWrap()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mvn(0, 0),
            ArmTestAssembler.Mov(1, 1),
            ArmTestAssembler.DpRegister(4, 2, 0, 1, setFlags: true));

        rig.Run(3);

        Assert.Equal(0u, rig.Cpu.ReadRegister(2));
        Assert.Equal(1u, (rig.Cpu.Cpsr >> 30) & 1);
        Assert.Equal(1u, (rig.Cpu.Cpsr >> 29) & 1);
        Assert.Equal(0u, (rig.Cpu.Cpsr >> 31) & 1);
        Assert.Equal(0u, (rig.Cpu.Cpsr >> 28) & 1);
    }

    [Fact]
    public void Adds_SetsOverflowOnSignedOverflow()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mvn(0, 0x80000000),
            ArmTestAssembler.Mov(1, 1),
            ArmTestAssembler.DpRegister(4, 2, 0, 1, setFlags: true));

        rig.Run(3);

        Assert.Equal(0x80000000u, rig.Cpu.ReadRegister(2));
        Assert.Equal(1u, (rig.Cpu.Cpsr >> 31) & 1);
        Assert.Equal(1u, (rig.Cpu.Cpsr >> 28) & 1);
        Assert.Equal(0u, (rig.Cpu.Cpsr >> 29) & 1);
        Assert.Equal(0u, (rig.Cpu.Cpsr >> 30) & 1);
    }

    [Fact]
    public void Subs_SetsBorrowAndNegative()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0),
            ArmTestAssembler.Mov(1, 1),
            ArmTestAssembler.DpRegister(2, 2, 0, 1, setFlags: true));

        rig.Run(3);

        Assert.Equal(0xffffffffu, rig.Cpu.ReadRegister(2));
        Assert.Equal(1u, (rig.Cpu.Cpsr >> 31) & 1);
        Assert.Equal(0u, (rig.Cpu.Cpsr >> 29) & 1);
        Assert.Equal(0u, (rig.Cpu.Cpsr >> 28) & 1);
    }

    [Theory]
    [InlineData(0, 0x00000000u)]
    [InlineData(1, 0x000000ffu)]
    [InlineData(2, 0x000000e1u)]
    [InlineData(3, 0xffffff1fu)]
    [InlineData(4, 0x000000ffu)]
    [InlineData(5, 0x000000ffu)]
    [InlineData(6, 0x000000e0u)]
    [InlineData(7, 0xffffff1eu)]
    [InlineData(12, 0x000000ffu)]
    [InlineData(13, 0x0000000fu)]
    [InlineData(14, 0x000000f0u)]
    [InlineData(15, 0xfffffff0u)]
    public void AluOpcodeMatrix_ProducesExpectedResult(int opcode, uint expected)
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0xf0),
            ArmTestAssembler.Mov(1, 0x0f),
            ArmTestAssembler.DpRegister(opcode, 2, 0, 1));

        rig.Cpu.Cpsr = 0x13;
        rig.Run(3);

        Assert.Equal(expected, rig.Cpu.ReadRegister(2));
    }

    [Fact]
    public void TestOperations_SetExpectedFlags()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0xf0),
            ArmTestAssembler.Mov(1, 0x0f),
            ArmTestAssembler.TstRegister(0, 1),
            ArmTestAssembler.TeqRegister(0, 1),
            ArmTestAssembler.CmpRegister(0, 1),
            ArmTestAssembler.DpRegister(11, 0, 0, 1, setFlags: true));

        rig.Run(3);
        Assert.Equal(1u, (rig.Cpu.Cpsr >> 30) & 1);
        Assert.Equal(0u, (rig.Cpu.Cpsr >> 29) & 1);

        rig.Cpu.Step();
        Assert.Equal(0u, (rig.Cpu.Cpsr >> 30) & 1);

        rig.Cpu.Step();
        Assert.Equal(1u, (rig.Cpu.Cpsr >> 29) & 1);

        rig.Cpu.Step();
        Assert.Equal(0u, (rig.Cpu.Cpsr >> 29) & 1);
    }

    [Fact]
    public void TestOperations_SetFlagsWithoutWritingDestination()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0xf0),
            ArmTestAssembler.Mov(1, 0x0f),
            ArmTestAssembler.TstRegister(0, 1),
            ArmTestAssembler.TeqRegister(0, 1),
            ArmTestAssembler.CmpRegister(0, 1),
            ArmTestAssembler.DpRegister(11, 0, 0, 1, setFlags: true));

        rig.Run(6);

        Assert.Equal(0xf0u, rig.Cpu.ReadRegister(0));
        Assert.Equal(0x0fu, rig.Cpu.ReadRegister(1));
    }

    [Fact]
    public void Movs_SetsCarryFromShifter()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 3),
            ArmTestAssembler.DpRegister(13, 1, 0, 0, shiftType: 1, shift: 1, setFlags: true));

        rig.Run(2);

        Assert.Equal(1u, rig.Cpu.ReadRegister(1));
        Assert.Equal(1u, (rig.Cpu.Cpsr >> 29) & 1);
    }

    [Fact]
    public void ImmediateMovs_KeepsStaleShifterCarry()
    {
        var rig = new ArmTestRig();
        rig.Cpu.Cpsr = 0x20000013;
        rig.LoadProgram(ArmTestAssembler.DpImmediate(13, 0, 0, 1, setFlags: true));

        rig.Cpu.Step();

        Assert.Equal(0u, (rig.Cpu.Cpsr >> 29) & 1);
    }

    [Fact]
    public void Mrs_Msr_UpdateFlagsAndReadCpsr()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Msr(0, mask: 0x9),
            ArmTestAssembler.Mrs(1),
            ArmTestAssembler.Nop());

        rig.Cpu.WriteRegister(0, 0xf0000013);
        rig.Run(3);

        Assert.Equal(0xf0000013u, rig.Cpu.Cpsr);
        Assert.Equal(0xf0000013u, rig.Cpu.ReadRegister(1));
    }

    [Fact]
    public void Swi_UpdatesSpsrAndMrsSpsrReadsIt()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(
            0x00000000,
            ArmTestAssembler.Mov(0, 0x11),
            ArmTestAssembler.Swi(0),
            ArmTestAssembler.Mrs(1, spsr: true),
            ArmTestAssembler.Nop());

        rig.Run(3);

        Assert.Equal(0x11u, rig.Cpu.ReadRegister(0));
        Assert.Equal(0x13u, rig.Cpu.Cpsr & 0x1f);
        Assert.Equal(0x13u, rig.Cpu.ReadRegister(1));
        Assert.Equal(0x0000000cu, rig.Cpu.ProgramCounter);
    }
}
