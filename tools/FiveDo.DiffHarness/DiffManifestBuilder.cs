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
