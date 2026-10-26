using System;
using FiveDo.Core;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Cpu;

public sealed class Arm60StateTests
{
    [Fact]
    public void Reset_UsesFreeDoPowerOnValues()
    {
        var rig = new ArmTestRig();
        rig.Cpu.WriteRegister(0, 0xDEADBEEF);
        rig.Cpu.WriteRegister(13, 0x12345678);
        rig.Cpu.Cpsr = 0x1f;
        rig.Cpu.FiqPending = true;
        rig.Cpu.SecondRom = true;

        rig.Cpu.Reset();

        Assert.Equal(0x03000000u, rig.Cpu.ProgramCounter);
        Assert.Equal(0x13u, rig.Cpu.Cpsr);
        Assert.False(rig.Cpu.FiqPending);
        Assert.False(rig.Cpu.SecondRom);
        Assert.False(rig.Cpu.DataAbortPending);
        for (var i = 0; i < 15; i++)
        {
            Assert.Equal(0u, rig.Cpu.ReadRegister(i));
        }
    }

    [Fact]
    public void Constructor_ResetsState()
    {
        var rig = new ArmTestRig();

        Assert.Equal(0x03000000u, rig.Cpu.ProgramCounter);
        Assert.Equal(0x13u, rig.Cpu.Cpsr);
        Assert.Equal(0, rig.Cpu.InstructionsExecuted);
    }

    [Fact]
    public void SelectRom_SelectsSecondBank()
    {
        var rig = new ArmTestRig();

        rig.Cpu.SelectRom(1);
        Assert.True(rig.Cpu.SecondRom);

        rig.Cpu.SelectRom(0);
        Assert.False(rig.Cpu.SecondRom);
    }

    [Fact]
    public void RegisterIndexer_RejectsOutOfRange()
    {
        var rig = new ArmTestRig();

        Assert.Throws<ArgumentOutOfRangeException>(() => rig.Cpu.ReadRegister(16));
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.Cpu.WriteRegister(-1, 0));
    }

    [Fact]
    public void StateSection_RoundtripsArchitecturalState()
    {
        var rig = new ArmTestRig();
        rig.Cpu.WriteRegister(0, 0x11223344);
        rig.Cpu.WriteRegister(13, 0x55667788);
        rig.Cpu.Cpsr = 0x60000013;
        rig.Cpu.FiqPending = true;
        rig.Cpu.SecondRom = true;
        rig.Cpu.DataAbortPending = true;

        var writer = new StateWriter();
        writer.WriteSection(rig.Cpu);
        var reader = new StateReader(writer.ToArray());
        Assert.True(reader.TryReadSection(out var section));
        Assert.NotNull(section);
        Assert.Equal(StateSectionIds.Cpu, section.Id);

        var restored = new ArmTestRig();
        restored.Cpu.Load(section);

        Assert.Equal(0x11223344u, restored.Cpu.ReadRegister(0));
        Assert.Equal(0x55667788u, restored.Cpu.ReadRegister(13));
        Assert.Equal(0x60000013u, restored.Cpu.Cpsr);
        Assert.True(restored.Cpu.FiqPending);
        Assert.True(restored.Cpu.SecondRom);
        Assert.True(restored.Cpu.DataAbortPending);
    }
}
