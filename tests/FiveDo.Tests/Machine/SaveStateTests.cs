using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Machine;
using FiveDo.Tests.TestUtilities;
using FiveDo.Video;

namespace FiveDo.Tests.Machine;

public sealed class SaveStateTests
{
    [Fact]
    public void SaveLoad_RunAheadMatchesControlRun()
    {
        FiveDoMachine first = CreateMachine();
        first.RunFrames(5);

        using var state = new MemoryStream();
        first.SaveState(state, "disc");
        first.RunFrames(60);
        (uint Hash, uint ProgramCounter, long FramesExecuted) expected = Snapshot(first);

        FiveDoMachine loaded = CreateMachine();
        state.Position = 0;
        loaded.LoadState(state);
        loaded.RunFrames(60);
        (uint Hash, uint ProgramCounter, long FramesExecuted) actual = Snapshot(loaded);

        Assert.Equal(expected.Hash, actual.Hash);
        Assert.Equal(expected.ProgramCounter, actual.ProgramCounter);
        Assert.Equal(expected.FramesExecuted, actual.FramesExecuted);
        Assert.Empty(loaded.StateWarnings);
    }

    [Fact]
    public void LoadState_RestoresTheParsedFrameImmediately()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(2);

        VdlLine source = machine.Frame[3];
        source.Width = 64;
        source.OutControl = 0x12345678;
        source.ClutDma = 0x9ABCDEF0;
        source.Background = 0x0BADF00D;
        for (var i = 0; i < VdlLine.MaxPixels; i++)
        {
            source.PixelBuffer[i] = (ushort)((i * 7) + 3);
        }

        source.ClutB[0] = 1;
        source.ClutG[1] = 2;
        source.ClutR[2] = 3;

        using var state = new MemoryStream();
        machine.SaveState(state);

        FiveDoMachine loaded = CreateMachine();
        state.Position = 0;
        loaded.LoadState(state);

        VdlLine restored = loaded.Frame[3];
        Assert.Equal(64, restored.Width);
        Assert.Equal(0x12345678u, restored.OutControl);
        Assert.Equal(0x9ABCDEF0u, restored.ClutDma);
        Assert.Equal(0x0BADF00Du, restored.Background);
        Assert.Equal(1, restored.ClutB[0]);
        Assert.Equal(2, restored.ClutG[1]);
        Assert.Equal(3, restored.ClutR[2]);
        Assert.Equal(source.PixelBuffer.ToArray(), restored.PixelBuffer.ToArray());
    }

    [Fact]
    public void LoadState_RejectsTruncatedData()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(2);
        using var state = new MemoryStream();
        machine.SaveState(state);
        var bytes = state.ToArray();

        Assert.Throws<InvalidStateException>(() => machine.LoadState(new MemoryStream(bytes[..20])));
        Assert.Throws<InvalidStateException>(() => machine.LoadState(new MemoryStream([])));
    }

    [Fact]
    public void LoadState_RejectsInvalidMagic()
    {
        FiveDoMachine machine = CreateMachine();
        using var stream = new MemoryStream(new byte[64]);
        Assert.Throws<InvalidStateException>(() => machine.LoadState(stream));
    }

    [Fact]
    public void LoadState_WarnsOnBiosHashMismatch()
    {
        FiveDoMachine writer = CreateMachine();
        writer.RunFrames(2);
        using var state = new MemoryStream();
        writer.SaveState(state);

        var otherBios = new byte[EmulatorConstants.RomBankSize];
        otherBios[0] = 1;
        FiveDoMachine reader = CreateMachine(otherBios);
        state.Position = 0;
        reader.LoadState(state);

        Assert.Contains(reader.StateWarnings, warning => warning.Contains("BIOS", StringComparison.Ordinal));
    }

    [Fact]
    public void SaveStateToFile_IsAtomicAndRoundtrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-state-{Guid.NewGuid():N}.tdos");
        try
        {
            FiveDoMachine machine = CreateMachine();
            machine.RunFrames(3);
            machine.SaveStateToFile(path);

            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));
            FiveDoMachine loaded = CreateMachine();
            loaded.LoadStateFromFile(path);
            Assert.Equal(machine.Cpu.ProgramCounter, loaded.Cpu.ProgramCounter);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static FiveDoMachine CreateMachine(byte[]? rom = null)
    {
        var machine = new FiveDoMachine();
        machine.LoadBios(rom ?? BuildSyntheticRom());
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
