using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FiveDo.DiffHarness;

/// <summary>Loads and validates differential manifests.</summary>
internal static class DiffManifestLoader
{
    private static readonly HashSet<string> KnownFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "frameHash",
        "audioHash",
        "pc",
        "samples",
        "stateHash",
    };

    /// <summary>Loads a manifest from disk.</summary>
    /// <param name="path">Manifest path.</param>
    /// <returns>The parsed manifest.</returns>
    /// <exception cref="InvalidDataException">Thrown when the manifest is invalid.</exception>
    public static DiffManifest Load(string path)
    {
        return Normalize(LoadRaw(path));
    }

    /// <summary>Loads a manifest without expanding named roots or filling defaults.</summary>
    /// <param name="path">Manifest path.</param>
    /// <returns>The parsed and validated manifest with its original <c>${name}</c> paths.</returns>
    /// <exception cref="InvalidDataException">Thrown when the manifest is invalid.</exception>
    /// <remarks>
    /// Use this for manifest-to-manifest transforms, such as the panel generator, that
    /// must preserve the source's portable root tokens.
    /// </remarks>
    public static DiffManifest LoadRaw(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Manifest not found.", path);
        }

        DiffManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize(
                File.ReadAllText(path),
                DiffJsonContext.Default.DiffManifest);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Manifest JSON is invalid: {exception.Message}", exception);
        }

        if (manifest is null)
        {
            throw new InvalidDataException("Manifest JSON is empty.");
        }

        Validate(manifest, path);
        return manifest;
    }

    /// <summary>Fills omitted optional values with their documented defaults.</summary>
    /// <param name="manifest">Manifest to normalize.</param>
    /// <returns>A manifest with non-null defaults.</returns>
    public static DiffManifest Normalize(DiffManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var defaults = manifest.Defaults ?? new DiffDefaults();
        var compare = defaults.Compare ?? new DiffCompareOptions();
        var roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((var key, var value) in manifest.Roots ?? [])
        {
            roots[key] = value;
        }

        var titles = (manifest.Titles ?? []).Select(title => new DiffTitle
        {
            Id = title.Id,
            Name = title.Name,
            Disc = ExpandPath(title.Disc, roots) ?? string.Empty,
            Iso = ExpandPath(title.Iso, roots),
            Bios = ExpandPath(title.Bios, roots),
            Frames = title.Frames,
            Checkpoints = title.Checkpoints,
            InputScript = ExpandPath(title.InputScript, roots),
            Options = title.Options,
            Allow = title.Allow,
        }).ToArray();

        return new DiffManifest
        {
            SchemaVersion = manifest.SchemaVersion,
            Name = manifest.Name,
            Roots = roots,
            Defaults = new DiffDefaults
            {
                Bios = ExpandPath(defaults.Bios, roots) ?? "bios/panafz1.bin",
                Frames = defaults.Frames is > 0 ? defaults.Frames : 3000,
                Checkpoints = defaults.Checkpoints,
                InputScript = ExpandPath(defaults.InputScript, roots),
                Options = defaults.Options ?? new DiffMachineOptions(),
                Cli = ExpandPath(defaults.Cli, roots),
                OperaCore = ExpandPath(defaults.OperaCore, roots),
                TimeoutSeconds = defaults.TimeoutSeconds is > 0 ? defaults.TimeoutSeconds : 180,
                Compare = new DiffCompareOptions
                {
                    CompareAudio = compare.CompareAudio,
                    ComparePc = compare.ComparePc,
                    CompareCpsr = compare.CompareCpsr,
                    CompareCd = compare.CompareCd,
                    PcTolerance = compare.PcTolerance < 0 ? 0 : compare.PcTolerance,
                    CompareSamples = compare.CompareSamples ?? true,
                    SampleTolerance = compare.SampleTolerance < 0 ? 0 : compare.SampleTolerance,
                    FrameWindow = compare.FrameWindow < 0 ? 0 : compare.FrameWindow,
                },
                WorkDirectory = ExpandPath(defaults.WorkDirectory, roots)
                    ?? "artifacts/diff/work",
            },
            Titles = titles,
        };
    }

    private static string? ExpandPath(string? value, Dictionary<string, string> roots)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var expanded = Environment.ExpandEnvironmentVariables(value);
        var builder = new System.Text.StringBuilder(expanded.Length);
        for (var i = 0; i < expanded.Length; i++)
        {
            if (expanded[i] == '$'
                && i + 1 < expanded.Length
                && expanded[i + 1] == '{'
                && expanded.IndexOf('}', i + 2) is var close
                && close > i + 2)
            {
                var key = expanded[(i + 2)..close];
                if (roots.TryGetValue(key, out var replacement))
                {
                    builder.Append(replacement);
                    i = close;
                    continue;
                }
            }

            builder.Append(expanded[i]);
        }

        return builder.ToString();
    }

    /// <summary>Validates a manifest.</summary>
    /// <param name="manifest">Manifest to validate.</param>
    /// <param name="path">Manifest path, used in error messages.</param>
    /// <exception cref="InvalidDataException">Thrown when the manifest is invalid.</exception>
    public static void Validate(DiffManifest manifest, string path)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"{path}: unsupported schemaVersion {manifest.SchemaVersion}; expected 1.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            throw new InvalidDataException($"{path}: name is required.");
        }

        var defaults = Normalize(manifest).Defaults!;
        var titles = manifest.Titles ?? [];
        if (defaults.Frames is not > 0)
        {
            throw new InvalidDataException($"{path}: defaults.frames must be positive.");
        }

        if (defaults.TimeoutSeconds is not > 0)
        {
            throw new InvalidDataException($"{path}: defaults.timeoutSeconds must be positive.");
        }

        if (titles.Length == 0)
        {
            throw new InvalidDataException($"{path}: titles is empty.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var title in titles)
        {
            if (string.IsNullOrWhiteSpace(title.Id))
            {
                throw new InvalidDataException($"{path}: every title needs an id.");
            }

            if (!ids.Add(title.Id))
            {
                throw new InvalidDataException($"{path}: duplicate title id '{title.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(title.Disc))
            {
                throw new InvalidDataException($"{path}: title '{title.Id}' has no disc.");
            }

            var frames = title.Frames ?? defaults.Frames ?? 3000;
            if (frames <= 0)
            {
                throw new InvalidDataException($"{path}: title '{title.Id}' has a non-positive frame count.");
            }

            var checkpoints = ResolveCheckpoints(manifest, title);
            if (checkpoints.Any(frame => frame <= 0))
            {
                throw new InvalidDataException($"{path}: title '{title.Id}' has a non-positive checkpoint.");
            }

            if (checkpoints.Any(frame => frame > frames))
            {
                throw new InvalidDataException(
                    $"{path}: title '{title.Id}' has a checkpoint after the requested frame count.");
            }

            foreach (var allowance in title.Allow ?? [])
            {
                if (!KnownFields.Contains(allowance.Field))
                {
                    throw new InvalidDataException(
                        $"{path}: title '{title.Id}' allowlist has unknown field '{allowance.Field}'.");
                }

                if (string.IsNullOrWhiteSpace(allowance.Reason))
                {
                    throw new InvalidDataException(
                        $"{path}: title '{title.Id}' allowlist entry for '{allowance.Field}' needs a reason.");
                }

                if (allowance.Checkpoint is null
                    && allowance.From is null
                    && allowance.To is null)
                {
                    throw new InvalidDataException(
                        $"{path}: title '{title.Id}' allowlist entry for '{allowance.Field}' "
                        + "needs checkpoint, from, or to.");
                }
            }
        }
    }

    /// <summary>Resolves the frame count for a title.</summary>
    public static int ResolveFrames(DiffManifest manifest, DiffTitle title)
    {
        return title.Frames ?? manifest.Defaults?.Frames ?? 3000;
    }

    /// <summary>Resolves the sorted checkpoint list for a title.</summary>
    public static int[] ResolveCheckpoints(DiffManifest manifest, DiffTitle title)
    {
        var source = title.Checkpoints ?? manifest.Defaults?.Checkpoints;
        if (source is null || source.Length == 0)
        {
            return [ResolveFrames(manifest, title)];
        }

        return source.Distinct().Order().ToArray();
    }

    /// <summary>Expands checkpoints by a frame window for offset diagnostics.</summary>
    public static int[] ExpandCheckpoints(int[] checkpoints, int window, int maxFrame)
    {
        if (window <= 0)
        {
            return checkpoints;
        }

        var frames = new SortedSet<int>();
        foreach (var checkpoint in checkpoints)
        {
            for (var offset = -window; offset <= window; offset++)
            {
                var frame = checkpoint + offset;
                if (frame > 0 && frame <= maxFrame)
                {
                    frames.Add(frame);
                }
            }
        }

        return [.. frames];
    }

    /// <summary>Resolves the input script path for a title.</summary>
    public static string? ResolveInputScript(string repoRoot, string manifestDirectory, DiffManifest manifest, DiffTitle title)
    {
        var path = title.InputScript ?? manifest.Defaults?.InputScript;
        return string.IsNullOrWhiteSpace(path) ? null : ResolvePath(repoRoot, manifestDirectory, path);
    }

    /// <summary>Resolves the BIOS path for a title.</summary>
    public static string ResolveBios(string repoRoot, string manifestDirectory, DiffManifest manifest, DiffTitle title)
    {
        var path = title.Bios ?? manifest.Defaults?.Bios;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "bios/panafz1.bin";
        }

        return ResolvePath(repoRoot, manifestDirectory, path);
    }

    /// <summary>Resolves a path against the repository root, then the manifest directory.</summary>
    public static string ResolvePath(string repoRoot, string manifestDirectory, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        var fromRoot = Path.GetFullPath(Path.Combine(repoRoot, path));
        if (File.Exists(fromRoot) || Directory.Exists(fromRoot))
        {
            return fromRoot;
        }

        var fromManifest = Path.GetFullPath(Path.Combine(manifestDirectory, path));
        if (File.Exists(fromManifest) || Directory.Exists(fromManifest))
        {
            return fromManifest;
        }

        return fromRoot;
    }

    /// <summary>Finds the repository root by walking up for <c>5DO.sln</c> or <c>.git</c>.</summary>
    public static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "5DO.sln"))
                || Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    /// <summary>Merges title options over manifest defaults.</summary>
    public static DiffMachineOptions MergeOptions(
        DiffMachineOptions? defaults,
        DiffMachineOptions? title,
        string? clioTimersOverride = null,
        string? profileOverride = null,
        string? armTimingOverride = null,
        string? celTimingOverride = null,
        string? cdTimingOverride = null)
    {
        defaults ??= new DiffMachineOptions();
        if (title is null)
        {
            return clioTimersOverride is null
                && profileOverride is null
                && armTimingOverride is null
                && celTimingOverride is null
                && cdTimingOverride is null
                ? defaults
                : new DiffMachineOptions
                {
                    Profile = profileOverride ?? defaults.Profile,
                    ArmTiming = armTimingOverride ?? defaults.ArmTiming,
                    ClioTimers = clioTimersOverride ?? defaults.ClioTimers,
                    CelTiming = celTimingOverride ?? defaults.CelTiming,
                    CdTiming = cdTimingOverride ?? defaults.CdTiming,
                    CdSpeed = defaults.CdSpeed,
                    CpuClock = defaults.CpuClock,
                    RandomSeed = defaults.RandomSeed,
                    HighResolution = defaults.HighResolution,
                    FixMode = defaults.FixMode,
                    NoGameFixes = defaults.NoGameFixes,
                    StateHash = defaults.StateHash,
                    ExtraArgs = defaults.ExtraArgs,
                };
        }

        return new DiffMachineOptions
        {
            Profile = profileOverride ?? title.Profile ?? defaults.Profile,
            ArmTiming = armTimingOverride ?? title.ArmTiming ?? defaults.ArmTiming,
            ClioTimers = clioTimersOverride ?? title.ClioTimers ?? defaults.ClioTimers,
            CelTiming = celTimingOverride ?? title.CelTiming ?? defaults.CelTiming,
            CdTiming = cdTimingOverride ?? title.CdTiming ?? defaults.CdTiming,
            CdSpeed = title.CdSpeed ?? defaults.CdSpeed,
            CpuClock = title.CpuClock ?? defaults.CpuClock,
            RandomSeed = title.RandomSeed ?? defaults.RandomSeed,
            HighResolution = title.HighResolution ?? defaults.HighResolution,
            FixMode = title.FixMode ?? defaults.FixMode,
            NoGameFixes = title.NoGameFixes ?? defaults.NoGameFixes,
            StateHash = title.StateHash ?? defaults.StateHash,
            ExtraArgs = [.. defaults.ExtraArgs ?? [], .. title.ExtraArgs ?? []],
        };
    }
}
