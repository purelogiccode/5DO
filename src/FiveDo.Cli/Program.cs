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
using FiveDo.Audio;
using FiveDo.Cdrom;
using FiveDo.Cheats;
using FiveDo.Core;
using FiveDo.Cpu;
using FiveDo.Diagnostics;
using FiveDo.Diagnostics.Logging;
using FiveDo.Diagnostics.Statistics;
using FiveDo.Host;
using FiveDo.Input;
using FiveDo.Machine;
using FiveDo.Storage;
using FiveDo.Tas;
using FiveDo.Video;

namespace FiveDo.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (Array.Exists(args, arg => string.Equals(arg, "--portable", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.SetEnvironmentVariable("FIVEDO_PORTABLE", "1");
            args = Array.FindAll(args, arg => !string.Equals(arg, "--portable", StringComparison.OrdinalIgnoreCase));
        }
        if (TryTakeOption(args, "--config-profile", out var configProfile, out var configArgs))
        {
            Environment.SetEnvironmentVariable("FIVEDO_PROFILE", configProfile);
            args = configArgs;
        }

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
                    "settings" => SettingsCommand(args),
                    "fs" => FsCommand(args),
                    "bios" => Bios(args),
                    "cheat" => CheatCommand(args),
                    "nvram" => NvramCommand(args),
                    "movie" => MovieCommand(args),
                    "audio" => AudioCommand(args),
                    "diagnostics" => DiagnosticsCommand(args),
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
        var framesSpecified = options.Get("--frames") is not null;
        var frames = options.GetInt("--frames", 600);
        var movieRecordPath = options.Get("--movie-record");
        var moviePlayPath = options.Get("--movie-play");
        var movieVerify = HasFlag(args, "--movie-verify");
        var movieIncludeState = !HasFlag(args, "--movie-no-state");
        Movie? movie = null;
        if (moviePlayPath is not null)
        {
            movie = MovieFile.Load(moviePlayPath);
            if (!framesSpecified)
            {
                frames = movie.FrameCount;
            }
            else if (frames > movie.FrameCount)
            {
                throw new ArgumentException(
                    $"--frames {frames} exceeds the movie length of {movie.FrameCount}.",
                    nameof(args));
            }
        }

        if (movieRecordPath is not null && moviePlayPath is not null)
        {
            throw new ArgumentException(
                "--movie-record and --movie-play cannot be combined.",
                nameof(args));
        }

        if (movieVerify && movie is null)
        {
            throw new ArgumentException("--movie-verify requires --movie-play.", nameof(args));
        }

        if (movieVerify && frames != movie!.FrameCount)
        {
            throw new ArgumentException(
                "Movie verification must play every frame; do not truncate with --frames.",
                nameof(args));
        }

        if (movie is not null && options.Get("--input-script") is not null)
        {
            throw new ArgumentException(
                "--movie-play and --input-script cannot be combined.",
                nameof(args));
        }

        if (movie is not null && options.Get("--autopilot") is not null)
        {
            throw new ArgumentException(
                "--movie-play and --autopilot cannot be combined.",
                nameof(args));
        }
        var biosPath = options.Get("--bios");
        var bios2Path = options.Get("--bios2");
        var biosAutoFolder = options.Get("--bios-auto");
        var stateIn = options.Get("--state-in");
        var stateOut = options.Get("--state-out");
        var frameOut = options.Get("--frame-out");
        var cdTrace = options.Get("--cd-trace");
        var cdTranscriptPath = options.Get("--cd-transcript");
        var recordGifPath = options.Get("--record-gif");
        var recordGifFps = options.GetInt("--record-fps", 30);
        if (recordGifFps is < 1 or > 60)
        {
            throw new ArgumentException("--record-fps must be between 1 and 60.", nameof(args));
        }
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
        var inputDevices = ParseInputDevices(options.Get("--input-device"));
        PbusFormat? pbusFormat = ParsePbusFormat(options.Get("--pbus-format"));
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
        var checkpointFrames = ParseCheckpoints(options.Get("--checkpoints"));
        var captureFrames = ParseCheckpoints(options.Get("--capture-frames"));
        var stateHashEnabled = HasFlag(args, "--state-hash");
        var audioHashEnabled = HasFlag(args, "--audio-hash") || checkpointFrames.Length > 0;
        var pokeSpec = options.Get("--poke");
        var cheatPath = options.Get("--cheat");
        var cheatCode = options.Get("--cheat-code");
        var cheatsDirectory = options.Get("--cheats-dir");
        var cheatAuto = HasFlag(args, "--cheat-auto");
        var realtime = HasFlag(args, "--realtime");
        var discPath = options.Positional;
        var playlistOption = options.Get("--playlist");
        if (playlistOption is not null && discPath is not null)
        {
            throw new ArgumentException("Use either a positional disc or --playlist, not both.", nameof(args));
        }

        if (playlistOption is not null)
        {
            discPath = DiscPlaylist.Load(playlistOption).Current.Path;
        }
        else if (discPath is not null && IsPlaylistPath(discPath))
        {
            discPath = DiscPlaylist.Load(discPath).Current.Path;
        }

        var swapAtFrame = options.GetInt("--swap-at", -1);
        var swapDiscPath = options.Get("--swap-disc");
        if (swapDiscPath is not null && swapAtFrame < 0)
        {
            throw new ArgumentException("--swap-disc requires --swap-at.", nameof(args));
        }

        if (swapDiscPath is null && swapAtFrame >= 0)
        {
            throw new ArgumentException("--swap-at requires --swap-disc.", nameof(args));
        }

        if (biosPath is not null && biosAutoFolder is not null)
        {
            throw new ArgumentException("Use either --bios or --bios-auto, not both.", nameof(args));
        }

        if (bios2Path is not null && biosPath is null && biosAutoFolder is null)
        {
            throw new ArgumentException("--bios2 requires --bios or --bios-auto.", nameof(args));
        }

        if (biosPath is null && biosAutoFolder is null && stateIn is null && movie?.State is null)
        {
            throw new ArgumentException(
                "run needs --bios <path> or --bios-auto <folder> unless --state-in or an embedded movie state supplies the ROM state.",
                nameof(args));
        }

        if (cheatPath is not null && cheatAuto)
        {
            throw new ArgumentException("Use either --cheat or --cheat-auto, not both.", nameof(args));
        }

        if (cheatsDirectory is not null && !cheatAuto)
        {
            throw new ArgumentException("--cheats-dir requires --cheat-auto.", nameof(args));
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

        var discHash = disc is null ? string.Empty : GameId.Compute(disc);
        if (movie is not null
            && !string.IsNullOrEmpty(movie.Header.DiscHash)
            && !string.IsNullOrEmpty(discHash)
            && !string.Equals(movie.Header.DiscHash, discHash, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine(
                $"warning: movie was recorded with disc {movie.Header.DiscHash}, current disc is {discHash}.");
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
        profileTuning = ApplyGameProfile(profileTuning, options.Get("--game-profile"));
        VideoRegion fallbackRegion = biosLoad.Selection?.Primary?.VideoStandard == BiosVideoStandard.Pal
            ? VideoRegion.Pal1
            : profileTuning.Region;
        var machine = new FiveDoMachine(new EmulatorTuning
        {
            HighResolution = HasFlag(args, "--high-res"),
            Region = ParseRegion(options.Get("--region"), fallbackRegion),
            ArmTiming = ParseArmTiming(options.Get("--arm-timing"), profileTuning.ArmTiming),
            ClioTimers = ParseClioTimers(options.Get("--clio-timers"), profileTuning.ClioTimers),
            CelTiming = ParseCelTiming(options.Get("--cel-timing"), profileTuning.CelTiming),
            CdromTiming = ParseCdromTiming(options.Get("--cd-timing"), profileTuning.CdromTiming),
            CdSpeed = ParseCdSpeed(options.Get("--cd-speed"), profileTuning.CdSpeed),
            ArmClock = ParseCpuClock(options.Get("--cpu-clock"), profileTuning.ArmClock),
            OperaDipir = profileTuning.OperaDipir,
            OperaSoftReset = profileTuning.OperaSoftReset,
            RandomSeed = ParseRandomSeed(options.Get("--random-seed")),
            FixMode = fixMode,
        });
        if (biosLoad.Image is not null)
        {
            machine.LoadBios(biosLoad.Image.Bios1.Span, biosLoad.Image.Bios2.Span);
        }

        using AudioOutput audio = CreateAudio(options, audioHashEnabled);
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
        ApplyInputDevices(machine.PbusSource, inputDevices);
        ApplyPbusFormat(machine.PbusSource, pbusFormat);

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

        CdromCommandTranscript? cdTranscript = null;
        StreamWriter? cdTranscriptWriter = null;
        if (cdTranscriptPath is not null)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(cdTranscriptPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            cdTranscriptWriter = new StreamWriter(cdTranscriptPath) { AutoFlush = true };
            cdTranscript = new CdromCommandTranscript();
            cdTranscript.Attach(machine.Cdrom);
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
        else if (movie?.State is not null)
        {
            using var stateStream = new MemoryStream(movie.State);
            machine.LoadState(stateStream);
        }

        if (ParseOptionalUnitInterval(options.Get("--cd-volume"), "--cd-volume") is { } cdVolume)
        {
            machine.CdAudioVolume = cdVolume;
        }

        if (HasFlag(args, "--mute-cd"))
        {
            machine.CdAudioMuted = true;
        }

        if (pokeSpec is not null)
        {
            var patched = MemoryPatch.Apply(machine, pokeSpec);
            Console.Error.WriteLine($"patched {patched} RAM byte(s)");
        }

        CheatSet? cheats = null;
        string? resolvedCheatPath = null;
        if (cheatAuto)
        {
            resolvedCheatPath = CheatStore.ResolvePath(
                CheatStore.ResolveDirectory(cheatsDirectory),
                gameId);
            if (File.Exists(resolvedCheatPath))
            {
                cheats = CheatSet.FromDocument(CheatStore.Load(resolvedCheatPath, gameId));
            }
        }
        else if (cheatPath is not null)
        {
            resolvedCheatPath = Path.GetFullPath(cheatPath);
            if (!File.Exists(resolvedCheatPath))
            {
                throw new FileNotFoundException("The cheat file was not found.", resolvedCheatPath);
            }

            cheats = CheatSet.FromDocument(CheatStore.Load(resolvedCheatPath, gameId));
        }

        if (cheatCode is not null)
        {
            cheats ??= new CheatSet();
            cheats.Add("Command line", string.Empty, cheatCode, enabled: true);
        }

        if (cheats is not null)
        {
            CheatEntry? invalid = cheats.Entries.FirstOrDefault(
                entry => entry.Enabled && entry.Error.Length > 0);
            if (invalid is not null)
            {
                throw new InvalidDataException(
                    $"Cheat '{invalid.Name}' is invalid: {invalid.Error}");
            }

            CheatSet activeCheats = cheats;
            machine.BeforeFrame = () => activeCheats.Apply(machine);
            activeCheats.Apply(machine);
            if (resolvedCheatPath is not null)
            {
                Console.Error.WriteLine(
                    $"loaded {activeCheats.ActiveCount} cheat(s) from {resolvedCheatPath}");
            }
        }

        MoviePbusSource? moviePlayback = null;
        MovieRecorder? movieRecorder = null;
        byte[]? movieState = null;
        var movieStartFrame = machine.FramesExecuted;
        if (movie is not null)
        {
            moviePlayback = new MoviePbusSource(movie);
            machine.PbusSource = moviePlayback;
        }
        else if (movieRecordPath is not null)
        {
            if (movieIncludeState)
            {
                using var stateStream = new MemoryStream();
                machine.SaveState(stateStream, discHash);
                movieState = stateStream.ToArray();
            }

            movieRecorder = new MovieRecorder();
            machine.PbusSource = new MovieRecordingPbusSource(
                machine.PbusSource ?? new PbusSource(),
                movieRecorder);
        }

        GifRecorder? gifRecorder = null;
        FileStream? gifStream = null;
        if (recordGifPath is not null)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(recordGifPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            gifStream = new FileStream(recordGifPath, FileMode.Create, FileAccess.Write);
            gifRecorder = new GifRecorder(
                gifStream,
                machine.FramesPerSecond,
                recordGifFps,
                options.GetInt("--record-frames", 0));
        }

        AudioFramePacer? realtimePacer = realtime ? new AudioFramePacer() : null;
        var realtimeClock = realtime ? Stopwatch.StartNew() : null;
        var realtimeNext = realtimeClock?.ElapsedTicks ?? 0.0;
        if (realtimePacer is not null)
        {
            RealtimeTimer.Begin();
        }

        var runClock = Stopwatch.StartNew();
        var checkpointResults = new List<CliCheckpointResult>(checkpointFrames.Length);
        var checkpointIndex = 0;
        var captureFrameIndex = 0;
        try
        {
            if (scriptedInput is null
                && roadAutopilot is null
                && progressEvery <= 0
                && pbusTraceWriter is null
                && captureDir is null
                && checkpointFrames.Length == 0
                && captureFrames.Length == 0
                && !realtime
                && swapDiscPath is null
                && gifRecorder is null)
            {
                machine.RunFrames(frames);
            }
            else
            {
                if (captureDir is not null && (captureEvery > 0 || captureFrames.Length > 0))
                {
                    Directory.CreateDirectory(captureDir);
                }

                for (var i = 0; i < frames; i++)
                {
                    if (swapAtFrame >= 0 && machine.FramesExecuted >= swapAtFrame)
                    {
                        IDiscSource swapDisc = DiscImageFactory.Open(
                            swapDiscPath!,
                            prefetch: HasFlag(args, "--prefetch"));
                        machine.InsertDisc(swapDisc);
                        disc?.Dispose();
                        disc = swapDisc;
                        discHash = GameId.Compute(swapDisc);
                        Console.Error.WriteLine(
                            $"swapped disc at frame {machine.FramesExecuted}: {swapDiscPath}");
                        swapAtFrame = -1;
                    }

                    scriptedInput?.AdvanceTo(machine.FramesExecuted);
                    machine.RunFrame();
                    gifRecorder?.OnFrame(machine.Frame);
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

                    if (captureDir is not null && captureFrames.Length > 0)
                    {
                        while (captureFrameIndex < captureFrames.Length
                            && machine.FramesExecuted >= captureFrames[captureFrameIndex])
                        {
                            if (machine.FramesExecuted == captureFrames[captureFrameIndex])
                            {
                                WriteFrame(machine, Path.Combine(captureDir, $"f{machine.FramesExecuted:D6}.png"));
                            }

                            captureFrameIndex++;
                        }
                    }

                    while (checkpointIndex < checkpointFrames.Length
                        && machine.FramesExecuted >= checkpointFrames[checkpointIndex])
                    {
                        if (machine.FramesExecuted == checkpointFrames[checkpointIndex])
                        {
                            checkpointResults.Add(CaptureCheckpoint(machine, audio, stateHashEnabled));
                        }

                        checkpointIndex++;
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

        gifRecorder?.Complete();
        gifStream?.Dispose();

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
        if (cdTranscriptWriter is not null)
        {
            cdTranscriptWriter.Write(cdTranscript?.ToText());
            cdTranscriptWriter.Dispose();
        }
        celTraceWriter?.Dispose();
        pbusTraceWriter?.Dispose();
        kprintWriter?.Dispose();
        kprintPcWriter?.Dispose();
        watchWriter?.Dispose();

        if (frameOut is not null)
        {
            WriteFrame(machine, frameOut);
        }

        if (stateOut is not null)
        {
            machine.SaveStateToFile(stateOut, discHash);
        }

        MovieVerification? movieVerification = null;
        if (movieRecorder is not null && movieRecordPath is not null)
        {
            var movieHeader = new MovieHeader
            {
                GameName = gameRecord?.Name
                    ?? (discPath is null ? null : Path.GetFileNameWithoutExtension(discPath)),
                GameId = gameId,
                DiscHash = string.IsNullOrEmpty(discHash) ? null : discHash,
                DiscFile = discPath is null ? null : Path.GetFileName(discPath),
                BiosMd5 = biosLoad.Image?.Md5,
                BiosName = biosLoad.Selection?.Primary?.Name
                    ?? (biosLoad.PrimaryPath is null
                        ? null
                        : Path.GetFileNameWithoutExtension(biosLoad.PrimaryPath)),
                StartFrame = movieStartFrame,
                FrameCount = movieRecorder.FrameCount,
                FinalFrameHash = FrameHash(machine),
                FinalProgramCounter = machine.Cpu.ProgramCounter,
                FinalStateHash = HashState(machine),
                HasState = movieState is not null,
                Options = BuildMovieOptions(machine),
            };
            MovieFile.Save(movieRecordPath, movieRecorder.Build(movieHeader, movieState));
            Console.Error.WriteLine(
                $"movie: wrote {movieRecorder.FrameCount} frame(s) to {movieRecordPath}");
        }

        if (moviePlayback is not null && movieVerify)
        {
            MovieHeader movieHeader = moviePlayback.Movie.Header;
            uint? finalStateHash = movieHeader.FinalStateHash is null ? null : HashState(machine);
            movieVerification = MovieVerifier.Verify(
                movieHeader,
                moviePlayback.Position,
                FrameHash(machine),
                machine.Cpu.ProgramCounter,
                finalStateHash);
            foreach (var difference in MovieVerifier.CompareOptions(movieHeader, BuildMovieOptions(machine)))
            {
                Console.Error.WriteLine($"movie option: {difference}");
            }

            Console.Error.WriteLine($"movie: {movieVerification.Summary}");
        }

        var result = new CliRunResult(
            Ok: true,
            Frames: machine.FramesExecuted,
            ElapsedSeconds: runClock.Elapsed.TotalSeconds,
            CyclesPerFrame: machine.LastFrameCycles,
            SourceWidth: machine.Frame.SourceWidth,
            SourceHeight: machine.Frame.SourceHeight,
            Region: machine.RegionProfile.Region.ToString(),
            FieldRate: machine.RegionProfile.FieldRate,
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
            NvramVersion: nvramSelection.NvramVersion,
            AudioHash: audio.Hash,
            Checkpoints: checkpointResults,
            Warnings: machine.StateWarnings,
            CheatCount: cheats?.ActiveCount ?? 0,
            CheatWrites: cheats?.Writes ?? 0,
            CheatPath: resolvedCheatPath,
            MoviePath: moviePlayPath ?? movieRecordPath,
            MovieFrames: moviePlayback?.Position ?? movieRecorder?.FrameCount,
            MovieVerification: movieVerification is null
                ? null
                : movieVerification.Ok ? "pass" : "fail",
            MovieFailures: movieVerification?.Failures,
            InputDevices: FormatInputDevices(inputDevices),
            GifPath: gifRecorder is null ? null : Path.GetFullPath(recordGifPath!),
            GifFrames: gifRecorder?.CapturedFrames);

        SaveNvramIfRequested(machine, nvramSelection);

        disc?.Dispose();
        WriteRunResult(result, json);
        return movieVerification is { Ok: false } ? 2 : 0;
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
        var inputDevices = ParseInputDevices(options.Get("--input-device"));
        PbusFormat? pbusFormat = ParsePbusFormat(options.Get("--pbus-format"));
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
        profileTuning = ApplyGameProfile(profileTuning, options.Get("--game-profile"));
        VideoRegion fallbackRegion = biosLoad.Selection?.Primary?.VideoStandard == BiosVideoStandard.Pal
            ? VideoRegion.Pal1
            : profileTuning.Region;
        var machine = new FiveDoMachine(new EmulatorTuning
        {
            HighResolution = HasFlag(args, "--high-res"),
            Region = ParseRegion(options.Get("--region"), fallbackRegion),
            ArmTiming = ParseArmTiming(options.Get("--arm-timing"), profileTuning.ArmTiming),
            ClioTimers = ParseClioTimers(options.Get("--clio-timers"), profileTuning.ClioTimers),
            CelTiming = ParseCelTiming(options.Get("--cel-timing"), profileTuning.CelTiming),
            CdromTiming = ParseCdromTiming(options.Get("--cd-timing"), profileTuning.CdromTiming),
            CdSpeed = ParseCdSpeed(options.Get("--cd-speed"), profileTuning.CdSpeed),
            ArmClock = ParseCpuClock(options.Get("--cpu-clock"), profileTuning.ArmClock),
            OperaDipir = profileTuning.OperaDipir,
            OperaSoftReset = profileTuning.OperaSoftReset,
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
        ApplyInputDevices(machine.PbusSource, inputDevices);
        ApplyPbusFormat(machine.PbusSource, pbusFormat);

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

    private static int SettingsCommand(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("settings needs the 'validate' or 'migrate' subcommand.", nameof(args));
        }

        ParsedOptions options = Parse(args, startIndex: 2);
        if (string.Equals(args[1], "profiles", StringComparison.OrdinalIgnoreCase))
        {
            var profiles = AppSettingsStore.ListProfiles()
                .Select(name => new CliSettingsProfile(
                    name,
                    AppSettingsStore.GetProfilePath(name),
                    AppSettingsStore.Inspect(AppSettingsStore.GetProfilePath(name)).Version))
                .ToArray();
            var result = new CliSettingsProfilesResult(
                true,
                PortablePaths.ProfilesDirectory,
                profiles);
            if (options.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(
                    result,
                    CliJsonContext.Default.CliSettingsProfilesResult));
            }
            else
            {
                Console.WriteLine($"directory: {result.Directory}");
                foreach (CliSettingsProfile profile in result.Profiles)
                {
                    Console.WriteLine($"  {profile.Name} (v{profile.Version})  {profile.Path}");
                }
            }

            return 0;
        }

        var path = options.Positional ?? options.Get("--path") ?? AppSettingsStore.DefaultPath;
        if (string.Equals(args[1], "validate", StringComparison.OrdinalIgnoreCase))
        {
            SettingsFileInfo info = AppSettingsStore.Inspect(path);
            var result = new CliSettingsValidationResult(
                info.Exists && info.Valid,
                path,
                info.Exists,
                info.Valid,
                info.Version,
                info.Error);
            if (options.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(
                    result,
                    CliJsonContext.Default.CliSettingsValidationResult));
            }
            else
            {
                Console.WriteLine($"path:    {path}");
                Console.WriteLine($"exists:  {info.Exists}");
                Console.WriteLine($"valid:   {info.Valid}");
                Console.WriteLine($"version: {info.Version}");
                if (info.Error is not null)
                {
                    Console.WriteLine($"error:   {info.Error}");
                }
            }

            return result.Ok ? 0 : 1;
        }

        if (string.Equals(args[1], "migrate", StringComparison.OrdinalIgnoreCase))
        {
            int fromVersion = AppSettingsStore.Migrate(path);
            var result = new CliSettingsMigrationResult(
                true,
                path,
                fromVersion,
                AppSettings.CurrentVersion,
                fromVersion != AppSettings.CurrentVersion);
            if (options.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(
                    result,
                    CliJsonContext.Default.CliSettingsMigrationResult));
            }
            else
            {
                Console.WriteLine($"path:        {path}");
                Console.WriteLine($"fromVersion: {fromVersion}");
                Console.WriteLine($"toVersion:   {AppSettings.CurrentVersion}");
                Console.WriteLine($"changed:     {result.Changed}");
            }

            return 0;
        }

        throw new ArgumentException($"Unknown settings subcommand '{args[1]}'.", nameof(args));
    }

    private static int DiscCommand(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("disc needs the 'dump', 'tracks', 'verify', or 'playlist' subcommand.", nameof(args));
        }

        if (string.Equals(args[1], "playlist", StringComparison.OrdinalIgnoreCase))
        {
            ParsedOptions options = Parse(args, startIndex: 2);
            var playlistPath = options.Positional
                ?? throw new ArgumentException("disc playlist needs an M3U path.", nameof(args));
            DiscPlaylist playlist = DiscPlaylist.Load(playlistPath);
            var entries = new List<CliDiscPlaylistEntry>(playlist.Count);
            for (var i = 0; i < playlist.Count; i++)
            {
                DiscPlaylistEntry entry = playlist.Entries[i];
                entries.Add(new CliDiscPlaylistEntry(i, entry.Path, entry.Title, entry.DisplayName));
            }

            var result = new CliDiscPlaylistResult(
                true,
                playlist.SourcePath,
                playlist.CurrentIndex,
                playlist.Count,
                entries);
            if (options.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(
                    result,
                    CliJsonContext.Default.CliDiscPlaylistResult));
            }
            else
            {
                Console.WriteLine($"path:    {playlist.SourcePath}");
                Console.WriteLine($"entries: {playlist.Count}");
                foreach (CliDiscPlaylistEntry entry in entries)
                {
                    var marker = entry.Index == playlist.CurrentIndex ? "*" : " ";
                    Console.WriteLine($"{marker} {entry.Index}: {entry.DisplayName} -> {entry.Path}");
                }
            }

            return 0;
        }

        if (string.Equals(args[1], "verify", StringComparison.OrdinalIgnoreCase))
        {
            ParsedOptions options = Parse(args, startIndex: 2);
            var verifyPath = options.Positional ?? throw new ArgumentException("disc verify needs a disc path.", nameof(args));
            using IDiscSource source = DiscImageFactory.Open(verifyPath);
            DiscVerificationResult verification = DiscVerifier.Verify(
                source,
                options.GetInt("--max-errors", 32));
            var result = new CliDiscVerifyResult(
                verification.Ok,
                verifyPath,
                verification.Sectors,
                verification.SectorsVerified,
                verification.Tracks,
                verification.BytesVerified,
                $"0x{verification.Hash:x16}",
                verification.Errors);
            if (options.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(
                    result,
                    CliJsonContext.Default.CliDiscVerifyResult));
            }
            else
            {
                Console.WriteLine($"path:        {result.Path}");
                Console.WriteLine($"ok:          {result.Ok}");
                Console.WriteLine($"sectors:     {result.SectorsVerified}/{result.Sectors}");
                Console.WriteLine($"tracks:      {result.Tracks}");
                Console.WriteLine($"bytes:       {result.BytesVerified}");
                Console.WriteLine($"hash:        {result.Hash}");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"error:       {error}");
                }
            }

            return result.Ok ? 0 : 1;
        }

        if (string.Equals(args[1], "tracks", StringComparison.OrdinalIgnoreCase))
        {
            ParsedOptions options = Parse(args, startIndex: 2);
            var trackPath = options.Positional ?? throw new ArgumentException("disc tracks needs a disc path.", nameof(args));
            using IDiscSource source = DiscImageFactory.Open(trackPath);
            var format = Path.GetExtension(trackPath).TrimStart('.').ToLowerInvariant();
            var tracks = new List<CliDiscTrackResult>();
            if (source is ITrackedDiscSource tracked)
            {
                foreach (DiscTrack track in tracked.Tracks)
                {
                    tracks.Add(new CliDiscTrackResult(
                        track.Number,
                        track.IsAudio ? "audio" : "data",
                        track.SectorSize,
                        track.SectorCount,
                        track.StartLba,
                        track.PayloadOffset));
                }
            }
            else
            {
                tracks.Add(new CliDiscTrackResult(
                    1,
                    "data",
                    source.SectorSize,
                    source.SectorCount,
                    0,
                    0));
            }

            if (options.Json)
            {
                var result = new CliDiscTracksResult(
                    true,
                    trackPath,
                    tracks);
                Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliDiscTracksResult));
                return 0;
            }

            foreach (CliDiscTrackResult track in tracks)
            {
                int dataSize = track.DataSize;
                if (source is not ITrackedDiscSource)
                {
                    dataSize = (long)source.SectorCount * source.SectorSize <= int.MaxValue
                        ? source.SectorCount * source.SectorSize
                        : dataSize;
                }

                Console.WriteLine(
                    $"track {track.Number}: type={track.Type} format={(format.Length == 0 ? "raw" : format)} "
                    + $"dataSize={dataSize} frames={track.Frames} start={track.Start} payload={track.PayloadOffset}");
            }

            return 0;
        }

        if (!string.Equals(args[1], "dump", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("disc needs the 'dump', 'tracks', 'verify', or 'playlist' subcommand.", nameof(args));
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

        if (args.Length >= 2 && string.Equals(args[1], "diff", StringComparison.OrdinalIgnoreCase))
        {
            ParsedOptions diffOptions = Parse(args, startIndex: 2);
            string[] paths = args.Skip(2)
                .Where(arg => !arg.StartsWith('-'))
                .Take(2)
                .ToArray();
            if (paths.Length != 2)
            {
                throw new ArgumentException("state diff needs two state paths.", nameof(args));
            }

            StateDiffResult diff = StateDiff.Compare(paths[0], paths[1]);
            if (diffOptions.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(
                    diff,
                    CliHostJsonContext.Default.StateDiffResult));
            }
            else
            {
                Console.WriteLine($"a: v{diff.VersionA} frames={diff.FramesA} disc={diff.DiscHashA}");
                Console.WriteLine($"b: v{diff.VersionB} frames={diff.FramesB} disc={diff.DiscHashB}");
                Console.WriteLine($"changed: {diff.ChangedSections} of {diff.Sections.Count}");
                foreach (StateSectionDiff section in diff.Sections)
                {
                    if (section.IsSame)
                    {
                        Console.WriteLine(
                            $"  {section.Id:D2} {section.Name,-8} same  {section.LengthA} bytes  hash=0x{section.HashA:x8}");
                        continue;
                    }

                    var location = section.VersionA == 0
                        ? "only in b"
                        : section.VersionB == 0
                            ? "only in a"
                            : $"first={section.FirstDifference} differing={section.DifferingBytes}";
                    Console.WriteLine(
                        $"  {section.Id:D2} {section.Name,-8} DIFF  {section.LengthA}/{section.LengthB} bytes  "
                        + $"hash=0x{section.HashA:x8}/0x{section.HashB:x8}  {location}");
                }
            }

            return 0;
        }

        if (args.Length < 2 || !string.Equals(args[1], "inspect", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "state needs 'inspect <path>', 'diff <a> <b>', or 'slots <dir>'.",
                nameof(args));
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

    private static int CheatCommand(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(
                "cheat needs list, add, enable, disable, remove, import, or export.",
                nameof(args));
        }

        var subcommand = args[1].ToLowerInvariant();
        ParsedOptions options = Parse(args, startIndex: 2);
        var directory = CheatStore.ResolveDirectory(options.Get("--dir"));
        var gameId = options.Get("--game");
        if (string.IsNullOrWhiteSpace(gameId) && options.Positional is not null)
        {
            using IDiscSource disc = DiscImageFactory.Open(options.Positional);
            gameId = GameId.Compute(disc);
        }

        if (string.IsNullOrWhiteSpace(gameId))
        {
            throw new ArgumentException("cheat needs --game <id> or a disc path.", nameof(args));
        }

        var path = CheatStore.ResolvePath(directory, gameId);
        switch (subcommand)
        {
            case "list":
                {
                    CheatDocument document = CheatStore.Load(path, gameId);
                    CheatSet set = CheatSet.FromDocument(document);
                    var entries = set.Entries
                        .Select(entry => new CliCheatEntry(
                            entry.Name,
                            entry.Description,
                            entry.Code,
                            entry.Enabled,
                            entry.Error.Length == 0 ? null : entry.Error))
                        .ToArray();
                    if (options.Json)
                    {
                        Console.WriteLine(JsonSerializer.Serialize(
                            new CliCheatListResult(true, path, CheatStore.NormalizeGameId(gameId), entries),
                            CliJsonContext.Default.CliCheatListResult));
                    }
                    else
                    {
                        Console.WriteLine($"path:  {path}");
                        Console.WriteLine($"game:  {CheatStore.NormalizeGameId(gameId)}");
                        if (entries.Length == 0)
                        {
                            Console.WriteLine("(no cheats)");
                        }

                        foreach (CliCheatEntry entry in entries)
                        {
                            var state = entry.Enabled ? "on " : "off";
                            Console.WriteLine(
                                $"{state}  {entry.Name}  {entry.Code}"
                                + (entry.Error is null ? string.Empty : $"  error={entry.Error}"));
                        }
                    }

                    return 0;
                }

            case "add":
                {
                    var name = options.Get("--name")
                        ?? throw new ArgumentException("cheat add needs --name <name>.", nameof(args));
                    var code = options.Get("--code")
                        ?? throw new ArgumentException("cheat add needs --code <code>.", nameof(args));
                    CheatDocument document = CheatStore.Load(path, gameId);
                    CheatSet set = CheatSet.FromDocument(document);
                    CheatEntry entry = set.Add(
                        name,
                        options.Get("--description") ?? string.Empty,
                        code,
                        enabled: !HasFlag(args, "--disabled"));
                    CheatStore.Save(path, CheatDocument.FromSet(gameId, set));
                    WriteCheatActionResult(options, "add", path, gameId, entry.Name, set.Count);
                    return 0;
                }

            case "enable":
            case "disable":
                {
                    var name = options.Get("--name")
                        ?? throw new ArgumentException($"cheat {subcommand} needs --name <name>.", nameof(args));
                    CheatDocument document = CheatStore.Load(path, gameId);
                    CheatSet set = CheatSet.FromDocument(document);
                    if (!set.SetEnabled(name, string.Equals(subcommand, "enable", StringComparison.Ordinal)))
                    {
                        throw new ArgumentException($"No cheat named '{name}' exists.", nameof(args));
                    }

                    CheatStore.Save(path, CheatDocument.FromSet(gameId, set));
                    WriteCheatActionResult(options, subcommand, path, gameId, name, set.Count);
                    return 0;
                }

            case "remove":
                {
                    var name = options.Get("--name")
                        ?? throw new ArgumentException("cheat remove needs --name <name>.", nameof(args));
                    CheatDocument document = CheatStore.Load(path, gameId);
                    CheatSet set = CheatSet.FromDocument(document);
                    if (!set.Remove(name))
                    {
                        throw new ArgumentException($"No cheat named '{name}' exists.", nameof(args));
                    }

                    CheatStore.Save(path, CheatDocument.FromSet(gameId, set));
                    WriteCheatActionResult(options, "remove", path, gameId, name, set.Count);
                    return 0;
                }

            case "import":
                {
                    var input = options.Get("--in")
                        ?? throw new ArgumentException("cheat import needs --in <path>.", nameof(args));
                    var inputPath = Path.GetFullPath(input);
                    if (!File.Exists(inputPath))
                    {
                        throw new FileNotFoundException("The cheat import file was not found.", inputPath);
                    }

                    CheatDocument source = CheatStore.Load(inputPath, gameId);
                    CheatDocument target = CheatStore.Load(path, gameId);
                    MergeCheats(target, source);
                    CheatStore.Save(path, target);
                    WriteCheatActionResult(options, "import", path, gameId, null, target.Cheats.Count);
                    return 0;
                }

            case "export":
                {
                    var output = options.Get("--out")
                        ?? throw new ArgumentException("cheat export needs --out <path>.", nameof(args));
                    CheatDocument document = CheatStore.Load(path, gameId);
                    CheatStore.Save(Path.GetFullPath(output), document);
                    WriteCheatActionResult(options, "export", path, gameId, null, document.Cheats.Count);
                    return 0;
                }

            default:
                throw new ArgumentException(
                    $"Unknown cheat subcommand '{subcommand}'. Use list, add, enable, disable, remove, import, or export.",
                    nameof(args));
        }
    }

    private static void MergeCheats(CheatDocument target, CheatDocument source)
    {
        foreach (CheatEntry entry in source.Cheats)
        {
            CheatEntry? existing = target.Cheats.FirstOrDefault(
                item => string.Equals(item.Name, entry.Name, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                target.Cheats.Add(entry.Clone());
            }
            else
            {
                existing.Description = entry.Description;
                existing.Code = entry.Code;
                existing.Enabled = entry.Enabled;
            }
        }
    }

    private static void WriteCheatActionResult(
        ParsedOptions options,
        string action,
        string path,
        string? gameId,
        string? name,
        int count)
    {
        var result = new CliCheatActionResult(
            true,
            action,
            path,
            CheatStore.NormalizeGameId(gameId),
            name,
            count);
        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliCheatActionResult));
        }
        else
        {
            Console.WriteLine($"{action}: {path}");
            Console.WriteLine($"game:  {result.GameId}");
            if (name is not null)
            {
                Console.WriteLine($"name:  {name}");
            }

            Console.WriteLine($"count: {count}");
        }
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
        int NvramVersion,
        IReadOnlyList<string> Fallbacks);

    private static int MovieCommand(string[] args)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException(
                "movie needs a subcommand: info <movie> | record <disc> --out <movie> | "
                + "play <movie> | verify <movie> | branch <movie> --frame N --out <movie>.",
                nameof(args));
        }

        return args[1].ToLowerInvariant() switch
        {
            "info" => MovieInfo(args),
            "record" => MovieRecord(args),
            "play" => MoviePlay(args, verify: false),
            "verify" => MoviePlay(args, verify: true),
            "branch" or "trim" => MovieBranch(args),
            _ => throw new ArgumentException(
                $"Unknown movie subcommand '{args[1]}'. Supported: info, record, play, verify, branch.",
                nameof(args)),
        };
    }

    private static int MovieInfo(string[] args)
    {
        ParsedOptions options = Parse(args, startIndex: 2);
        var path = options.Positional
            ?? throw new ArgumentException("movie info needs a movie path.", nameof(args));
        Movie movie = MovieFile.Load(path);
        var info = new CliMovieInfoResult(
            Ok: true,
            Path: Path.GetFullPath(path),
            FormatVersion: movie.Header.FormatVersion,
            EmulatorVersion: movie.Header.EmulatorVersion,
            CreatedUtc: movie.Header.CreatedUtc,
            GameName: movie.Header.GameName,
            GameId: movie.Header.GameId,
            DiscHash: movie.Header.DiscHash,
            DiscFile: movie.Header.DiscFile,
            BiosMd5: movie.Header.BiosMd5,
            BiosName: movie.Header.BiosName,
            StartFrame: movie.Header.StartFrame,
            FrameCount: movie.FrameCount,
            EndFrame: movie.Header.EndFrame,
            FinalFrameHash: movie.Header.FinalFrameHash,
            FinalProgramCounter: movie.Header.FinalProgramCounter,
            FinalStateHash: movie.Header.FinalStateHash,
            HasState: movie.HasState,
            StateBytes: movie.State?.Length ?? 0,
            FileBytes: new FileInfo(path).Length,
            Options: movie.Header.Options,
            Notes: movie.Header.Notes);
        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(info, CliJsonContext.Default.CliMovieInfoResult));
            return 0;
        }

        Console.WriteLine($"movie:       {info.Path}");
        Console.WriteLine($"format:      {info.FormatVersion} ({info.EmulatorVersion})");
        Console.WriteLine($"created:     {info.CreatedUtc}");
        Console.WriteLine($"game:        {info.GameName ?? "(unknown)"}");
        Console.WriteLine($"game id:     {info.GameId ?? "(unknown)"}");
        Console.WriteLine($"disc:        {info.DiscFile ?? "(none)"} {info.DiscHash}");
        Console.WriteLine($"bios:        {info.BiosName ?? "(none)"} {info.BiosMd5}");
        Console.WriteLine($"frames:      {info.FrameCount} (start {info.StartFrame}, end {info.EndFrame})");
        Console.WriteLine($"state:       {(info.HasState ? $"{info.StateBytes} bytes" : "none")}");
        Console.WriteLine($"file:        {info.FileBytes} bytes");
        if (info.FinalFrameHash is { } frameHash)
        {
            Console.WriteLine($"frame hash:  0x{frameHash:x8}");
            Console.WriteLine($"final pc:    0x{info.FinalProgramCounter.GetValueOrDefault():x8}");
            if (info.FinalStateHash is { } stateHash)
            {
                Console.WriteLine($"state hash:  0x{stateHash:x8}");
            }
        }

        if (info.Options is not null)
        {
            Console.WriteLine("options:");
            foreach ((var key, var value) in info.Options)
            {
                Console.WriteLine($"  {key}: {value}");
            }
        }

        if (!string.IsNullOrWhiteSpace(info.Notes))
        {
            Console.WriteLine($"notes:       {info.Notes}");
        }

        return 0;
    }

    private static int MovieRecord(string[] args)
    {
        ParsedOptions options = Parse(args, startIndex: 2);
        var output = options.Get("--out")
            ?? throw new ArgumentException("movie record needs --out <movie>.", nameof(args));
        var forwarded = new List<string> { "run" };
        for (var i = 2; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--out", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                continue;
            }

            forwarded.Add(args[i]);
        }

        forwarded.Add("--movie-record");
        forwarded.Add(output);
        return Run([.. forwarded]);
    }

    private static int MoviePlay(string[] args, bool verify)
    {
        ParsedOptions options = Parse(args, startIndex: 2);
        var moviePath = options.Positional
            ?? throw new ArgumentException(
                verify ? "movie verify needs a movie path." : "movie play needs a movie path.",
                nameof(args));
        var forwarded = new List<string> { "run" };
        var disc = options.Get("--disc");
        if (!string.IsNullOrWhiteSpace(disc))
        {
            forwarded.Add(disc);
        }

        var skippedMovie = false;
        for (var i = 2; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--disc", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                continue;
            }

            if (string.Equals(args[i], "--movie-play", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                continue;
            }

            if (!skippedMovie && string.Equals(args[i], moviePath, StringComparison.Ordinal))
            {
                skippedMovie = true;
                continue;
            }

            forwarded.Add(args[i]);
        }

        forwarded.Add("--movie-play");
        forwarded.Add(moviePath);
        if (verify)
        {
            forwarded.Add("--movie-verify");
        }

        return Run([.. forwarded]);
    }

    private static int MovieBranch(string[] args)
    {
        ParsedOptions options = Parse(args, startIndex: 2);
        var path = options.Positional
            ?? throw new ArgumentException("movie branch needs a movie path.", nameof(args));
        var output = options.Get("--out")
            ?? throw new ArgumentException("movie branch needs --out <movie>.", nameof(args));
        var frame = options.GetInt("--frame", -1);
        if (frame < 0)
        {
            throw new ArgumentException("movie branch needs --frame N.", nameof(args));
        }

        Movie movie = MovieFile.Load(path);
        Movie branch = MovieEditor.Branch(movie, frame);
        MovieFile.Save(output, branch);
        var result = new CliMovieActionResult(
            Ok: true,
            Path: Path.GetFullPath(output),
            FrameCount: branch.FrameCount,
            Message: $"branched at frame {frame}");
        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CliMovieActionResult));
            return 0;
        }

        Console.WriteLine(
            $"movie: branched {Path.GetFullPath(path)} at frame {frame} -> "
            + $"{Path.GetFullPath(output)} ({branch.FrameCount} frames)");
        return 0;
    }

    private static int AudioCommand(string[] args)
    {
        if (args.Length >= 2 && string.Equals(args[1], "devices", StringComparison.OrdinalIgnoreCase))
        {
            ParsedOptions deviceOptions = Parse(args, startIndex: 2);
            var devicesResult = new CliAudioDevicesResult(
                Ok: true,
                Devices: OpenAlAudioOutput.EnumerateDevices());
            if (deviceOptions.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(
                    devicesResult,
                    CliJsonContext.Default.CliAudioDevicesResult));
            }
            else
            {
                foreach (var device in devicesResult.Devices)
                {
                    Console.WriteLine(device);
                }
            }

            return 0;
        }

        if (args.Length < 2 || !string.Equals(args[1], "analyze", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("audio needs 'analyze <wav>' or 'devices'.", nameof(args));
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

    private static int DiagnosticsCommand(string[] args)
    {
        if (args.Length < 2 || !string.Equals(args[1], "bundle", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("diagnostics needs the 'bundle' subcommand.", nameof(args));
        }

        ParsedOptions options = Parse(args, startIndex: 2);
        var outputPath = options.Get("--out")
            ?? Path.Combine(
                PortablePaths.DataDirectory,
                "diagnostics",
                $"5do-diagnostics-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip");
        var result = DiagnosticsBundle.Create(
            outputPath,
            new DiagnosticsBundleOptions
            {
                SettingsPath = options.Get("--settings") ?? AppSettingsStore.DefaultPath,
                LogDirectory = options.Get("--logs") ?? LogPaths.GetLogDirectory(),
                StatePath = options.Get("--state"),
                FramePath = options.Get("--frame"),
                MoviePath = options.Get("--movie"),
                Notes = options.Get("--notes"),
            });
        var cliResult = new CliDiagnosticsBundleResult(
            Ok: true,
            Path: result.Path,
            Entries: result.Entries,
            ScrubHits: result.ScrubHits);
        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(
                cliResult,
                CliJsonContext.Default.CliDiagnosticsBundleResult));
        }
        else
        {
            Console.WriteLine($"bundle:      {cliResult.Path}");
            Console.WriteLine($"entries:     {cliResult.Entries.Count}");
            Console.WriteLine($"scrubbed:    {cliResult.ScrubHits}");
            foreach (var entry in cliResult.Entries)
            {
                Console.WriteLine($"  {entry}");
            }
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

    private static AudioOutput CreateAudio(ParsedOptions options, bool hashAudio)
    {
        var mode = options.Get("--audio") ?? "null";
        if (string.Equals(mode, "wav", StringComparison.OrdinalIgnoreCase))
        {
            var path = options.Get("--wav-out") ?? "audio.wav";
            return new AudioOutput(new WavAudioSink(path), null, hashAudio);
        }

        if (string.Equals(mode, "openal", StringComparison.OrdinalIgnoreCase))
        {
            var bufferMilliseconds = options.GetInt("--audio-buffer", 100);
            if (bufferMilliseconds is < 20 or > 500)
            {
                throw new ArgumentException(
                    "--audio-buffer must be between 20 and 500 milliseconds.",
                    nameof(options));
            }

            var output = OpenAlAudioOutput.TryCreate(
                bufferMilliseconds,
                options.Get("--audio-device"),
                out var openAlError);
            if (output is null)
            {
                Serilog.Log
                    .ForContext("SourceContext", "FiveDo.Cli.Program.Audio")
                    .WithoutBugReport()
                    .Warning(
                        "OpenAL is unavailable; audio is silent ({Error}).",
                        openAlError ?? "unknown error");

                return new AudioOutput(null, null, hashAudio);
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

            return new AudioOutput(null, output, hashAudio);
        }

        if (!string.Equals(mode, "null", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Unknown audio backend '{mode}'.", nameof(options));
        }

        return new AudioOutput(null, null, hashAudio);
    }

    /// <summary>Owns the CLI's optional WAV/OpenAL audio backend.</summary>
    private sealed class AudioOutput : IDisposable
    {
        private readonly WavAudioSink? _wav;
        private readonly OpenAlAudioOutput? _openAl;
        private readonly HashingAudioSink? _hashing;

        internal AudioOutput(WavAudioSink? wav, OpenAlAudioOutput? openAl, bool hashAudio = false)
        {
            _wav = wav;
            _openAl = openAl;
            if (hashAudio)
            {
                _hashing = new HashingAudioSink((IAudioSink?)_wav ?? _openAl);
            }
        }

        internal IAudioSink? Sink => (IAudioSink?)_hashing ?? (IAudioSink?)_wav ?? _openAl;

        internal uint? Hash => _hashing?.Hash;

        internal long SampleCount => _hashing?.SampleCount
            ?? _wav?.SampleCount
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
        return FrameHasher.Hash(machine.Frame);
    }

    private static CliCheckpointResult CaptureCheckpoint(
        FiveDoMachine machine,
        AudioOutput audio,
        bool stateHashEnabled)
    {
        return new CliCheckpointResult(
            Frame: machine.FramesExecuted,
            Pc: machine.Cpu.ProgramCounter,
            Instructions: machine.Cpu.InstructionsExecuted,
            FrameHash: FrameHash(machine),
            Samples: machine.AudioSamplesProduced,
            AudioHash: audio.Hash,
            StateHash: stateHashEnabled ? HashState(machine) : null,
            CdStatus: machine.Cdrom.DriveStatus,
            CdPoll: machine.Cdrom.Poll,
            DspPc: machine.Dsp.ProgramCounter,
            DspSema: ((uint)machine.Dsp.SemaphoreStatus << 16) | machine.Dsp.SemaphoreData,
            DspSeed: machine.Dsp.Seed,
            CelBusyCycles: machine.Madam.CelBusyCyclesRemaining);
    }

    private static uint HashState(FiveDoMachine machine)
    {
        using var stream = new MemoryStream();
        machine.SaveState(stream);
        return FrameHasher.HashBytes(stream.GetBuffer().AsSpan(0, checked((int)stream.Length)));
    }

    private static Dictionary<string, string> BuildMovieOptions(FiveDoMachine machine)
    {
        EmulatorTuning tuning = machine.Tuning;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["profile"] = EmulationProfiles.Detect(tuning).ToString(),
            ["armTiming"] = tuning.ArmTiming.ToString(),
            ["clioTimers"] = tuning.ClioTimers.ToString(),
            ["celTiming"] = tuning.CelTiming.ToString(),
            ["cdromTiming"] = tuning.CdromTiming.ToString(),
            ["cdSpeed"] = tuning.CdSpeed.ToString(),
            ["cpuClock"] = tuning.ArmClock.ToString(CultureInfo.InvariantCulture),
            ["randomSeed"] = tuning.RandomSeed?.ToString(CultureInfo.InvariantCulture) ?? "default",
            ["fixMode"] = $"0x{(uint)tuning.FixMode:x8}",
            ["highResolution"] = tuning.HighResolution.ToString(),
            ["operaDipir"] = tuning.OperaDipir.ToString(),
            ["operaSoftReset"] = tuning.OperaSoftReset.ToString(),
        };
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
        Console.WriteLine($"region:      {result.Region} ({result.FieldRate} Hz)");
        Console.WriteLine($"vdl modulo:  {result.VdlModulo}");
        Console.WriteLine($"pc:          0x{result.Pc:x8}");
        Console.WriteLine($"frame hash:  0x{result.FrameHash:x8}");
        Console.WriteLine($"samples:     {result.Samples}");
        if (result.GifPath is not null)
        {
            Console.WriteLine($"gif:         {result.GifPath} ({result.GifFrames} frames)");
        }
        if (result.AudioHash is { } audioHash)
        {
            Console.WriteLine($"audio hash:  0x{audioHash:x8}");
        }

        if (result.Checkpoints.Count > 0)
        {
            Console.WriteLine("checkpoints:");
            foreach (var checkpoint in result.Checkpoints)
            {
                Console.WriteLine(
                    $"  f={checkpoint.Frame} pc=0x{checkpoint.Pc:x8} "
                    + $"frame=0x{checkpoint.FrameHash:x8} "
                    + $"audio=0x{checkpoint.AudioHash.GetValueOrDefault():x8} "
                    + $"samples={checkpoint.Samples}");
            }
        }
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
        if (result.MoviePath is not null)
        {
            Console.WriteLine($"movie:       {result.MoviePath}");
            Console.WriteLine($"movie frames:{result.MovieFrames}");
            if (result.MovieVerification is not null)
            {
                Console.WriteLine($"movie check: {result.MovieVerification}");
                foreach (var failure in result.MovieFailures ?? [])
                {
                    Console.WriteLine($"  {failure}");
                }
            }
        }
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

    private static EmulatorTuning ApplyGameProfile(EmulatorTuning tuning, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return tuning;
        }

        GameProfile profile = GameProfileJson.Deserialize(File.ReadAllText(path));
        return tuning with
        {
            Region = profile.Region,
            ArmTiming = profile.ArmTiming,
            ClioTimers = profile.ClioTimers,
            CelTiming = profile.CelTiming,
            CdromTiming = profile.CdromTiming,
            CdSpeed = profile.CdSpeed,
            ArmClock = (int)Math.Round(
                EmulatorConstants.ArmClock * profile.CpuClockMultiplier,
                MidpointRounding.AwayFromZero),
        };
    }

    private static VideoRegion ParseRegion(string? value, VideoRegion fallback = VideoRegion.Ntsc)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        if (string.Equals(value, "ntsc", StringComparison.OrdinalIgnoreCase))
        {
            return VideoRegion.Ntsc;
        }

        if (string.Equals(value, "pal1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "pal", StringComparison.OrdinalIgnoreCase))
        {
            return VideoRegion.Pal1;
        }

        if (string.Equals(value, "pal2", StringComparison.OrdinalIgnoreCase))
        {
            return VideoRegion.Pal2;
        }

        throw new ArgumentException(
            $"Invalid --region value '{value}'; expected auto, ntsc, pal1, or pal2.",
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

    private static PbusFormat? ParsePbusFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "legacy" or "4do" => PbusFormat.Legacy,
            "opera" or "chain" => PbusFormat.OperaChain,
            _ => throw new ArgumentException(
                $"Invalid --pbus-format value '{value}'; expected legacy or opera.",
                nameof(value)),
        };
    }

    private static void ApplyPbusFormat(IPbusSource source, PbusFormat? format)
    {
        if (format is null)
        {
            return;
        }

        switch (source)
        {
            case PbusSource pbus:
                pbus.Format = format.Value;
                break;
            case ScriptedPbusSource scripted:
                scripted.Source.Format = format.Value;
                break;
        }
    }

    private static InputDeviceType[] ParseInputDevices(string? value)
    {
        var devices = new InputDeviceType[PbusEncoder.MaxControllers];
        Array.Fill(devices, InputDeviceType.Joypad);
        if (string.IsNullOrWhiteSpace(value))
        {
            return devices;
        }

        foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = item.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0 || separator == item.Length - 1)
            {
                throw new ArgumentException(
                    $"Invalid --input-device item '{item}'; expected <port>=<device>.",
                    nameof(value));
            }

            if (!int.TryParse(item[..separator], NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
                || port < 1
                || port > PbusEncoder.MaxControllers)
            {
                throw new ArgumentException(
                    $"Invalid --input-device port '{item[..separator]}'; expected 1-{PbusEncoder.MaxControllers}.",
                    nameof(value));
            }

            devices[port - 1] = ParseInputDevice(item[(separator + 1)..]);
        }

        return devices;
    }

    private static InputDeviceType ParseInputDevice(string value)
    {
        var text = value.Trim().Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);
        return text.ToLowerInvariant() switch
        {
            "joypad" or "pad" or "gamepad" => InputDeviceType.Joypad,
            "flightstick" or "stick" => InputDeviceType.Flightstick,
            "mouse" => InputDeviceType.Mouse,
            "lightgun" or "gun" => InputDeviceType.Lightgun,
            "arcadelightgun" or "arcadegun" => InputDeviceType.ArcadeLightgun,
            "orbatak" or "orbataktrackball" or "trackball" => InputDeviceType.OrbatakTrackball,
            "none" or "empty" => InputDeviceType.None,
            _ => throw new ArgumentException(
                $"Invalid --input-device value '{value}'; expected joypad, flightstick, mouse, lightgun, "
                + "arcade-lightgun, orbatak-trackball, or none.",
                nameof(value)),
        };
    }

    private static string FormatInputDevices(InputDeviceType[] devices)
    {
        return string.Join(
            ',',
            devices.Select(static (device, index) => $"{index + 1}={DeviceName(device)}"));
    }

    private static string DeviceName(InputDeviceType device)
    {
        return device switch
        {
            InputDeviceType.Joypad => "joypad",
            InputDeviceType.Flightstick => "flightstick",
            InputDeviceType.Mouse => "mouse",
            InputDeviceType.Lightgun => "lightgun",
            InputDeviceType.ArcadeLightgun => "arcade-lightgun",
            InputDeviceType.OrbatakTrackball => "orbatak-trackball",
            InputDeviceType.None => "none",
            _ => device.ToString(),
        };
    }

    private static void ApplyInputDevices(IPbusSource source, InputDeviceType[] devices)
    {
        var count = 1;
        for (var i = 0; i < devices.Length; i++)
        {
            if (devices[i] != InputDeviceType.Joypad)
            {
                count = i + 1;
            }
        }

        switch (source)
        {
            case PbusSource pbus:
                for (var i = 0; i < devices.Length; i++)
                {
                    pbus.SetDevice(i, devices[i]);
                }

                pbus.SetControllerCount(count);
                break;
            case ScriptedPbusSource scripted:
                for (var i = 0; i < devices.Length; i++)
                {
                    scripted.Source.SetDevice(i, devices[i]);
                }

                scripted.Source.SetControllerCount(count);
                break;
            case InputManager input:
                for (var i = 0; i < devices.Length; i++)
                {
                    input.SetDevice(i, devices[i]);
                }

                input.ControllerCount = count;
                break;
        }
    }

    private static bool IsPlaylistPath(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".m3u", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".m3u8", StringComparison.OrdinalIgnoreCase);
    }

    private static double? ParseOptionalUnitInterval(string? value, string option)
    {
        if (value is null)
        {
            return null;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed)
            || parsed is < 0 or > 1)
        {
            throw new ArgumentException($"{option} must be between 0 and 1.", option);
        }

        return parsed;
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

    private static bool TryTakeOption(string[] args, string option, out string value, out string[] remaining)
    {
        var index = Array.FindIndex(args, arg => string.Equals(arg, option, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length)
        {
            value = string.Empty;
            remaining = args;
            return false;
        }

        value = args[index + 1];
        remaining = args.Where((_, i) => i != index && i != index + 1).ToArray();
        return true;
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

    private static int[] ParseCheckpoints(string? specification)
    {
        if (string.IsNullOrWhiteSpace(specification))
        {
            return [];
        }

        var frames = new SortedSet<int>();
        foreach (var part in specification.Split(
            [',', ';', ' '],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var frame)
                || frame <= 0)
            {
                throw new ArgumentException(
                    $"Invalid checkpoint frame '{part}'. Checkpoints must be positive integers.",
                    nameof(specification));
            }

            frames.Add(frame);
        }

        return [.. frames];
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
        Console.WriteLine("             [--frame-out path] [--audio null|wav|openal] [--audio-device name] [--audio-buffer 20..500] [--wav-out path] [--volume 0..1] [--cd-volume 0..1] [--mute-cd] [--cd-trace path] [--cd-transcript path]");
        Console.WriteLine("             [--clio-trace path] [--fiq-trace path] [--kprint path] [--kprint-pc path]");
        Console.WriteLine("             [--watch-lowmem path] [--watch-from-frame N] [--watch-start N] [--watch-end N]");
        Console.WriteLine("             [--dump-ram path] [--dump-ram-offset N] [--dump-ram-length N]");
        Console.WriteLine("             [--dump-dsp prefix] [--input-script path] [--script-base absolute|state] [--autopilot road] [--progress N] [--cel-trace path] [--pbus-trace path]");
        Console.WriteLine("             [--capture-dir path] [--capture-every N] [--nvram path] [--high-res] [--prefetch]");
        Console.WriteLine("             [--nvram-mode global|per-game] [--nvram-version 0-9] [--nvram-dir path]");
        Console.WriteLine("             [--profile compatible|accuracy|performance|custom]");
        Console.WriteLine("             [--game-profile path] [--region auto|ntsc|pal1|pal2] [--arm-timing freedo|arm60] [--clio-timers freedo|opera] [--cel-timing freedo|opera]");
        Console.WriteLine("             [--cd-timing freedo|opera] [--cd-speed unbounded|1x|2x|4x|8x|16x|24x|32x]");
        Console.WriteLine("             [--input-device 1=joypad,2=mouse,...] [--pbus-format legacy|opera]");
        Console.WriteLine("             [--cpu-clock 0.5x-4x|6.25-50MHz]");
        Console.WriteLine("             [--random-seed default|random|SEED] [--poke ADDR=HEX] [--fix-mode 0xMASK] [--no-game-fixes] [--realtime] [--json]");
        Console.WriteLine("             [--cheat path] [--cheat-code code] [--cheat-auto] [--cheats-dir path]");
        Console.WriteLine("             [--checkpoints 3000,6000,12000] [--audio-hash] [--state-hash]");
        Console.WriteLine("             [--capture-frames 3000,6000] [--capture-dir path]");
        Console.WriteLine("             [--movie-record path] [--movie-play path] [--movie-verify] [--movie-no-state]");
        Console.WriteLine("             [--playlist path] [--swap-at N --swap-disc path]");
        Console.WriteLine("                                       Emit deterministic checkpoint samples and optional frame images.");
        Console.WriteLine("  trace [disc] --bios path [--bios2 path] [--bios-auto folder] [--frames N] [--trace-frames N] [--ring N]");
        Console.WriteLine("             [--stop-pc 0xADDR] [--stop-kprint text] [--stop-cd 0xCMD]");
        Console.WriteLine("             [--stop-invalid] [--count N] [--out path] [--dump-dsp prefix]");
        Console.WriteLine("             [--dsp-trace path] [--input-script path] [--nvram path] [--high-res] [--prefetch]");
        Console.WriteLine("             [--nvram-mode global|per-game] [--nvram-version 0-9] [--nvram-dir path]");
        Console.WriteLine("             [--profile compatible|accuracy|performance|custom]");
        Console.WriteLine("             [--game-profile path] [--region auto|ntsc|pal1|pal2] [--arm-timing freedo|arm60] [--clio-timers freedo|opera] [--cel-timing freedo|opera]");
        Console.WriteLine("             [--cd-timing freedo|opera] [--cd-speed unbounded|1x|2x|4x|8x|16x|24x|32x]");
        Console.WriteLine("             [--input-device 1=joypad,2=mouse,...] [--pbus-format legacy|opera]");
        Console.WriteLine("             [--cpu-clock 0.5x-4x|6.25-50MHz]");
        Console.WriteLine("             [--random-seed default|random|SEED] [--fix-mode 0xMASK] [--no-game-fixes]");
        Console.WriteLine("             [--dump-ram path] [--dump-ram-offset N] [--dump-ram-length N]");
        Console.WriteLine("             Runs the machine and records a diagnostic instruction ring buffer.");
        Console.WriteLine("  info <disc> [--json]                 Disc geometry, volume header, and game ID.");
        Console.WriteLine("  disc tracks <disc> [--json]          List every CUE/CHD track (data and audio).");
        Console.WriteLine("  disc playlist <m3u> [--json]        List multi-disc playlist entries.");
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
        Console.WriteLine("  settings validate [path] [--path path] [--json]");
        Console.WriteLine("                                       Check settings.json syntax and schema version.");
        Console.WriteLine("  settings migrate [path] [--path path] [--json]");
        Console.WriteLine("                                       Normalize and rewrite settings at the current version.");
        Console.WriteLine("  settings profiles [--json]           List named configuration profiles under the data directory.");
        Console.WriteLine("  --config-profile <name>              Use profiles/<name> for settings, NVRAM, cheats, and logs.");
        Console.WriteLine("  --portable                           Store settings, NVRAM, cheats, and logs beside the executable.");
        Console.WriteLine("  cheat list|add|enable|disable|remove|import|export --game ID [--dir path] [--json]");
        Console.WriteLine("                                       Manage per-game cheats. add needs --name and --code;");
        Console.WriteLine("                                       import/export use --in/--out. See docs/Cheats.md.");
        Console.WriteLine("  movie info <movie> [--json]          Movie metadata without running the emulator.");
        Console.WriteLine("  movie record <disc> --out <movie> [run options]");
        Console.WriteLine("                                       Record deterministic PBUS input while running.");
        Console.WriteLine("  movie play <movie> [--disc path] [run options]");
        Console.WriteLine("                                       Play a movie; the embedded state is optional.");
        Console.WriteLine("  movie verify <movie> [--disc path] [--json]");
        Console.WriteLine("                                       Replay a movie and compare recorded evidence.");
        Console.WriteLine("  movie branch <movie> --frame N --out <movie>");
        Console.WriteLine("                                       Keep frames before N for a new branch. See docs/Tas.md.");
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
