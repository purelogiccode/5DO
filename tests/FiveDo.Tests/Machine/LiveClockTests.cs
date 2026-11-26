using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Machine;
using FiveDo.Tests.TestUtilities;
using FiveDo.Video;

namespace FiveDo.Tests.Machine;

public sealed class LiveClockTests
{
    [Fact]
    public void SetArmClock_UpdatesTheTuningAndComponentClocks()
    {
        FiveDoMachine machine = CreateMachine();
        var clock = EmulatorConstants.ArmClock * 2;

        machine.SetArmClock(clock);

        Assert.Equal(clock, machine.Tuning.ArmClock);
        Assert.Equal(clock, machine.Quartz.ArmClock);
        Assert.Equal(clock, machine.Madam.ArmClock);
        Assert.Equal(clock, machine.Cdrom.ArmClock);
        Assert.Equal(clock, machine.Clio.ArmClock);
    }

    [Fact]
    public void SetArmClock_KeepsTheFrameCountAndContinuesRunning()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(5);
        var framesBefore = machine.FramesExecuted;

        machine.SetArmClock(EmulatorConstants.ArmClock * 2);
        machine.RunFrames(5);

        Assert.Equal(framesBefore + 5, machine.FramesExecuted);
    }

    [Fact]
    public void SetArmClock_RejectsNonPositiveClocks()
    {
        FiveDoMachine machine = CreateMachine();

        Assert.Throws<ArgumentOutOfRangeException>(() => machine.SetArmClock(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => machine.SetArmClock(-1));
    }

    [Fact]
    public void SetArmClock_StateRoundtripAtTheNewClockIsDeterministic()
    {
        FiveDoMachine live = CreateMachine();
        live.RunFrames(10);
        live.SetArmClock(EmulatorConstants.ArmClock * 2);
        using var state = new MemoryStream();
        live.SaveState(state, "disc");
        live.RunFrames(30);
        (uint Hash, uint ProgramCounter, long FramesExecuted) expected = Snapshot(live);

        FiveDoMachine loaded = CreateMachine(new EmulatorTuning
        {
            ArmClock = EmulatorConstants.ArmClock * 2,
        });
        state.Position = 0;
        loaded.LoadState(state);
        loaded.RunFrames(30);
        (uint Hash, uint ProgramCounter, long FramesExecuted) actual = Snapshot(loaded);

        Assert.Equal(EmulatorConstants.ArmClock * 2, loaded.Tuning.ArmClock);
        Assert.Equal(expected.Hash, actual.Hash);
        Assert.Equal(expected.ProgramCounter, actual.ProgramCounter);
        Assert.Equal(expected.FramesExecuted, actual.FramesExecuted);
        Assert.Empty(loaded.StateWarnings);
    }

    [Fact]
    public void LiveClockSwitch_MatchesAClockOverrideOfTheSamePreSwitchState()
    {
        FiveDoMachine live = CreateMachine();
        live.RunFrames(10);
        using var state = new MemoryStream();
        live.SaveState(state, "disc");

        live.SetArmClock(EmulatorConstants.ArmClock * 2);
        live.RunFrames(30);
        (uint Hash, uint ProgramCounter, long FramesExecuted) expected = Snapshot(live);

        FiveDoMachine overridden = CreateMachine(new EmulatorTuning
        {
            ArmClock = EmulatorConstants.ArmClock * 2,
        });
        state.Position = 0;
        overridden.LoadState(state);
        overridden.RunFrames(30);
        (uint Hash, uint ProgramCounter, long FramesExecuted) actual = Snapshot(overridden);

        Assert.Equal(expected.Hash, actual.Hash);
        Assert.Equal(expected.ProgramCounter, actual.ProgramCounter);
        Assert.Equal(expected.FramesExecuted, actual.FramesExecuted);
        Assert.Contains(
            overridden.StateWarnings,
            warning => warning.Contains("ARM clock", StringComparison.Ordinal));
    }

    [Fact]
    public void SetArmClock_StockRunsAreUnchanged()
    {
        FiveDoMachine first = CreateMachine();
        first.RunFrames(60);
        (uint Hash, uint ProgramCounter, long FramesExecuted) expected = Snapshot(first);

        FiveDoMachine second = CreateMachine();
        second.RunFrames(60);
        (uint Hash, uint ProgramCounter, long FramesExecuted) actual = Snapshot(second);

        Assert.Equal(expected.Hash, actual.Hash);
        Assert.Equal(expected.ProgramCounter, actual.ProgramCounter);
        Assert.Equal(expected.FramesExecuted, actual.FramesExecuted);
    }

    private static FiveDoMachine CreateMachine(EmulatorTuning? tuning = null)
    {
        var machine = new FiveDoMachine(tuning);
        machine.LoadBios(BuildSyntheticRom());
        machine.Reset();
        return machine;
    }

    private static (uint Hash, uint ProgramCounter, long FramesExecuted) Snapshot(FiveDoMachine machine)
    {
        var buffer = new FrameBuffer(320, 240);
        FrameAssembler.Assemble(machine.Frame, buffer, 320, 240);
        var hash = 2166136261u;
        foreach (var value in buffer.PixelSpan)
        {
            hash = unchecked((hash ^ value) * 16777619);
        }

        return (hash, machine.Cpu.ProgramCounter, machine.FramesExecuted);
    }

    private static byte[] BuildSyntheticRom()
    {
        uint[] program =
        [
            ArmTestAssembler.Mov(0, 0x03300000),
            ArmTestAssembler.Mov(1, 0xb0000),
            ArmTestAssembler.StrImmediate(1, 0, 0x580),
            ArmTestAssembler.Mov(2, 0x03200000),
            ArmTestAssembler.AddImmediate(2, 2, 0x2000),
            ArmTestAssembler.Mvn(3, 0),
            ArmTestAssembler.StrImmediate(3, 2),
            ArmTestAssembler.Branch(0x03000018, 0x03000018),
        ];

        var rom = new byte[EmulatorConstants.RomBankSize];
        for (var i = 0; i < program.Length; i++)
        {
            var offset = i * 4;
            var word = program[i];
            rom[offset] = (byte)(word >> 24);
            rom[offset + 1] = (byte)(word >> 16);
            rom[offset + 2] = (byte)(word >> 8);
            rom[offset + 3] = (byte)word;
        }

        return rom;
    }
}
