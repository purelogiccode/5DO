using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CHDSharp;
using CHDSharp.Models;
using ThreeDo.Audio;
using ThreeDo.Core;
using ThreeDo.Cpu;
using ThreeDo.Diagnostics;
using ThreeDo.Host;
using ThreeDo.Input;
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
                "fs" => FsCommand(args),
                "bios" => Bios(args),
                "state" => State(args),
                "bench" => Bench(args),
                "selftest" => SelfTest(args),
                "disasm" => Disassemble(args),
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "AccessToDisposedClosure",
        Justification = "Trace writers are disposed after the run loop, when no observer can fire.")]
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
        var fiqTrace = options.Get("--fiq-trace");
        var kprintPath = options.Get("--kprint");
        var kprintPcPath = options.Get("--kprint-pc");
        var watchLowMem = options.Get("--watch-lowmem");
        var watchFromFrame = options.GetInt("--watch-from-frame", 0);
        var watchStart = options.GetUInt32("--watch-start", 0) ?? 0;
        var watchEnd = options.GetUInt32("--watch-end", 0x100) ?? 0x100;
        var dumpRamPath = options.Get("--dump-ram");
        var dumpRamOffset = options.GetUInt32("--dump-ram-offset", 0) ?? 0;
        var dumpRamLength = options.GetInt("--dump-ram-length", 0x1000);
        var dumpDspPrefix = options.Get("--dump-dsp");
        var inputScriptPath = options.Get("--input-script");
        var progressEvery = options.GetInt("--progress", 0);
        var celTrace = options.Get("--cel-trace");
        var pbusTrace = options.Get("--pbus-trace");
        var nvramPath = options.Get("--nvram");
        var captureDir = options.Get("--capture-dir");
        var captureEvery = options.GetInt("--capture-every", 0);
        var discPath = options.Positional;

        if (biosPath is null && stateIn is null)
        {
            throw new ArgumentException("run needs --bios <path> unless --state-in supplies the ROM state.");
        }

        var machine = new ThreeDoMachine(new EmulatorTuning
        {
            HighResolution = HasFlag(args, "--high-res"),
        });
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

        if (stateIn is null)
        {
            var nvram = nvramPath is not null && File.Exists(nvramPath)
                ? NvramImage.Load(nvramPath)
                : NvramImage.CreateFresh();
            machine.LoadNvram(nvram.Data.Span);
        }

        ScriptedPbusSource? scriptedInput = null;
        if (inputScriptPath is not null)
        {
            scriptedInput = new ScriptedPbusSource(InputScript.Load(inputScriptPath));
            machine.PbusSource = scriptedInput;
        }

        StreamWriter? fiqTraceWriter = null;
        if (fiqTrace is not null)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(fiqTrace));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            fiqTraceWriter = new StreamWriter(fiqTrace);
            machine.Clio.FiqObserver = (reason1, reason2) =>
                fiqTraceWriter.WriteLine(
                    $"f={machine.FramesExecuted} pc={machine.Cpu.ProgramCounter:x8} "
                    + $"reason1={reason1:x8} reason2={reason2:x8}");
        }

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
            machine.Clio.FrameProvider = () => machine.FramesExecuted;
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

        StreamWriter? celTraceWriter = null;
        if (celTrace is not null)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(celTrace));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            celTraceWriter = new StreamWriter(celTrace) { AutoFlush = true };
            machine.Madam.CelTraceWriter = celTraceWriter;
        }

        StreamWriter? pbusTraceWriter = null;
        if (pbusTrace is not null)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(pbusTrace));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            pbusTraceWriter = new StreamWriter(pbusTrace) { AutoFlush = true };
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

        StreamWriter? watchWriter = null;
        if (watchLowMem is not null)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(watchLowMem));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            watchWriter = new StreamWriter(watchLowMem);
            machine.Memory.RamWriteObserver = (address, value, size) =>
            {
                if (address < watchStart || address >= watchEnd || machine.FramesExecuted < watchFromFrame)
                {
                    return;
                }

                var sp = machine.Cpu.ReadRegister(13);
                var stack = new StringBuilder();
                for (var i = 0; i < 6; i++)
                {
                    stack.Append($"{machine.Memory.Ram.Read32(sp + (uint)(i * 4)):x8} ");
                }

                watchWriter.WriteLine(
                    $"f={machine.FramesExecuted} pc={machine.Cpu.ProgramCounter:x8} "
                    + $"addr={address:x8} value={value:x8} size={size} "
                    + $"r0={machine.Cpu.ReadRegister(0):x8} r1={machine.Cpu.ReadRegister(1):x8} "
                    + $"r2={machine.Cpu.ReadRegister(2):x8} r3={machine.Cpu.ReadRegister(3):x8} "
                    + $"sp={sp:x8} lr={machine.Cpu.ReadRegister(14):x8} stack={stack}"
                    + $"caller={DescribeWriter()}");
            };
        }

        if (stateIn is not null)
        {
            machine.LoadStateFromFile(stateIn);
        }

        if (scriptedInput is null && progressEvery <= 0 && pbusTraceWriter is null && captureDir is null)
        {
            machine.RunFrames(frames);
        }
        else
        {
            if (captureDir is not null && captureEvery > 0)
            {
                Directory.CreateDirectory(captureDir);
            }

            for (var i = 0; i < frames; i++)
            {
                scriptedInput?.AdvanceTo(machine.FramesExecuted);
                machine.RunFrame();
                if (progressEvery > 0 && machine.FramesExecuted % progressEvery == 0)
                {
                    Console.Error.WriteLine(
                        $"frame {machine.FramesExecuted} pc={machine.Cpu.ProgramCounter:x8} "
                        + $"dsp={machine.Dsp.ProgramCounter:x4} sema={machine.Dsp.SemaphoreStatus:x4}/{machine.Dsp.SemaphoreData:x4}");
                }

                if (pbusTraceWriter is not null)
                {
                    pbusTraceWriter.WriteLine(
                        $"f={machine.FramesExecuted} pbus={Convert.ToHexString(machine.Madam.PbusQueue)}");
                }

                if (captureDir is not null
                    && captureEvery > 0
                    && machine.FramesExecuted % captureEvery == 0)
                {
                    WriteFrame(machine, Path.Combine(captureDir, $"f{machine.FramesExecuted:D6}.png"));
                }
            }
        }

        if (dumpRamPath is not null)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(dumpRamPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var buffer = new byte[dumpRamLength];
            machine.Memory.CopyRam(dumpRamOffset, buffer);
            File.WriteAllBytes(dumpRamPath, buffer);
        }

        if (dumpDspPrefix is not null)
        {
            DumpDsp(machine, dumpDspPrefix);
        }

        fiqTraceWriter?.Dispose();
        clioTraceWriter?.Dispose();
        cdTraceWriter?.Dispose();
        celTraceWriter?.Dispose();
        pbusTraceWriter?.Dispose();
        kprintWriter?.Dispose();
        kprintPcWriter?.Dispose();
        watchWriter?.Dispose();

        if (frameOut is not null)
        {
            WriteFrame(machine, frameOut);
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
            sourceWidth = machine.Frame.SourceWidth,
            sourceHeight = machine.Frame.SourceHeight,
            vdlModulo = machine.Vdlp.Modulo,
            pc = machine.Cpu.ProgramCounter,
            instructions = machine.Cpu.InstructionsExecuted,
            frameHash = FrameHash(machine),
            samples = audio?.SampleCount ?? 0,
            discHash,
            dspRunning = machine.Dsp.IsRunning,
            dspPc = machine.Dsp.ProgramCounter,
            dspSema = ((uint)machine.Dsp.SemaphoreStatus << 16) | machine.Dsp.SemaphoreData,
            dspSeed = machine.Dsp.Seed,
            warnings = machine.StateWarnings,
        };

        if (nvramPath is not null)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(nvramPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            NvramImage.FromBytes(machine.Memory.Nvram).Save(nvramPath);
        }

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
        var stopInvalid = HasFlag(args, "--stop-invalid");
        var output = options.Get("--out");
        var dumpDspPrefix = options.Get("--dump-dsp");
        var dspTracePath = options.Get("--dsp-trace");
        var inputScriptPath = options.Get("--input-script");
        var nvramPath = options.Get("--nvram");
        var dumpRamPath = options.Get("--dump-ram");
        var dumpRamOffset = options.GetUInt32("--dump-ram-offset", 0) ?? 0;
        var dumpRamLength = options.GetInt("--dump-ram-length", 0x1000);

        if (biosPath is null && stateIn is null)
        {
            throw new ArgumentException("trace needs --bios <path> unless --state-in supplies the ROM state.");
        }

        var machine = new ThreeDoMachine(new EmulatorTuning
        {
            HighResolution = HasFlag(args, "--high-res"),
        });
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
        else
        {
            var nvram = nvramPath is not null && File.Exists(nvramPath)
                ? NvramImage.Load(nvramPath)
                : NvramImage.CreateFresh();
            machine.LoadNvram(nvram.Data.Span);
        }

        ScriptedPbusSource? scriptedInput = null;
        if (inputScriptPath is not null)
        {
            scriptedInput = new ScriptedPbusSource(InputScript.Load(inputScriptPath));
            machine.PbusSource = scriptedInput;
        }

        machine.RunFrames(frames);

        if (dspTracePath is not null)
        {
            machine.Dsp.ClearDspTrace();
            machine.Dsp.DspTraceEnabled = true;
        }

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
            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
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

            if (stopInvalid && pc >= 0x04000000)
            {
                throw new TraceStopException();
            }
        }

        try
        {
            for (var frame = 0; frame < traceFrames; frame++)
            {
                scriptedInput?.AdvanceTo(machine.FramesExecuted);
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
                + $"r4={machine.Cpu.ReadRegister(4):x8} r5={machine.Cpu.ReadRegister(5):x8} "
                + $"r6={machine.Cpu.ReadRegister(6):x8} r7={machine.Cpu.ReadRegister(7):x8} "
                + $"r8={machine.Cpu.ReadRegister(8):x8} r9={machine.Cpu.ReadRegister(9):x8} "
                + $"r10={machine.Cpu.ReadRegister(10):x8} r11={machine.Cpu.ReadRegister(11):x8} "
                + $"r12={machine.Cpu.ReadRegister(12):x8} "
                + $"sp={sp:x8} lr={machine.Cpu.ReadRegister(14):x8} "
                + $"cdStatus={machine.Cdrom.DriveStatus:x2} cdPoll={machine.Cdrom.Poll:x2} "
                + $"disc={(machine.Cdrom.Disc is null ? "no" : "yes")}");
            writer.WriteLine(
                $"# dsp running={machine.Dsp.IsRunning} pc={machine.Dsp.ProgramCounter:x4} "
                + $"sema={machine.Dsp.SemaphoreStatus:x4}/{machine.Dsp.SemaphoreData:x4} "
                + $"prld={machine.Dsp.PeriodReload} audcnt={machine.Dsp.AudioCounter} "
                + $"seed={machine.Dsp.Seed:x8}");
            writer.WriteLine($"# stack: {stack}");

            foreach (var entry in ring)
            {
                writer.WriteLine(
                    $"{entry.Pc:x8} {entry.Cpsr:x8} {entry.Instruction:x8} "
                    + ArmDisassembler.Disassemble(entry.Instruction, entry.Pc));
            }

            if (dumpDspPrefix is not null)
            {
                DumpDsp(machine, dumpDspPrefix);
            }

            if (dspTracePath is not null)
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(dspTracePath));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var dspWriter = new StreamWriter(dspTracePath);
                foreach (var entry in machine.Dsp.DspTrace)
                {
                    dspWriter.WriteLine(
                        $"{entry.Pc:x4} {entry.Instruction:x4} "
                        + $"sema={entry.SemaphoreStatus:x4}/{entry.SemaphoreData:x4} next={entry.NextPc:x4}");
                }
            }

            if (dumpRamPath is not null)
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(dumpRamPath));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var buffer = new byte[dumpRamLength];
                machine.Memory.CopyRam(dumpRamOffset, buffer);
                File.WriteAllBytes(dumpRamPath, buffer);
            }

            if (nvramPath is not null)
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(nvramPath));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                NvramImage.FromBytes(machine.Memory.Nvram).Save(nvramPath);
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

    private static int FsCommand(string[] args)
    {
        if (args.Length < 3)
        {
            throw new ArgumentException(
                "fs needs a subcommand and a disc path: fs list <disc> | fs extract <disc> --out <dir> [--filter <prefix>]");
        }

        var subcommand = args[1];
        var discPath = args[2];
        var options = Parse(args, startIndex: 3);
        using var container = new VideoGameFileSystemParser.Parsers.ChdContainer(discPath);
        if (!container.MountAndParse(VideoGameFileSystemParser.Models.ConsoleType.ThreeDo))
        {
            throw new InvalidDataException(
                $"Cannot parse the 3DO file system in '{discPath}': {container.LastError}");
        }

        if (string.Equals(subcommand, "list", StringComparison.OrdinalIgnoreCase))
        {
            if (options.Json)
            {
                var entries = new List<object>(container.Entries.Count);
                foreach (var entry in container.Entries)
                {
                    entries.Add(new
                    {
                        path = entry.FullPath,
                        directory = entry.IsDirectory,
                        size = entry.Size,
                        lba = entry.Lba,
                    });
                }

                Console.WriteLine(JsonSerializer.Serialize(new { ok = true, entries }, JsonOptions));
                return 0;
            }

            Console.WriteLine($"volume={container.VolumeName} entries={container.Entries.Count}");
            foreach (var entry in container.Entries)
            {
                var indent = new string(' ', entry.FullPath.Count(c => c is '\\' or '/') * 2);
                var kind = entry.IsDirectory ? "[D]" : "[F]";
                Console.WriteLine($"{indent}{kind} {entry.Name} {entry.Size} lba={entry.Lba}");
            }

            return 0;
        }

        if (!string.Equals(subcommand, "extract", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("fs needs the 'list' or 'extract' subcommand.");
        }

        var outputDirectory = options.Get("--out")
            ?? throw new ArgumentException("fs extract needs --out <directory>.");
        var filter = options.Get("--filter");
        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var buffer = new byte[1 << 20];
        var extracted = 0;
        foreach (var entry in container.Entries)
        {
            if (entry.IsDirectory)
            {
                continue;
            }

            var relative = entry.FullPath.TrimStart('\\', '/').Replace('\\', '/');
            if (filter is not null
                && !relative.StartsWith(filter.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var destination = Path.GetFullPath(Path.Combine(outputDirectory, relative));
            if (!destination.StartsWith(outputDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !string.Equals(destination, outputDirectory, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"The file entry '{entry.FullPath}' escapes the output directory.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using (var output = File.Create(destination))
            {
                ulong offset = 0;
                int read;
                while ((read = container.ReadFile(entry, offset, buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    offset += (ulong)read;
                }
            }

            extracted++;
            if (!options.Json)
            {
                Console.WriteLine($"{relative} {entry.Size} bytes");
            }
        }

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { ok = true, extracted, output = outputDirectory }, JsonOptions));
        }

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

    private static int Disassemble(string[] args)
    {
        var options = Parse(args);
        var path = options.Positional ?? throw new ArgumentException("disasm needs a binary path.");
        var offset = options.GetInt("--offset", 0);
        var count = options.GetInt("--count", 32);
        var baseAddress = options.GetUInt32("--base", 0) ?? 0;
        var bytes = File.ReadAllBytes(path);
        if (offset < 0 || offset >= bytes.Length)
        {
            throw new ArgumentException($"The offset 0x{offset:x} is outside the file.");
        }

        var words = Math.Min(count, (bytes.Length - offset) / 4);
        for (var i = 0; i < words; i++)
        {
            var address = baseAddress + (uint)(offset + (i * 4));
            var word = BitConverter.ToUInt32(bytes, offset + (i * 4));
            Console.WriteLine($"{address:x8} {word:x8} {ArmDisassembler.Disassemble(word, address)}");
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

    private static void DumpDsp(ThreeDoMachine machine, string prefix)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(prefix));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var instruction = new ushort[1024];
        machine.Dsp.CopyInstructionMemory(instruction);
        var data = new ushort[2048];
        machine.Dsp.CopyDataMemory(data);

        static byte[] ToBytes(ushort[] words)
        {
            var bytes = new byte[words.Length * 2];
            for (var i = 0; i < words.Length; i++)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
                    bytes.AsSpan(i * 2, 2), words[i]);
            }

            return bytes;
        }

        File.WriteAllBytes(prefix + ".imem.bin", ToBytes(instruction));
        File.WriteAllBytes(prefix + ".dmem.bin", ToBytes(data));
    }

    private static void WriteFrame(ThreeDoMachine machine, string path)
    {
        var buffer = new FrameBuffer(machine.Frame.SourceWidth, machine.Frame.SourceHeight);
        FrameAssembler.Assemble(machine.Frame, buffer, buffer.Width, buffer.Height);
        if (string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
        {
            PngDumper.Write(buffer, path);
        }
        else
        {
            FrameDumper.WriteBmp(buffer, path);
        }
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
        Console.WriteLine($"source:      {type.GetProperty("sourceWidth")!.GetValue(result)}x{type.GetProperty("sourceHeight")!.GetValue(result)}");
        Console.WriteLine($"vdl modulo:  {type.GetProperty("vdlModulo")!.GetValue(result)}");
        Console.WriteLine($"pc:          0x{(uint)type.GetProperty("pc")!.GetValue(result)!:x8}");
        Console.WriteLine($"frame hash:  0x{(uint)type.GetProperty("frameHash")!.GetValue(result)!:x8}");
        Console.WriteLine($"samples:     {type.GetProperty("samples")!.GetValue(result)}");
        Console.WriteLine($"disc hash:   {type.GetProperty("discHash")!.GetValue(result)}");
        Console.WriteLine($"dsp running: {type.GetProperty("dspRunning")!.GetValue(result)}");
        Console.WriteLine($"dsp pc:      0x{(uint)type.GetProperty("dspPc")!.GetValue(result)!:x4}");
        Console.WriteLine($"dsp sema:    0x{(uint)type.GetProperty("dspSema")!.GetValue(result)!:x8}");
        Console.WriteLine($"dsp seed:    0x{(uint)type.GetProperty("dspSeed")!.GetValue(result)!:x8}");
    }

    private static string DescribeWriter()
    {
        var trace = new StackTrace(2, false);
        var parts = new List<string>();
        foreach (var frame in trace.GetFrames())
        {
            var method = frame.GetMethod();
            var type = method?.DeclaringType;
            if (method is null || type is null)
            {
                continue;
            }

            var typeName = type.FullName ?? type.Name;
            if (typeName.StartsWith("ThreeDo.Memory.", StringComparison.Ordinal)
                || typeName.StartsWith("ThreeDo.Cli.Program+", StringComparison.Ordinal))
            {
                continue;
            }

            parts.Add($"{typeName}.{method.Name}");
            if (parts.Count >= 3)
            {
                break;
            }
        }

        return string.Join(" <- ", parts);
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
        Console.WriteLine("             [--clio-trace path] [--fiq-trace path] [--kprint path] [--kprint-pc path]");
        Console.WriteLine("             [--watch-lowmem path] [--watch-from-frame N] [--watch-start N] [--watch-end N]");
        Console.WriteLine("             [--dump-ram path] [--dump-ram-offset N] [--dump-ram-length N]");
        Console.WriteLine("             [--dump-dsp prefix] [--input-script path] [--progress N] [--cel-trace path] [--pbus-trace path]");
        Console.WriteLine("             [--capture-dir path] [--capture-every N] [--nvram path] [--high-res] [--json]");
        Console.WriteLine("  trace [disc] --bios path [--frames N] [--trace-frames N] [--ring N]");
        Console.WriteLine("             [--stop-pc 0xADDR] [--stop-kprint text] [--stop-cd 0xCMD]");
        Console.WriteLine("             [--stop-invalid] [--count N] [--out path] [--dump-dsp prefix]");
        Console.WriteLine("             [--dsp-trace path] [--input-script path] [--nvram path] [--high-res]");
        Console.WriteLine("             [--dump-ram path] [--dump-ram-offset N] [--dump-ram-length N]");
        Console.WriteLine("             Runs the machine and records a diagnostic instruction ring buffer.");
        Console.WriteLine("  info <disc> [--json]                 Disc geometry, volume header, and game ID.");
        Console.WriteLine("  disc dump <disc> --out path [--sector N] [--count N]");
        Console.WriteLine("                                       Write raw 2048-byte sectors for diagnostics.");
        Console.WriteLine("  fs list <disc> [--json]              List the 3DO (Opera FS) file tree.");
        Console.WriteLine("  fs extract <disc> --out dir [--filter prefix]");
        Console.WriteLine("                                       Extract files from a 3DO disc image.");
        Console.WriteLine("  bios info <path> [--json]            BIOS size, MD5, and Anvil detection.");
        Console.WriteLine("  state inspect <path> [--json]        Save-state header and section table.");
        Console.WriteLine("  disasm <file> [--offset N] [--count N] [--base 0xADDR]");
        Console.WriteLine("                                       Disassemble raw little-endian ARM words.");
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
            if (value is null)
            {
                return fallback;
            }

            return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? (int)uint.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : int.Parse(value, CultureInfo.InvariantCulture);
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
