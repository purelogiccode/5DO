using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Cpu;

public sealed class ArmModeBankTests
{
    [Fact]
    public void IrqBank_SavesAndRestoresR13R14()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(
            ArmTestAssembler.Msr(0),
            ArmTestAssembler.Mov(0, 0x13),
            ArmTestAssembler.Msr(0),
            ArmTestAssembler.Mov(0, 0x12),
            ArmTestAssembler.Msr(0));

        rig.Cpu.WriteRegister(0, 0x12);
        rig.Cpu.WriteRegister(13, 0x11);
        rig.Cpu.WriteRegister(14, 0x22);

        rig.Cpu.Step();
        Assert.Equal(0u, rig.Cpu.ReadRegister(13));
        Assert.Equal(0u, rig.Cpu.ReadRegister(14));

        rig.Cpu.WriteRegister(13, 0x33);
        rig.Cpu.WriteRegister(14, 0x44);

        rig.Run(2);
        Assert.Equal(0x13u, rig.Cpu.Cpsr & 0x1f);
        Assert.Equal(0x11u, rig.Cpu.ReadRegister(13));
        Assert.Equal(0x22u, rig.Cpu.ReadRegister(14));

        rig.Run(2);
        Assert.Equal(0x12u, rig.Cpu.Cpsr & 0x1f);
        Assert.Equal(0x33u, rig.Cpu.ReadRegister(13));
        Assert.Equal(0x44u, rig.Cpu.ReadRegister(14));
    }

    [Fact]
    public void FiqBank_SavesAndRestoresR8ToR14()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.Nop());
        rig.Cpu.WriteRegister(8, 0x88);
        rig.Cpu.WriteRegister(13, 0x1111);
        rig.Cpu.WriteRegister(14, 0x2222);
        rig.Interrupts.NeedFiq = true;

        rig.Cpu.Step();

        Assert.Equal(0x11u, rig.Cpu.Cpsr & 0x1f);
        Assert.Equal(0u, rig.Cpu.ReadRegister(8));
        Assert.Equal(0u, rig.Cpu.ReadRegister(13));
        Assert.Equal(0x1008u, rig.Cpu.ReadRegister(14));

        rig.Cpu.WriteRegister(8, 0x99);
        rig.Cpu.WriteRegister(13, 0x3333);
        rig.Cpu.WriteRegister(14, 0x4444);
        rig.Interrupts.NeedFiq = false;
        rig.Memory.Ram.Write32(0x1c, ArmTestAssembler.DpImmediate(2, 15, 14, 4, setFlags: true));
        rig.Cpu.Step();

        Assert.Equal(0x13u, rig.Cpu.Cpsr & 0x1f);
        Assert.Equal(0x88u, rig.Cpu.ReadRegister(8));
        Assert.Equal(0x1111u, rig.Cpu.ReadRegister(13));
        Assert.Equal(0x2222u, rig.Cpu.ReadRegister(14));
    }

    [Fact]
    public void SwiFromUserMode_RestoresUserBankOnMovsPc()
    {
        var rig = new ArmTestRig();
        rig.LoadProgramAt(0x1000, ArmTestAssembler.Swi(0));
        rig.Memory.Ram.Write32(0x08, ArmTestAssembler.DpRegister(13, 15, 0, 14, setFlags: true));
        rig.Cpu.Cpsr = 0x10;
        rig.Cpu.WriteRegister(13, 0x1111);
        rig.Cpu.WriteRegister(14, 0x2222);

        rig.Cpu.Step();
        Assert.Equal(0x13u, rig.Cpu.Cpsr & 0x1f);
        Assert.Equal(0x08u, rig.Cpu.ProgramCounter);

        rig.Cpu.Step();
        Assert.Equal(0x10u, rig.Cpu.Cpsr & 0x1f);
        Assert.Equal(0x1004u, rig.Cpu.ProgramCounter);
        Assert.Equal(0x1111u, rig.Cpu.ReadRegister(13));
        Assert.Equal(0x2222u, rig.Cpu.ReadRegister(14));
    }

    [Fact]
    public void StrtPostIndex_ReadsUserBankedRegister()
    {
        var rig = new ArmTestRig();
        rig.Cpu.Cpsr = 0x10;
        rig.Cpu.WriteRegister(13, 0x1234);
        rig.Cpu.Cpsr = 0x13;
        rig.Cpu.WriteRegister(13, 0x9999);
        rig.WriteRam(0x100, 0);
        rig.LoadProgramAt(
            0x1000,
            ArmTestAssembler.Mov(0, 0x100),
            ArmTestAssembler.StrImmediate(13, 0, 4, preIndex: false, writeback: true));

        rig.Run(2);

        Assert.Equal(0x1234u, rig.ReadRam(0x100));
        Assert.Equal(0x9999u, rig.Cpu.ReadRegister(13));
        Assert.Equal(0x104u, rig.Cpu.ReadRegister(0));
    }
}
