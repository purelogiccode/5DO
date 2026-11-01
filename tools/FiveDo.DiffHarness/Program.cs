using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace FiveDo.DiffHarness;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintUsage();
                return args.Length == 0 ? 2 : 0;
            }

            return args[0].ToLowerInvariant() switch
            {
                "run" => RunCommand(args),
                "validate" => ValidateCommand(args),
                "baseline" => BaselineCommand(args),
                "manifest" => ManifestCommand(args),
                "help" => HelpCommand(),
                _ => UnknownCommand(args[0]),
            };
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or FileNotFoundException
            or JsonException)
        {
            Console.Error.WriteLine("error: " + exception.Message);
            return 2;
        }
    }

    private static int RunCommand(string[] args)
    {
        HarnessOptions harness = ParseRunOptions(args);
        var repoRoot = DiffManifestLoader.FindRepositoryRoot();
        var manifestPath = Path.GetFullPath(harness.ManifestPath);
        var manifestDirectory = Path.GetDirectoryName(manifestPath)
            ?? Directory.GetCurrentDirectory();
        DiffManifest manifest = DiffManifestLoader.Load(manifestPath);
        var defaults = manifest.Defaults ?? new DiffDefaults();
        var outputDirectory = Path.IsPathRooted(harness.OutputDirectory)
            ? harness.OutputDirectory
            : Path.Combine(repoRoot, harness.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var titles = FilterTitles(manifest, harness).ToArray();
        if (titles.Length == 0)
        {
            throw new InvalidDataException("No manifest titles matched the filter.");
        }

        CliCommand cli = HarnessCommon.ResolveCli(
            repoRoot,
            harness.CliPath ?? defaults.Cli);
        var cliVersion = HarnessCommon.RunProcess(cli.FileName, [.. cli.Prefix, "--version"], repoRoot, 30)
            .StdOut.Trim();
        var operaCorePath = harness.NoOpera
            ? null
            : Path.GetFullPath(
                harness.OperaCorePath
                ?? DiffManifestLoader.ResolvePath(
                    repoRoot,
                    manifestDirectory,
                    manifest.Defaults?.OperaCore
                    ?? "References/CPP_SimpleGui/cmake-build-release/SimpleGui/Core/OperaCore.dll"));
        var defaultBios = DiffManifestLoader.ResolveBios(
            repoRoot,
            manifestDirectory,
            manifest,
            titles[0]);

        Console.WriteLine(
            $"diff: {titles.Length} title(s), manifest {manifest.Name}, output {outputDirectory}");

        var results = new List<DiffTitleResult>(titles.Length);
        foreach (var title in titles)
        {
            var frames = harness.Frames ?? DiffManifestLoader.ResolveFrames(manifest, title);
            var titleDirectory = Path.Combine(outputDirectory, HarnessCommon.SafeName(title.Id));
            Directory.CreateDirectory(titleDirectory);
            var fiveDoPath = Path.Combine(titleDirectory, "fivedo.capture.json");
            var operaPath = Path.Combine(titleDirectory, "opera.capture.json");

            Console.Write($"[{title.Id}] 5DO ... ");
            var fiveDoDisc = DiffManifestLoader.ResolvePath(repoRoot, manifestDirectory, title.Disc);
            CaptureResult fiveDo = LoadOrCapture(
                fiveDoPath,
                harness,
                () => FiveDoCaptureRunner.Capture(
                    harness, manifest, title, repoRoot, manifestDirectory, outputDirectory, cli),
                existing => existing.Ok
                    && existing.FramesRequested == frames
                    && string.Equals(existing.Disc, fiveDoDisc, StringComparison.OrdinalIgnoreCase));
            Console.WriteLine(fiveDo.Ok ? $"ok ({fiveDo.FramesRun} frames, {fiveDo.ElapsedSeconds:F1}s)" : "failed");
            SaveCapture(fiveDoPath, fiveDo);

            CaptureResult? opera;
            if (harness.NoOpera)
            {
                results.Add(new DiffTitleResult
                {
                    Id = title.Id,
                    Name = title.Name ?? title.Id,
                    Disc = fiveDo.Disc,
                    Status = fiveDo.Ok ? "FiveDoOnly" : "Error",
                    Frames = frames,
                    Error = fiveDo.Ok ? null : fiveDo.Error,
                    FiveDo = fiveDo,
                });
                continue;
            }

            Console.Write($"[{title.Id}] Opera ... ");
            opera = LoadOrCapture(
                operaPath,
                harness,
                () => OperaCoreCaptureRunner.Capture(
                    harness, manifest, title, repoRoot, manifestDirectory, outputDirectory),
                existing => existing.Ok && existing.FramesRequested == frames);
            Console.WriteLine(opera.Ok ? $"ok ({opera.FramesRun} frames, {opera.ElapsedSeconds:F1}s)" : "failed");
            SaveCapture(operaPath, opera);
            if (harness.CleanupIso && opera.Ok && opera.Iso is not null)
            {
                HarnessCommon.DeleteFile(opera.Iso);
                HarnessCommon.DeleteFile(opera.Iso + ".srm");
            }

            DiffTitleResult result = DiffComparer.Compare(
                manifest,
                title,
                fiveDo,
                opera,
                frames,
                harness.ComparePc,
                harness.PcTolerance);
            results.Add(result);
            Console.WriteLine(
                $"[{title.Id}] {result.Status} "
                + $"({result.AllowedCount} allowed, {result.DivergedCount} diverged)");
        }

        var provenance = new DiffProvenance
        {
            GitCommit = HarnessCommon.GitCommit(repoRoot),
            CliPath = cli.Path,
            CliVersion = cliVersion,
            OperaCorePath = operaCorePath,
            OperaCoreMd5 = operaCorePath is not null && File.Exists(operaCorePath)
                ? HarnessCommon.Md5File(operaCorePath)
                : null,
            BiosPath = defaultBios,
            BiosMd5 = File.Exists(defaultBios) ? HarnessCommon.Md5File(defaultBios) : null,
            Host = RuntimeInformation.OSDescription,
            Runtime = RuntimeInformation.FrameworkDescription,
        };
        DiffReport report = DiffComparer.BuildReport(
            manifest,
            manifestPath,
            [.. results],
            provenance);
        var reportPath = DiffReportWriter.WriteAll(outputDirectory, report, harness.ReportName);

        Console.WriteLine();
        Console.WriteLine(
            $"verdict: {(report.Summary.Passed ? "PASS" : "FAIL")} "
            + $"({report.Summary.Matched} match, {report.Summary.Allowed} allowed, "
            + $"{report.Summary.Diverged} diverged, {report.Summary.Errors} error)");
        Console.WriteLine($"report: {reportPath}");
        if (HasFlag(args, "--json"))
        {
            Console.WriteLine(JsonSerializer.Serialize(report, DiffJsonContext.Default.DiffReport));
        }

        return report.Summary.Passed ? 0 : 1;
    }

    private static int ValidateCommand(string[] args)
    {
        string? path = null;
        var strict = false;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--strict":
                    strict = true;
                    break;
                case "--json":
                    break;
                default:
                    path ??= args[i];
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("validate needs a manifest path.", nameof(args));
        }

        var repoRoot = DiffManifestLoader.FindRepositoryRoot();
        var manifestPath = Path.GetFullPath(path);
        var manifestDirectory = Path.GetDirectoryName(manifestPath) ?? repoRoot;
        DiffManifest manifest = DiffManifestLoader.Load(manifestPath);
        var problems = new List<string>();
        foreach (var title in manifest.Titles ?? [])
        {
            var bios = DiffManifestLoader.ResolveBios(repoRoot, manifestDirectory, manifest, title);
            var disc = DiffManifestLoader.ResolvePath(repoRoot, manifestDirectory, title.Disc);
            if (strict && !File.Exists(bios))
            {
                problems.Add($"{title.Id}: BIOS not found: {bios}");
            }

            if (strict && !File.Exists(disc))
            {
                problems.Add($"{title.Id}: disc not found: {disc}");
            }

            if (strict && !string.IsNullOrWhiteSpace(title.Iso))
            {
                var iso = DiffManifestLoader.ResolvePath(repoRoot, manifestDirectory, title.Iso);
                if (!File.Exists(iso))
                {
                    problems.Add($"{title.Id}: ISO not found: {iso}");
                }
            }

            var inputScript = DiffManifestLoader.ResolveInputScript(repoRoot, manifestDirectory, manifest, title);
            if (strict && inputScript is not null && !File.Exists(inputScript))
            {
                problems.Add($"{title.Id}: input script not found: {inputScript}");
            }
        }

        if (problems.Count > 0)
        {
            foreach (var problem in problems)
            {
                Console.Error.WriteLine(problem);
            }

            return 1;
        }

        Console.WriteLine(
            $"manifest ok: {manifest.Name}, {manifest.Titles?.Length ?? 0} title(s), "
            + $"schema {manifest.SchemaVersion}, strict {strict}");
        return 0;
    }

    private static int BaselineCommand(string[] args)
    {
        string? baselinePath = null;
        string? currentPath = null;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--baseline":
                    baselinePath = NextValue(args, ref i);
                    break;
                case "--current":
                    currentPath = NextValue(args, ref i);
                    break;
                case "--json":
                    break;
                default:
                    throw new ArgumentException($"Unknown baseline option '{args[i]}'.", nameof(args));
            }
        }

        if (baselinePath is null || currentPath is null)
        {
            throw new ArgumentException("baseline needs --baseline <report.json> and --current <report.json>.", nameof(args));
        }

        DiffReport baseline = LoadReport(baselinePath);
        DiffReport current = LoadReport(currentPath);
        var changes = new List<string>();
        foreach (var title in current.Titles)
        {
            DiffTitleResult? previous = baseline.Titles.FirstOrDefault(item =>
                string.Equals(item.Id, title.Id, StringComparison.Ordinal));
            if (previous is null)
            {
                continue;
            }

            foreach (var checkpoint in title.Checkpoints)
            {
                DiffCheckpointResult? oldCheckpoint = previous.Checkpoints.FirstOrDefault(item => item.Frame == checkpoint.Frame);
                if (oldCheckpoint is null)
                {
                    continue;
                }

                foreach (var field in checkpoint.Fields)
                {
                    DiffFieldResult? oldField = oldCheckpoint.Fields.FirstOrDefault(item =>
                        string.Equals(item.Field, field.Field, StringComparison.Ordinal));
                    if (oldField is null
                        || string.Equals(oldField.FiveDo, field.FiveDo, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    changes.Add(
                        $"{title.Id} f={checkpoint.Frame} {field.Field}: "
                        + $"{oldField.FiveDo ?? "-"} -> {field.FiveDo ?? "-"}");
                }
            }
        }

        if (changes.Count == 0)
        {
            Console.WriteLine("baseline ok: no 5DO field changed.");
            return 0;
        }

        foreach (var change in changes)
        {
            Console.WriteLine(change);
        }

        Console.WriteLine($"baseline changed: {changes.Count} field(s).");
        return 1;
    }

    private static int ManifestCommand(string[] args)
    {
        var roots = new List<RootSpec>();
        var extensions = new List<string> { ".chd" };
        var name = "5DO full collection";
        var output = "benchmarks/diff/full.json";
        var bios = "bios/panafz1.bin";
        var workDirectory = "artifacts/diff/work";
        string? operaCore = null;
        var frames = 3000;
        var checkpoints = new List<int>();
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--root":
                    roots.Add(new RootSpec("games" + (roots.Count == 0 ? string.Empty : (roots.Count + 1).ToString(CultureInfo.InvariantCulture)), NextValue(args, ref i)));
                    break;
                case "--out":
                    output = NextValue(args, ref i);
                    break;
                case "--name":
                    name = NextValue(args, ref i);
                    break;
                case "--bios":
                    bios = NextValue(args, ref i);
                    break;
                case "--core":
                    operaCore = NextValue(args, ref i);
                    break;
                case "--work":
                    workDirectory = NextValue(args, ref i);
                    break;
                case "--frames":
                    frames = int.Parse(NextValue(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--checkpoint":
                    checkpoints.Add(int.Parse(NextValue(args, ref i), CultureInfo.InvariantCulture));
                    break;
                case "--extension":
                    extensions.Add(NextValue(args, ref i));
                    break;
                default:
                    throw new ArgumentException($"Unknown manifest option '{args[i]}'.", nameof(args));
            }
        }

        if (roots.Count == 0)
        {
            throw new ArgumentException("manifest needs at least one --root.", nameof(args));
        }

        var repoRoot = DiffManifestLoader.FindRepositoryRoot();
        var manifest = DiffManifestBuilder.Build(
            name,
            roots,
            frames,
            [.. checkpoints],
            bios,
            operaCore,
            workDirectory,
            extensions);
        var outputPath = Path.IsPathRooted(output) ? output : Path.Combine(repoRoot, output);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(
            outputPath,
            JsonSerializer.Serialize(manifest, DiffJsonContext.Default.DiffManifest),
            Encoding.UTF8);
        Console.WriteLine(
            $"manifest: {manifest.Titles!.Length} title(s), {roots.Count} root(s), {outputPath}");
        return 0;
    }

    private static int HelpCommand()
    {
        PrintUsage();
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        return 2;
    }

    private static HarnessOptions ParseRunOptions(string[] args)
    {
        var harness = new HarnessOptions();
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out":
                    harness.OutputDirectory = NextValue(args, ref i);
                    break;
                case "--cli":
                    harness.CliPath = NextValue(args, ref i);
                    break;
                case "--core":
                    harness.OperaCorePath = NextValue(args, ref i);
                    break;
                case "--bios":
                    harness.BiosPath = NextValue(args, ref i);
                    break;
                case "--frames":
                    harness.Frames = int.Parse(NextValue(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--title":
                    harness.TitleFilters = [.. harness.TitleFilters, NextValue(args, ref i)];
                    break;
                case "--limit":
                    harness.Limit = int.Parse(NextValue(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--resume":
                    harness.Resume = true;
                    break;
                case "--no-opera":
                    harness.NoOpera = true;
                    break;
                case "--no-images":
                    harness.NoImages = true;
                    break;
                case "--cleanup-iso":
                    harness.CleanupIso = true;
                    break;
                case "--compare-pc":
                    harness.ComparePc = true;
                    break;
                case "--pc-tolerance":
                    harness.PcTolerance = long.Parse(NextValue(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--force":
                    harness.Force = true;
                    break;
                case "--timeout":
                    harness.TimeoutSeconds = int.Parse(NextValue(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--report-name":
                    harness.ReportName = NextValue(args, ref i);
                    break;
                case "--json":
                    break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"Unknown run option '{args[i]}'.", nameof(args));
                    }

                    if (harness.ManifestPath.Length > 0)
                    {
                        throw new ArgumentException($"Unexpected argument '{args[i]}'.", nameof(args));
                    }

                    harness.ManifestPath = args[i];
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(harness.ManifestPath))
        {
            throw new ArgumentException("run needs a manifest path.", nameof(args));
        }

        return harness;
    }

    private static IEnumerable<DiffTitle> FilterTitles(DiffManifest manifest, HarnessOptions harness)
    {
        IEnumerable<DiffTitle> titles = manifest.Titles ?? [];
        if (harness.TitleFilters.Length > 0)
        {
            titles = titles.Where(title => harness.TitleFilters.Any(filter =>
                title.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || (title.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)));
        }

        if (harness.Limit > 0)
        {
            titles = titles.Take(harness.Limit);
        }

        return titles;
    }

    private static CaptureResult LoadOrCapture(
        string path,
        HarnessOptions harness,
        Func<CaptureResult> capture,
        Func<CaptureResult, bool>? isValid = null)
    {
        if (harness.Resume && !harness.Force && File.Exists(path))
        {
            try
            {
                CaptureResult? existing = JsonSerializer.Deserialize(
                    File.ReadAllText(path),
                    DiffJsonContext.Default.CaptureResult);
                if (existing is not null && (isValid is null || isValid(existing)))
                {
                    return existing;
                }
            }
            catch (JsonException)
            {
                // A corrupt cache is re-captured.
            }
        }

        return capture();
    }

    private static void SaveCapture(string path, CaptureResult capture)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(capture, DiffJsonContext.Default.CaptureResult),
            Encoding.UTF8);
    }

    private static DiffReport LoadReport(string path)
    {
        DiffReport? report = JsonSerializer.Deserialize(
            File.ReadAllText(path),
            DiffJsonContext.Default.DiffReport);
        return report ?? throw new InvalidDataException($"Report '{path}' is empty.");
    }

    private static bool HasFlag(string[] args, string flag)
    {
        return args.Any(argument => string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsHelp(string argument)
    {
        return argument is "-h" or "--help" or "help";
    }

    private static string NextValue(string[] args, ref int index)
    {
        if (++index >= args.Length)
        {
            throw new ArgumentException($"Missing value for '{args[index - 1]}'.", nameof(args));
        }

        return args[index];
    }

    private static void PrintUsage()
    {
        Console.WriteLine("FiveDo.DiffHarness - 5DO vs Opera differential harness");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  FiveDo.DiffHarness run <manifest.json> [options]");
        Console.WriteLine("  FiveDo.DiffHarness validate <manifest.json> [--strict]");
        Console.WriteLine("  FiveDo.DiffHarness baseline --baseline <report.json> --current <report.json>");
        Console.WriteLine("  FiveDo.DiffHarness manifest --root <dir> [--root <dir>] [--out <path>] [options]");
        Console.WriteLine();
        Console.WriteLine("manifest options:");
        Console.WriteLine("  --root <dir>       Disc root; repeated roots become ${games}, ${games2}, ...");
        Console.WriteLine("  --out <path>       Manifest path (default benchmarks/diff/full.json)");
        Console.WriteLine("  --frames <n>       Frame count per title (default 3000)");
        Console.WriteLine("  --checkpoint <n>   Checkpoint frame; repeatable (default final frame)");
        Console.WriteLine("  --extension <ext>  Disc extension; repeatable (default .chd)");
        Console.WriteLine("  --bios <path>      BIOS path written to the manifest");
        Console.WriteLine();
        Console.WriteLine("run options:");
        Console.WriteLine("  --out <dir>        Output directory (default artifacts/diff)");
        Console.WriteLine("  --cli <path>       Explicit 5DO CLI executable or dll");
        Console.WriteLine("  --core <path>      Explicit OperaCore.dll");
        Console.WriteLine("  --bios <path>      BIOS override for every title");
        Console.WriteLine("  --frames <n>       Frame-count override for every title");
        Console.WriteLine("  --title <filter>   Restrict to matching title ids or names (repeatable)");
        Console.WriteLine("  --limit <n>        Run at most n titles");
        Console.WriteLine("  --resume           Reuse existing capture JSON");
        Console.WriteLine("  --force            Ignore existing captures");
        Console.WriteLine("  --no-opera         Capture 5DO only");
        Console.WriteLine("  --no-images        Skip checkpoint BMP/PNG captures");
        Console.WriteLine("  --cleanup-iso      Delete the staged ISO after a successful Opera capture");
        Console.WriteLine("  --compare-pc       Include the ARM PC in the pass/fail gate (requires the debug core)");
        Console.WriteLine("  --pc-tolerance <n> Allowed absolute PC delta in bytes (default 0)");
        Console.WriteLine("  --timeout <s>      Per-engine timeout override");
        Console.WriteLine("  --report-name <n>  Report file stem (default report)");
        Console.WriteLine("  --json             Also print the report JSON to stdout");
        Console.WriteLine();
        Console.WriteLine("Exit codes: 0 pass, 1 divergence/baseline change, 2 usage/config error.");
    }
}
