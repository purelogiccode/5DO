using System;
using System.IO;
using FiveDo.Memory;
using FiveDo.Tests.TestUtilities;
using ClioChip = FiveDo.Clio.Clio;
using MadamChip = FiveDo.Madam.Madam;

namespace FiveDo.Tests.Clio;

public sealed class ClioDmaAuditTests
{
    [Fact]
    public void OutputFifo_WithoutTheChannelRequestBitDetachesAtTheEnd()
    {
        var memory = new FiveDoMemory();
        var clio = new ClioChip { Ram = memory.Ram };
        clio.SetFifo(0x500, 0x3000);
        clio.SetFifo(0x504, 0);
        clio.SetFifo(0x508, 0x3100);
        clio.SetFifo(0x50c, 0);

        clio.WriteOutputFifo(0, 0x1111);
        clio.WriteOutputFifo(0, 0x2222);
        clio.WriteOutputFifo(0, 0x3333);

        Assert.Equal(0u, clio.FifoStruct(0x500));
        Assert.Equal(0x3100u, clio.FifoStruct(0x508));
        clio.WriteOutputFifo(0, 0x4444);
        Assert.Equal(0u, memory.Ram.Read32(0x3100));
    }

    [Fact]
    public void OutputFifo_ReloadsWhenTheChannelRequestBitIsSet()
    {
        var memory = new FiveDoMemory();
        var clio = new ClioChip { Ram = memory.Ram };
        clio.Poke(0x304, 1u);
        clio.SetFifo(0x500, 0x3000);
        clio.SetFifo(0x504, 0);
        clio.SetFifo(0x508, 0x3100);
        clio.SetFifo(0x50c, 0);

        clio.WriteOutputFifo(0, 0x1111);
        clio.WriteOutputFifo(0, 0x2222);
        clio.WriteOutputFifo(0, 0x3333);

        Assert.Equal(0x3100u, clio.FifoStruct(0x500));
        Assert.Equal(4u, clio.FifoStruct(0x504));

        clio.WriteOutputFifo(0, 0x4444);
        Assert.Equal(0x4444, memory.Ram.Read16(0x3102));
    }

    [Fact]
    public void XbusDma_IsBlockedByADipirLatchUntilItIsCleared()
    {
        var memory = new FiveDoMemory();
        var madam = new MadamChip();
        madam.Poke(0x540, 0x1000);
        madam.Poke(0x544, 3);
        var xbus = new FakeClioXbus();
        xbus.EnqueueData(0x11, 0x22, 0x33, 0x44);
        var clio = new ClioChip
        {
            Ram = memory.Ram,
            Madam = madam,
            Xbus = xbus,
            OperaDipirEnabled = true,
        };
        clio.XbusMediaAccess(0);

        clio.Poke(0x304, 0x100000);

        Assert.Equal(0u, memory.Ram.Read32(0x1000));
        Assert.Equal(3u, madam.Peek(0x544));
        Assert.Equal(0u, clio.Peek(0x40) & (1u << 29));
        Assert.Equal(0x100000u, clio.Peek(0x304) & 0x100000u);

        clio.Poke(0x410, 0);
        clio.Poke(0x304, 0x100000);

        Assert.Equal(0x44u, memory.Ram.Read8(0x1000));
        Assert.Equal(0x33u, memory.Ram.Read8(0x1001));
        Assert.Equal(0x22u, memory.Ram.Read8(0x1002));
        Assert.Equal(0x11u, memory.Ram.Read8(0x1003));
        Assert.Equal(0xFFFFFFFCu, madam.Peek(0x544));
        Assert.Equal(1u << 29, clio.Peek(0x40) & (1u << 29));
        Assert.Equal(0u, clio.Peek(0x304) & 0x100000u);
    }

    [Theory]
    [InlineData(-4, 0)]
    [InlineData(0, 1)]
    [InlineData(4, 2)]
    [InlineData(8, 3)]
    [InlineData(16, 5)]
    public void XbusDma_TransferLengthMatchesTheFreeDoLoop(int length, int words)
    {
        var memory = new FiveDoMemory();
        var madam = new MadamChip();
        madam.Poke(0x540, 0x1000);
        madam.Poke(0x544, unchecked((uint)length));
        var xbus = new FakeClioXbus();
        for (var i = 0; i < 32; i++)
        {
            xbus.EnqueueData((uint)i);
        }

        var clio = new ClioChip { Ram = memory.Ram, Madam = madam, Xbus = xbus };
        clio.Poke(0x304, 0x100000);

        for (var i = 0; i < words; i++)
        {
            var first = (uint)(i * 4);
            var expected = (first + 3)
                | ((first + 2) << 8)
                | ((first + 1) << 16)
                | (first << 24);
            Assert.Equal(expected, memory.Ram.Read32(0x1000 + ((uint)i * 4)));
        }

        Assert.Equal(0u, memory.Ram.Read32(0x1000 + ((uint)words * 4)));
        Assert.Equal(0xFFFFFFFCu, madam.Peek(0x544));
        Assert.Equal(1u << 29, clio.Peek(0x40) & (1u << 29));
    }

    [Fact]
    public void XbusDma_TracesTheStartCompletionAndBlockedPaths()
    {
        var memory = new FiveDoMemory();
        var madam = new MadamChip();
        madam.Poke(0x540, 0x1000);
        madam.Poke(0x544, 3);
        var xbus = new FakeClioXbus();
        xbus.EnqueueData(0x11, 0x22, 0x33, 0x44);
        var trace = new StringWriter();
        var clio = new ClioChip
        {
            Ram = memory.Ram,
            Madam = madam,
            Xbus = xbus,
            TraceWriter = trace,
            FrameProvider = () => 7,
            OperaDipirEnabled = true,
        };
        clio.XbusMediaAccess(0);

        clio.Poke(0x304, 0x100000);
        Assert.Contains("clio dma-blocked f=7", trace.ToString(), StringComparison.Ordinal);

        clio.Poke(0x410, 0);
        clio.Poke(0x304, 0x100000);

        var text = trace.ToString();
        Assert.Contains("clio dma f=7", text, StringComparison.Ordinal);
        Assert.Contains("clio dma-done f=7", text, StringComparison.Ordinal);
        Assert.Contains("target=00001004", text, StringComparison.Ordinal);
        Assert.Contains("fiq=29", text, StringComparison.Ordinal);
    }
}
