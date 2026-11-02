using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FiveDo.Fuzz;

/// <summary>Command-line options for the standalone fuzz harness.</summary>
internal sealed class FuzzOptions
{
    /// <summary>Gets the targets to exercise.</summary>
    internal FuzzTargetKind Targets { get; private init; } = FuzzTargetKind.All;

    /// <summary>Gets the maximum number of iterations per target, or zero for time-limited runs.</summary>
    internal int Iterations { get; private init; }

    /// <summary>Gets the run duration in seconds, or zero when <see cref="Iterations"/> bounds the run.</summary>
    internal double Seconds { get; private init; } = 30;

    /// <summary>Gets the deterministic random seed.</summary>
    internal int Seed { get; private init; } = 1;

    /// <summary>Gets a value indicating whether the report is emitted as JSON.</summary>
    internal bool Json { get; private init; }

    /// <summary>Gets the directory used for failure reproductions.</summary>
    internal string OutputDirectory { get; private init; } = Path.Combine("artifacts", "fuzz");

    /// <summary>Gets a value indicating whether help was requested.</summary>
    internal bool ShowHelp { get; private init; }

    /// <summary>Creates bounded options for a test or a deterministic smoke run.</summary>
    /// <param name="targets">Targets to exercise.</param>
    /// <param name="iterations">Iterations per target.</param>
    /// <param name="seed">Base seed.</param>
    /// <param name="outputDirectory">Reproduction directory.</param>
    /// <returns>Bounded options.</returns>
    internal static FuzzOptions CreateForTesting(
        FuzzTargetKind targets,
        int iterations,
        int seed,
        string outputDirectory)
    {
        return new FuzzOptions
        {
            Targets = targets,
            Iterations = iterations,
            Seconds = 0,
            Seed = seed,
            OutputDirectory = outputDirectory,
        };
    }

    /// <summary>Parses command-line arguments.</summary>
    /// <param name="args">Process arguments.</param>
    /// <returns>Parsed options.</returns>
    /// <exception cref="ArgumentException">Thrown when an option is missing or invalid.</exception>
    internal static FuzzOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        FuzzTargetKind targets = FuzzTargetKind.All;
        var iterations = 0;
        double? seconds = null;
        var seed = 1;
        var json = false;
        var output = Path.Combine("artifacts", "fuzz");
        var help = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--help":
                case "-h":
                case "help":
                    help = true;
                    break;
                case "--json":
                    json = true;
                    break;
                case "--target":
                    targets = ParseTargets(RequireValue(args, ref index, argument));
                    break;
                case "--iterations":
                    iterations = ParsePositiveInt(RequireValue(args, ref index, argument), argument);
                    break;
                case "--seconds":
                    seconds = ParsePositiveDouble(RequireValue(args, ref index, argument), argument);
                    break;
                case "--seed":
                    seed = ParseInt(RequireValue(args, ref index, argument), argument);
                    break;
                case "--out":
                    output = RequireValue(args, ref index, argument);
                    break;
                default:
                    throw new ArgumentException($"Unknown fuzz option '{argument}'.", nameof(args));
            }
        }

        if (help)
        {
            return new FuzzOptions { ShowHelp = true };
        }

        if (iterations == 0)
        {
            seconds ??= 30;
            if (seconds <= 0)
            {
                throw new ArgumentException(
                    "--seconds must be greater than zero when --iterations is not set.",
                    nameof(args));
            }
        }

        return new FuzzOptions
        {
            Targets = targets,
            Iterations = iterations,
            Seconds = seconds ?? 0,
            Seed = seed,
            Json = json,
            OutputDirectory = output,
        };
    }

    private static FuzzTargetKind ParseTargets(string value)
    {
        FuzzTargetKind result = FuzzTargetKind.None;
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            result |= part.ToLowerInvariant() switch
            {
                "all" => FuzzTargetKind.All,
                "state" or "states" => FuzzTargetKind.State,
                "disc" or "discs" or "iso" or "chd" => FuzzTargetKind.Disc,
                "cue" => FuzzTargetKind.Cue,
                "fs" or "opera" or "opera-fs" or "filesystem" => FuzzTargetKind.OperaFileSystem,
                "cd" or "cdrom" or "command" => FuzzTargetKind.CdCommand,
                "cpu" or "arm" or "arm60" => FuzzTargetKind.Cpu,
                _ => throw new ArgumentException($"Unknown fuzz target '{part}'.", nameof(value)),
            };
        }

        if (result == FuzzTargetKind.None)
        {
            throw new ArgumentException("At least one fuzz target is required.", nameof(value));
        }

        return result;
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Option '{option}' needs a value.", nameof(args));
        }

        index++;
        return args[index];
    }

    private static int ParsePositiveInt(string value, string option)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            throw new ArgumentException($"Option '{option}' needs a positive integer.", nameof(value));
        }

        return parsed;
    }

    private static int ParseInt(string value, string option)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentException($"Option '{option}' needs an integer.", nameof(value));
        }

        return parsed;
    }

    private static double ParsePositiveDouble(string value, string option)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed)
            || parsed < 0)
        {
            throw new ArgumentException($"Option '{option}' needs a non-negative number.", nameof(value));
        }

        return parsed;
    }

    /// <summary>Enumerates the selected targets in a stable order.</summary>
    /// <returns>One flag value per target.</returns>
    internal static IEnumerable<FuzzTargetKind> EnumerateTargets(FuzzTargetKind targets)
    {
        if ((targets & FuzzTargetKind.State) != FuzzTargetKind.None)
        {
            yield return FuzzTargetKind.State;
        }

        if ((targets & FuzzTargetKind.Disc) != FuzzTargetKind.None)
        {
            yield return FuzzTargetKind.Disc;
        }

        if ((targets & FuzzTargetKind.Cue) != FuzzTargetKind.None)
        {
            yield return FuzzTargetKind.Cue;
        }

        if ((targets & FuzzTargetKind.OperaFileSystem) != FuzzTargetKind.None)
        {
            yield return FuzzTargetKind.OperaFileSystem;
        }

        if ((targets & FuzzTargetKind.CdCommand) != FuzzTargetKind.None)
        {
            yield return FuzzTargetKind.CdCommand;
        }

        if ((targets & FuzzTargetKind.Cpu) != FuzzTargetKind.None)
        {
            yield return FuzzTargetKind.Cpu;
        }
    }
}
