using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FiveDo.DiffHarness;

/// <summary>Generates a differential manifest from one or more disc roots.</summary>
internal static class DiffManifestBuilder
{
    /// <summary>Builds a manifest by scanning roots for disc images.</summary>
    public static DiffManifest Build(
        string name,
        IReadOnlyList<RootSpec> roots,
        int frames,
        int[] checkpoints,
        string bios,
        string? operaCore,
        string workDirectory,
        IReadOnlyList<string> extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frames);
        if (roots.Count == 0)
        {
            throw new ArgumentException("At least one root is required.", nameof(roots));
        }

        var extensionSet = new HashSet<string>(
            extensions.Select(extension => extension.StartsWith('.') ? extension : "." + extension),
            StringComparer.OrdinalIgnoreCase);
        var rootMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var titles = new List<DiffTitle>();
        foreach (var root in roots)
        {
            if (!Directory.Exists(root.Path))
            {
                throw new DirectoryNotFoundException("Disc root not found: " + root.Path);
            }

            var key = UniqueKey(rootMap, root.Name);
            rootMap[key] = root.Path;
            foreach (var file in Directory.EnumerateFiles(root.Path, "*", SearchOption.AllDirectories))
            {
                if (!extensionSet.Contains(Path.GetExtension(file)))
                {
                    continue;
                }

                var relative = Path.GetRelativePath(root.Path, file).Replace('\\', '/');
                var stem = Path.GetFileNameWithoutExtension(file);
                titles.Add(new DiffTitle
                {
                    Id = HarnessCommon.SafeName(stem) + "-" + ShortHash(key + "/" + relative),
                    Name = stem,
                    Disc = "${" + key + "}/" + relative,
                });
            }
        }

        if (titles.Count == 0)
        {
            throw new InvalidDataException("No disc images matched the requested extensions.");
        }

        return new DiffManifest
        {
            Name = name,
            Roots = rootMap,
            Defaults = new DiffDefaults
            {
                Bios = bios,
                Frames = frames,
                Checkpoints = checkpoints.Length == 0 ? [frames] : checkpoints,
                TimeoutSeconds = 600,
                WorkDirectory = workDirectory,
                OperaCore = operaCore,
                Options = new DiffMachineOptions
                {
                    Profile = "accuracy",
                    CdSpeed = "unbounded",
                    NoGameFixes = true,
                },
                Compare = new DiffCompareOptions
                {
                    CompareAudio = false,
                    ComparePc = false,
                    CompareSamples = false,
                    FrameWindow = 0,
                },
            },
            Titles = titles
                .OrderBy(title => title.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(title => title.Disc, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    /// <summary>
    /// Builds a deterministic N-title panel from an existing manifest.
    /// </summary>
    /// <param name="source">Source manifest, usually the full-collection manifest.</param>
    /// <param name="count">Number of titles to select.</param>
    /// <param name="frames">Frame count for every panel title.</param>
    /// <param name="checkpoints">Checkpoint frames; the final frame when empty.</param>
    /// <param name="operaCore">Optional debug-core path override.</param>
    /// <param name="workDirectory">Optional work-directory override.</param>
    /// <param name="name">Optional panel name.</param>
    /// <returns>A panel manifest with evenly spaced titles and the TEST-07 comparison policy.</returns>
    public static DiffManifest BuildPanel(
        DiffManifest source,
        int count,
        int frames,
        int[] checkpoints,
        string? operaCore,
        string? workDirectory,
        string? name)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frames);

        var defaults = source.Defaults ?? new DiffDefaults();
        var roots = source.Roots is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(source.Roots, StringComparer.OrdinalIgnoreCase);
        DiffTitle[] selected = SelectPanelTitles(source.Titles ?? [], count);

        return new DiffManifest
        {
            SchemaVersion = source.SchemaVersion,
            Name = string.IsNullOrWhiteSpace(name) ? "5DO differential panel" : name,
            Roots = roots,
            Defaults = new DiffDefaults
            {
                Bios = defaults.Bios,
                Frames = frames,
                Checkpoints = checkpoints.Length == 0 ? [frames] : checkpoints,
                InputScript = null,
                Options = defaults.Options ?? new DiffMachineOptions(),
                Cli = defaults.Cli,
                OperaCore = operaCore ?? defaults.OperaCore,
                TimeoutSeconds = defaults.TimeoutSeconds,
                Compare = new DiffCompareOptions
                {
                    CompareAudio = false,
                    ComparePc = true,
                    CompareCpsr = true,
                    CompareCd = true,
                    CompareSamples = true,
                    SampleTolerance = 512,
                    PcTolerance = 0,
                    FrameWindow = 2,
                },
                WorkDirectory = workDirectory ?? "artifacts/diff/work-panel",
            },
            Titles = selected
                .Select(title => new DiffTitle
                {
                    Id = title.Id,
                    Name = title.Name,
                    Disc = title.Disc,
                    Iso = title.Iso,
                    Bios = title.Bios,
                    Frames = null,
                    Checkpoints = null,
                    InputScript = null,
                    Options = title.Options,
                    Allow = [],
                })
                .ToArray(),
        };
    }

    private static DiffTitle[] SelectPanelTitles(DiffTitle[] titles, int count)
    {
        if (titles.Length == 0)
        {
            return [];
        }

        if (count >= titles.Length)
        {
            return [.. titles];
        }

        if (count == 1)
        {
            return [titles[0]];
        }

        var selected = new List<DiffTitle>(count);
        var used = new HashSet<int>();
        for (var i = 0; i < count; i++)
        {
            var index = (int)((long)i * (titles.Length - 1) / (count - 1));
            if (used.Add(index))
            {
                selected.Add(titles[index]);
            }
        }

        for (var i = 0; selected.Count < count && i < titles.Length; i++)
        {
            if (used.Add(i))
            {
                selected.Add(titles[i]);
            }
        }

        return [.. selected];
    }

    private static string UniqueKey(Dictionary<string, string> roots, string name)
    {
        var key = HarnessCommon.SafeName(name);
        var candidate = key;
        var suffix = 2;
        while (roots.ContainsKey(candidate))
        {
            candidate = key + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
            suffix++;
        }

        return candidate;
    }

    private static string ShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes)[..8];
    }
}

/// <summary>A named disc root.</summary>
internal sealed record RootSpec(string Name, string Path);
