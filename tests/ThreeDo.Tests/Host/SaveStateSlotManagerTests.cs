using System;
using System.IO;
using ThreeDo.Core;
using ThreeDo.Host;
using ThreeDo.Machine;
using ThreeDo.Tests.TestUtilities;

namespace ThreeDo.Tests.Host;

public sealed class SaveStateSlotManagerTests
{
    [Fact]
    public void SaveAndLoad_RoundtripThroughASlot()
    {
        using var directory = new TempDirectory();
        var manager = new SaveStateSlotManager(directory.Path, slotCount: 4);
        var machine = CreateMachine();
        machine.RunFrames(3);

        var saved = manager.Save(machine, "9F7D72BC", "Alone in the Dark", slot: 2);

        Assert.Equal(2, saved.Slot);
        Assert.True(saved.Exists);
        Assert.Equal(3, saved.FramesExecuted);
        Assert.Equal("9F7D72BC", saved.GameId);
        Assert.Equal("Alone in the Dark", saved.GameName);
        Assert.NotNull(saved.SavedAtUtc);
        Assert.True(File.Exists(manager.GetStatePath(2)));
        Assert.True(File.Exists(manager.GetMetadataPath(2)));
        Assert.False(File.Exists(manager.GetStatePath(2) + ".tmp"));
        Assert.False(File.Exists(manager.GetMetadataPath(2) + ".tmp"));
        Assert.Equal(2, manager.CurrentSlot);

        var expectedPc = machine.Cpu.ProgramCounter;
        machine.RunFrames(10);

        var loaded = manager.Load(machine, slot: 2);
        Assert.Equal(3, loaded.FramesExecuted);
        Assert.Equal(expectedPc, machine.Cpu.ProgramCounter);
        Assert.Equal(3, machine.FramesExecuted);
    }

    [Fact]
    public void List_ReportsEmptyAndCorruptSlotsWithoutThrowing()
    {
        using var directory = new TempDirectory();
        var manager = new SaveStateSlotManager(directory.Path, slotCount: 3);
        var machine = CreateMachine();
        manager.Save(machine, "d7672550", "Road Rash", slot: 1);
        File.WriteAllBytes(manager.GetStatePath(0), [1, 2, 3, 4]);

        var slots = manager.List();

        Assert.Equal(3, slots.Count);
        Assert.False(slots[2].Exists);
        Assert.Equal(-1, slots[2].FramesExecuted);
        Assert.True(slots[1].Exists);
        Assert.Equal("Road Rash", slots[1].GameName);
        Assert.True(slots[0].Exists);
        Assert.Equal(-1, slots[0].FramesExecuted);
        Assert.NotEmpty(slots[0].Error);
    }

    [Fact]
    public void NextAndPrevious_WrapAndCanSkipEmptySlots()
    {
        using var directory = new TempDirectory();
        var manager = new SaveStateSlotManager(directory.Path, slotCount: 4);
        var machine = CreateMachine();
        manager.Save(machine, gameId: "a", gameName: "A", slot: 1);
        manager.Save(machine, gameId: "b", gameName: "B", slot: 3);

        manager.Select(0);
        Assert.Equal(1, manager.Next());
        Assert.Equal(2, manager.Next());
        Assert.Equal(3, manager.Next());
        Assert.Equal(0, manager.Next());
        Assert.Equal(3, manager.Previous());

        manager.Select(0);
        Assert.Equal(1, manager.Next(existingOnly: true));
        Assert.Equal(3, manager.Next(existingOnly: true));
        Assert.Equal(1, manager.Next(existingOnly: true));
        Assert.Equal(3, manager.Previous(existingOnly: true));
    }

    [Fact]
    public void Delete_RemovesStateAndMetadata()
    {
        using var directory = new TempDirectory();
        var manager = new SaveStateSlotManager(directory.Path, slotCount: 2);
        var machine = CreateMachine();
        manager.Save(machine, slot: 0);

        Assert.True(manager.Delete(0));
        Assert.False(manager.Exists(0));
        Assert.False(File.Exists(manager.GetMetadataPath(0)));
        Assert.False(manager.Delete(0));
    }

    [Fact]
    public void Load_EmptySlotThrowsFileNotFound()
    {
        using var directory = new TempDirectory();
        var manager = new SaveStateSlotManager(directory.Path, slotCount: 2);
        var machine = CreateMachine();

        Assert.Throws<FileNotFoundException>(() => manager.Load(machine, slot: 1));
    }

    [Fact]
    public void Slots_AreNumberedAndValidateTheirRange()
    {
        using var directory = new TempDirectory();
        var manager = new SaveStateSlotManager(directory.Path, slotCount: 2);

        Assert.EndsWith("slot-00.tdos", manager.GetStatePath(0), StringComparison.Ordinal);
        Assert.EndsWith("slot-01.json", manager.GetMetadataPath(1), StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => manager.Select(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => manager.Select(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => manager.GetStatePath(2));
    }

    private static ThreeDoMachine CreateMachine()
    {
        var machine = new ThreeDoMachine();
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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"threedo-slots-{Guid.NewGuid():N}");
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
