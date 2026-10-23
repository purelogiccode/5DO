using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using CHDSharp;
using CHDSharp.Models;
using ThreeDo.Audio;
using ThreeDo.Core;
using ThreeDo.Cpu;
using ThreeDo.Diagnostics;
using ThreeDo.Host;
using ThreeDo.Machine;
using ThreeDo.Storage;
using ThreeDo.Video;

namespace ThreeDo.Cli;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        var json = HasFlag(args, "--json");
        try
        {
            return args[0] switch
            {
                "--version" or "-v" => Version(),
                "--help" or "-h" or "help" => Help(),
                "run" => Run(args),
                "trace" => TraceCommand(args),
                "info" => Info(args),
                "disc" => DiscCommand(args),
                "bios" => Bios(args),
                "state" => State(args),
                "bench" => Bench(args),
                "selftest" => SelfTest(args),
                _ => Unknown(args[0], json),
            };
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidStateException
            or ArgumentException
            or NotSupportedException)
        {
            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = exception.Message }, JsonOptions));
            }
            else
            {
                Console.Error.WriteLine($"error: {exception.Message}");
            }

            return 1;
        }
    }

    private static int Version()
    {
        Console.WriteLine($"{ThreeDoVersion.ProductName} {ThreeDoVersion.Version} ({ThreeDoVersion.NamespacePrefix})");
        return 0;
    }

    private static int Help()
    {
        PrintUsage();
        return 0;
    }

    private static int Unknown(string command, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = $"Unknown command: {command}" }, JsonOptions));
        }
        else
        {
            Console.Error.WriteLine($"Unknown command: {command}");
        }

        return 2;
    }

    private static int Run(string[] args)
    {
        var options = Parse(args);
        var json = options.Json;
        var frames = options.GetInt("--frames", 600);
        var biosPath = options.Get("--bios");
        var stateIn = options.Get("--state-in");
        var stateOut = options.Get("--state-out");
        var frameOut = options.Get("--frame-out");
        var cdTrace = options.Get("--cd-trace");
        var clioTrace = options.Get("--clio-trace");
        var kprintPath = options.Get("--kprint");
        var kprintPcPath = options.Get("--kprint-pc");
        var discPath = options.Positional;

        if (biosPath is null && stateIn is null)
        {
            throw new ArgumentException("run needs --bios <path> unless --state-in supplies the ROM state.");
        }

        var machine = new ThreeDoMachine();
        if (biosPath is not null)
        {
            var bios = BiosImage.Load(biosPath);
            machine.LoadBios(bios.Bios1.Span, bios.Bios2.Span);
        }

        IDiscSource? disc = null;
        using var audio = CreateAudio(options);
        if (discPath is not null)
        {
            disc = DiscImageFactory.Open(discPath);
            machine.InsertDisc(disc);
        }

        machine.Reset();
        machine.AudioSink = audio;

        StreamWriter? clioTraceWriter = null;
        if (clioTrace is not null)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(clioTrace));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            clioTraceWriter = new StreamWriter(clioTrace);
            machine.Clio.TraceWriter = clioTraceWriter;
        }

        StreamWriter? cdTraceWriter = null;
        if (cdTrace is not null)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(cdTrace));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            cdTraceWriter = new StreamWriter(cdTrace);
            machine.Cdrom.TraceWriter = cdTraceWriter;
        }

        StreamWriter? kprintWriter = null;
        StreamWriter? kprintPcWriter = null;
        if (kprintPath is not null || kprintPcPath is not null)
        {
            if (kprintPath is not null)
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(kprintPath));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                kprintWriter = new StreamWriter(kprintPath);
            }

            if (kprintPcPath is not null)
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(kprintPcPath));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                kprintPcWriter = new StreamWriter(kprintPcPath);
            }

            machine.Madam.KPrint = value =>
            {
                var character = (char)(value & 0xff);
                if (kprintPcWriter is not null)
                {
                    var sp = machine.Cpu.ReadRegister(13);
                    var stack = string.Empty;
                    for (var i = 0; i < 8; i++)
                    {
                        stack += $"{machine.Memory.Ram.Read32(sp + (uint)(i * 4)):x8} ";
                    }

                    kprintPcWriter.WriteLine(
                        $"{machine.Cpu.ProgramCounter:x8} {value & 0xff:x2} "
                        + $"lr={machine.Cpu.ReadRegister(14):x8} sp={sp:x8} {stack}");
                }
                if (kprintWriter is null)
                {
                    return;
                }

                if (character is '\r' or '\n')
                {
                    kprintWriter.WriteLine();
                }
                else if (character >= ' ')
                {
                    kprintWriter.Write(character);
                }
            };
        }

        if (stateIn is not null)
        {
            machine.LoadStateFromFile(stateIn);
        }

        machine.RunFrames(frames);
        clioTraceWriter?.Dispose();
        cdTraceWriter?.Dispose();
        kprintWriter?.Dispose();
        kprintPcWriter?.Dispose();

        if (frameOut is not null)
        {
            var buffer = new FrameBuffer(machine.Frame.SourceWidth, machine.Frame.SourceHeight);
            FrameAssembler.Assemble(machine.Frame, buffer, buffer.Width, buffer.Height);
            if (string.Equals(Path.GetExtension(frameOut), ".png", StringComparison.OrdinalIgnoreCase))
            {
                PngDumper.Write(buffer, frameOut);
            }
            else
            {
                FrameDumper.WriteBmp(buffer, frameOut);
            }
        }

        var discHash = disc is null ? string.Empty : GameId.Compute(disc);
        if (stateOut is not null)
        {
            machine.SaveStateToFile(stateOut, discHash);
        }

        var result = new
        {
            ok = true,
            frames = machine.FramesExecuted,
            cyclesPerFrame = machine.LastFrameCycles,
            pc = machine.Cpu.ProgramCounter,
            instructions = machine.Cpu.InstructionsExecuted,
            frameHash = FrameHash(machine),
            samples = audio?.SampleCount ?? 0,
            discHash,
            warnings = machine.StateWarnings,
        };

        disc?.Dispose();
        WriteRunResult(result, json);
        return 0;
    }

    private static int TraceCommand(string[] args)
    {
        var options = Parse(args);
        var biosPath = options.Get("--bios");
        var stateIn = options.Get("--state-in");
        var discPath = options.Positional;
        var frames = options.GetInt("--frames", 0);
        var traceFrames = options.GetInt("--trace-frames", 1);
        var count = options.GetInt("--count", 0);
        var ringSize = options.GetInt("--ring", 4096);
        var stopPc = options.GetUInt32("--stop-pc", null);
        var stopKprint = options.Get("--stop-kprint");
        var stopCd = options.GetUInt32("--stop-cd", null);
        var output = options.Get("--out");

        if (biosPath is null && stateIn is null)
        {
            throw new ArgumentException("trace needs --bios <path> unless --state-in supplies the ROM state.");
        }

        var machine = new ThreeDoMachine();
        if (biosPath is not null)
        {
            var bios = BiosImage.Load(biosPath);
            machine.LoadBios(bios.Bios1.Span, bios.Bios2.Span);
        }

        IDiscSource? disc = null;
        if (discPath is not null)
        {
            disc = DiscImageFactory.Open(discPath);
            machine.InsertDisc(disc);
        }

        machine.Reset();
        if (stateIn is not null)
        {
            machine.LoadStateFromFile(stateIn);
        }

        machine.RunFrames(frames);

        TextWriter writer = Console.Out;
        StreamWriter? file = null;
        if (output is not null)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(output));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            file = new StreamWriter(output);
            writer = file;
        }

        var ring = new Queue<(uint Pc, uint Cpsr, uint Instruction)>(Math.Max(ringSize, 1));
        var observed = 0;
        var stopped = false;

        if (stopCd is not null)
        {
            var command = (byte)(stopCd.Value & 0xff);
            machine.Cdrom.CommandObserver = value =>
            {
                if (value == command)
                {
                    throw new TraceStopException();
                }
            };
        }

        if (stopKprint is not null)
        {
            var text = new StringBuilder();
            machine.Madam.KPrint = value =>
            {
                var character = (char)(value & 0xff);
                if (character is '\r' or '\n')
                {
                    text.Clear();
                }
                else
                {
                    text.Append(character);
                }

                if (text.Length >= stopKprint.Length
                    && text.ToString(text.Length - stopKprint.Length, stopKprint.Length) == stopKprint)
                {
                    throw new TraceStopException();
                }
            };
        }

        void Observe(uint pc, uint cpsr, uint instruction)
        {
            if (count > 0 && observed >= count)
            {
                throw new TraceLimitReachedException();
            }

            ring.Enqueue((pc, cpsr, instruction));
            if (ring.Count > ringSize)
            {
                ring.Dequeue();
            }

            observed++;
            if (stopPc is not null && pc == stopPc)
            {
                throw new TraceStopException();
            }
        }

        try
        {
            for (var frame = 0; frame < traceFrames; frame++)
            {
                machine.RunFrame(false, Observe);
            }
        }
        catch (TraceStopException)
        {
            stopped = true;
        }
        catch (TraceLimitReachedException)
        {
            stopped = true;
        }
        finally
        {
            if (stopped)
            {
                writer.WriteLine($"# stopped at 0x{machine.Cpu.ProgramCounter:x8} after {observed} instructions");
            }

            var sp = machine.Cpu.ReadRegister(13);
            var stack = new StringBuilder();
            for (var i = 0; i < 16; i++)
            {
                stack.Append($"{machine.Memory.Ram.Read32(sp + (uint)(i * 4)):x8} ");
            }

            writer.WriteLine(
                $"# pc={machine.Cpu.ProgramCounter:x8} cpsr={machine.Cpu.Cpsr:x8} "
                + $"r0={machine.Cpu.ReadRegister(0):x8} r1={machine.Cpu.ReadRegister(1):x8} "
                + $"r2={machine.Cpu.ReadRegister(2):x8} r3={machine.Cpu.ReadRegister(3):x8} "
                + $"sp={sp:x8} lr={machine.Cpu.ReadRegister(14):x8} "
                + $"cdStatus={machine.Cdrom.DriveStatus:x2} cdPoll={machine.Cdrom.Poll:x2} "
                + $"disc={(machine.Cdrom.Disc is null ? "no" : "yes")}");
            writer.WriteLine($"# stack: {stack}");

            foreach (var entry in ring)
            {
                writer.WriteLine(
                    $"{entry.Pc:x8} {entry.Cpsr:x8} {entry.Instruction:x8} "
                    + ArmDisassembler.Disassemble(entry.Instruction, entry.Pc));
            }

            file?.Dispose();
            disc?.Dispose();
        }

        return 0;
    }

    private sealed class TraceStopException : Exception;

    private sealed class TraceLimitReachedException : Exception;

    private static int Info(string[] args)
    {
        var options = Parse(args);
        var path = options.Positional ?? throw new ArgumentException("info needs a disc path.");
        using var disc = DiscImageFactory.Open(path);
        var header = VolumeHeader.TryRead(disc);
        var gameId = GameId.Compute(disc);
        var record = GameDatabase.Default.FindByChecksum(gameId);

        var result = new
        {
            ok = true,
            path,
            sectorCount = disc.SectorCount,
            volumeLabel = header?.Label ?? string.Empty,
            volumeBlockCount = header?.BlockCount ?? 0,
            blockSize = header?.BlockSize ?? 0,
            gameId,
            known = record is not null,
            name = record?.Name ?? string.Empty,
            publisher = record?.Publisher ?? string.Empty,
            releaseYear = record?.ReleaseYear ?? string.Empty,
            regions = record?.Regions ?? string.Empty,
        };

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            Console.WriteLine($"path:        {result.path}");
            Console.WriteLine($"sectors:     {result.sectorCount}");
            Console.WriteLine($"label:       {result.volumeLabel}");
            Console.WriteLine($"blocks:      {result.volumeBlockCount} x {result.blockSize}");
            Console.WriteLine($"game id:     {result.gameId}");
            Console.WriteLine($"game:        {(result.known ? $"{result.name} ({result.releaseYear}, {result.publisher}, {result.regions})" : "unknown")}");
        }

        return 0;
    }

    private static int DiscCommand(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("disc needs the 'dump' or 'tracks' subcommand.");
        }

        if (string.Equals(args[1], "tracks", StringComparison.OrdinalIgnoreCase))
        {
            var options = Parse(args, startIndex: 2);
            var trackPath = options.Positional ?? throw new ArgumentException("disc tracks needs a disc path.");
            if (ChdFile.Open(trackPath, out var chd) != ChdError.Chderrnone || chd is null)
            {
                throw new InvalidDataException($"Cannot open CHD '{trackPath}'.");
            }

            using (chd)
            {
                foreach (var track in chd.Tracks ?? [])
                {
                    Console.WriteLine(
                        $"track {track.TrackNumber}: type={track.TrackType} dataSize={track.DataSize} "
                        + $"frames={track.Frames} start={track.StartFrame}");
                }
            }

            return 0;
        }

        if (!string.Equals(args[1], "dump", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("disc needs the 'dump' or 'tracks' subcommand.");
        }

        var dumpOptions = Parse(args, startIndex: 2);
        var path = dumpOptions.Positional ?? throw new ArgumentException("disc dump needs a disc path.");
        var sector = dumpOptions.GetInt("--sector", 0);
        var count = dumpOptions.GetInt("--count", 1);
        var output = dumpOptions.Get("--out") ?? throw new ArgumentException("disc dump needs --out <path>.");
        using var disc = DiscImageFactory.Open(path);
        var buffer = new byte[2048 * count];
        for (var i = 0; i < count; i++)
        {
            disc.ReadSector(sector + i, buffer.AsSpan(i * 2048, 2048));
        }

        File.WriteAllBytes(output, buffer);
        Console.WriteLine($"wrote {count} sector(s) from LBA {sector} to {output}");
        return 0;
    }

    private static int Bios(string[] args)
    {
        if (args.Length < 2 || !string.Equals(args[1], "info", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("bios needs the 'info <path>' subcommand.");
        }

        var options = Parse(args, startIndex: 2);
        var path = options.Positional ?? throw new ArgumentException("bios info needs a path.");
        var bios = BiosImage.Load(path);
        var result = new
        {
            ok = true,
            path,
            size = bios.Bios1.Length,
            md5 = bios.Md5,
            anvil = bios.IsAnvil,
            anvilKind = bios.AnvilKind,
        };

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            Console.WriteLine($"path:  {path}");
            Console.WriteLine($"size:  {result.size}");
            Console.WriteLine($"md5:   {result.md5}");
            Console.WriteLine($"anvil: {result.anvil} (kind {result.anvilKind})");
        }

        return 0;
    }

    private static int State(string[] args)
    {
        if (args.Length < 2 || !string.Equals(args[1], "inspect", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("state needs the 'inspect <path>' subcommand.");
        }

        var options = Parse(args, startIndex: 2);
        var path = options.Positional ?? throw new ArgumentException("state inspect needs a path.");
        var info = StateInspector.Read(path);

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(info, JsonOptions));
        }
        else
        {
            Console.WriteLine($"version:     {info.Version}");
            Console.WriteLine($"emulator:    {info.EmulatorVersion}");
            Console.WriteLine($"arm clock:   {info.ArmClock}");
            Console.WriteLine($"fix mode:    0x{info.FixMode:x8}");
            Console.WriteLine($"bios md5:    {info.BiosHash}");
            Console.WriteLine($"disc hash:   {info.DiscHash}");
            Console.WriteLine($"frames:      {info.FramesExecuted}");
            foreach (var section in info.Sections)
            {
                Console.WriteLine($"section {section.Id}: v{section.Version}, {section.PayloadLength} bytes");
            }
        }

        return 0;
    }

    private static int Bench(string[] args)
    {
        var options = Parse(args);
        var frames = options.GetInt("--frames", 600);
        var machine = CreateSyntheticMachine();
        machine.RunFrames(10);

        var stopwatch = Stopwatch.StartNew();
        machine.RunFrames(frames);
        stopwatch.Stop();

        var seconds = stopwatch.Elapsed.TotalSeconds;
        var result = new
        {
            ok = true,
            frames,
            seconds,
            framesPerSecond = seconds > 0 ? frames / seconds : 0,
            cyclesPerFrame = machine.LastFrameCycles,
            instructions = machine.Cpu.InstructionsExecuted,
            frameHash = FrameHash(machine),
        };

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            Console.WriteLine($"frames:      {result.frames}");
            Console.WriteLine($"time:        {result.seconds:F3}s");
            Console.WriteLine($"fps:         {result.framesPerSecond:F1}");
            Console.WriteLine($"cycles/frm:  {result.cyclesPerFrame}");
            Console.WriteLine($"frame hash:  {result.frameHash:x8}");
        }

        return 0;
    }

    private static int SelfTest(string[] args)
    {
        var options = Parse(args);
        var first = CreateSyntheticMachine();
        first.RunFrames(30);
        var hash1 = FrameHash(first);

        var second = CreateSyntheticMachine();
        second.RunFrames(30);
        var hash2 = FrameHash(second);

        using var state = new MemoryStream();
        first.SaveState(state);
        var loaded = CreateSyntheticMachine();
        state.Position = 0;
        loaded.LoadState(state);
        loaded.RunFrames(30);

        var checks = new List<object>
        {
            new { name = "deterministic frame", ok = hash1 == hash2 },
            new { name = "frame non-empty", ok = hash1 != 0 },
            new { name = "state roundtrip", ok = FrameHash(loaded) == FrameHash(RunThirtyMore(first)) },
            new { name = "state warnings empty", ok = loaded.StateWarnings.Count == 0 },
        };

        var ok = checks.TrueForAll(check => (bool)check.GetType().GetProperty("ok")!.GetValue(check)!);
        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { ok, checks }, JsonOptions));
        }
        else
        {
            foreach (var check in checks)
            {
                var name = (string)check.GetType().GetProperty("name")!.GetValue(check)!;
                var passed = (bool)check.GetType().GetProperty("ok")!.GetValue(check)!;
                Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  {name}");
            }
        }

        return ok ? 0 : 1;
    }

    private static ThreeDoMachine RunThirtyMore(ThreeDoMachine machine)
    {
        machine.RunFrames(30);
        return machine;
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

    private static WavAudioSink? CreateAudio(ParsedOptions options)
    {
        var mode = options.Get("--audio") ?? "null";
        if (string.Equals(mode, "wav", StringComparison.OrdinalIgnoreCase))
        {
            var path = options.Get("--wav-out") ?? "audio.wav";
            return new WavAudioSink(path);
        }

        if (!string.Equals(mode, "null", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Unknown audio backend '{mode}'.");
        }

        return null;
    }

    private static uint FrameHash(ThreeDoMachine machine)
    {
        var buffer = new FrameBuffer(machine.Frame.SourceWidth, machine.Frame.SourceHeight);
        FrameAssembler.Assemble(machine.Frame, buffer, buffer.Width, buffer.Height);
        var hash = 2166136261u;
        foreach (var value in buffer.PixelSpan)
        {
            hash = unchecked((hash ^ value) * 16777619);
        }

        return hash;
    }

    private static void WriteRunResult(object result, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return;
        }

        var type = result.GetType();
        Console.WriteLine($"frames:      {type.GetProperty("frames")!.GetValue(result)}");
        Console.WriteLine($"cycles/frm:  {type.GetProperty("cyclesPerFrame")!.GetValue(result)}");
        Console.WriteLine($"pc:          0x{(uint)type.GetProperty("pc")!.GetValue(result)!:x8}");
        Console.WriteLine($"frame hash:  0x{(uint)type.GetProperty("frameHash")!.GetValue(result)!:x8}");
        Console.WriteLine($"samples:     {type.GetProperty("samples")!.GetValue(result)}");
        Console.WriteLine($"disc hash:   {type.GetProperty("discHash")!.GetValue(result)}");
    }

    private static bool HasFlag(string[] args, string flag)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static ParsedOptions Parse(string[] args, int startIndex = 1)
    {
        var options = new ParsedOptions();
        for (var i = startIndex; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                options.Json = true;
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    options.Values[arg] = args[++i];
                }
                else
                {
                    options.Values[arg] = "true";
                }

                continue;
            }

            options.Positional ??= arg;
        }

        return options;
    }

    private static void PrintUsage()
    {
        Console.WriteLine($"{ThreeDoVersion.ProductName} {ThreeDoVersion.Version}");
        Console.WriteLine("Usage: 3do <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  run [disc] [--bios path] [--frames N] [--state-in path] [--state-out path]");
        Console.WriteLine("             [--frame-out path] [--audio null|wav] [--wav-out path] [--cd-trace path]");
        Console.WriteLine("             [--clio-trace path] [--kprint path] [--kprint-pc path] [--json]");
        Console.WriteLine("  trace [disc] --bios path [--frames N] [--trace-frames N] [--ring N]");
        Console.WriteLine("             [--stop-pc 0xADDR] [--stop-kprint text] [--stop-cd 0xCMD]");
        Console.WriteLine("             [--count N] [--out path]");
        Console.WriteLine("             Runs the machine and records a diagnostic instruction ring buffer.");
        Console.WriteLine("  info <disc> [--json]                 Disc geometry, volume header, and game ID.");
        Console.WriteLine("  disc dump <disc> --out path [--sector N] [--count N]");
        Console.WriteLine("                                       Write raw 2048-byte sectors for diagnostics.");
        Console.WriteLine("  bios info <path> [--json]            BIOS size, MD5, and Anvil detection.");
        Console.WriteLine("  state inspect <path> [--json]        Save-state header and section table.");
        Console.WriteLine("  bench [--frames N] [--json]          Synthetic-ROM frame benchmark.");
        Console.WriteLine("  selftest [--json]                    Determinism and state roundtrip checks.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -v, --version  Show version");
        Console.WriteLine("  -h, --help     Show help");
    }

    private sealed class ParsedOptions
    {
        internal Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal bool Json { get; set; }

        internal string? Positional { get; set; }

        internal string? Get(string name) => Values.TryGetValue(name, out var value) ? value : null;

        internal int GetInt(string name, int fallback)
        {
            var value = Get(name);
            return value is null ? fallback : int.Parse(value, CultureInfo.InvariantCulture);
        }

        internal uint? GetUInt32(string name, uint? fallback)
        {
            var value = Get(name);
            if (value is null)
            {
                return fallback;
            }

            var text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? value[2..]
                : value;
            return uint.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }
}
/// <summary>Minimal ARM encoder for the CLI's synthetic-ROM self test and benchmark.</summary>
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
