using System;
using System.IO;
using FiveDo.Diagnostics;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Diagnostics;

public sealed class DiagnosticToolTests
{
    [Fact]
    public void CpuTracer_StepsAndReportsInstruction()
    {
        var rig = new ArmTestRig();
        rig.LoadProgram(ArmTestAssembler.Mov(0, 1), ArmTestAssembler.Mov(1, 2));

        CpuTraceEntry first = CpuTracer.Step(rig.Cpu, rig.Memory.Bus);
        CpuTraceEntry second = CpuTracer.Step(rig.Cpu, rig.Memory.Bus);

        Assert.Equal(0x1000u, first.Pc);
        Assert.StartsWith("mov", first.Disassembly, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0x1004u, second.Pc);
        Assert.Equal(1u, rig.Cpu.ReadRegister(0));
        Assert.Equal(2u, rig.Cpu.ReadRegister(1));
    }

    [Fact]
    public void MemoryDumper_WritesRequestedRange()
    {
        var rig = new ArmTestRig();
        rig.Memory.Ram.Write8(0, 1);
        rig.Memory.Ram.Write8(1, 2);
        rig.Memory.Ram.Write8(2, 3);
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-mem-{Guid.NewGuid():N}.bin");
        try
        {
            MemoryDumper.WriteRam(rig.Memory.Ram, 0, 4, path);
            Assert.Equal(new byte[] { 1, 2, 3, 0 }, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
