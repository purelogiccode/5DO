using ThreeDo.Tests.TestUtilities;

namespace ThreeDo.Tests.Cpu;

public sealed class ArmBlockTransferTests
{
    [Fact]
    public void Stmia_Ldmia_Roundtrip()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x100),
            ArmTestAssembler.Block(false, 0, 0x6, preIndex: false, add: true, writeback: true),
            ArmTestAssembler.Mov(3, 0x100),
            ArmTestAssembler.Block(true, 3, 0x6, preIndex: false, add: true, writeback: true));

        rig.Cpu.WriteRegister(1, 0x11111111);
        rig.Cpu.WriteRegister(2, 0x22222222);
        rig.Run(4);

        Assert.Equal(0x11111111u, rig.ReadRam(0x100));
        Assert.Equal(0x22222222u, rig.ReadRam(0x104));
        Assert.Equal(0x11111111u, rig.Cpu.ReadRegister(1));
        Assert.Equal(0x22222222u, rig.Cpu.ReadRegister(2));
        Assert.Equal(0x108u, rig.Cpu.ReadRegister(0));
        Assert.Equal(0x108u, rig.Cpu.ReadRegister(3));
    }

    [Fact]
    public void Stmdb_Ldmdb_Roundtrip()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x100),
            ArmTestAssembler.Block(false, 0, 0x6, preIndex: true, add: false, writeback: true),
            ArmTestAssembler.Mov(3, 0x100),
            ArmTestAssembler.Block(true, 3, 0x6, preIndex: true, add: false, writeback: true));

        rig.Cpu.WriteRegister(1, 0x11111111);
        rig.Cpu.WriteRegister(2, 0x22222222);
        rig.Run(4);

        Assert.Equal(0x11111111u, rig.ReadRam(0xf8));
        Assert.Equal(0x22222222u, rig.ReadRam(0xfc));
        Assert.Equal(0x11111111u, rig.Cpu.ReadRegister(1));
        Assert.Equal(0x22222222u, rig.Cpu.ReadRegister(2));
        Assert.Equal(0xf8u, rig.Cpu.ReadRegister(0));
        Assert.Equal(0xf8u, rig.Cpu.ReadRegister(3));
    }

    [Fact]
    public void Ldmia_WithPc_LoadsProgramCounter()
    {
        var rig = new ArmTestRig();
        rig.WriteRam(0x100, 0x11111111);
        rig.WriteRam(0x104, 0x00001010);
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x100),
            ArmTestAssembler.Block(true, 0, 0x8002, preIndex: false, add: true));

        rig.Run(2);

        Assert.Equal(0x11111111u, rig.Cpu.ReadRegister(1));
        Assert.Equal(0x00001010u, rig.Cpu.ProgramCounter);
    }

    [Fact]
    public void Stmia_WithPc_StoresPcPlusEight()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(
             0x1000,
            ArmTestAssembler.Mov(0, 0x200),
            ArmTestAssembler.Block(false, 0, 0x8002, preIndex: false, add: true));

        rig.Cpu.WriteRegister(1, 0x11111111);
        rig.Run(2);

        Assert.Equal(0x11111111u, rig.ReadRam(0x200));
        Assert.Equal(0x00001010u, rig.ReadRam(0x204));
    }

    [Fact]
    public void Ldmia_EmptyList_DoesNotCrash()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x100),
            ArmTestAssembler.Block(true, 0, 0, preIndex: false, add: true));

        rig.Run(2);

        Assert.Equal(0x1008u, rig.Cpu.ProgramCounter);
        Assert.Equal(0x100u, rig.Cpu.ReadRegister(0));
    }

    [Fact]
    public void Ldmia_BaseInList_LoadedValueWinsOverWriteback()
    {
        var rig = new ArmTestRig();
        rig.WriteRam(0x100, 0x11111111);
        rig.WriteRam(0x104, 0x22222222);
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x100),
            ArmTestAssembler.Block(true, 0, 0x3, preIndex: false, add: true, writeback: true));

        rig.Run(2);

        Assert.Equal(0x11111111u, rig.Cpu.ReadRegister(0));
        Assert.Equal(0x22222222u, rig.Cpu.ReadRegister(1));
    }
}
