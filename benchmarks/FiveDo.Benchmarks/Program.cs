using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using FiveDo.Core;
using FiveDo.Diagnostics;
using FiveDo.Machine;
using FiveDo.Storage;
using FiveDo.Video;

namespace FiveDo.Benchmarks;

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
        var warmupFrames = 60;
        string? biosPath = null;
        string? discPath = null;
        string? statePath = null;
        var json = false;
        var gate = false;
        var prefetch = false;
        var highResolution = false;
        var label = "synthetic";
        var labelSet = false;
        string? baselinePath = null;
        var baselineThreshold = BenchmarkComparison.DefaultThreshold;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--frames" when i + 1 < args.Length:
                    frames = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--warmup" when i + 1 < args.Length:
                    warmupFrames = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--state-in" when i + 1 < args.Length:
                    statePath = args[++i];
                    if (!labelSet)
                    {
                        label = "state";
                    }

                    break;
                case "--bios" when i + 1 < args.Length:
                    biosPath = args[++i];
                    if (!labelSet && statePath is null)
                    {
                        label = "disc";
                    }

                    break;
                case "--disc" when i + 1 < args.Length:
                    discPath = args[++i];
                    break;
                case "--baseline" when i + 1 < args.Length:
                    baselinePath = args[++i];
                    break;
                case "--baseline-threshold" when i + 1 < args.Length:
                    baselineThreshold = double.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--json":
                    json = true;
                    break;
                case "--gate":
                    gate = true;
                    break;
                case "--prefetch":
                    prefetch = true;
                    break;
                case "--high-res":
                    highResolution = true;
                    break;
                case "--label" when i + 1 < args.Length:
                    label = args[++i];
                    labelSet = true;
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

        if (warmupFrames < 0)
        {
            Console.Error.WriteLine("--warmup must be zero or greater.");
            return 2;
        }

        if (baselineThreshold <= 0)
        {
            Console.Error.WriteLine("--baseline-threshold must be positive.");
            return 2;
        }

        IDiscSource? disc = null;
        FiveDoMachine machine;
        if (statePath is not null)
        {
            machine = new FiveDoMachine(new EmulatorTuning { HighResolution = highResolution });
            if (biosPath is not null)
            {
                var bios = BiosImage.Load(biosPath);
                machine.LoadBios(bios.Bios1.Span, bios.Bios2.Span);
            }

            if (discPath is not null)
            {
                disc = DiscImageFactory.Open(discPath, prefetch: prefetch);
                machine.InsertDisc(disc);
            }

            machine.Reset();
            machine.LoadStateFromFile(statePath);
        }
        else if (biosPath is null)
        {
            machine = CreateSyntheticMachine(highResolution);
        }
        else
        {
            var bios = BiosImage.Load(biosPath);
            machine = new FiveDoMachine(new EmulatorTuning { HighResolution = highResolution });
            machine.LoadBios(bios.Bios1.Span, bios.Bios2.Span);
            if (discPath is not null)
            {
                disc = DiscImageFactory.Open(discPath, prefetch: prefetch);
                machine.InsertDisc(disc);
            }

            machine.Reset();
        }

        using (disc)
        {
            var uploader = new VdlFrameUploader();
            var mailbox = new VdlFrameMailbox();
            var presentFrame = new VdlFrame();

            // Warm up JIT, the DSP tables, and the video packers. A longer warm-up
            // keeps one-time runtime allocations out of the steady-state byte count.
            // With --state-in the warm-up also advances a loaded gameplay state into
            // the scene under test (for example a Road Rash race).
            for (var i = 0; i < warmupFrames; i++)
            {
                machine.RunFrame();
                uploader.Update(machine.Frame);
                mailbox.Write(machine.Frame);
                mailbox.Read(presentFrame, NoOp);
            }

            var coreTicks = 0L;
            var packTicks = 0L;
            var mailboxTicks = 0L;
            var readTicks = 0L;
            var coreAllocated = 0L;
            var packAllocated = 0L;
            var mailboxAllocated = 0L;
            var readAllocated = 0L;
            var coreFrameMs = new double[frames];
            var totalFrameMs = new double[frames];
            var startInstructions = machine.Cpu.InstructionsExecuted;
            var startFrames = machine.FramesExecuted;

            for (var i = 0; i < frames; i++)
            {
                var frameStart = Stopwatch.GetTimestamp();
                var a0 = GC.GetAllocatedBytesForCurrentThread();
                var t0 = Stopwatch.GetTimestamp();
                machine.RunFrame();
                var t1 = Stopwatch.GetTimestamp();
                var a1 = GC.GetAllocatedBytesForCurrentThread();
                uploader.Update(machine.Frame);
                var t2 = Stopwatch.GetTimestamp();
                var a2 = GC.GetAllocatedBytesForCurrentThread();
                mailbox.Write(machine.Frame);
                var t3 = Stopwatch.GetTimestamp();
                var a3 = GC.GetAllocatedBytesForCurrentThread();
                mailbox.Read(presentFrame, NoOp);
                var t4 = Stopwatch.GetTimestamp();
                var a4 = GC.GetAllocatedBytesForCurrentThread();

                coreTicks += t1 - t0;
                packTicks += t2 - t1;
                mailboxTicks += t3 - t2;
                readTicks += t4 - t3;
                coreAllocated += a1 - a0;
                packAllocated += a2 - a1;
                mailboxAllocated += a3 - a2;
                readAllocated += a4 - a3;
                var frequency = (double)Stopwatch.Frequency;
                coreFrameMs[i] = (t1 - t0) * 1000.0 / frequency;
                totalFrameMs[i] = (t4 - frameStart) * 1000.0 / frequency;
            }

            var allocatedBytes = coreAllocated + packAllocated + mailboxAllocated + readAllocated;
            var frequencyTotal = (double)Stopwatch.Frequency;
            var coreMs = coreTicks * 1000.0 / frequencyTotal;
            var packMs = packTicks * 1000.0 / frequencyTotal;
            var mailboxMs = mailboxTicks * 1000.0 / frequencyTotal;
            var readMs = readTicks * 1000.0 / frequencyTotal;
            var totalMs = coreMs + packMs + mailboxMs + readMs;
            var instructions = machine.Cpu.InstructionsExecuted - startInstructions;
            var elapsedFrames = machine.FramesExecuted - startFrames;
            FrameTimingSummary coreTiming = FrameTiming.Summarize(coreFrameMs);
            FrameTimingSummary totalTiming = FrameTiming.Summarize(totalFrameMs);

            var result = new
            {
                ok = true,
                label,
                frames = elapsedFrames,
                warmupFrames,
                stateWarnings = machine.StateWarnings,
                core = new
                {
                    totalMs = Round(coreMs),
                    msPerFrame = Round(coreMs / elapsedFrames),
                    p50Ms = Round(coreTiming.P50Ms),
                    p95Ms = Round(coreTiming.P95Ms),
                    p99Ms = Round(coreTiming.P99Ms),
                    maxMs = Round(coreTiming.MaxMs),
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
                    p50Ms = Round(totalTiming.P50Ms),
                    p95Ms = Round(totalTiming.P95Ms),
                    p99Ms = Round(totalTiming.P99Ms),
                    maxMs = Round(totalTiming.MaxMs),
                    fps = Round(elapsedFrames / (totalMs / 1000.0)),
                    budget60Ms = 16.667,
                    headroom = Round(16.667 - (totalMs / elapsedFrames)),
                },
                allocations = new
                {
                    totalBytes = allocatedBytes,
                    bytesPerFrame = allocatedBytes / elapsedFrames,
                    coreBytesPerFrame = coreAllocated / elapsedFrames,
                    packBytesPerFrame = packAllocated / elapsedFrames,
                    mailboxBytesPerFrame = mailboxAllocated / elapsedFrames,
                    readBytesPerFrame = readAllocated / elapsedFrames,
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
                Console.WriteLine($"frames:      {elapsedFrames} (warm-up {warmupFrames})");
                if (machine.StateWarnings.Count != 0)
                {
                    foreach (var warning in machine.StateWarnings)
                    {
                        Console.WriteLine($"state:       {warning}");
                    }
                }
                Console.WriteLine($"core:        {result.core.msPerFrame:F3} ms/frame  ({result.core.fps:F1} fps, {result.core.millionInstructionsPerSecond:F1} MIPS)");
                Console.WriteLine($"  core p50/p95/p99/max: {result.core.p50Ms:F3} / {result.core.p95Ms:F3} / {result.core.p99Ms:F3} / {result.core.maxMs:F3} ms");
                Console.WriteLine($"video pack:  {result.pack.microsecondsPerFrame:F1} us/frame");
                Console.WriteLine($"mailbox:     {result.mailbox.microsecondsPerFrame:F1} us/frame");
                Console.WriteLine($"read copy:   {result.read.microsecondsPerFrame:F1} us/frame");
                Console.WriteLine($"total:       {result.total.msPerFrame:F3} ms/frame  ({result.total.fps:F1} fps)");
                Console.WriteLine($"  total p50/p95/p99/max: {result.total.p50Ms:F3} / {result.total.p95Ms:F3} / {result.total.p99Ms:F3} / {result.total.maxMs:F3} ms");
                Console.WriteLine($"allocations: {result.allocations.bytesPerFrame} bytes/frame ({result.allocations.totalBytes} bytes)");
                Console.WriteLine($"  core/pack/mailbox/read: {result.allocations.coreBytesPerFrame} / {result.allocations.packBytesPerFrame} / {result.allocations.mailboxBytesPerFrame} / {result.allocations.readBytesPerFrame} bytes/frame");
                Console.WriteLine($"60 Hz budget:{result.total.headroom,8:F3} ms headroom");
                Console.WriteLine($"frame hash:  {result.machine.frameHash:x8}");
            }

            if (gate)
            {
                var bytesPerFrame = allocatedBytes / elapsedFrames;
                var totalPerFrame = totalMs / elapsedFrames;
                IReadOnlyList<string> failures = BenchmarkGate.Evaluate(
                    bytesPerFrame,
                    coreTiming.P99Ms,
                    totalPerFrame,
                    totalTiming.P99Ms);
                if (failures.Count != 0)
                {
                    foreach (var failure in failures)
                    {
                        Console.Error.WriteLine($"benchmark gate: {failure}");
                    }

                    return 3;
                }

                Console.Error.WriteLine($"benchmark gate: pass ({BenchmarkGate.Description})");
            }

            if (baselinePath is not null)
            {
                BenchmarkBaseline baseline;
                try
                {
                    baseline = ReadBaseline(baselinePath);
                }
                catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
                {
                    Console.Error.WriteLine($"benchmark comparison: cannot read '{baselinePath}': {exception.Message}");
                    return 2;
                }

                IReadOnlyList<string> failures = BenchmarkComparison.Evaluate(
                    coreTiming.P99Ms,
                    totalMs / elapsedFrames,
                    totalTiming.P99Ms,
                    baseline.CoreP99Ms,
                    baseline.TotalMeanMs,
                    baseline.TotalP99Ms,
                    baselineThreshold);
                if (failures.Count != 0)
                {
                    foreach (var failure in failures)
                    {
                        Console.Error.WriteLine($"benchmark comparison: {failure}");
                    }

                    return 4;
                }

                Console.Error.WriteLine($"benchmark comparison: pass ({BenchmarkComparison.Description})");
            }
        }

        return 0;
    }

    private static BenchmarkBaseline ReadBaseline(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        return new BenchmarkBaseline(
            ReadNumber(root, "core", "p99Ms"),
            ReadNumber(root, "total", "msPerFrame"),
            ReadNumber(root, "total", "p99Ms"));
    }

    private static double ReadNumber(JsonElement root, string sectionName, string propertyName)
    {
        if (!root.TryGetProperty(sectionName, out JsonElement section)
            || section.ValueKind != JsonValueKind.Object
            || !section.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.Number)
        {
            return 0;
        }

        return value.GetDouble();
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct BenchmarkBaseline(
        double CoreP99Ms,
        double TotalMeanMs,
        double TotalP99Ms);

    private static double Round(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private static uint FrameHash(FiveDoMachine machine)
    {
        var hash = 2166136261u;
        VdlFrame frame = machine.Frame;
        for (var y = 0; y < frame.SourceHeight; y++)
        {
            VdlLine line = frame[y];
            Span<ushort> pixels = line.Pixels;
            for (var x = 0; x < line.Width; x++)
            {
                hash = (hash ^ pixels[x]) * 16777619u;
            }
        }

        return hash;
    }

    private static FiveDoMachine CreateSyntheticMachine(bool highResolution)
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

        var machine = new FiveDoMachine(new EmulatorTuning { HighResolution = highResolution });
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

        throw new ArgumentException($"The value 0x{value:x8} cannot be encoded as an ARM immediate.", nameof(value));
    }

    private static uint RotateLeft(uint value, int count)
    {
        count &= 31;
        return count == 0 ? value : (value << count) | (value >> (32 - count));
    }
}
