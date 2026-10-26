using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Cpu;

public sealed class ArmMemoryTests
{
    [Fact]
    public void StrLdr_WordRoundtrip()
    {
        var rig = new ArmTestRig();
        rig.WriteRam(0x100, 0x11223344);
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x100),
            ArmTestAssembler.LdrImmediate(1, 0),
            ArmTestAssembler.StrImmediate(2, 0),
            ArmTestAssembler.LdrImmediate(3, 0));

        rig.Cpu.WriteRegister(2, 0x55667788);
        rig.Run(4);

        Assert.Equal(0x11223344u, rig.Cpu.ReadRegister(1));
        Assert.Equal(0x55667788u, rig.ReadRam(0x100));
        Assert.Equal(0x55667788u, rig.Cpu.ReadRegister(3));
    }

    [Fact]
    public void Ldrb_ReadsBigEndianLane()
    {
        var rig = new ArmTestRig();
        rig.WriteRam(0x100, 0x11223344);
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x100),
            ArmTestAssembler.LdrImmediate(1, 0, byteAccess: true));

        rig.Run(2);

        Assert.Equal(0x11u, rig.Cpu.ReadRegister(1));
    }

    [Fact]
    public void Strb_WritesBigEndianLane()
    {
        var rig = new ArmTestRig();
        rig.WriteRam(0x100, 0x11223344);
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x100),
            ArmTestAssembler.Mov(1, 0xaa),
            ArmTestAssembler.StrImmediate(1, 0, byteAccess: true));

        rig.Run(3);

        Assert.Equal(0xaa223344u, rig.ReadRam(0x100));
    }

    [Fact]
    public void Ldr_UnalignedAddress_RotatesLoadedWord()
    {
        var rig = new ArmTestRig();
        rig.WriteRam(0x100, 0x11223344);
        rig.LoadProgram(
            ArmTestAssembler.LdrImmediate(1, 0));

        rig.Cpu.WriteRegister(0, 0x101);
        rig.Cpu.Step();

        Assert.Equal(0x44112233u, rig.Cpu.ReadRegister(1));
    }

    [Fact]
    public void Ldr_PreIndexWriteback_UpdatesBase()
    {
        var rig = new ArmTestRig();
        rig.WriteRam(0x100, 0x11111111);
        rig.WriteRam(0x104, 0x22222222);
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x100),
            ArmTestAssembler.LdrImmediate(1, 0, 4, writeback: true));

        rig.Run(2);

        Assert.Equal(0x22222222u, rig.Cpu.ReadRegister(1));
        Assert.Equal(0x104u, rig.Cpu.ReadRegister(0));
    }

    [Fact]
    public void Ldr_PostIndex_UpdatesBaseAfterTransfer()
    {
        var rig = new ArmTestRig();
        rig.WriteRam(0x100, 0x11111111);
        rig.WriteRam(0x104, 0x22222222);
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x100),
            ArmTestAssembler.LdrImmediate(1, 0, 4, preIndex: false));

        rig.Run(2);

        Assert.Equal(0x11111111u, rig.Cpu.ReadRegister(1));
        Assert.Equal(0x104u, rig.Cpu.ReadRegister(0));
    }

    [Fact]
    public void Ldr_NegativeOffset_AddressesBelowBase()
    {
        var rig = new ArmTestRig();
        rig.WriteRam(0x100, 0x11111111);
        rig.WriteRam(0x104, 0x22222222);
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x104),
            ArmTestAssembler.LdrImmediate(1, 0, 4, add: false));

        rig.Run(2);

        Assert.Equal(0x11111111u, rig.Cpu.ReadRegister(1));
    }

    [Fact]
    public void Ldr_RegisterOffset_UsesScaledRegister()
    {
        var rig = new ArmTestRig();
        rig.WriteRam(0x100, 0x11111111);
        rig.WriteRam(0x104, 0x22222222);
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x100),
            ArmTestAssembler.Mov(1, 1),
            ArmTestAssembler.LdrRegister(2, 0, 1, shiftType: 0, shift: 2));

        rig.Run(3);

        Assert.Equal(0x22222222u, rig.Cpu.ReadRegister(2));
    }

    [Fact]
    public void Ldr_RegisterShiftedByRegister_FollowsFreeDoUndefinedPath()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.LdrRegisterShiftedByRegister(3, 0, 2, 1));

        rig.Cpu.Step();

        Assert.Equal(0x00000004u, rig.Cpu.ProgramCounter);
        Assert.Equal(0x1bu, rig.Cpu.Cpsr & 0x1f);
    }

    [Fact]
    public void Ldr_ToPc_ChangesProgramCounter()
    {
        var rig = new ArmTestRig();
        rig.WriteRam(0x200, 0x00001010);
        rig.LoadProgramAt(
             0x1000,
            ArmTestAssembler.Mov(0, 0x200),
            ArmTestAssembler.LdrImmediate(15, 0));

        rig.Run(2);

        Assert.Equal(0x00001010u, rig.Cpu.ProgramCounter);
    }

    [Fact]
    public void Str_PostIndex_UpdatesBaseAndWritesMemory()
    {
        var rig = new ArmTestRig();
        rig.WriteRam(0x100, 0);
        rig.LoadProgram(
            ArmTestAssembler.Mov(0, 0x100),
            ArmTestAssembler.StrImmediate(1, 0, 4, preIndex: false, writeback: true));

        rig.Cpu.WriteRegister(1, 0x1234);
        rig.Run(2);

        Assert.Equal(0x1234u, rig.ReadRam(0x100));
        Assert.Equal(0x104u, rig.Cpu.ReadRegister(0));
    }
}
