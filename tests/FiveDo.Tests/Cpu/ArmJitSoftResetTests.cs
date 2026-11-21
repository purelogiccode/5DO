using FiveDo.Core;
using FiveDo.Cpu;
using FiveDo.Tests.TestUtilities;

namespace FiveDo.Tests.Cpu;

/// <summary>JIT state handling when a guest store requests a CLIO soft reset.</summary>
public sealed class ArmJitSoftResetTests
{
    [Fact]
    public void Jit_SoftResetDuringStore_PreservesResetStateAndRestartsAtZero()
    {
        var bus = new SoftResetBus();
        var cpu = new Arm60(
            bus,
            tuning: new EmulatorTuning { CpuBackend = CpuBackendKind.Jit });
        bus.Cpu = cpu;

        bus.WriteInstruction(0x00000000, ArmTestAssembler.Mov(3, 0x55));
        bus.WriteInstruction(0x1000, ArmTestAssembler.StrImmediate(1, 0));
        bus.WriteInstruction(0x1004, ArmTestAssembler.Branch(0x1000, 0x1004));

        cpu.ProgramCounter = 0x1000;
        cpu.State.User[0] = 0x2000;
        cpu.State.User[1] = 0x21;

        for (var attempt = 0; attempt < 200_000 && cpu.JitBlockCount == 0; attempt++)
        {
            cpu.RunJitBlock();
        }

        Assert.True(cpu.JitBlockCount > 0, cpu.JitLastFailure ?? "no blocks compiled");

        // Point the next store at the CLIO CSTAT reset register. The next instruction
        // address is 0x1004, which SoftReset must preserve as R14_svc.
        cpu.State.User[0] = 0x03400028;
        uint interruptedCpsr = cpu.Cpsr;
        cpu.RunJitBlock();

        Assert.Equal(0u, cpu.ProgramCounter);
        Assert.Equal((interruptedCpsr & ~0x1fu) | 0x13u | 0x40u | 0x80u, cpu.Cpsr);
        Assert.Equal(0x1004u, cpu.State.User[14]);
        Assert.Equal(interruptedCpsr, cpu.State.Spsr[ArmModes.Svc]);
        Assert.Equal(0, cpu.JitBlockCount);

        cpu.Step();
        Assert.Equal(0x55u, cpu.State.User[3]);
        Assert.Equal(4u, cpu.ProgramCounter);
    }

    private sealed class SoftResetBus : IAddressBus
    {
        private readonly uint[] _memory = new uint[0x10000 / sizeof(uint)];

        public Arm60? Cpu { get; set; }

        public void WriteInstruction(uint address, uint value)
        {
            _memory[(address & 0xffff) >> 2] = value;
        }

        public uint ReadInstruction(uint address) => Read32(address);

        public uint Read32(uint address) => _memory[(address & 0xffff) >> 2];

        public byte Read8(uint address)
        {
            return (byte)(Read32(address & ~3u) >> (int)((address & 3) * 8));
        }

        public ushort Read16(uint address)
        {
            return (ushort)(Read32(address & ~2u) >> (int)((address & 2) * 8));
        }

        public void Write32(uint address, uint value)
        {
            if (address == 0x03400028)
            {
                Assert.NotNull(Cpu);
                Cpu.SoftReset();
                return;
            }

            _memory[(address & 0xffff) >> 2] = value;
        }

        public void Write8(uint address, byte value)
        {
            uint word = Read32(address & ~3u);
            var shift = (int)((address & 3) * 8);
            word = (word & ~(0xffu << shift)) | ((uint)value << shift);
            Write32(address & ~3u, word);
        }

        public void Write16(uint address, ushort value)
        {
            uint word = Read32(address & ~2u);
            var shift = (int)((address & 2) * 8);
            word = (word & ~(0xffffu << shift)) | ((uint)value << shift);
            Write32(address & ~2u, word);
        }
    }
}
