using System.IO;
using System.Linq;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Machine;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Host;

public sealed class StateDiffTests
{
    [Fact]
    public void Compare_IdenticalStatesReportsNoChanges()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(3);
        byte[] state = Save(machine);

        StateDiffResult diff = StateDiff.Compare(state, state);

        Assert.Equal(StateFormat.Version, diff.VersionA);
        Assert.Equal(StateFormat.Version, diff.VersionB);
        Assert.Equal(0, diff.ChangedSections);
        Assert.All(diff.Sections, section => Assert.True(section.IsSame));
        Assert.Contains(diff.Sections, section => section.Id == StateSectionIds.Cpu);
    }

    [Fact]
    public void Compare_DifferentFramesReportsChangedSectionsAndOffsets()
    {
        FiveDoMachine first = CreateMachine();
        first.RunFrames(2);
        FiveDoMachine second = CreateMachine();
        second.RunFrames(5);

        StateDiffResult diff = StateDiff.Compare(Save(first), Save(second));

        Assert.True(diff.ChangedSections > 0);
        StateSectionDiff cpu = diff.Sections.Single(section => section.Id == StateSectionIds.Cpu);
        Assert.False(cpu.IsSame);
        Assert.True(cpu.FirstDifference >= 0);
        Assert.True(cpu.DifferingBytes > 0);
        Assert.NotEqual(cpu.HashA, cpu.HashB);
    }

    [Fact]
    public void Compare_ReportsAnExtraSectionInOnlyOneState()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(2);
        byte[] baseline = Save(machine);
        byte[] withExtra = StateTestHelper.AppendDeclaredSection(baseline, new UnknownSection());

        StateDiffResult diff = StateDiff.Compare(baseline, withExtra);

        StateSectionDiff extra = diff.Sections.Single(section => section.Id == 0xDEADBEEF);
        Assert.Equal(0, extra.VersionA);
        Assert.Equal(1, extra.VersionB);
        Assert.Equal(0, extra.LengthA);
        Assert.Equal(1, extra.LengthB);
        Assert.False(extra.IsSame);
        Assert.Equal(1, diff.ChangedSections);
    }

    [Fact]
    public void Compare_RejectsInvalidStateData()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(1);
        byte[] baseline = Save(machine);
        byte[] invalid = baseline.ToArray();
        invalid[0] = 0;

        Assert.Throws<InvalidStateException>(() => StateDiff.Compare(baseline, invalid));
    }

    private static byte[] Save(FiveDoMachine machine)
    {
        using var stream = new MemoryStream();
        machine.SaveState(stream);
        return stream.ToArray();
    }

    private static FiveDoMachine CreateMachine()
    {
        var machine = new FiveDoMachine();
        machine.LoadBios(new byte[EmulatorConstants.RomBankSize]);
        machine.Reset();
        return machine;
    }

    private sealed class UnknownSection : IStateSection
    {
        public uint Id => 0xDEADBEEF;

        public ushort Version => 1;

        public void Save(IStateWriter writer) => writer.WriteByte(0x5A);

        public void Load(IStateReader reader) => _ = reader.ReadByte();
    }
}
