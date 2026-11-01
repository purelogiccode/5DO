using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Host.Rewind;
using FiveDo.Input;
using FiveDo.Machine;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Host;

public sealed class RewindBufferTests
{
    [Fact]
    public void RewindTo_ReplaysInputAndRestoresTheExactMachineState()
    {
        FiveDoMachine machine = CreateMachine();
        var input = new PbusSource();
        machine.PbusSource = input;
        var rewind = new RewindBuffer(
            machine,
            new RewindOptions { Seconds = 30, KeyframeInterval = 10 });
        rewind.ResetHistory();

        RunFrames(machine, input, rewind, 1, 90);

        Assert.True(rewind.CanRewind);
        Assert.Equal(0, rewind.OldestFrame);
        Assert.Equal(90, rewind.NewestFrame);

        Assert.True(rewind.RewindTo(37));

        FiveDoMachine control = CreateMachine();
        var controlInput = new PbusSource();
        control.PbusSource = controlInput;
        RunFrames(control, controlInput, null, 1, 37);

        Assert.Equal(37, machine.FramesExecuted);
        Assert.Equal(SaveStateBytes(control), SaveStateBytes(machine));
    }

    [Fact]
    public void RewindTo_ClampsToTheOldestRetainedFrame()
    {
        FiveDoMachine machine = CreateMachine();
        var input = new PbusSource();
        machine.PbusSource = input;
        var rewind = new RewindBuffer(
            machine,
            new RewindOptions { Seconds = 30, KeyframeInterval = 10 });
        rewind.ResetHistory();
        RunFrames(machine, input, rewind, 1, 25);

        Assert.True(rewind.RewindTo(-100));

        Assert.Equal(0, machine.FramesExecuted);
        Assert.Equal(0, rewind.NewestFrame);
        Assert.Equal(0, rewind.OldestFrame);
    }

    [Fact]
    public void Capture_ResetsHistoryWhenTheFrameCounterJumps()
    {
        FiveDoMachine machine = CreateMachine();
        var rewind = new RewindBuffer(
            machine,
            new RewindOptions { Seconds = 30, KeyframeInterval = 10 });
        rewind.ResetHistory();
        machine.RunFrame();
        rewind.Capture(machine.FramesExecuted);
        machine.RunFrame();
        rewind.Capture(machine.FramesExecuted);
        Assert.True(rewind.CanRewind);

        rewind.Capture(500);

        Assert.False(rewind.CanRewind);
        Assert.Equal(500, rewind.OldestFrame);
        Assert.Equal(500, rewind.NewestFrame);
        Assert.Equal(1, rewind.KeyframeCount);
    }

    [Fact]
    public void RewindTo_RejectsAStaleTimelineAfterAnExternalStateLoad()
    {
        FiveDoMachine machine = CreateMachine();
        var rewind = new RewindBuffer(
            machine,
            new RewindOptions { Seconds = 30, KeyframeInterval = 10 });
        rewind.ResetHistory();
        machine.RunFrames(20);
        rewind.Capture(machine.FramesExecuted);

        using var state = new MemoryStream();
        machine.SaveState(state);
        machine.RunFrames(10);
        rewind.Capture(machine.FramesExecuted);
        state.Position = 0;
        machine.LoadState(state);

        Assert.False(rewind.CanRewind);
        Assert.False(rewind.RewindTo(5));
        Assert.Equal(0, rewind.KeyframeCount);
    }

    [Fact]
    public void MemoryLimit_KeepsAtLeastOneKeyframe()
    {
        FiveDoMachine machine = CreateMachine();
        var rewind = new RewindBuffer(
            machine,
            new RewindOptions
            {
                Seconds = 30,
                KeyframeInterval = 5,
                MemoryLimitBytes = 1,
            });
        rewind.ResetHistory();
        for (var i = 0; i < 40; i++)
        {
            machine.RunFrame();
            rewind.Capture(machine.FramesExecuted);
        }

        Assert.Equal(1, rewind.KeyframeCount);
        Assert.True(rewind.MemoryUsage > 0);
    }

    [Fact]
    public void DisabledBuffer_RecordsNothingAndCannotRewind()
    {
        FiveDoMachine machine = CreateMachine();
        var rewind = new RewindBuffer(
            machine,
            new RewindOptions { Enabled = false, Seconds = 30 });
        rewind.ResetHistory();
        machine.RunFrame();
        rewind.Capture(machine.FramesExecuted);

        Assert.False(rewind.Enabled);
        Assert.False(rewind.CanRewind);
        Assert.False(rewind.RewindTo(0));
        Assert.Equal(0, rewind.KeyframeCount);
    }

    [Fact]
    public void RewindToCurrentFrame_DoesNotChangeState()
    {
        FiveDoMachine machine = CreateMachine();
        var rewind = new RewindBuffer(
            machine,
            new RewindOptions { Seconds = 30, KeyframeInterval = 10 });
        rewind.ResetHistory();
        machine.RunFrame();
        rewind.Capture(machine.FramesExecuted);
        byte[] before = SaveStateBytes(machine);

        Assert.True(rewind.RewindTo(machine.FramesExecuted));

        Assert.Equal(before, SaveStateBytes(machine));
    }

    [Theory]
    [InlineData(29, 120, 1024)]
    [InlineData(301, 120, 1024)]
    [InlineData(60, 0, 1024)]
    [InlineData(60, 601, 1024)]
    [InlineData(60, 120, 0)]
    public void Options_RejectOutOfRangeValues(int seconds, int keyframeInterval, long memoryLimit)
    {
        var options = new RewindOptions
        {
            Seconds = seconds,
            KeyframeInterval = keyframeInterval,
            MemoryLimitBytes = memoryLimit,
        };

        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    private static void RunFrames(
        FiveDoMachine machine,
        PbusSource input,
        RewindBuffer? rewind,
        int firstFrame,
        int lastFrame)
    {
        for (var frame = firstFrame; frame <= lastFrame; frame++)
        {
            input[0] = new ControllerState(InputFor(frame));
            machine.RunFrame();
            rewind?.Capture(machine.FramesExecuted);
        }
    }

    private static ControllerButton InputFor(int frame)
    {
        return (frame % 7) switch
        {
            0 => ControllerButton.A,
            1 => ControllerButton.B,
            2 => ControllerButton.Left,
            3 => ControllerButton.Right,
            4 => ControllerButton.Up,
            5 => ControllerButton.Down,
            _ => ControllerButton.C,
        };
    }

    private static byte[] SaveStateBytes(FiveDoMachine machine)
    {
        using var stream = new MemoryStream();
        machine.SaveState(stream);
        return stream.ToArray();
    }

    private static FiveDoMachine CreateMachine()
    {
        var machine = new FiveDoMachine();
        machine.LoadBios(BuildSyntheticRom());
        machine.Reset();
        return machine;
    }

    private static byte[] BuildSyntheticRom()
    {
        var program = new[]
        {
            ArmTestAssembler.Mov(0, 0x03300000),
            ArmTestAssembler.Mov(1, 0xb0000),
            ArmTestAssembler.StrImmediate(1, 0, 0x580),
            ArmTestAssembler.Mov(2, 0x03200000),
            ArmTestAssembler.AddImmediate(2, 2, 0x2000),
            ArmTestAssembler.Mvn(3, 0),
            ArmTestAssembler.StrImmediate(3, 2),
            ArmTestAssembler.Branch(0x03000018, 0x03000018),
        };

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
