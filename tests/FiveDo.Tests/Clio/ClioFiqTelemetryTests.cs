using System.Collections.Generic;
using FiveDo.Core;
using ClioChip = FiveDo.Clio.Clio;

namespace FiveDo.Tests.Clio;

public sealed class ClioFiqTelemetryTests
{
    [Fact]
    public void Request_ReportsOnlyWhenActiveAndEnableMasksIntersect()
    {
        var clio = new ClioChip { CycleProvider = () => 42 };
        var requests = new List<FiqRequest>();
        clio.FiqRequested = requests.Add;

        clio.GenerateFiq(1u << 9, 0);
        Assert.Empty(requests);

        clio.Poke(0x48, 1u << 9);
        var request = Assert.Single(requests);
        Assert.Equal(42, request.Cycle);
        Assert.Equal(1u << 9, request.ActiveReason1);
        Assert.Equal(0u, request.ActiveReason2);
    }

    [Fact]
    public void RepeatedRaisesWhilePending_AreNotNewRequests()
    {
        var clio = new ClioChip();
        var count = 0;
        clio.FiqRequested = _ => count++;
        clio.Poke(0x48, 1u << 9);

        clio.GenerateFiq(1u << 9, 0);
        clio.GenerateFiq(1u << 9, 0);
        clio.GenerateFiq(1u << 3, 0);

        Assert.Equal(1, count);
        Assert.True(clio.NeedFiq);
    }

    [Fact]
    public void ClearingTheActiveMask_AllowsTheNextRaiseToBeANewRequest()
    {
        var clio = new ClioChip();
        var reasons = new List<uint>();
        clio.FiqRequested = request => reasons.Add(request.ActiveReason1);
        clio.Poke(0x48, 1u << 9);

        clio.GenerateFiq(1u << 9, 0);
        clio.Poke(0x44, 1u << 9);
        Assert.False(clio.NeedFiq);

        clio.GenerateFiq(1u << 4, 0);
        clio.GenerateFiq(1u << 4, 0);
        clio.Poke(0x48, 1u << 4);

        Assert.Equal([1u << 9, 1u << 4], reasons);
    }

    [Fact]
    public void HighBankReason_ReportsTheActiveHighRegister()
    {
        var clio = new ClioChip { CycleProvider = () => 7 };
        FiqRequest? captured = null;
        clio.FiqRequested = request => captured = request;
        clio.Poke(0x68, 1u << 3);

        clio.GenerateFiq(0, 1u << 3);

        Assert.NotNull(captured);
        Assert.Equal(7, captured.Value.Cycle);
        Assert.Equal(0x80000000u, captured.Value.ActiveReason1 & 0x80000000u);
        Assert.Equal(1u << 3, captured.Value.ActiveReason2);
    }

    [Fact]
    public void TimerUnderflow_ReportsOneRequestForTheLevel()
    {
        var clio = new ClioChip();
        var count = 0;
        clio.FiqRequested = _ => count++;
        clio.Poke(0x200, 0x10);
        clio.Poke(0x208, 0);
        clio.Poke(0x108, 0);
        clio.Poke(0x10c, 0);
        clio.Poke(0x48, 1u << 10);

        clio.DoTimers();
        clio.DoTimers();

        Assert.Equal(1, count);
        Assert.Equal(1u << 10, clio.ActiveFiqReason1);
    }

    [Fact]
    public void Reset_ClearsThePendingTransitionSoTheNextRequestIsReported()
    {
        var clio = new ClioChip();
        var count = 0;
        clio.FiqRequested = _ => count++;
        clio.Poke(0x48, 1u << 9);
        clio.GenerateFiq(1u << 9, 0);
        Assert.Equal(1, count);

        clio.Reset();

        clio.GenerateFiq(1u << 9, 0);
        clio.Poke(0x48, 1u << 9);

        Assert.Equal(2, count);
    }

    [Fact]
    public void Load_DoesNotSynthesizeARequestForTheRestoredLevel()
    {
        var source = new ClioChip();
        source.Poke(0x48, 1u << 9);
        source.GenerateFiq(1u << 9, 0);
        var writer = new StateWriter();
        source.Save(writer);

        var target = new ClioChip();
        var count = 0;
        target.FiqRequested = _ => count++;
        target.Load(new StateReader(writer.ToArray()));

        Assert.Equal(0, count);
        Assert.True(target.NeedFiq);

        target.Poke(0x44, 1u << 9);
        target.GenerateFiq(1u << 9, 0);

        Assert.Equal(1, count);
    }
}
