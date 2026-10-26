using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Machine;
using FiveDo.Tests.TestUtilities;
using FiveDo.Video;

namespace FiveDo.Tests.Machine;

public sealed class FiveDoMachineTests
{
    [Fact]
    public void Reset_InitializesChips()
    {
        var machine = new FiveDoMachine();

        Assert.Equal(EmulatorConstants.RomBase, machine.Cpu.ProgramCounter);
        Assert.Equal(0x13u, machine.Cpu.Cpsr);
        Assert.Equal(0x40u, machine.Clio.Peek(0x28));
        Assert.Equal(0x80u, machine.Clio.Peek(0x400));
        Assert.Equal(64u, machine.Clio.Peek(0x220));
        Assert.Equal(0x29u, machine.Madam.Peek(0x04));
        Assert.Equal(0x01020000u, machine.Madam.Peek(0x00));
        Assert.Equal(0xfffffffeu, machine.Madam.Peek(0x574) + 2);
        Assert.Equal(0xb0000u, machine.Vdlp.HeadVdl);
        Assert.Equal(320, machine.Frame.SourceWidth);
        Assert.Equal(240, machine.Frame.SourceHeight);
    }

    [Fact]
    public void LoadBios_DetectsAnvil()
    {
        var machine = new FiveDoMachine();
        var bios = new byte[EmulatorConstants.RomBankSize];
        bios[28] = 234;

        machine.LoadBios(bios);

        Assert.Equal(30, machine.Compatibility.IsAnvil);
        Assert.True(machine.Compatibility.BiosAnvil);

        var plain = new FiveDoMachine();
        plain.LoadBios(new byte[EmulatorConstants.RomBankSize]);
        Assert.Equal(0, plain.Compatibility.IsAnvil);
        Assert.False(plain.Compatibility.BiosAnvil);
    }

    [Fact]
    public void RunFrame_InvokesSinkAndAdvancesScheduler()
    {
        var machine = new FiveDoMachine();
        var sink = new RecordingFrameSink();
        machine.FrameSink = sink;

        machine.RunFrame();

        Assert.Equal(1, sink.FrameCount);
        Assert.Equal(320, sink.LastWidth);
        Assert.Equal(240, sink.LastHeight);
        Assert.Equal(1, machine.FramesExecuted);
        Assert.True(machine.Quartz.VdCurrOverline > 0);
    }

    [Fact]
    public void RunFrame_CycleBudgetMatchesReference()
    {
        var machine = new FiveDoMachine();

        machine.RunFrame();

        var budget = EmulatorConstants.ArmClock / 60;
        Assert.True(machine.LastFrameCycles >= budget);
        Assert.True(machine.LastFrameCycles < budget + 16);
    }

    [Fact]
    public void RunFrame_ExecutesDspWithoutAudioSink()
    {
        var machine = new FiveDoMachine();
        machine.Dsp.SetRunning(true);

        machine.RunFrame();

        Assert.Null(machine.AudioSink);
        Assert.True(machine.Dsp.InstructionsExecuted > 0);
    }

    [Fact]
    public void RunFrame_DoesNotAllocateInSteadyState()
    {
        var machine = new FiveDoMachine();
        for (var i = 0; i < 10; i++)
        {
            machine.RunFrame();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 60; i++)
        {
            machine.RunFrame();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated <= 64, $"RunFrame allocated {allocated} bytes over 60 frames.");
    }

    [Fact]
    public void RunFrame_SkipFrameTriggersWithoutPresenting()
    {
        var machine = new FiveDoMachine();
        var sink = new RecordingFrameSink();
        machine.FrameSink = sink;

        machine.RunFrame(true);

        Assert.Equal(0, sink.FrameCount);
        Assert.Equal(1, sink.TriggerCount);
    }

    [Fact]
    public void SyntheticProgram_WritesSportAndProducesStableFrame()
    {
        var rom = BuildSyntheticRom();
        var first = RunSynthetic(rom);
        var second = RunSynthetic(rom);

        Assert.Equal(0xffffffffu, first.Machine.Memory.Ram.Read32(2 * 1024 * 1024));
        Assert.Equal(320, first.Machine.Frame.SourceWidth);
        Assert.Equal(240, first.Machine.Frame.SourceHeight);
        Assert.Equal(first.Hash, second.Hash);
        Assert.True(first.NonBlackPixels > 0);
    }

    [Fact]
    public void RenderFrameMultitask_RendersVisibleLines()
    {
        var machine = new FiveDoMachine();
        machine.RenderFrameMultitask();

        Assert.Equal(320, machine.Frame[239].Width);
    }

    [Fact]
    public void RealBios_Runs300FramesWhenProvided()
    {
        var path = Environment.GetEnvironmentVariable("FIVEDO_BIOS");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var machine = new FiveDoMachine();
        var sink = new RecordingFrameSink();
        machine.FrameSink = sink;
        machine.LoadBios(File.ReadAllBytes(path));
        machine.Reset();

        machine.RunFrames(300);

        Assert.Equal(300, machine.FramesExecuted);
        Assert.True(sink.FrameCount >= 300);
    }

    private static SyntheticResult RunSynthetic(byte[] rom)
    {
        var machine = new FiveDoMachine();
        var sink = new RecordingFrameSink();
        machine.FrameSink = sink;
        machine.LoadBios(rom);
        machine.Reset();
        machine.RunFrame();

        var buffer = new FrameBuffer(320, 240);
        FrameAssembler.Assemble(machine.Frame, buffer, 320, 240);

        var hash = 2166136261u;
        var nonBlack = 0;
        foreach (var value in buffer.PixelSpan)
        {
            hash = unchecked((hash ^ value) * 16777619);
            if (value != 0)
            {
                nonBlack++;
            }
        }

        return new SyntheticResult(hash, nonBlack, machine);
    }

    private sealed record SyntheticResult(uint Hash, int NonBlackPixels, FiveDoMachine Machine);

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
            ArmTestAssembler.AddImmediate(2, 2, 0x2000),
            ArmTestAssembler.StrImmediate(3, 2),
            ArmTestAssembler.Branch(0x03000024, 0x03000024),
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
