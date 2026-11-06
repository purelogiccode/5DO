using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FiveDo.DiffHarness;

/// <summary>Runs the 5DO CLI and maps its checkpoint JSON.</summary>
internal static class FiveDoCaptureRunner
{
    /// <summary>Captures one title with 5DO.</summary>
    public static CaptureResult Capture(
        HarnessOptions harness,
        DiffManifest manifest,
        DiffTitle title,
        string repoRoot,
        string manifestDirectory,
        string outputDirectory,
        CliCommand cli,
        uint seed)
    {
        var frames = harness.Frames ?? DiffManifestLoader.ResolveFrames(manifest, title);
        var checkpoints = DiffManifestLoader.ResolveCheckpoints(manifest, title);
        if (harness.Frames is not null)
        {
            checkpoints = checkpoints.Where(frame => frame <= frames).ToArray();
            if (checkpoints.Length == 0)
            {
                checkpoints = [frames];
            }
        }

        var window = manifest.Defaults?.Compare?.FrameWindow ?? 0;
        var runFrames = frames + window;
        var captureCheckpoints = DiffManifestLoader.ExpandCheckpoints(checkpoints, window, runFrames);

        var bios = harness.BiosPath ?? DiffManifestLoader.ResolveBios(repoRoot, manifestDirectory, manifest, title);
        var disc = DiffManifestLoader.ResolvePath(repoRoot, manifestDirectory, title.Disc);
        var inputScript = DiffManifestLoader.ResolveInputScript(repoRoot, manifestDirectory, manifest, title);
        var options = DiffManifestLoader.MergeOptions(
            manifest.Defaults?.Options,
            title.Options,
            harness.ClioTimers,
            harness.Profile,
            harness.ArmTiming,
            harness.CelTiming,
            harness.CdTiming);
        var titleDirectory = Path.Combine(outputDirectory, HarnessCommon.SafeName(title.Id));
        Directory.CreateDirectory(titleDirectory);

        if (!File.Exists(disc))
        {
            return Failed(disc, bios, frames, "disc image not found: " + disc);
        }

        if (!File.Exists(bios))
        {
            return Failed(disc, bios, frames, "BIOS not found: " + bios);
        }

        var arguments = new List<string>(cli.Prefix)
        {
            "run",
            disc,
            "--bios",
            bios,
            "--frames",
            runFrames.ToString(CultureInfo.InvariantCulture),
            "--checkpoints",
            string.Join(',', captureCheckpoints),
            "--audio",
            "null",
            "--audio-hash",
            "--prefetch",
            "--json",
            "--random-seed",
            seed.ToString(CultureInfo.InvariantCulture),
            "--nvram",
            Path.Combine(titleDirectory, "nvram.ram"),
        };

        var imageDirectory = Path.Combine(titleDirectory, "images");
        if (!harness.NoImages)
        {
            arguments.Add("--capture-dir");
            arguments.Add(imageDirectory);
            arguments.Add("--capture-frames");
            arguments.Add(string.Join(',', checkpoints));
        }
        AddOptions(arguments, options);
        if (inputScript is not null)
        {
            arguments.Add("--input-script");
            arguments.Add(inputScript);
        }

        ProcessResult process = HarnessCommon.RunProcess(
            cli.FileName,
            arguments,
            repoRoot,
            harness.TimeoutSeconds ?? manifest.Defaults?.TimeoutSeconds ?? 180);

        var stdoutPath = Path.Combine(titleDirectory, "fivedo.stdout.json");
        var stderrPath = Path.Combine(titleDirectory, "fivedo.stderr.txt");
        File.WriteAllText(stdoutPath, process.StdOut);
        File.WriteAllText(stderrPath, process.StdErr);

        FiveDoRunJson? run;
        try
        {
            run = JsonSerializer.Deserialize(process.StdOut, DiffJsonContext.Default.FiveDoRunJson);
        }
        catch (JsonException exception)
        {
            return Failed(
                disc,
                bios,
                frames,
                $"5DO output was not valid JSON: {exception.Message}\n{process.StderrTail()}",
                process.ElapsedSeconds,
                stderrPath);
        }

        if (!process.TimedOut && process.ExitCode != 0)
        {
            return Failed(
                disc,
                bios,
                frames,
                $"5DO {process.Describe()}\n{process.StderrTail()}",
                process.ElapsedSeconds,
                stderrPath);
        }

        if (process.TimedOut || run is null || !run.Ok)
        {
            return Failed(
                disc,
                bios,
                frames,
                $"5DO {(process.TimedOut ? "timed out" : "reported failure")}\n{process.StderrTail()}",
                process.ElapsedSeconds,
                stderrPath);
        }

        var samples = new List<CaptureSample>(captureCheckpoints.Length);
        var requiredFrames = checkpoints.ToHashSet();
        foreach (var checkpoint in captureCheckpoints)
        {
            FiveDoCheckpointJson? sample = run.Checkpoints?.FirstOrDefault(item => item.Frame == checkpoint);
            if (sample is null)
            {
                if (requiredFrames.Contains(checkpoint))
                {
                    return Failed(
                        disc,
                        bios,
                        frames,
                        $"5DO did not emit checkpoint {checkpoint}.",
                        process.ElapsedSeconds,
                        stderrPath);
                }

                continue;
            }

            samples.Add(new CaptureSample
            {
                Frame = sample.Frame,
                Pc = sample.Pc,
                Cpsr = sample.Cpsr,
                Instructions = sample.Instructions,
                FrameHash = sample.FrameHash,
                Samples = sample.Samples,
                AudioHash = sample.AudioHash,
                StateHash = sample.StateHash,
                CdStatus = sample.CdStatus,
                CdPoll = sample.CdPoll,
                CdCommandHash = sample.CdCommandHash,
                CdCommandCount = sample.CdCommandCount,
                DspPc = sample.DspPc,
                DspSema = sample.DspSema,
                DspSeed = sample.DspSeed,
                CelBusyCycles = sample.CelBusyCycles,
                ImagePath = ImagePath(imageDirectory, sample.Frame),
            });
        }

        return new CaptureResult
        {
            Engine = "5do",
            Ok = true,
            Disc = disc,
            Bios = bios,
            BiosMd5 = HarnessCommon.Md5File(bios),
            FramesRequested = frames,
            FramesRun = run.Frames,
            ElapsedSeconds = process.ElapsedSeconds,
            Profile = run.Profile,
            Width = run.SourceWidth,
            Height = run.SourceHeight,
            Checkpoints = [.. samples],
            Warnings = run.Warnings ?? [],
            LogPath = stderrPath,
            Seed = seed,
        };
    }

    private static string? ImagePath(string imageDirectory, long frame)
    {
        var path = Path.Combine(imageDirectory, $"f{frame:D6}.png");
        return File.Exists(path) ? path : null;
    }

    private static void AddOptions(List<string> arguments, DiffMachineOptions options)
    {
        AddValue(arguments, "--profile", options.Profile);
        AddValue(arguments, "--arm-timing", options.ArmTiming);
        AddValue(arguments, "--clio-timers", options.ClioTimers);
        AddValue(arguments, "--cel-timing", options.CelTiming);
        AddValue(arguments, "--cd-timing", options.CdTiming);
        AddValue(arguments, "--cd-speed", options.CdSpeed);
        AddValue(arguments, "--cpu-clock", options.CpuClock);
        AddValue(arguments, "--fix-mode", options.FixMode);
        if (options.HighResolution == true)
        {
            arguments.Add("--high-res");
        }

        if (options.NoGameFixes == true)
        {
            arguments.Add("--no-game-fixes");
        }

        if (options.StateHash == true)
        {
            arguments.Add("--state-hash");
        }

        arguments.AddRange(options.ExtraArgs ?? []);
    }

    private static void AddValue(List<string> arguments, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            arguments.Add(name);
            arguments.Add(value);
        }
    }

    private static CaptureResult Failed(
        string disc,
        string bios,
        int frames,
        string error,
        double elapsedSeconds = 0,
        string? logPath = null)
    {
        return new CaptureResult
        {
            Engine = "5do",
            Ok = false,
            Error = error,
            Disc = disc,
            Bios = bios,
            FramesRequested = frames,
            ElapsedSeconds = elapsedSeconds,
            Checkpoints = [],
            LogPath = logPath,
        };
    }
}

/// <summary>The subset of <c>run --json</c> consumed by the harness.</summary>
internal sealed class FiveDoRunJson
{
    public bool Ok { get; init; }

    public long Frames { get; init; }

    public double ElapsedSeconds { get; init; }

    public int SourceWidth { get; init; }

    public int SourceHeight { get; init; }

    public string? Profile { get; init; }

    public uint? AudioHash { get; init; }

    public FiveDoCheckpointJson[]? Checkpoints { get; init; }

    public string[]? Warnings { get; init; }
}

/// <summary>A checkpoint emitted by the 5DO CLI.</summary>
internal sealed class FiveDoCheckpointJson
{
    public long Frame { get; init; }

    public uint Pc { get; init; }

    public uint Cpsr { get; init; }

    public long Instructions { get; init; }

    public uint FrameHash { get; init; }

    public long Samples { get; init; }

    public uint? AudioHash { get; init; }

    public uint? StateHash { get; init; }

    public byte CdStatus { get; init; }

    public byte CdPoll { get; init; }

    public uint CdCommandHash { get; init; }

    public int CdCommandCount { get; init; }

    public uint DspPc { get; init; }

    public uint DspSema { get; init; }

    public uint DspSeed { get; init; }

    public ulong CelBusyCycles { get; init; }
}
