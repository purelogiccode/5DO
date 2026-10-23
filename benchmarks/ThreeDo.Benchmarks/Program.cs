using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ThreeDo.Core;
using ThreeDo.Cpu;
using ThreeDo.Machine;
using ThreeDo.Storage;
using ThreeDo.Video;

namespace ThreeDo.Benchmarks;

/// <summary>
/// Measures the steady-state cost of one emulated frame and the CPU-side presentation stages.
/// </summary>
/// <remarks>
/// The benchmark is intentionally dependency-free and deterministic. Without <c>--bios</c> it
/// runs a tiny synthetic ARM program, so CI can gate the core loop without any copyrighted
/// material. With <c>--bios</c> and <c>--disc</c> it measures a real title's boot path.
/// </remarks>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly Action<VdlFrame> NoOp = _ => { };

    private static int Main(string[] args)
    {
        var frames = 600;
        string? biosPath = null;
        string? discPath = null;
        var json = false;
        var label = "synthetic";

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--frames" when i + 1 < args.Length:
                    frames = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--bios" when i + 1 < args.Length:
                    biosPath = args[++i];
                    label = "disc";
                    break;
                case "--disc" when i + 1 < args.Length:
                    discPath = args[++i];
                    break;
                case "--json":
                    json = true;
                    break;
                case "--label" when i + 1 < args.Length:
                    label = args[++i];
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    return 2;
            }
        }

        if (frames < 1)
        {
            Console.Error.WriteLine("--frames must be at least 1.");
            return 2;
        }

        IDiscSource? disc = null;
        ThreeDoMachine machine;
        if (biosPath is null)
        {
            machine = CreateSyntheticMachine();
        }
        else
        {
            var bios = BiosImage.Load(biosPath);
            machine = new ThreeDoMachine();
            machine.LoadBios(bios.Bios1.Span, bios.Bios2.Span);
            if (discPath is not null)
            {
                disc = DiscImageFactory.Open(discPath);
                machine.InsertDisc(disc);
            }

            machine.Reset();
        }

        using (disc)
        {
            var uploader = new VdlFrameUploader();
            var mailbox = new VdlFrameMailbox();
            var presentFrame = new VdlFrame();

            // Warm up JIT, the DSP tables, and the video packers.
            for (var i = 0; i < 10; i++)
            {
                machine.RunFrame();
                uploader.Update(machine.Frame);
                mailbox.Write(machine.Frame);
            }

            var coreTicks = 0L;
            var packTicks = 0L;
            var mailboxTicks = 0L;
            var readTicks = 0L;
            var startInstructions = machine.Cpu.InstructionsExecuted;
            var startFrames = machine.FramesExecuted;

            for (var i = 0; i < frames; i++)
            {
                var t0 = Stopwatch.GetTimestamp();
                machine.RunFrame();
                var t1 = Stopwatch.GetTimestamp();
                uploader.Update(machine.Frame);
                var t2 = Stopwatch.GetTimestamp();
                mailbox.Write(machine.Frame);
                var t3 = Stopwatch.GetTimestamp();
                mailbox.Read(presentFrame, NoOp);
                var t4 = Stopwatch.GetTimestamp();

                coreTicks += t1 - t0;
                packTicks += t2 - t1;
                mailboxTicks += t3 - t2;
                readTicks += t4 - t3;
            }

            var frequency = (double)Stopwatch.Frequency;
            var coreMs = coreTicks * 1000.0 / frequency;
            var packMs = packTicks * 1000.0 / frequency;
            var mailboxMs = mailboxTicks * 1000.0 / frequency;
            var readMs = readTicks * 1000.0 / frequency;
            var totalMs = coreMs + packMs + mailboxMs + readMs;
            var instructions = machine.Cpu.InstructionsExecuted - startInstructions;
            var elapsedFrames = machine.FramesExecuted - startFrames;

            var result = new
            {
                ok = true,
                label,
                frames = elapsedFrames,
                core = new
                {
                    totalMs = Round(coreMs),
                    msPerFrame = Round(coreMs / elapsedFrames),
                    fps = Round(elapsedFrames / (coreMs / 1000.0)),
                    millionInstructionsPerSecond = Round(instructions / (coreMs / 1000.0) / 1_000_000.0),
                    instructionsPerFrame = instructions / elapsedFrames,
                },
                pack = new
                {
                    totalMs = Round(packMs),
                    microsecondsPerFrame = Round(packMs * 1000.0 / elapsedFrames),
                },
                mailbox = new
                {
                    totalMs = Round(mailboxMs),
                    microsecondsPerFrame = Round(mailboxMs * 1000.0 / elapsedFrames),
                },
                read = new
                {
                    totalMs = Round(readMs),
                    microsecondsPerFrame = Round(readMs * 1000.0 / elapsedFrames),
                },
                total = new
                {
                    msPerFrame = Round(totalMs / elapsedFrames),
                    fps = Round(elapsedFrames / (totalMs / 1000.0)),
                    budget60Ms = 16.667,
                    headroom = Round(16.667 - (totalMs / elapsedFrames)),
                },
                machine = new
                {
                    cyclesPerFrame = machine.LastFrameCycles,
                    pc = machine.Cpu.ProgramCounter,
                    frameHash = FrameHash(machine),
                },
            };

            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            }
            else
            {
                Console.WriteLine($"benchmark:   {label}");
                Console.WriteLine($"frames:      {elapsedFrames}");
                Console.WriteLine($"core:        {result.core.msPerFrame:F3} ms/frame  ({result.core.fps:F1} fps, {result.core.millionInstructionsPerSecond:F1} MIPS)");
                Console.WriteLine($"video pack:  {result.pack.microsecondsPerFrame:F1} us/frame");
                Console.WriteLine($"mailbox:     {result.mailbox.microsecondsPerFrame:F1} us/frame");
                Console.WriteLine($"read copy:   {result.read.microsecondsPerFrame:F1} us/frame");
                Console.WriteLine($"total:       {result.total.msPerFrame:F3} ms/frame  ({result.total.fps:F1} fps)");
                Console.WriteLine($"60 Hz budget:{result.total.headroom,8:F3} ms headroom");
                Console.WriteLine($"frame hash:  {result.machine.frameHash:x8}");
            }
        }

        return 0;
    }

    private static double Round(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private static uint FrameHash(ThreeDoMachine machine)
    {
        var hash = 2166136261u;
        var frame = machine.Frame;
        for (var y = 0; y < frame.SourceHeight; y++)
        {
            var line = frame[y];
            var pixels = line.Pixels;
            for (var x = 0; x < line.Width; x++)
            {
                hash = (hash ^ pixels[x]) * 16777619u;
            }
        }

        return hash;
    }

    private static ThreeDoMachine CreateSyntheticMachine()
    {
        var program = new[]
        {
            Arm.Mov(0, 0x03300000),
            Arm.Mov(1, 0xb0000),
            Arm.StrImmediate(1, 0, 0x580),
            Arm.Mov(2, 0x03200000),
            Arm.AddImmediate(2, 2, 0x2000),
            Arm.Mvn(3, 0),
            Arm.StrImmediate(3, 2, 0),
            Arm.Branch(0x0300001c, 0x03000018),
        };

        var rom = new byte[EmulatorConstants.RomBankSize];
        for (var i = 0; i < program.Length; i++)
        {
            var word = program[i];
            rom[i * 4] = (byte)(word >> 24);
            rom[(i * 4) + 1] = (byte)(word >> 16);
            rom[(i * 4) + 2] = (byte)(word >> 8);
            rom[(i * 4) + 3] = (byte)word;
        }

        var machine = new ThreeDoMachine();
        machine.LoadBios(rom);
        machine.Reset();
        return machine;
    }
}

/// <summary>Minimal ARM encoder for the synthetic benchmark ROM.</summary>
internal static class Arm
{
    private const uint Al = 14;

    internal static uint Mov(int rd, uint value) => DpImmediate(13, rd, 0, value);

    internal static uint Mvn(int rd, uint value) => DpImmediate(15, rd, 0, value);

    internal static uint AddImmediate(int rd, int rn, uint value) => DpImmediate(4, rd, rn, value);

    internal static uint StrImmediate(int rd, int rn, int offset)
    {
        return 0x04000000u | (1u << 24) | (1u << 23) | ((uint)rn << 16) | ((uint)rd << 12) | ((uint)offset & 0xfff);
    }

    internal static uint Branch(uint target, uint pc)
    {
        var offset = unchecked((target - (pc + 8)) >> 2) & 0xffffff;
        return (Al << 28) | 0x0a000000u | offset;
    }

    private static uint DpImmediate(int opcode, int rd, int rn, uint immediate)
    {
        return (Al << 28) | 0x02000000u | ((uint)opcode << 21) | ((uint)rn << 16) | ((uint)rd << 12) | EncodeImmediate(immediate);
    }

    private static uint EncodeImmediate(uint value)
    {
        for (var rotate = 0; rotate < 16; rotate++)
        {
            var candidate = RotateLeft(value, rotate * 2);
            if ((candidate & ~0xffu) == 0)
            {
                return ((uint)rotate << 8) | candidate;
            }
        }

        throw new ArgumentException($"The value 0x{value:x8} cannot be encoded as an ARM immediate.");
    }

    private static uint RotateLeft(uint value, int count)
    {
        count &= 31;
        return count == 0 ? value : (value << count) | (value >> (32 - count));
    }
}
