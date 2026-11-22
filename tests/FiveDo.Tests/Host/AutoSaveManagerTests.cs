using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Machine;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Host;

public sealed class AutoSaveManagerTests
{
    [Fact]
    public void SaveAndLoad_RoundtripsAcrossMachines()
    {
        using var directory = new TempDirectory();
        var manager = new AutoSaveManager(directory.Path);
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(3);

        SaveStateSlotInfo saved = manager.Save(machine, "9F7D72BC", "Alone in the Dark");
        string biosHash = StateInspector.ReadHeader(manager.StatePath).BiosHash;

        Assert.True(manager.Exists);
        Assert.Equal(3, saved.FramesExecuted);
        Assert.EndsWith(
            Path.Combine("autosave", "slot-00.tdos"),
            manager.StatePath,
            StringComparison.Ordinal);

        // A second manager simulates a clean exit and the next launch.
        var relaunched = new AutoSaveManager(directory.Path);
        FiveDoMachine nextMachine = CreateMachine();
        AutoSaveLoadResult result = relaunched.TryLoad(
            nextMachine,
            "9F7D72BC",
            biosHash,
            out byte[]? progress);

        Assert.True(result.Loaded);
        Assert.Null(progress);
        Assert.Equal(3, nextMachine.FramesExecuted);
        Assert.Empty(nextMachine.StateWarnings);
        Assert.Contains("frame 3", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryLoad_RefusesADifferentDiscWithoutTouchingTheMachine()
    {
        using var directory = new TempDirectory();
        var manager = new AutoSaveManager(directory.Path);
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(3);
        manager.Save(machine, "9F7D72BC", "Alone in the Dark");
        string biosHash = StateInspector.ReadHeader(manager.StatePath).BiosHash;
        machine.RunFrames(10);

        AutoSaveLoadResult result = manager.TryLoad(
            machine,
            "D7672550",
            biosHash,
            out _);

        Assert.False(result.Loaded);
        Assert.Contains("different disc", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(13, machine.FramesExecuted);
    }

    [Fact]
    public void TryLoad_RefusesADifferentBiosWithoutTouchingTheMachine()
    {
        using var directory = new TempDirectory();
        var manager = new AutoSaveManager(directory.Path);
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(3);
        manager.Save(machine, "9F7D72BC", "Alone in the Dark");
        machine.RunFrames(10);

        AutoSaveLoadResult result = manager.TryLoad(
            machine,
            "9F7D72BC",
            "00000000",
            out _);

        Assert.False(result.Loaded);
        Assert.Contains("different BIOS", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(13, machine.FramesExecuted);
    }

    [Fact]
    public void TryLoad_ReportsACorruptAutosave()
    {
        using var directory = new TempDirectory();
        var manager = new AutoSaveManager(directory.Path);
        FiveDoMachine machine = CreateMachine();
        manager.Save(machine, "9F7D72BC", "Alone in the Dark");
        File.WriteAllBytes(manager.StatePath, [1, 2, 3, 4]);

        AutoSaveLoadResult result = manager.TryLoad(machine, "9F7D72BC", string.Empty, out _);

        Assert.False(result.Loaded);
        Assert.Contains("could not be read", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Save_OverwritesTheRollingAutosave()
    {
        using var directory = new TempDirectory();
        var manager = new AutoSaveManager(directory.Path);
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(3);
        manager.Save(machine, "9F7D72BC", "Alone in the Dark");
        machine.RunFrames(7);

        SaveStateSlotInfo saved = manager.Save(machine, "9F7D72BC", "Alone in the Dark");

        Assert.Equal(10, saved.FramesExecuted);
        Assert.Equal(10, manager.Inspect().FramesExecuted);
        Assert.Single(
            Directory.GetFiles(manager.Directory, "slot-00.tdos", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void Delete_RemovesTheAutosave()
    {
        using var directory = new TempDirectory();
        var manager = new AutoSaveManager(directory.Path);
        FiveDoMachine machine = CreateMachine();
        manager.Save(machine, "9F7D72BC", "Alone in the Dark");

        Assert.True(manager.Delete());
        Assert.False(manager.Exists);
        Assert.False(manager.Delete());
        Assert.Equal("No autosave is available.", manager.TryLoad(machine, "9F7D72BC", null, out _).Message);
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

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fivedo-autosave-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
