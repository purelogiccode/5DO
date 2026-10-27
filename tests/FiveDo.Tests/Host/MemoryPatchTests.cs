using System;
using System.Collections.Generic;
using FiveDo.Host;
using FiveDo.Machine;

namespace FiveDo.Tests.Host;

public sealed class MemoryPatchTests
{
    [Fact]
    public void Parse_ReadsHexAddressesAndByteLists()
    {
        IReadOnlyList<MemoryPatchEntry> entries = MemoryPatch.Parse("0x96574=4e 4c,4b;0x1000=00ff");

        Assert.Equal(2, entries.Count);
        Assert.Equal(0x96574u, entries[0].Address);
        Assert.Equal(new byte[] { 0x4e, 0x4c, 0x4b }, entries[0].Bytes);
        Assert.Equal(0x1000u, entries[1].Address);
        Assert.Equal(new byte[] { 0x00, 0xff }, entries[1].Bytes);
    }

    [Fact]
    public void Apply_WritesBytesToMachineRam()
    {
        var machine = new FiveDoMachine();

        var written = MemoryPatch.Apply(machine, "0x2000=12 34 56");

        Assert.Equal(3, written);
        Assert.Equal(0x12, machine.Memory.Ram.Read8(0x2000));
        Assert.Equal(0x34, machine.Memory.Ram.Read8(0x2001));
        Assert.Equal(0x56, machine.Memory.Ram.Read8(0x2002));
    }

    [Fact]
    public void Parse_RejectsMalformedSpecifications()
    {
        Assert.Throws<FormatException>(() => MemoryPatch.Parse("0x1000"));
        Assert.Throws<FormatException>(() => MemoryPatch.Parse("0x1000="));
        Assert.Throws<FormatException>(() => MemoryPatch.Parse("nope=00"));
        Assert.Throws<FormatException>(() => MemoryPatch.Parse("0x1000=xyz"));
        Assert.Throws<FormatException>(() => MemoryPatch.Parse(";"));
    }
}
