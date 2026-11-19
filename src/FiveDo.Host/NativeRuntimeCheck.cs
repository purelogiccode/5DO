using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FiveDo.Host;

/// <summary>Operating-system family used to pick the bundled native runtime files.</summary>
public enum NativeRuntimePlatform
{
    /// <summary>Windows.</summary>
    Windows,

    /// <summary>Linux.</summary>
    Linux,

    /// <summary>macOS.</summary>
    MacOS,
}

/// <summary>
/// Checks that the native libraries shipped beside the apphosts are present.
/// </summary>
/// <remarks>
/// The .NET runtime is embedded in the single-file apphosts, but SkiaSharp and HarfBuzz
/// load their native libraries from disk. A partial extraction (or running the apphost
/// from inside a ZIP) fails deep inside Avalonia with a bare
/// <c>TypeInitializationException</c>/<c>DllNotFoundException</c>; this check turns that
/// into an actionable message before the UI starts.
/// </remarks>
public static class NativeRuntimeCheck
{
    /// <summary>Returns the native files required by the desktop frontend.</summary>
    /// <param name="platform">Operating-system family.</param>
    /// <returns>File names relative to the application folder.</returns>
    public static IReadOnlyList<string> RequiredLibraries(NativeRuntimePlatform platform)
    {
        return platform switch
        {
            NativeRuntimePlatform.Windows => ["libSkiaSharp.dll", "libHarfBuzzSharp.dll"],
            NativeRuntimePlatform.Linux => ["libSkiaSharp.so", "libHarfBuzzSharp.so"],
            NativeRuntimePlatform.MacOS => ["libSkiaSharp.dylib", "libHarfBuzzSharp.dylib"],
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown platform."),
        };
    }

    /// <summary>Detects the platform of the current process.</summary>
    /// <returns>The operating-system family.</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown on an unsupported platform.</exception>
    public static NativeRuntimePlatform DetectPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return NativeRuntimePlatform.Windows;
        }

        if (OperatingSystem.IsLinux())
        {
            return NativeRuntimePlatform.Linux;
        }

        if (OperatingSystem.IsMacOS())
        {
            return NativeRuntimePlatform.MacOS;
        }

        throw new PlatformNotSupportedException("5DO supports Windows, Linux, and macOS.");
    }

    /// <summary>Finds the required native files that are missing from a folder.</summary>
    /// <param name="directory">Folder that contains the apphosts and native libraries.</param>
    /// <param name="platform">Operating-system family.</param>
    /// <returns>Missing file names, in the order the platform lists them.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="directory"/> is empty.</exception>
    public static IReadOnlyList<string> FindMissing(string directory, NativeRuntimePlatform platform)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var missing = new List<string>();
        foreach (var name in RequiredLibraries(platform))
        {
            if (!File.Exists(Path.Combine(directory, name)))
            {
                missing.Add(name);
            }
        }

        return missing;
    }

    /// <summary>Builds the user-facing explanation for a partial extraction.</summary>
    /// <param name="directory">Application folder.</param>
    /// <param name="missing">Missing native files.</param>
    /// <param name="platform">Operating-system family.</param>
    /// <returns>A multi-line message suitable for a dialog or stderr.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="missing"/> is empty.</exception>
    public static string BuildStartupMessage(
        string directory,
        IReadOnlyList<string> missing,
        NativeRuntimePlatform platform)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(missing);
        if (missing.Count == 0)
        {
            throw new ArgumentException("At least one missing file is required.", nameof(missing));
        }

        var archive = platform switch
        {
            NativeRuntimePlatform.Windows => "Right-click the downloaded ZIP, choose Extract All, and run FiveDo.exe from the extracted folder.",
            NativeRuntimePlatform.Linux => "Extract the downloaded .tar.gz completely and run ./FiveDo from the extracted folder.",
            NativeRuntimePlatform.MacOS => "Open the .dmg and drag 5DO to Applications, or extract the .tar.gz completely.",
            _ => "Extract the downloaded archive completely before running 5DO.",
        };

        return $"5DO cannot start because these files are missing from its folder:{Environment.NewLine}"
            + string.Join(Environment.NewLine, missing.Select(name => $"  {name}"))
            + $"{Environment.NewLine}{Environment.NewLine}"
            + "The release archive must be extracted as a whole. "
            + archive
            + $"{Environment.NewLine}Running the executable from inside the archive, or copying only the executable, cannot work."
            + $"{Environment.NewLine}{Environment.NewLine}Folder: {directory}";
    }
}
