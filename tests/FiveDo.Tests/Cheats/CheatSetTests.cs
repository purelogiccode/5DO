using System;
using System.IO;
using FiveDo.Cheats;
using FiveDo.Input;
using FiveDo.Machine;

namespace FiveDo.Tests.Cheats;

public sealed class CheatSetTests
{
    [Fact]
    public void RawWrite_AppliesByteSequence()
    {
        var machine = new FiveDoMachine();
        var set = new CheatSet();
        set.Add("raw", string.Empty, "0x2000 = 12 34 56", enabled: true);

        var writes = set.Apply(machine);

        Assert.Equal(3, writes);
        Assert.Equal(0x12, machine.Memory.Ram.Read8(0x2000));
        Assert.Equal(0x34, machine.Memory.Ram.Read8(0x2001));
        Assert.Equal(0x56, machine.Memory.Ram.Read8(0x2002));
    }

    [Fact]
    public void BareHexLetters_AreAcceptedAsValues()
    {
        var machine = new FiveDoMachine();
        var set = new CheatSet();
        set.Add("bare", string.Empty, "0x2000 = FF 00; write8 0x2002 = AB", enabled: true);

        set.Apply(machine);

        Assert.Equal(0xff, machine.Memory.Ram.Read8(0x2000));
        Assert.Equal(0x00, machine.Memory.Ram.Read8(0x2001));
        Assert.Equal(0xab, machine.Memory.Ram.Read8(0x2002));
    }

    [Fact]
    public void TypedWrites_UseGuestWordOrder()
    {
        var machine = new FiveDoMachine();
        var set = new CheatSet();
        set.Add(
            "typed",
            string.Empty,
            "write16 0x2000 = 0x1234; write32 0x2010 = 0x89abcdef",
            enabled: true);

        set.Apply(machine);

        Assert.Equal(0x1234, machine.Memory.Ram.Read16(0x2000));
        Assert.Equal(0x89abcdefu, machine.Memory.Ram.Read32(0x2010));
        Assert.Equal(0x34, machine.Memory.Ram.Read8(0x2000));
        Assert.Equal(0x12, machine.Memory.Ram.Read8(0x2001));
    }

    [Fact]
    public void ConditionalWrite_OnlyRunsWhenMemoryMatches()
    {
        var machine = new FiveDoMachine();
        machine.Memory.Ram.Write32(0x3000, 1);
        var set = new CheatSet();
        set.Add("conditional", string.Empty, "if 0x3000 == 1 then write8 0x3004 = 0xaa", enabled: true);

        Assert.Equal(1, set.Apply(machine));
        Assert.Equal(0xaa, machine.Memory.Ram.Read8(0x3004));

        machine.Memory.Ram.Write8(0x3004, 0);
        machine.Memory.Ram.Write32(0x3000, 0);
        Assert.Equal(0, set.Apply(machine));
        Assert.Equal(0, machine.Memory.Ram.Read8(0x3004));
    }

    [Fact]
    public void ButtonCondition_UsesPlayerOnePacket()
    {
        var machine = new FiveDoMachine();
        var set = new CheatSet();
        set.Add("button", string.Empty, "if button A then write8 0x4000 = 0x55", enabled: true);

        Span<byte> packet = stackalloc byte[16];
        PbusEncoder.Encode([new ControllerState(ControllerButton.A)], packet);
        machine.Madam.KeyPressed(packet);
        Assert.Equal(1, set.Apply(machine));
        Assert.Equal(0x55, machine.Memory.Ram.Read8(0x4000));

        machine.Memory.Ram.Write8(0x4000, 0);
        packet.Clear();
        machine.Madam.KeyPressed(packet);
        Assert.Equal(0, set.Apply(machine));
        Assert.Equal(0, machine.Memory.Ram.Read8(0x4000));
    }

    [Fact]
    public void LogicalAndNegatedConditions_AreEvaluated()
    {
        var machine = new FiveDoMachine();
        machine.Memory.Ram.Write32(0x5000, 2);
        var set = new CheatSet();
        set.Add(
            "logic",
            string.Empty,
            "if not (0x5000 == 1) and button B then write8 0x5004 = 0x7f",
            enabled: true);

        Span<byte> packet = stackalloc byte[16];
        PbusEncoder.Encode([new ControllerState(ControllerButton.B)], packet);
        machine.Madam.KeyPressed(packet);

        Assert.Equal(1, set.Apply(machine));
        Assert.Equal(0x7f, machine.Memory.Ram.Read8(0x5004));
    }

    [Fact]
    public void InvalidCode_IsReportedAndSkipped()
    {
        var document = new CheatDocument
        {
            Cheats =
            [
                new CheatEntry
                {
                    Name = "too wide",
                    Code = "write8 0x1000 = 0x1ff",
                    Enabled = true,
                },
            ],
        };

        CheatSet set = CheatSet.FromDocument(document);

        Assert.Single(set.Entries);
        Assert.True(set.Entries[0].HasError);
        Assert.Equal(0, set.ActiveCount);
        Assert.Equal(0, set.Apply(new FiveDoMachine()));
    }

    [Fact]
    public void DisabledCode_IsNotApplied()
    {
        var machine = new FiveDoMachine();
        var set = new CheatSet();
        set.Add("disabled", string.Empty, "write8 0x6000 = 0x99", enabled: false);

        Assert.Equal(0, set.Apply(machine));
        Assert.Equal(0, machine.Memory.Ram.Read8(0x6000));
    }

    [Fact]
    public void StateLoad_ReappliesConfiguredCheat()
    {
        var machine = new FiveDoMachine();
        machine.LoadBios(new byte[1_048_576]);
        machine.Reset();
        var set = new CheatSet();
        set.Add("health", string.Empty, "write8 0x7000 = 0x64", enabled: true);
        set.Apply(machine);

        using var state = new MemoryStream();
        machine.SaveState(state);
        machine.Memory.Ram.Write8(0x7000, 0);
        state.Position = 0;
        machine.LoadState(state);
        set.Apply(machine);

        Assert.Equal(0x64, machine.Memory.Ram.Read8(0x7000));
    }

    [Fact]
    public void BeforeFrame_AppliesCheatOnEveryFrame()
    {
        var machine = new FiveDoMachine();
        machine.LoadBios(new byte[1_048_576]);
        machine.Reset();
        var set = new CheatSet();
        set.Add("hook", string.Empty, "write8 0x7000 = 0x11", enabled: true);
        machine.BeforeFrame = () => set.Apply(machine);

        machine.RunFrame();

        Assert.Equal(0x11, machine.Memory.Ram.Read8(0x7000));
        Assert.True(set.Writes >= 1);
    }

    [Fact]
    public void Add_RejectsDuplicateNamesAndMalformedCode()
    {
        var set = new CheatSet();
        set.Add("health", string.Empty, "write8 0x1000 = 1", enabled: false);

        Assert.Throws<ArgumentException>(() => set.Add(
            "HEALTH",
            string.Empty,
            "write8 0x1000 = 2",
            enabled: false));
        Assert.Throws<FormatException>(() => set.Add(
            "broken",
            string.Empty,
            "if 0x1000 == then write8 0x1000 = 1",
            enabled: false));
    }
}
