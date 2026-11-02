using FiveDo.Core;
using FiveDo.Machine;

namespace FiveDo.Tests.Machine;

public sealed class FiqLatencyTests
{
    [Fact]
    public void Machine_WiresClioRequestsToCpuEntries()
    {
        var machine = new FiveDoMachine();
        machine.Memory.Ram.Write32(0x1000, 0xe1a00000);
        machine.Cpu.ProgramCounter = 0x1000;
        machine.Clio.Poke(0x48, 1u << 9);

        FiqRequest? request = null;
        FiqEntry? entry = null;
        machine.FiqRequested = value => request = value;
        machine.FiqEntered = value => entry = value;

        machine.Clio.GenerateFiq(1u << 9, 0);
        machine.RunFrame();

        Assert.NotNull(request);
        Assert.NotNull(entry);
        Assert.Equal(0, request.Value.Cycle);
        Assert.Equal(1, entry.Value.Cycle);
        Assert.Equal(1, entry.Value.LatencyCycles);
        Assert.True(entry.Value.MatchedRequest);
        Assert.Equal(1u << 9, entry.Value.ActiveReason1);
        Assert.Equal(1, machine.FiqLatency.Requests);
        Assert.Equal(1, machine.FiqLatency.Entries);
        Assert.Equal(1, machine.FiqLatency.MatchedEntries);
        Assert.Equal(1, machine.FiqLatency.MaxLatencyCycles);
    }

    [Fact]
    public void Machine_ResetClearsTelemetryAndCycleCount()
    {
        var machine = new FiveDoMachine();
        machine.Clio.Poke(0x48, 1u << 9);
        machine.Clio.GenerateFiq(1u << 9, 0);
        machine.RunFrame();
        Assert.True(machine.FiqLatency.Requests > 0);
        Assert.True(machine.Cpu.CyclesExecuted > 0);

        machine.Reset();

        Assert.Equal(0, machine.FiqLatency.Requests);
        Assert.Equal(0, machine.FiqLatency.Entries);
        Assert.Equal(0, machine.Cpu.CyclesExecuted);
    }
}
