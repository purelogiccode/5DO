using ThreeDo.Tests.TestUtilities;

namespace ThreeDo.Tests.Cpu;

public sealed class ArmCycleTests
{
    [Fact]
    public void Nop_ConsumesOneCycle()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.Nop());

        Assert.Equal(1, rig.Cpu.Step());
    }

    [Fact]
    public void MovImmediate_ConsumesOneCycle()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.Mov(0, 1));

        Assert.Equal(1, rig.Cpu.Step());
    }

    [Fact]
    public void AddRegister_ConsumesOneCycle()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.DpRegister(4, 2, 0, 1));

        Assert.Equal(1, rig.Cpu.Step());
    }

    [Fact]
    public void RegisterShift_ConsumesTwoCycles()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.DpRegisterShiftedByRegister(13, 1, 0, 0, 2));

        Assert.Equal(2, rig.Cpu.Step());
    }

    [Fact]
    public void Ldr_ConsumesSixCycles()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.LdrImmediate(0, 1));

        Assert.Equal(6, rig.Cpu.Step());
    }

    [Fact]
    public void LdrToPc_ConsumesElevenCycles()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.LdrImmediate(15, 1));

        Assert.Equal(11, rig.Cpu.Step());
    }

    [Fact]
    public void Str_ConsumesEightCycles()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.StrImmediate(0, 1));

        Assert.Equal(8, rig.Cpu.Step());
    }

    [Fact]
    public void Branch_ConsumesSixCycles()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(0x1000, ArmTestAssembler.Branch(0x1010, 0x1000));

        Assert.Equal(6, rig.Cpu.Step());
    }

    [Fact]
    public void Swi_ConsumesSixCycles()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(0x1000, ArmTestAssembler.Swi(0));

        Assert.Equal(6, rig.Cpu.Step());
    }

    [Fact]
    public void Undefined_ConsumesSixCycles()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(0x1000, 0xe6000010);

        Assert.Equal(6, rig.Cpu.Step());
    }

    [Fact]
    public void Multiply_ConsumesOperandDependentCycles()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 6),
            ArmTestAssembler.Mov(1, 7),
            ArmTestAssembler.Mul(2, 0, 1));

        Assert.Equal(1, rig.Cpu.Step());
        Assert.Equal(1, rig.Cpu.Step());
        Assert.Equal(4, rig.Cpu.Step());
    }

    [Fact]
    public void Multiply_LargeOperandIsCappedAtSeventeenCycles()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mvn(0, 0),
            ArmTestAssembler.Mvn(1, 0),
            ArmTestAssembler.Mul(2, 0, 1));

        rig.Run(2);

        Assert.Equal(17, rig.Cpu.Step());
    }

    [Fact]
    public void Swap_ConsumesTenCycles()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.Swp(2, 1, 0));

        Assert.Equal(10, rig.Cpu.Step());
    }

    [Fact]
    public void Ldm_ConsumesReferenceCycles()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.Block(true, 0, 0x6, preIndex: false, add: true));

        Assert.Equal(7, rig.Cpu.Step());
    }

    [Fact]
    public void LdmWithPc_ConsumesReferenceCycles()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.Block(true, 0, 0x8002, preIndex: false, add: true));

        Assert.Equal(12, rig.Cpu.Step());
    }

    [Fact]
    public void LdmEmptyList_ConsumesReferenceCycles()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.Block(true, 0, 0, preIndex: false, add: true));

        Assert.Equal(5, rig.Cpu.Step());
    }

    [Fact]
    public void Stm_ConsumesReferenceCycles()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.Block(false, 0, 0x6, preIndex: false, add: true));

        Assert.Equal(9, rig.Cpu.Step());
    }

    [Fact]
    public void DataProcessingToPc_ConsumesSixCycles()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.DpRegister(13, 15, 0, 0));

        Assert.Equal(6, rig.Cpu.Step());
    }
}
