using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using CHDSharp;
using CHDSharp.Models;
using FiveDo.Audio;
using FiveDo.Core;
using FiveDo.Cpu;
using FiveDo.Diagnostics;
using FiveDo.Diagnostics.Logging;
using FiveDo.Diagnostics.Statistics;
using FiveDo.Host;
using FiveDo.Input;
using FiveDo.Machine;
using FiveDo.Storage;
using FiveDo.Video;

namespace FiveDo.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            AppLogging.Initialize(new LoggingOptions
            {
                ApplicationMode = "CLI",
                // The CLI owns its stdout/stderr contract (JSON and concise errors);
                // diagnostics go to the rolling file and the bug-report sink.
                WriteToConsole = false,
                WriteToFile = true,
                EnableBugReports = true,
            });
        }
        catch (Exception exception)
        {
            // Logging must never prevent the CLI from running.
            Console.Error.WriteLine($"Logging initialization failed: {exception.Message}");
        }

        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 0;
            }

            var json = HasFlag(args, "--json");
            var logger = Serilog.Log.ForContext("SourceContext", "FiveDo.Cli.Program");
            logger.Information("CLI command {Command} starting.", args[0]);
            if (!IsDiagnosticCommand(args[0]))
            {
                ApplicationStatsClient.TrackLaunch();
            }
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
                    "nvram" => NvramCommand(args),
                    "audio" => AudioCommand(args),
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
                or FormatException
                or NotSupportedException)
            {
                // Bad arguments, missing paths, and malformed input scripts are expected
                // user errors. Keep them in the log, but do not submit them as bug reports.
                logger.WithoutBugReport().Warning(exception, "CLI command {Command} failed.", args[0]);
                if (json)
                {
                    Console.WriteLine(JsonSerializer.Serialize(
                        new CliErrorResult(false, exception.Message),
                        CliJsonContext.Default.CliErrorResult));
                }
                else
                {
                    Console.Error.WriteLine($"error: {exception.Message}");
                }

                return 1;
            }
            catch (Exception exception)
            {
                logger.Fatal(exception, "CLI command {Command} terminated unexpectedly.", args[0]);
                if (json)
                {
                    Console.WriteLine(JsonSerializer.Serialize(
                        new CliErrorResult(false, exception.Message),
                        CliJsonContext.Default.CliErrorResult));
                }
                else
                {
                    Console.Error.WriteLine($"fatal: {exception.Message}");
                }

                return 1;
            }
        }
        finally
        {
            ApplicationStatsClient.WaitForPending(TimeSpan.FromSeconds(2));
            AppLogging.Shutdown();
        }
    }

    private static bool IsDiagnosticCommand(string command)
    {
        return command is "--version" or "-v" or "--help" or "-h" or "help" or "bench" or "selftest";
    }

    private sealed record BiosLoadResult(
        BiosImage? Image,
        string? PrimaryPath,
        string? SecondaryPath,
        BiosSelection? Selection,
        string Source);

    private static BiosLoadResult LoadBios(
        string? biosPath,
        string? bios2Path,
        string? biosAutoFolder,
        GameRecord? game,
        string? discPath)
    {
        if (biosAutoFolder is not null)
        {
            BiosLibrary library = BiosLibrary.Scan(biosAutoFolder);
            BiosSelection selection = library.SelectForGame(
                game,
                new BiosSelectionOptions(DiscName: discPath));
            if (!selection.HasBios)
            {
                throw new ArgumentException(
                    $"No usable BIOS found in '{biosAutoFolder}'. {selection.Reason}",
                    nameof(biosAutoFolder));
            }

            var secondary = string.IsNullOrWhiteSpace(bios2Path) ? selection.Secondary?.Path : bios2Path;
            BiosImage image = BiosImage.Load(selection.Primary!.Path, secondary);
            return new BiosLoadResult(image, selection.Primary.Path, secondary, selection, "auto");
        }

        if (biosPath is not null)
        {
            BiosImage image = BiosImage.Load(biosPath, bios2Path);
            return new BiosLoadResult(image, biosPath, bios2Path, null, "configured");
        }

        return new BiosLoadResult(null, null, null, null, "none");
    }

    private static int Version()
    {
        Console.WriteLine($"{FiveDoVersion.ProductName} {FiveDoVersion.Version} ({FiveDoVersion.NamespacePrefix})");
        Console.WriteLine("License: FreeDO modified GNU LGPL (see LICENSE)");
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
            Console.WriteLine(JsonSerializer.Serialize(
                new CliErrorResult(false, $"Unknown command: {command}"),
                CliJsonContext.Default.CliErrorResult));
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
        ParsedOptions options = Parse(args);
        var json = options.Json;
        var frames = options.GetInt("--frames", 600);
        var biosPath = options.Get("--bios");
        var bios2Path = options.Get("--bios2");
        var biosAutoFolder = options.Get("--bios-auto");
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
        var inputScriptBase = options.Get("--script-base");
        var autopilotMode = options.Get("--autopilot");
        var progressEvery = options.GetInt("--progress", 0);
        var celTrace = options.Get("--cel-trace");
        var pbusTrace = options.Get("--pbus-trace");
        var nvramPathOption = options.Get("--nvram");
        var nvramModeOption = options.Get("--nvram-mode");
        var nvramVersionOption = options.Get("--nvram-version");
        var nvramDirectoryOption = options.Get("--nvram-dir");
        var captureDir = options.Get("--capture-dir");
        var captureEvery = options.GetInt("--capture-every", 0);
        var pokeSpec = options.Get("--poke");
        var realtime = HasFlag(args, "--realtime");
        var discPath = options.Positional;

        if (biosPath is not null && biosAutoFolder is not null)
        {
            throw new ArgumentException("Use either --bios or --bios-auto, not both.", nameof(args));
        }

        if (bios2Path is not null && biosPath is null && biosAutoFolder is null)
        {
            throw new ArgumentException("--bios2 requires --bios or --bios-auto.", nameof(args));
        }

        if (biosPath is null && biosAutoFolder is null && stateIn is null)
        {
            throw new ArgumentException(
                "run needs --bios <path> or --bios-auto <folder> unless --state-in supplies the ROM state.",
                nameof(args));
        }

        IDiscSource? disc = null;
        string? gameId = null;
        GameRecord? gameRecord = null;
        if (discPath is not null)
        {
            disc = DiscImageFactory.Open(discPath, prefetch: HasFlag(args, "--prefetch"));
            gameId = GameId.Compute(disc);
            gameRecord = GameDatabase.Default.FindByChecksum(gameId);
        }

        NvramSelection nvramSelection = ResolveNvramSelection(
            nvramPathOption,
            nvramModeOption,
            nvramVersionOption,
            nvramDirectoryOption,
            gameId);

        BiosLoadResult biosLoad = LoadBios(biosPath, bios2Path, biosAutoFolder, gameRecord, discPath);

        FixMode fixMode = ResolveFixMode(options, args, gameId);
        EmulatorTuning profileTuning = EmulationProfiles.Apply(
            ParseEmulationProfile(options.Get("--profile")));
        var machine = new FiveDoMachine(new EmulatorTuning
        {
            HighResolution = HasFlag(args, "--high-res"),
            ArmTiming = ParseArmTiming(options.Get("--arm-timing"), profileTuning.ArmTiming),
            ClioTimers = ParseClioTimers(options.Get("--clio-timers"), profileTuning.ClioTimers),
            CelTiming = ParseCelTiming(options.Get("--cel-timing"), profileTuning.CelTiming),
            CdromTiming = ParseCdromTiming(options.Get("--cd-timing"), profileTuning.CdromTiming),
            CdSpeed = ParseCdSpeed(options.Get("--cd-speed"), profileTuning.CdSpeed),
            ArmClock = ParseCpuClock(options.Get("--cpu-clock"), profileTuning.ArmClock),
            RandomSeed = ParseRandomSeed(options.Get("--random-seed")),
            FixMode = fixMode,
        });
        if (biosLoad.Image is not null)
        {
            machine.LoadBios(biosLoad.Image.Bios1.Span, biosLoad.Image.Bios2.Span);
        }

        using AudioOutput audio = CreateAudio(options);
        if (disc is not null)
        {
            machine.InsertDisc(disc);
        }

        machine.Reset();
        machine.AudioSink = audio.Sink;

        if (stateIn is null)
        {
            NvramImage nvram = nvramSelection.Path is not null
                ? LoadNvramImage(nvramSelection.Path, nvramSelection.Fallbacks)
                : NvramImage.CreateFresh();
            machine.LoadNvram(nvram.Data.Span);
        }

        ScriptedPbusSource? scriptedInput = null;
        IReadOnlyList<InputScriptEntry>? scriptEntries = null;
        if (inputScriptPath is not null)
        {
            scriptEntries = InputScript.Load(inputScriptPath);
            scriptedInput = new ScriptedPbusSource(scriptEntries);
            machine.PbusSource = scriptedInput;
        }

        if (inputScriptBase is not null
            && !string.Equals(inputScriptBase, "absolute", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(inputScriptBase, "state", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Unknown script base '{inputScriptBase}'. Supported: absolute, state.", nameof(args));
        }

        if (inputScriptBase is not null && inputScriptPath is null)
        {
            throw new ArgumentException("--script-base requires --input-script.", nameof(args));
        }

        if (string.Equals(inputScriptBase, "state", StringComparison.OrdinalIgnoreCase)
            && stateIn is null)
        {
            throw new ArgumentException("--script-base state requires --state-in.", nameof(args));
        }

        RoadRashAutopilot? roadAutopilot = null;
        PbusSource? autopilotInput = null;
        if (autopilotMode is not null)
        {
            if (!string.Equals(autopilotMode, "road", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Unknown autopilot '{autopilotMode}'. Supported: road.", nameof(args));
            }

            if (scriptedInput is not null)
            {
                throw new ArgumentException("--autopilot and --input-script cannot be combined.", nameof(args));
            }

            roadAutopilot = new RoadRashAutopilot
            {
                EnableMenuAdvance = true,
                IsLoadActive = () => machine.Cdrom.LastReadFrame >= 0
                    && machine.FramesExecuted - machine.Cdrom.LastReadFrame < 30,
            };
            autopilotInput = new PbusSource();
            machine.PbusSource = autopilotInput;
        }

        // Headless runs must still report a connected player-1 pad. Some titles
        // (for example Olympic Summer Games) reject the boot when PBUS is empty.
        machine.PbusSource ??= new PbusSource();

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
            machine.LoadStateFromFile(stateIn, gameId);
            if (scriptEntries is not null
                && string.Equals(inputScriptBase, "state", StringComparison.OrdinalIgnoreCase))
            {
                IReadOnlyList<InputScriptEntry> offsetEntries = InputScript.Offset(scriptEntries, machine.FramesExecuted);
                scriptedInput = new ScriptedPbusSource(offsetEntries);
                machine.PbusSource = scriptedInput;
            }
        }

        if (pokeSpec is not null)
        {
            var patched = MemoryPatch.Apply(machine, pokeSpec);
            Console.Error.WriteLine($"patched {patched} RAM byte(s)");
        }

        AudioFramePacer? realtimePacer = realtime ? new AudioFramePacer() : null;
        var realtimeClock = realtime ? Stopwatch.StartNew() : null;
        var realtimeNext = realtimeClock?.ElapsedTicks ?? 0.0;
        if (realtimePacer is not null)
        {
            RealtimeTimer.Begin();
        }

        var runClock = Stopwatch.StartNew();
        try
        {
            if (scriptedInput is null
                && roadAutopilot is null
                && progressEvery <= 0
                && pbusTraceWriter is null
                && captureDir is null
                && !realtime)
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
                    if (realtimePacer is not null && realtimeClock is not null)
                    {
                        realtimePacer.FrameCompleted(machine.AudioSamplesProduced);
                        realtimeNext += realtimePacer.FramePeriodTicks;
                        var spinTicks = Stopwatch.Frequency / 1000.0;
                        var remaining = realtimeNext - realtimeClock.ElapsedTicks;
                        if (remaining > spinTicks * 2)
                        {
                            var sleepMs = (int)((remaining - spinTicks) * 1000.0 / Stopwatch.Frequency);
                            if (sleepMs > 0)
                            {
                                Thread.Sleep(sleepMs);
                            }
                        }

                        while (realtimeClock.ElapsedTicks < realtimeNext)
                        {
                            Thread.SpinWait(64);
                        }

                        if (realtimeClock.ElapsedTicks - realtimeNext > Stopwatch.Frequency / 10.0)
                        {
                            realtimeNext = realtimeClock.ElapsedTicks;
                        }
                    }

                    if (roadAutopilot is not null)
                    {
                        autopilotInput![0] = roadAutopilot.Update(machine.Frame, machine.FramesExecuted);
                    }

                    if (progressEvery > 0 && machine.FramesExecuted % progressEvery == 0)
                    {
                        var autopilot = roadAutopilot is null
                            ? string.Empty
                            : $" lineX={roadAutopilot.LineX:F1} cov={roadAutopilot.RoadCoverage:F2}"
                            + $" steer={roadAutopilot.Steering}"
                            + $" obst={roadAutopilot.ObstacleSteer}"
                            + $" la={roadAutopilot.LookaheadLeft:F2}/{roadAutopilot.LookaheadCentre:F2}/{roadAutopilot.LookaheadRight:F2}"
                            + (roadAutopilot.UrbanPacing ? " PACE" : string.Empty)
                            + (roadAutopilot.DetectorUpset ? " UPSET" : string.Empty)
                            + (roadAutopilot.RecoveryActive ? " RECOVERY" : string.Empty)
                            + (roadAutopilot.CrashRestart ? " RESTART" : string.Empty)
                            + (roadAutopilot.MenuAdvance ? " MENU" : string.Empty);
                        Console.Error.WriteLine(
                            $"frame {machine.FramesExecuted} pc={machine.Cpu.ProgramCounter:x8} "
                            + $"dsp={machine.Dsp.ProgramCounter:x4} run={machine.Dsp.IsRunning} sema={machine.Dsp.SemaphoreStatus:x4}/{machine.Dsp.SemaphoreData:x4} "
                            + $"audio={machine.AudioSamplesProduced}"
                            + autopilot);
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
        }
        finally
        {
            runClock.Stop();
            if (realtimePacer is not null)
            {
                RealtimeTimer.End();
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

        var result = new CliRunResult(
            Ok: true,
            Frames: machine.FramesExecuted,
            ElapsedSeconds: runClock.Elapsed.TotalSeconds,
            CyclesPerFrame: machine.LastFrameCycles,
            SourceWidth: machine.Frame.SourceWidth,
            SourceHeight: machine.Frame.SourceHeight,
            VdlModulo: machine.Vdlp.Modulo,
            Pc: machine.Cpu.ProgramCounter,
            Instructions: machine.Cpu.InstructionsExecuted,
            FrameHash: FrameHash(machine),
            Samples: audio.SampleCount,
            DiscHash: discHash,
            GameId: gameId,
            Profile: EmulationProfiles.Detect(machine.Tuning).ToString(),
            ArmTiming: machine.Tuning.ArmTiming.ToString(),
            ClioTimers: machine.Tuning.ClioTimers.ToString(),
            CelTiming: machine.Tuning.CelTiming.ToString(),
            CelBusyCycles: machine.Madam.CelBusyCyclesRemaining,
            CdromTiming: machine.Tuning.CdromTiming.ToString(),
            CdSpeed: machine.Tuning.CdSpeed.ToString(),
            CpuClock: machine.Tuning.ArmClock,
            FixMode: (uint)machine.Compatibility.FixMode,
            DspRunning: machine.Dsp.IsRunning,
            DspPc: machine.Dsp.ProgramCounter,
            DspSema: ((uint)machine.Dsp.SemaphoreStatus << 16) | machine.Dsp.SemaphoreData,
            DspSeed: machine.Dsp.Seed,
            CdStatus: machine.Cdrom.DriveStatus,
            CdPoll: machine.Cdrom.Poll,
            Autopilot: roadAutopilot is null ? null : "road",
            AutopilotLineX: roadAutopilot is null || double.IsNaN(roadAutopilot.LineX)
                ? null
                : roadAutopilot.LineX,
            AutopilotRoadCoverage: roadAutopilot is null || double.IsNaN(roadAutopilot.RoadCoverage)
                ? null
                : roadAutopilot.RoadCoverage,
            AutopilotSteering: roadAutopilot?.Steering,
            AutopilotObstacle: roadAutopilot?.ObstacleSteer,
            AutopilotRecovery: roadAutopilot?.RecoveryActive,
            AutopilotCrashRestart: roadAutopilot?.CrashRestart,
            AutopilotMenuAdvance: roadAutopilot?.MenuAdvance,
            AutopilotStaticFrames: roadAutopilot?.StaticFrames,
            AutopilotPacing: roadAutopilot?.UrbanPacing,
            AutopilotUpset: roadAutopilot?.DetectorUpset,
            AutopilotLookahead: roadAutopilot is null
                ? null
                :
                [
                    roadAutopilot.LookaheadLeft,
                    roadAutopilot.LookaheadCentre,
                    roadAutopilot.LookaheadRight,
                ],
            AudioDurationSeconds: audio.Statistics?.DurationSeconds,
            AudioPeak: audio.Statistics?.Peak,
            AudioRms: audio.Statistics?.Rms,
            AudioClippedFrames: audio.Statistics?.ClippedFrames,
            AudioSilentFrames: audio.Statistics?.SilentFrames,
            AudioDcOffsetLeft: audio.Statistics?.DcOffsetLeft,
            AudioDcOffsetRight: audio.Statistics?.DcOffsetRight,
            AudioDroppedSamples: audio.OpenAl?.SamplesDropped,
            AudioUnderrunBuffers: audio.OpenAl?.UnderrunBuffers,
            AudioQueuedBuffers: audio.OpenAl?.QueuedBuffers,
            AudioBufferedFrames: audio.OpenAl?.BufferedFrames,
            AudioLatencyMilliseconds: audio.OpenAl?.LatencyMilliseconds,
            BiosPath: biosLoad.PrimaryPath,
            Bios2Path: string.IsNullOrEmpty(biosLoad.SecondaryPath) ? null : biosLoad.SecondaryPath,
            BiosMd5: biosLoad.Image?.Md5,
            Bios2Md5: string.IsNullOrEmpty(biosLoad.Image?.Md5Bios2) ? null : biosLoad.Image.Md5Bios2,
            BiosName: biosLoad.Selection?.Primary?.Name
                ?? (biosLoad.PrimaryPath is null ? null : Path.GetFileNameWithoutExtension(biosLoad.PrimaryPath)),
            BiosRegion: biosLoad.Selection?.Primary?.RegionLabel,
            BiosSelection: biosLoad.Selection?.Reason,
            BiosSource: biosLoad.Source,
            NvramPath: nvramSelection.Path,
            NvramMode: nvramSelection.Mode,
            NvramVersion: nvramSelection.Version,
            Warnings: machine.StateWarnings);

        SaveNvramIfRequested(machine, nvramSelection);

        disc?.Dispose();
        WriteRunResult(result, json);
        return 0;
    }

    private static int TraceCommand(string[] args)
    {
        ParsedOptions options = Parse(args);
        var biosPath = options.Get("--bios");
        var bios2Path = options.Get("--bios2");
        var biosAutoFolder = options.Get("--bios-auto");
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
        var nvramPathOption = options.Get("--nvram");
        var nvramModeOption = options.Get("--nvram-mode");
        var nvramVersionOption = options.Get("--nvram-version");
        var nvramDirectoryOption = options.Get("--nvram-dir");
        var dumpRamPath = options.Get("--dump-ram");
        var dumpRamOffset = options.GetUInt32("--dump-ram-offset", 0) ?? 0;
        var dumpRamLength = options.GetInt("--dump-ram-length", 0x1000);
        var pokeSpec = options.Get("--poke");

        if (biosPath is not null && biosAutoFolder is not null)
        {
            throw new ArgumentException("Use either --bios or --bios-auto, not both.", nameof(args));
        }

        if (bios2Path is not null && biosPath is null && biosAutoFolder is null)
        {
            throw new ArgumentException("--bios2 requires --bios or --bios-auto.", nameof(args));
        }

        if (biosPath is null && biosAutoFolder is null && stateIn is null)
        {
            throw new ArgumentException(
                "trace needs --bios <path> or --bios-auto <folder> unless --state-in supplies the ROM state.",
                nameof(args));
        }

        IDiscSource? disc = null;
        string? gameId = null;
        GameRecord? gameRecord = null;
        if (discPath is not null)
        {
            disc = DiscImageFactory.Open(discPath, prefetch: HasFlag(args, "--prefetch"));
            gameId = GameId.Compute(disc);
            gameRecord = GameDatabase.Default.FindByChecksum(gameId);
        }

        NvramSelection nvramSelection = ResolveNvramSelection(
            nvramPathOption,
            nvramModeOption,
            nvramVersionOption,
            nvramDirectoryOption,
            gameId);

        BiosLoadResult biosLoad = LoadBios(biosPath, bios2Path, biosAutoFolder, gameRecord, discPath);

        FixMode fixMode = ResolveFixMode(options, args, gameId);
        EmulatorTuning profileTuning = EmulationProfiles.Apply(
            ParseEmulationProfile(options.Get("--profile")));
        var machine = new FiveDoMachine(new EmulatorTuning
        {
            HighResolution = HasFlag(args, "--high-res"),
            ArmTiming = ParseArmTiming(options.Get("--arm-timing"), profileTuning.ArmTiming),
            ClioTimers = ParseClioTimers(options.Get("--clio-timers"), profileTuning.ClioTimers),
            CelTiming = ParseCelTiming(options.Get("--cel-timing"), profileTuning.CelTiming),
            CdromTiming = ParseCdromTiming(options.Get("--cd-timing"), profileTuning.CdromTiming),
            CdSpeed = ParseCdSpeed(options.Get("--cd-speed"), profileTuning.CdSpeed),
            ArmClock = ParseCpuClock(options.Get("--cpu-clock"), profileTuning.ArmClock),
            RandomSeed = ParseRandomSeed(options.Get("--random-seed")),
            FixMode = fixMode,
        });
        if (biosLoad.Image is not null)
        {
            machine.LoadBios(biosLoad.Image.Bios1.Span, biosLoad.Image.Bios2.Span);
        }

        if (disc is not null)
        {
            machine.InsertDisc(disc);
        }

        machine.Reset();
        if (stateIn is not null)
        {
            machine.LoadStateFromFile(stateIn, gameId);
        }
        else
        {
            NvramImage nvram = nvramSelection.Path is not null
                ? LoadNvramImage(nvramSelection.Path, nvramSelection.Fallbacks)
                : NvramImage.CreateFresh();
            machine.LoadNvram(nvram.Data.Span);
        }

        if (pokeSpec is not null)
        {
            var patched = MemoryPatch.Apply(machine, pokeSpec);
            Console.Error.WriteLine($"patched {patched} RAM byte(s)");
        }

        ScriptedPbusSource? scriptedInput = null;
        if (inputScriptPath is not null)
        {
            scriptedInput = new ScriptedPbusSource(InputScript.Load(inputScriptPath));
            machine.PbusSource = scriptedInput;
        }

        // Match the run command: trace output should see the same player-1 pad.
        machine.PbusSource ??= new PbusSource();

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
                    && string.Equals(text.ToString(text.Length - stopKprint.Length, stopKprint.Length), stopKprint, StringComparison.Ordinal))
                {
                    throw new TraceStopException();
                }
            };
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

            foreach ((uint Pc, uint Cpsr, uint Instruction) entry in ring)
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
                foreach (Dsp.Dsp.DspTraceEntry entry in machine.Dsp.DspTrace)
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

            SaveNvramIfRequested(machine, nvramSelection);

            file?.Dispose();
            disc?.Dispose();
        }

        return 0;

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
    }

    private sealed class TraceStopException : Exception
    {
        public TraceStopException()
        {
        }

        // ReSharper disable once UnusedMember.Local
        public TraceStopException(string message)
            : base(message)
        {
        }

        // ReSharper disable once UnusedMember.Local
        public TraceStopException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    private sealed class TraceLimitReachedException : Exception
    {
        public TraceLimitReachedException()
        {
        }

        // ReSharper disable once UnusedMember.Local
        public TraceLimitReachedException(string message)
            : base(message)
        {
        }

        // ReSharper disable once UnusedMember.Local
        public TraceLimitReachedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    private static int Info(string[] args)
    {
        ParsedOptions options = Parse(args);
        var path = options.Positional ?? throw new ArgumentException("info needs a disc path.", nameof(args));
        using IDiscSource disc = DiscImageFactory.Open(path);
        VolumeHeader? header = VolumeHeader.TryRead(disc);
        var gameId = GameId.Compute(disc);
        GameRecord? record = GameDatabase.Default.FindByChecksum(gameId);
        FixMode fixMode = GameCompatibility.ForGameId(gameId).FixMode;

        var result = new CliInfoResult(
            Ok: true,
            Path: path,
            SectorCount: disc.SectorCount,
            VolumeLabel: header?.Label ?? string.Empty,
            VolumeBlockCount: header?.BlockCount ?? 0,
            BlockSize: header?.BlockSize ?? 0,
            GameId: gameId,
            FixMode: (uint)fixMode,
            Known: record is not null,
            Name: record?.Name ?? string.Empty,
            Publisher: record?.Publisher ?? string.Empty,
            ReleaseYear: record?.ReleaseYear ?? string.Empty,
            Regions: record?.Regions ?? string.Empty);

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliInfoResult));
        }
        else
        {
            Console.WriteLine($"path:        {result.Path}");
            Console.WriteLine($"sectors:     {result.SectorCount}");
            Console.WriteLine($"label:       {result.VolumeLabel}");
            Console.WriteLine($"blocks:      {result.VolumeBlockCount} x {result.BlockSize}");
            Console.WriteLine($"game id:     {result.GameId}");
            Console.WriteLine($"fix mode:    0x{result.FixMode:x8}");
            Console.WriteLine($"game:        {(result.Known ? $"{result.Name} ({result.ReleaseYear}, {result.Publisher}, {result.Regions})" : "unknown")}");
        }

        return 0;
    }

    private static int DiscCommand(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("disc needs the 'dump' or 'tracks' subcommand.", nameof(args));
        }

        if (string.Equals(args[1], "tracks", StringComparison.OrdinalIgnoreCase))
        {
            ParsedOptions options = Parse(args, startIndex: 2);
            var trackPath = options.Positional ?? throw new ArgumentException("disc tracks needs a disc path.", nameof(args));
            if (IsChdImage(trackPath))
            {
                if (ChdFile.Open(trackPath, out ChdFile? chd) != ChdError.Chderrnone || chd is null)
                {
                    throw new InvalidDataException($"Cannot open CHD '{trackPath}'.");
                }

                using (chd)
                {
                    foreach (ChdTrackInfo track in chd.Tracks ?? [])
                    {
                        Console.WriteLine(
                            $"track {track.TrackNumber}: type={track.TrackType} dataSize={track.DataSize} "
                            + $"frames={track.Frames} start={track.StartFrame}");
                    }
                }

                return 0;
            }

            // ISO, BIN, and CUE images expose one 2048-byte user-data track through
            // IDiscSource. Report the same columns as the CHD path so scripts can parse
            // both container families.
            using IDiscSource source = DiscImageFactory.Open(trackPath);
            var format = Path.GetExtension(trackPath).TrimStart('.').ToLowerInvariant();
            Console.WriteLine(
                $"track 1: type=data format={(format.Length == 0 ? "raw" : format)} "
                + $"dataSize={(long)source.SectorCount * source.SectorSize} "
                + $"frames={source.SectorCount} start=0");
            return 0;
        }

        if (!string.Equals(args[1], "dump", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("disc needs the 'dump' or 'tracks' subcommand.", nameof(args));
        }

        ParsedOptions dumpOptions = Parse(args, startIndex: 2);
        var path = dumpOptions.Positional ?? throw new ArgumentException("disc dump needs a disc path.", nameof(args));
        var sector = dumpOptions.GetInt("--sector", 0);
        var count = dumpOptions.GetInt("--count", 1);
        var output = dumpOptions.Get("--out") ?? throw new ArgumentException("disc dump needs --out <path>.", nameof(args));
        using IDiscSource disc = DiscImageFactory.Open(path);
        var buffer = new byte[2048 * count];
        for (var i = 0; i < count; i++)
        {
            disc.ReadSector(sector + i, buffer.AsSpan(i * 2048, 2048));
        }

        File.WriteAllBytes(output, buffer);
        Console.WriteLine($"wrote {count} sector(s) from LBA {sector} to {output}");
        return 0;
    }

    private static bool IsChdImage(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Span<byte> signature = stackalloc byte[8];
        return stream.Read(signature) == signature.Length
            && signature.SequenceEqual("MComprHD"u8);
    }

    private static int FsCommand(string[] args)
    {
        if (args.Length < 3)
        {
            throw new ArgumentException(
                "fs needs a subcommand and a disc path: fs list <disc> | fs extract <disc> --out <dir> [--filter <prefix>]", nameof(args));
        }

        var subcommand = args[1];
        var discPath = args[2];
        ParsedOptions options = Parse(args, startIndex: 3);
        using IDiscSource disc = DiscImageFactory.Open(discPath);
        OperaFileSystem fileSystem;
        try
        {
            fileSystem = OperaFileSystem.Mount(disc);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException(
                $"Cannot parse the 3DO file system in '{discPath}': {exception.Message}",
                exception);
        }

        if (string.Equals(subcommand, "list", StringComparison.OrdinalIgnoreCase))
        {
            if (options.Json)
            {
                var entries = new List<CliFsListEntry>(fileSystem.Entries.Count);
                foreach (OperaFileEntry entry in fileSystem.Entries)
                {
                    entries.Add(new CliFsListEntry(
                        Path: entry.Path,
                        Directory: entry.IsDirectory,
                        Size: entry.Size,
                        Lba: entry.Lba));
                }

                Console.WriteLine(JsonSerializer.Serialize(
                    new CliFsListResult(true, entries),
                    CliJsonContext.Default.CliFsListResult));
                return 0;
            }

            Console.WriteLine($"volume={fileSystem.Label} entries={fileSystem.Entries.Count}");
            foreach (OperaFileEntry entry in fileSystem.Entries)
            {
                var indent = new string(' ', entry.Path.Count(c => c is '\\' or '/') * 2);
                var kind = entry.IsDirectory ? "[D]" : "[F]";
                Console.WriteLine($"{indent}{kind} {entry.Name} {entry.Size} lba={entry.Lba}");
            }

            return 0;
        }

        if (!string.Equals(subcommand, "extract", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("fs needs the 'list' or 'extract' subcommand.", nameof(args));
        }

        var outputDirectory = options.Get("--out")
            ?? throw new ArgumentException("fs extract needs --out <directory>.", nameof(args));
        var filter = options.Get("--filter");
        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var buffer = new byte[1 << 20];
        var extracted = 0;
        foreach (OperaFileEntry entry in fileSystem.Entries)
        {
            if (entry.IsDirectory)
            {
                continue;
            }

            var relative = entry.Path.TrimStart('\\', '/').Replace('\\', '/');
            if (filter is not null
                && !relative.StartsWith(filter.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var destination = Path.GetFullPath(Path.Combine(outputDirectory, relative));
            if (!destination.StartsWith(outputDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !string.Equals(destination, outputDirectory, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"The file entry '{entry.Path}' escapes the output directory.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using (FileStream output = File.Create(destination))
            {
                long offset = 0;
                int read;
                while ((read = fileSystem.ReadFile(entry, offset, buffer)) > 0)
                {
                    output.Write(buffer, 0, read);
                    offset += read;
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
            Console.WriteLine(JsonSerializer.Serialize(
                new CliFsExtractResult(true, extracted, outputDirectory),
                CliJsonContext.Default.CliFsExtractResult));
        }

        return 0;
    }

    private static int Bios(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("bios needs the 'info', 'list', 'scan', or 'select' subcommand.", nameof(args));
        }

        return args[1].ToLowerInvariant() switch
        {
            "info" => BiosInfo(args),
            "list" => BiosList(args),
            "scan" => BiosList(args),
            "select" => BiosSelect(args),
            _ => throw new ArgumentException(
                $"Unknown bios subcommand '{args[1]}'. Supported: info, list, scan, select.",
                nameof(args)),
        };
    }

    private static int BiosInfo(string[] args)
    {
        ParsedOptions options = Parse(args, startIndex: 2);
        var path = options.Positional ?? throw new ArgumentException("bios info needs a path.", nameof(args));
        var bios2Path = options.Get("--bios2");
        BiosProfile profile = BiosLibrary.Describe(path);
        BiosImage? image = profile.IsPrimary ? BiosImage.Load(path, bios2Path) : null;
        var result = new CliBiosInfoResult(
            Ok: true,
            Path: path,
            Size: checked((int)profile.Size),
            Md5: profile.Md5,
            Anvil: profile.IsAnvil,
            AnvilKind: image?.AnvilKind ?? 0,
            Name: profile.Name,
            Kind: profile.Kind.ToString(),
            Territory: profile.Territory.ToString(),
            VideoStandard: profile.VideoStandard.ToString(),
            Region: profile.RegionLabel,
            Kanji: profile.IsKanji,
            Patched: profile.IsPatched,
            Preference: profile.Preference,
            Notes: profile.Notes,
            Md5Bios2: image?.Md5Bios2 ?? string.Empty);

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliBiosInfoResult));
        }
        else
        {
            Console.WriteLine($"path:     {result.Path}");
            Console.WriteLine($"size:     {result.Size}");
            Console.WriteLine($"md5:      {result.Md5}");
            Console.WriteLine($"name:     {result.Name}");
            Console.WriteLine($"kind:     {result.Kind}");
            Console.WriteLine($"region:   {result.Region}");
            Console.WriteLine($"kanji:    {result.Kanji}");
            Console.WriteLine($"anvil:    {result.Anvil} (kind {result.AnvilKind})");
            Console.WriteLine($"patched:  {result.Patched}");
            Console.WriteLine($"md5 bios2: {result.Md5Bios2}");
            Console.WriteLine($"notes:    {result.Notes}");
        }

        return 0;
    }

    private static int BiosList(string[] args)
    {
        ParsedOptions options = Parse(args, startIndex: 2);
        var folder = options.Positional ?? throw new ArgumentException("bios list/scan needs a folder.", nameof(args));
        var recursive = HasFlag(args, "--recursive");
        BiosLibrary library = BiosLibrary.Scan(folder, recursive: recursive);
        var profiles = library.Profiles.Select(CliBiosProfileResult.FromProfile).ToArray();
        var result = new CliBiosScanResult(
            Ok: true,
            Folder: Path.GetFullPath(folder),
            PrimaryCount: library.Primaries.Count,
            SecondaryCount: library.Secondaries.Count,
            ErrorCount: library.Errors.Count,
            Profiles: profiles,
            Errors: library.Errors);

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliBiosScanResult));
        }
        else
        {
            Console.WriteLine($"folder:    {result.Folder}");
            Console.WriteLine($"primary:   {result.PrimaryCount}");
            Console.WriteLine($"secondary: {result.SecondaryCount}");
            Console.WriteLine($"errors:    {result.ErrorCount}");
            foreach (CliBiosProfileResult profile in profiles)
            {
                Console.WriteLine(
                    $"{profile.Kind,-9} {profile.Md5}  {profile.Region,-18} {profile.Name}  ({profile.Path})");
                if (profile.Kanji || profile.Anvil || profile.Patched)
                {
                    Console.WriteLine(
                        $"          kanji={profile.Kanji} anvil={profile.Anvil} patched={profile.Patched}");
                }
            }

            foreach (var error in library.Errors)
            {
                Console.WriteLine($"error: {error}");
            }
        }

        return 0;
    }

    private static int BiosSelect(string[] args)
    {
        ParsedOptions options = Parse(args, startIndex: 2);
        var discPath = options.Positional ?? throw new ArgumentException("bios select needs a disc path.", nameof(args));
        var folder = options.Get("--folder")
            ?? throw new ArgumentException("bios select needs --folder <path>.", nameof(args));
        var recursive = HasFlag(args, "--recursive");
        using IDiscSource disc = DiscImageFactory.Open(discPath);
        var gameId = disc.SectorCount >= 2 ? GameId.Compute(disc) : string.Empty;
        GameRecord? record = gameId.Length == 0 ? null : GameDatabase.Default.FindByChecksum(gameId);
        BiosLibrary library = BiosLibrary.Scan(folder, recursive: recursive);
        BiosSelection selection = library.SelectForGame(
            record,
            new BiosSelectionOptions(
                AllowPatched: HasFlag(args, "--allow-patched"),
                RequireKanji: HasFlag(args, "--require-kanji"),
                PreferAnvil: HasFlag(args, "--prefer-anvil"),
                PreferredMd5: options.Get("--md5"),
                DiscName: discPath));
        var result = new CliBiosSelectionResult(
            Ok: selection.HasBios,
            GameId: gameId,
            GameName: record?.Name ?? Path.GetFileNameWithoutExtension(discPath),
            Regions: record?.Regions ?? string.Empty,
            Reason: selection.Reason,
            Primary: selection.Primary is null ? null : CliBiosProfileResult.FromProfile(selection.Primary),
            Secondary: selection.Secondary is null ? null : CliBiosProfileResult.FromProfile(selection.Secondary),
            Warnings: library.Errors);

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliBiosSelectionResult));
        }
        else
        {
            Console.WriteLine($"game:    {result.GameName} ({result.GameId})");
            Console.WriteLine($"regions: {result.Regions}");
            Console.WriteLine($"reason:  {result.Reason}");
            if (result.Primary is not null)
            {
                Console.WriteLine($"primary: {result.Primary.Name} ({result.Primary.Md5})");
                Console.WriteLine($"         {result.Primary.Path}");
            }

            if (result.Secondary is not null)
            {
                Console.WriteLine($"bios2:   {result.Secondary.Name} ({result.Secondary.Md5})");
                Console.WriteLine($"         {result.Secondary.Path}");
            }

            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"warning: {warning}");
            }
        }

        return selection.HasBios ? 0 : 1;
    }

    private static int State(string[] args)
    {
        if (args.Length >= 2 && string.Equals(args[1], "slots", StringComparison.OrdinalIgnoreCase))
        {
            ParsedOptions slotOptions = Parse(args, startIndex: 2);
            var directory = slotOptions.Positional ?? throw new ArgumentException("state slots needs a directory.", nameof(args));
            var slotCount = slotOptions.GetInt("--count", 10);
            var manager = new SaveStateSlotManager(directory, slotCount);
            IReadOnlyList<SaveStateSlotInfo> slots = manager.List();
            if (slotOptions.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(
                    slots,
                    typeof(IReadOnlyList<SaveStateSlotInfo>),
                    CliHostJsonContext.Default));
            }
            else
            {
                foreach (SaveStateSlotInfo slot in slots)
                {
                    if (!slot.Exists)
                    {
                        Console.WriteLine($"slot {slot.Slot:D2}: empty");
                        continue;
                    }

                    var savedAt = slot.SavedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "unknown";
                    var name = string.IsNullOrWhiteSpace(slot.GameName) ? "(unnamed)" : slot.GameName;
                    Console.WriteLine(
                        $"slot {slot.Slot:D2}: {name}  frames={slot.FramesExecuted}  saved={savedAt}  "
                        + $"size={slot.FileSize}{(slot.Error.Length > 0 ? $"  error={slot.Error}" : string.Empty)}");
                }
            }

            return 0;
        }

        if (args.Length < 2 || !string.Equals(args[1], "inspect", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("state needs the 'inspect <path>' or 'slots <dir>' subcommand.", nameof(args));
        }

        ParsedOptions options = Parse(args, startIndex: 2);
        var path = options.Positional ?? throw new ArgumentException("state inspect needs a path.", nameof(args));
        StateInfo info = StateInspector.Read(path);

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(info, CliHostJsonContext.Default.StateInfo));
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
            Console.WriteLine($"stored:      {info.StoredLength} bytes{(info.Compressed ? " (compressed sections)" : string.Empty)}");
            foreach (StateSectionInfo section in info.Sections)
            {
                Console.WriteLine(
                    $"section {section.Id}: v{section.Version}, {section.PayloadLength} bytes"
                    + (section.Compressed ? $" (stored {section.StoredLength}, compressed)" : string.Empty));
            }
        }

        return 0;
    }

    private static int NvramCommand(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(
                "nvram needs path, list, export, import, or reset.",
                nameof(args));
        }

        var subcommand = args[1].ToLowerInvariant();
        ParsedOptions options = Parse(args, startIndex: 2);
        var directory = options.Get("--dir") ?? AppSettingsStore.DefaultDirectory;
        var mode = ParseNvramStorageMode(options.Get("--mode"));
        var version = ParseNvramVersion(options.Get("--version"));
        var discPath = options.Positional;
        string? gameId = null;
        if (discPath is not null)
        {
            using IDiscSource disc = DiscImageFactory.Open(discPath);
            gameId = GameId.Compute(disc);
        }

        switch (subcommand)
        {
            case "path":
                {
                    var path = NvramLayout.Resolve(directory, mode, version, gameId);
                    var exists = File.Exists(path);
                    var length = exists ? new FileInfo(path).Length : 0;
                    var result = new CliNvramPathResult(
                        Ok: true,
                        Path: path,
                        Mode: mode == NvramStorageMode.PerGame ? "per-game" : "global",
                        Version: version,
                        GameId: gameId,
                        Exists: exists,
                        Length: length);
                    if (options.Json)
                    {
                        Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliNvramPathResult));
                    }
                    else
                    {
                        Console.WriteLine($"path:     {result.Path}");
                        Console.WriteLine($"mode:     {result.Mode}");
                        Console.WriteLine($"version:  {result.Version}");
                        Console.WriteLine($"game:     {result.GameId ?? "(shared)"}");
                        Console.WriteLine($"exists:   {(result.Exists ? "yes" : "no")}");
                        Console.WriteLine($"size:     {result.Length} bytes");
                    }

                    return 0;
                }

            case "list":
                {
                    IReadOnlyList<NvramFile> files = NvramLayout.Enumerate(directory);
                    var entries = files
                        .Select(file => new CliNvramEntry(
                            Path: file.Path,
                            Mode: file.Mode == NvramStorageMode.PerGame ? "per-game" : "global",
                            Version: file.Version,
                            GameId: file.GameId,
                            Length: file.Length,
                            LastWriteTimeUtc: file.LastWriteTimeUtc))
                        .ToArray();
                    if (options.Json)
                    {
                        Console.WriteLine(JsonSerializer.Serialize(
                            new CliNvramListResult(true, Path.GetFullPath(directory), entries),
                            CliJsonContext.Default.CliNvramListResult));
                    }
                    else
                    {
                        Console.WriteLine($"directory: {Path.GetFullPath(directory)}");
                        if (entries.Length == 0)
                        {
                            Console.WriteLine("(no NVRAM images)");
                        }

                        foreach (CliNvramEntry entry in entries)
                        {
                            var saved = entry.LastWriteTimeUtc?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "unknown";
                            var game = string.IsNullOrEmpty(entry.GameId) ? "shared" : entry.GameId;
                            Console.WriteLine(
                                $"{entry.Mode,-8} v{entry.Version} {game,-32} {entry.Length,8} bytes  {saved}  {entry.Path}");
                        }
                    }

                    return 0;
                }

            case "export":
                {
                    var output = options.Get("--out")
                        ?? throw new ArgumentException("nvram export needs --out <path>.", nameof(args));
                    var path = NvramLayout.Resolve(directory, mode, version, gameId);
                    NvramImage image = LoadNvramImage(
                        path,
                        NvramLayout.GetFallbackPaths(directory, mode, version, gameId));
                    var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(output));
                    if (!string.IsNullOrEmpty(outputDirectory))
                    {
                        Directory.CreateDirectory(outputDirectory);
                    }

                    image.Save(output);
                    WriteNvramActionResult(options, "export", output, path, image.Data.Length);
                    return 0;
                }

            case "import":
                {
                    var input = options.Get("--in")
                        ?? throw new ArgumentException("nvram import needs --in <path>.", nameof(args));
                    NvramImage image = NvramImage.Load(input);
                    var path = NvramLayout.Resolve(directory, mode, version, gameId);
                    using (var store = new NvramStore(path))
                    {
                        store.Save(image.Data.Span);
                    }

                    WriteNvramActionResult(options, "import", path, input, image.Data.Length);
                    return 0;
                }

            case "reset":
                {
                    var path = NvramLayout.Resolve(directory, mode, version, gameId);
                    using (var store = new NvramStore(path))
                    {
                        store.Reset();
                    }

                    WriteNvramActionResult(options, "reset", path, null, EmulatorConstants.NvramSize);
                    return 0;
                }

            default:
                throw new ArgumentException(
                    $"Unknown nvram subcommand '{subcommand}'. Use path, list, export, import, or reset.",
                    nameof(args));
        }
    }

    private static void WriteNvramActionResult(
        ParsedOptions options,
        string action,
        string path,
        string? source,
        long length)
    {
        var result = new CliNvramActionResult(true, action, path, source, length);
        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliNvramActionResult));
        }
        else
        {
            Console.WriteLine($"{action}: {path}");
            if (!string.IsNullOrEmpty(source))
            {
                Console.WriteLine($"source: {source}");
            }

            Console.WriteLine($"size:   {length} bytes");
        }
    }

    private static NvramSelection ResolveNvramSelection(
        string? explicitPath,
        string? modeText,
        string? versionText,
        string? directoryText,
        string? gameId)
    {
        var hasLayoutOption = modeText is not null || versionText is not null || directoryText is not null;
        if (explicitPath is not null)
        {
            if (hasLayoutOption)
            {
                throw new ArgumentException(
                    "--nvram cannot be combined with --nvram-mode, --nvram-version, or --nvram-dir.",
                    nameof(explicitPath));
            }

            return new NvramSelection(explicitPath, "explicit", NvramLayout.MinVersion, []);
        }

        if (!hasLayoutOption)
        {
            return new NvramSelection(null, "disabled", NvramLayout.MinVersion, []);
        }

        NvramStorageMode mode = ParseNvramStorageMode(modeText);
        var version = ParseNvramVersion(versionText);
        var directory = directoryText ?? AppSettingsStore.DefaultDirectory;
        if (mode == NvramStorageMode.PerGame && string.IsNullOrWhiteSpace(gameId))
        {
            throw new ArgumentException(
                "--nvram-mode per-game needs a disc so the game ID can be computed.",
                nameof(modeText));
        }

        return new NvramSelection(
            NvramLayout.Resolve(directory, mode, version, gameId),
            mode == NvramStorageMode.PerGame ? "per-game" : "global",
            version,
            NvramLayout.GetFallbackPaths(directory, mode, version, gameId));
    }

    private static NvramImage LoadNvramImage(string path, IReadOnlyList<string> fallbackPaths)
    {
        foreach (var candidate in new[] { path }.Concat(fallbackPaths))
        {
            if (File.Exists(candidate))
            {
                return NvramImage.Load(candidate);
            }
        }

        return NvramImage.CreateFresh();
    }

    private static void SaveNvramIfRequested(FiveDoMachine machine, NvramSelection selection)
    {
        if (selection.Path is null)
        {
            return;
        }

        using var store = new NvramStore(selection.Path);
        store.Save(machine.Memory.Nvram);
    }

    private static NvramStorageMode ParseNvramStorageMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "global", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "shared", StringComparison.OrdinalIgnoreCase))
        {
            return NvramStorageMode.Global;
        }

        if (string.Equals(value, "per-game", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "pergame", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "per_game", StringComparison.OrdinalIgnoreCase))
        {
            return NvramStorageMode.PerGame;
        }

        throw new ArgumentException(
            $"Invalid NVRAM storage mode '{value}'; expected global or per-game.",
            nameof(value));
    }

    private static int ParseNvramVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return NvramLayout.MinVersion;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            || version is < NvramLayout.MinVersion or > NvramLayout.MaxVersion)
        {
            throw new ArgumentException(
                $"Invalid NVRAM version '{value}'; expected 0-{NvramLayout.MaxVersion}.",
                nameof(value));
        }

        return version;
    }

    private sealed record NvramSelection(
        string? Path,
        string Mode,
        int Version,
        IReadOnlyList<string> Fallbacks);

    private static int AudioCommand(string[] args)
    {
        if (args.Length < 2 || !string.Equals(args[1], "analyze", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("audio needs the 'analyze <wav>' subcommand.", nameof(args));
        }

        ParsedOptions options = Parse(args, startIndex: 2);
        var path = options.Positional ?? throw new ArgumentException("audio analyze needs a WAV path.", nameof(args));
        AudioAnalysis analysis = AudioAnalyzer.Analyze(path);
        var result = new CliAudioResult(
            Ok: true,
            Path: analysis.File.Path,
            SampleRate: analysis.File.SampleRate,
            Channels: analysis.File.Channels,
            BitsPerSample: analysis.File.BitsPerSample,
            Frames: analysis.File.Frames,
            DurationSeconds: analysis.File.DurationSeconds,
            DataBytes: analysis.File.DataBytes,
            Peak: analysis.Statistics.Peak,
            Rms: analysis.Statistics.Rms,
            ClippedFrames: analysis.Statistics.ClippedFrames,
            SilentFrames: analysis.Statistics.SilentFrames,
            DcOffsetLeft: analysis.Statistics.DcOffsetLeft,
            DcOffsetRight: analysis.Statistics.DcOffsetRight);

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliAudioResult));
        }
        else
        {
            Console.WriteLine($"path:        {result.Path}");
            Console.WriteLine($"format:      {result.SampleRate} Hz, {result.BitsPerSample}-bit, {result.Channels} channel(s)");
            Console.WriteLine($"frames:      {result.Frames}");
            Console.WriteLine($"duration:    {result.DurationSeconds:F3} s");
            Console.WriteLine($"peak:        {result.Peak}");
            Console.WriteLine($"rms:         {result.Rms:F1}");
            Console.WriteLine($"clipped:     {result.ClippedFrames}");
            Console.WriteLine($"silent:      {result.SilentFrames}");
            Console.WriteLine($"dc offset:   L {result.DcOffsetLeft:F2}, R {result.DcOffsetRight:F2}");
        }

        return 0;
    }

    private static int Disassemble(string[] args)
    {
        ParsedOptions options = Parse(args);
        var path = options.Positional ?? throw new ArgumentException("disasm needs a binary path.", nameof(args));
        var offset = options.GetInt("--offset", 0);
        var count = options.GetInt("--count", 32);
        var baseAddress = options.GetUInt32("--base", 0) ?? 0;
        var bytes = File.ReadAllBytes(path);
        if (offset < 0 || offset >= bytes.Length)
        {
            throw new ArgumentException($"The offset 0x{offset:x} is outside the file.", nameof(args));
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
        ParsedOptions options = Parse(args);
        var frames = options.GetInt("--frames", 600);
        FiveDoMachine machine = CreateSyntheticMachine();
        machine.RunFrames(10);

        var stopwatch = Stopwatch.StartNew();
        machine.RunFrames(frames);
        stopwatch.Stop();

        var seconds = stopwatch.Elapsed.TotalSeconds;
        var result = new CliBenchResult(
            Ok: true,
            Frames: frames,
            Seconds: seconds,
            FramesPerSecond: seconds > 0 ? frames / seconds : 0,
            CyclesPerFrame: machine.LastFrameCycles,
            Instructions: machine.Cpu.InstructionsExecuted,
            FrameHash: FrameHash(machine));

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliBenchResult));
        }
        else
        {
            Console.WriteLine($"frames:      {result.Frames}");
            Console.WriteLine($"time:        {result.Seconds:F3}s");
            Console.WriteLine($"fps:         {result.FramesPerSecond:F1}");
            Console.WriteLine($"cycles/frm:  {result.CyclesPerFrame}");
            Console.WriteLine($"frame hash:  {result.FrameHash:x8}");
        }

        return 0;
    }

    private static int SelfTest(string[] args)
    {
        ParsedOptions options = Parse(args);
        FiveDoMachine first = CreateSyntheticMachine();
        first.RunFrames(30);
        var hash1 = FrameHash(first);

        FiveDoMachine second = CreateSyntheticMachine();
        second.RunFrames(30);
        var hash2 = FrameHash(second);

        using var state = new MemoryStream();
        first.SaveState(state);
        FiveDoMachine loaded = CreateSyntheticMachine();
        state.Position = 0;
        loaded.LoadState(state);
        loaded.RunFrames(30);

        var checks = new List<CliSelfTestCheck>
        {
            new("deterministic frame", hash1 == hash2),
            new("frame non-empty", hash1 != 0),
            new("state roundtrip", FrameHash(loaded) == FrameHash(RunThirtyMore(first))),
            new("state warnings empty", loaded.StateWarnings.Count == 0),
        };

        var ok = checks.TrueForAll(check => check.Ok);
        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(
                new CliSelfTestResult(ok, checks),
                CliJsonContext.Default.CliSelfTestResult));
        }
        else
        {
            foreach (CliSelfTestCheck check in checks)
            {
                Console.WriteLine($"{(check.Ok ? "PASS" : "FAIL")}  {check.Name}");
            }
        }

        return ok ? 0 : 1;
    }

    private static FiveDoMachine RunThirtyMore(FiveDoMachine machine)
    {
        machine.RunFrames(30);
        return machine;
    }

    private static FiveDoMachine CreateSyntheticMachine()
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

        var machine = new FiveDoMachine();
        machine.LoadBios(rom);
        machine.Reset();
        return machine;
    }

    private static AudioOutput CreateAudio(ParsedOptions options)
    {
        var mode = options.Get("--audio") ?? "null";
        if (string.Equals(mode, "wav", StringComparison.OrdinalIgnoreCase))
        {
            var path = options.Get("--wav-out") ?? "audio.wav";
            return new AudioOutput(new WavAudioSink(path), null);
        }

        if (string.Equals(mode, "openal", StringComparison.OrdinalIgnoreCase))
        {
            var output = OpenAlAudioOutput.TryCreate(4096, out var openAlError);
            if (output is null)
            {
                Serilog.Log
                    .ForContext("SourceContext", "FiveDo.Cli.Program.Audio")
                    .WithoutBugReport()
                    .Warning(
                        "OpenAL is unavailable; audio is silent ({Error}).",
                        openAlError ?? "unknown error");

                return new AudioOutput(null, null);
            }

            if (options.Get("--volume") is { } volumeText)
            {
                if (!float.TryParse(volumeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var volume))
                {
                    output.Dispose();
                    throw new ArgumentException($"Invalid audio volume '{volumeText}'.", nameof(options));
                }

                output.Volume = volume;
            }

            return new AudioOutput(null, output);
        }

        if (!string.Equals(mode, "null", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Unknown audio backend '{mode}'.", nameof(options));
        }

        return new AudioOutput(null, null);
    }

    /// <summary>Owns the CLI's optional WAV/OpenAL audio backend.</summary>
    private sealed class AudioOutput : IDisposable
    {
        private readonly WavAudioSink? _wav;
        private readonly OpenAlAudioOutput? _openAl;

        internal AudioOutput(WavAudioSink? wav, OpenAlAudioOutput? openAl)
        {
            _wav = wav;
            _openAl = openAl;
        }

        internal IAudioSink? Sink => (IAudioSink?)_wav ?? _openAl;

        internal long SampleCount => _wav?.SampleCount
            ?? ((_openAl?.SamplesPushed ?? 0) + (_openAl?.SamplesDropped ?? 0));

        internal AudioStatistics? Statistics => _wav?.Statistics;

        internal OpenAlAudioOutput? OpenAl => _openAl;

        public void Dispose()
        {
            _wav?.Dispose();
            _openAl?.Dispose();
        }
    }

    private static void DumpDsp(FiveDoMachine machine, string prefix)
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

        File.WriteAllBytes(prefix + ".imem.bin", ToBytes(instruction));
        File.WriteAllBytes(prefix + ".dmem.bin", ToBytes(data));
        return;

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
    }

    private static void WriteFrame(FiveDoMachine machine, string path)
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

    private static uint FrameHash(FiveDoMachine machine)
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

    private static void WriteRunResult(CliRunResult result, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliRunResult));
            return;
        }

        Console.WriteLine($"frames:      {result.Frames}");
        Console.WriteLine($"elapsed:     {result.ElapsedSeconds:F3} s");
        Console.WriteLine($"cycles/frm:  {result.CyclesPerFrame}");
        Console.WriteLine($"source:      {result.SourceWidth}x{result.SourceHeight}");
        Console.WriteLine($"vdl modulo:  {result.VdlModulo}");
        Console.WriteLine($"pc:          0x{result.Pc:x8}");
        Console.WriteLine($"frame hash:  0x{result.FrameHash:x8}");
        Console.WriteLine($"samples:     {result.Samples}");
        if (result.AudioDurationSeconds is { } audioSeconds)
        {
            Console.WriteLine(
                $"audio:       {audioSeconds:F3} s, peak {result.AudioPeak}, "
                + $"RMS {result.AudioRms:F1}, clipped {result.AudioClippedFrames}, "
                + $"silent {result.AudioSilentFrames}");
        }

        if (result.AudioDroppedSamples is { } droppedSamples)
        {
            Console.WriteLine(
                $"openal:      dropped {droppedSamples}, underruns {result.AudioUnderrunBuffers}, "
                + $"queued {result.AudioQueuedBuffers}, buffered {result.AudioBufferedFrames}, "
                + $"latency {result.AudioLatencyMilliseconds:F1} ms");
        }

        Console.WriteLine($"disc hash:   {result.DiscHash}");
        Console.WriteLine($"game id:     {result.GameId}");
        Console.WriteLine($"profile:     {result.Profile}");
        Console.WriteLine($"arm timing:  {result.ArmTiming}");
        Console.WriteLine($"clio timers: {result.ClioTimers}");
        Console.WriteLine($"cel timing:  {result.CelTiming}");
        Console.WriteLine($"cel busy:    {result.CelBusyCycles} cycles");
        Console.WriteLine($"cd timing:   {result.CdromTiming}");
        Console.WriteLine($"cd speed:    {result.CdSpeed}");
        Console.WriteLine($"cpu clock:   {result.CpuClock / 1_000_000.0:0.###} MHz");
        Console.WriteLine($"fix mode:    0x{result.FixMode:x8}");
        if (result.BiosMd5 is not null)
        {
            Console.WriteLine($"bios:        {result.BiosName} ({result.BiosMd5})");
            Console.WriteLine($"bios region: {result.BiosRegion} [{result.BiosSource}]");
            if (result.Bios2Md5 is not null)
            {
                Console.WriteLine($"bios2:       {result.Bios2Md5}");
            }

            if (result.BiosSelection is not null)
            {
                Console.WriteLine($"bios reason: {result.BiosSelection}");
            }
        }

        Console.WriteLine($"dsp running: {result.DspRunning}");
        Console.WriteLine($"dsp pc:      0x{result.DspPc:x4}");
        Console.WriteLine($"dsp sema:    0x{result.DspSema:x8}");
        Console.WriteLine($"dsp seed:    0x{result.DspSeed:x8}");
    }

    private static string DescribeWriter()
    {
        var trace = new StackTrace(2, false);
        var parts = new List<string>();
        foreach (StackFrame frame in trace.GetFrames())
        {
            var method = DiagnosticMethodInfo.Create(frame);
            if (method is null)
            {
                continue;
            }

            var typeName = method.DeclaringTypeName;
            if (typeName.StartsWith("FiveDo.Memory.", StringComparison.Ordinal)
                || typeName.StartsWith("FiveDo.Cli.Program+", StringComparison.Ordinal))
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

    private static FixMode ResolveFixMode(ParsedOptions options, string[] args, string? gameId)
    {
        var explicitMode = options.Get("--fix-mode");
        if (explicitMode is not null)
        {
            return ParseFixMode(explicitMode);
        }

        if (HasFlag(args, "--no-game-fixes"))
        {
            return FixMode.None;
        }

        return GameCompatibility.ForGameId(gameId).FixMode;
    }

    private static EmulationProfile ParseEmulationProfile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "compatible", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "default", StringComparison.OrdinalIgnoreCase))
        {
            return EmulationProfile.Compatible;
        }

        if (string.Equals(value, "accuracy", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "accurate", StringComparison.OrdinalIgnoreCase))
        {
            return EmulationProfile.Accuracy;
        }

        if (string.Equals(value, "performance", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "fast", StringComparison.OrdinalIgnoreCase))
        {
            return EmulationProfile.Performance;
        }

        if (string.Equals(value, "custom", StringComparison.OrdinalIgnoreCase))
        {
            return EmulationProfile.Custom;
        }

        throw new ArgumentException(
            $"Invalid --profile value '{value}'; expected compatible, accuracy, performance, or custom.",
            nameof(value));
    }

    private static ArmTimingMode ParseArmTiming(
        string? value,
        ArmTimingMode fallback = ArmTimingMode.FreeDoCompatible)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (string.Equals(value, "freedo", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "freedo-compatible", StringComparison.OrdinalIgnoreCase))
        {
            return ArmTimingMode.FreeDoCompatible;
        }

        if (string.Equals(value, "arm60", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "datasheet", StringComparison.OrdinalIgnoreCase))
        {
            return ArmTimingMode.Arm60Datasheet;
        }

        throw new ArgumentException(
            $"Invalid --arm-timing value '{value}'; expected freedo or arm60.",
            nameof(value));
    }

    private static ClioTimerMode ParseClioTimers(
        string? value,
        ClioTimerMode fallback = ClioTimerMode.FreeDoCompatible)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (string.Equals(value, "freedo", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "freedo-compatible", StringComparison.OrdinalIgnoreCase))
        {
            return ClioTimerMode.FreeDoCompatible;
        }

        if (string.Equals(value, "opera", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "opera-scheduler", StringComparison.OrdinalIgnoreCase))
        {
            return ClioTimerMode.OperaScheduler;
        }

        throw new ArgumentException(
            $"Invalid --clio-timers value '{value}'; expected freedo or opera.",
            nameof(value));
    }

    private static CelTimingMode ParseCelTiming(
        string? value,
        CelTimingMode fallback = CelTimingMode.FreeDoCompatible)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (string.Equals(value, "freedo", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "freedo-compatible", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "sync", StringComparison.OrdinalIgnoreCase))
        {
            return CelTimingMode.FreeDoCompatible;
        }

        if (string.Equals(value, "opera", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "opera-busy", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "busy", StringComparison.OrdinalIgnoreCase))
        {
            return CelTimingMode.OperaBusy;
        }

        throw new ArgumentException(
            $"Invalid --cel-timing value '{value}'; expected freedo or opera.",
            nameof(value));
    }

    private static CdromTimingMode ParseCdromTiming(
        string? value,
        CdromTimingMode fallback = CdromTimingMode.FreeDoCompatible)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (string.Equals(value, "freedo", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "freedo-compatible", StringComparison.OrdinalIgnoreCase))
        {
            return CdromTimingMode.FreeDoCompatible;
        }

        if (string.Equals(value, "opera", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "opera-cycle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "cycle", StringComparison.OrdinalIgnoreCase))
        {
            return CdromTimingMode.OperaCycleBased;
        }

        throw new ArgumentException(
            $"Invalid --cd-timing value '{value}'; expected freedo or opera.",
            nameof(value));
    }

    private static CdSpeed ParseCdSpeed(string? value, CdSpeed fallback = CdSpeed.X2)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var text = value.Trim();
        if (string.Equals(text, "unbounded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "none", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "max", StringComparison.OrdinalIgnoreCase))
        {
            return CdSpeed.Unbounded;
        }

        if (text.EndsWith('x') || text.EndsWith('X'))
        {
            text = text[..^1].Trim();
        }

        return text switch
        {
            "1" => CdSpeed.X1,
            "2" => CdSpeed.X2,
            "4" => CdSpeed.X4,
            "8" => CdSpeed.X8,
            "16" => CdSpeed.X16,
            "24" => CdSpeed.X24,
            "32" => CdSpeed.X32,
            _ => throw new ArgumentException(
                $"Invalid --cd-speed value '{value}'; expected unbounded or 1x, 2x, 4x, 8x, 16x, 24x, 32x.",
                nameof(value)),
        };
    }

    private static int ParseCpuClock(string? value, int fallback = EmulatorConstants.ArmClock)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var text = value.Trim();
        var isMegahertz = text.EndsWith("mhz", StringComparison.OrdinalIgnoreCase);
        if (isMegahertz)
        {
            text = text[..^3].Trim();
        }
        else if (text.EndsWith('x') || text.EndsWith('X'))
        {
            text = text[..^1].Trim();
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            || !double.IsFinite(number)
            || number <= 0)
        {
            throw new ArgumentException(
                $"Invalid --cpu-clock value '{value}'; expected a multiplier such as 2x or a clock such as 25MHz.",
                nameof(value));
        }

        var clock = isMegahertz
            ? (int)Math.Round(number * 1_000_000.0)
            : (int)Math.Round(number * EmulatorConstants.ArmClock);
        if (clock < EmulatorConstants.ArmClock / 2 || clock > EmulatorConstants.ArmClock * 4)
        {
            throw new ArgumentException(
                $"Invalid --cpu-clock value '{value}'; the clock must be between 0.5x and 4x "
                + $"({EmulatorConstants.ArmClock / 2_000_000.0:0.##}-{EmulatorConstants.ArmClock * 4 / 1_000_000.0:0.##} MHz).",
                nameof(value));
        }

        return clock;
    }

    private static uint? ParseRandomSeed(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "default", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(value, "random", StringComparison.OrdinalIgnoreCase))
        {
            return (uint)Random.Shared.NextInt64(0, (long)uint.MaxValue + 1);
        }

        var text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        if (!uint.TryParse(
                text,
                value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? NumberStyles.HexNumber : NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var seed))
        {
            throw new ArgumentException(
                $"Invalid --random-seed value '{value}'; expected default, random, or a 32-bit seed.",
                nameof(value));
        }

        return seed;
    }

    private static FixMode ParseFixMode(string value)
    {
        var text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bits))
        {
            throw new ArgumentException($"Invalid --fix-mode value '{value}'; expected hexadecimal bits such as 0x20.", nameof(value));
        }

        return (FixMode)bits;
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
        Console.WriteLine($"{FiveDoVersion.ProductName} {FiveDoVersion.Version}");
        Console.WriteLine("Usage: 5do <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  run [disc] [--bios path] [--bios2 path] [--bios-auto folder] [--frames N] [--state-in path] [--state-out path]");
        Console.WriteLine("             [--frame-out path] [--audio null|wav|openal] [--wav-out path] [--volume 0..1] [--cd-trace path]");
        Console.WriteLine("             [--clio-trace path] [--fiq-trace path] [--kprint path] [--kprint-pc path]");
        Console.WriteLine("             [--watch-lowmem path] [--watch-from-frame N] [--watch-start N] [--watch-end N]");
        Console.WriteLine("             [--dump-ram path] [--dump-ram-offset N] [--dump-ram-length N]");
        Console.WriteLine("             [--dump-dsp prefix] [--input-script path] [--script-base absolute|state] [--autopilot road] [--progress N] [--cel-trace path] [--pbus-trace path]");
        Console.WriteLine("             [--capture-dir path] [--capture-every N] [--nvram path] [--high-res] [--prefetch]");
        Console.WriteLine("             [--nvram-mode global|per-game] [--nvram-version 0-9] [--nvram-dir path]");
        Console.WriteLine("             [--profile compatible|accuracy|performance|custom]");
        Console.WriteLine("             [--arm-timing freedo|arm60] [--clio-timers freedo|opera] [--cel-timing freedo|opera]");
        Console.WriteLine("             [--cd-timing freedo|opera] [--cd-speed unbounded|1x|2x|4x|8x|16x|24x|32x]");
        Console.WriteLine("             [--cpu-clock 0.5x-4x|6.25-50MHz]");
        Console.WriteLine("             [--random-seed default|random|SEED] [--poke ADDR=HEX] [--fix-mode 0xMASK] [--no-game-fixes] [--realtime] [--json]");
        Console.WriteLine("  trace [disc] --bios path [--bios2 path] [--bios-auto folder] [--frames N] [--trace-frames N] [--ring N]");
        Console.WriteLine("             [--stop-pc 0xADDR] [--stop-kprint text] [--stop-cd 0xCMD]");
        Console.WriteLine("             [--stop-invalid] [--count N] [--out path] [--dump-dsp prefix]");
        Console.WriteLine("             [--dsp-trace path] [--input-script path] [--nvram path] [--high-res] [--prefetch]");
        Console.WriteLine("             [--nvram-mode global|per-game] [--nvram-version 0-9] [--nvram-dir path]");
        Console.WriteLine("             [--profile compatible|accuracy|performance|custom]");
        Console.WriteLine("             [--arm-timing freedo|arm60] [--clio-timers freedo|opera] [--cel-timing freedo|opera]");
        Console.WriteLine("             [--cd-timing freedo|opera] [--cd-speed unbounded|1x|2x|4x|8x|16x|24x|32x]");
        Console.WriteLine("             [--cpu-clock 0.5x-4x|6.25-50MHz]");
        Console.WriteLine("             [--random-seed default|random|SEED] [--fix-mode 0xMASK] [--no-game-fixes]");
        Console.WriteLine("             [--dump-ram path] [--dump-ram-offset N] [--dump-ram-length N]");
        Console.WriteLine("             Runs the machine and records a diagnostic instruction ring buffer.");
        Console.WriteLine("  info <disc> [--json]                 Disc geometry, volume header, and game ID.");
        Console.WriteLine("  disc dump <disc> --out path [--sector N] [--count N]");
        Console.WriteLine("                                       Write raw 2048-byte sectors for diagnostics.");
        Console.WriteLine("  fs list <disc> [--json]              List the 3DO (Opera FS) file tree.");
        Console.WriteLine("  fs extract <disc> --out dir [--filter prefix]");
        Console.WriteLine("                                       Extract files from a 3DO disc image.");
        Console.WriteLine("  bios info <path> [--json]            BIOS size, MD5, region, and Anvil detection.");
        Console.WriteLine("  bios list|scan <folder> [--recursive] [--json]  List and validate BIOS images.");
        Console.WriteLine("  bios select <disc> --folder <dir> [--json]      Auto-select a BIOS for a disc.");
        Console.WriteLine("  nvram path [disc] [--mode global|per-game] [--version 0-9] [--dir path] [--json]");
        Console.WriteLine("                                       Resolve the NVRAM image for a game.");
        Console.WriteLine("  nvram list [--dir path] [--json]    List global and per-game NVRAM images.");
        Console.WriteLine("  nvram export [disc] --out path [--mode ...] [--version N] [--dir path]");
        Console.WriteLine("  nvram import [disc] --in path [--mode ...] [--version N] [--dir path]");
        Console.WriteLine("  nvram reset [disc] [--mode ...] [--version N] [--dir path]");
        Console.WriteLine("                                       Back up, restore, or clear NVRAM data.");
        Console.WriteLine("  audio analyze <wav> [--json]         WAV format, peak, RMS, clipping, and DC offset.");
        Console.WriteLine("  state inspect <path> [--json]        Save-state header and section table.");
        Console.WriteLine("  state slots <dir> [--count N] [--json]");
        Console.WriteLine("                                       List numbered save slots and metadata.");
        Console.WriteLine("  disasm <file> [--offset N] [--count N] [--base 0xADDR]");
        Console.WriteLine("                                       Disassemble raw little-endian ARM words.");
        Console.WriteLine("  bench [--frames N] [--json]          Synthetic-ROM frame benchmark.");
        Console.WriteLine("  selftest [--json]                    Determinism and state roundtrip checks.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -v, --version  Show version");
        Console.WriteLine("  -h, --help     Show help");
    }

    /// <summary>Requests 1 ms timer resolution while a real-time CLI run is active.</summary>
    private static class RealtimeTimer
    {
        private static int _depth;

        internal static void Begin()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            if (Interlocked.Increment(ref _depth) == 1)
            {
                _ = timeBeginPeriod(1);
            }
        }

        internal static void End()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            if (Interlocked.Decrement(ref _depth) == 0)
            {
                _ = timeEndPeriod(1);
            }
        }

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint timeBeginPeriod(uint period);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint timeEndPeriod(uint period);
    }

    private sealed class ParsedOptions
    {
        internal Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal bool Json { get; set; }

        internal string? Positional { get; set; }

        internal string? Get(string name)
        {
            return Values.TryGetValue(name, out var value) ? value : null;
        }

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

    internal static uint Mov(int rd, uint value)
    {
        return DpImmediate(13, rd, 0, value);
    }

    internal static uint Mvn(int rd, uint value)
    {
        return DpImmediate(15, rd, 0, value);
    }

    internal static uint AddImmediate(int rd, int rn, uint value)
    {
        return DpImmediate(4, rd, rn, value);
    }

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
