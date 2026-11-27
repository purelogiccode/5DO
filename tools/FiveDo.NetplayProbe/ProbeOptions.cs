using System;
using System.Globalization;

namespace FiveDo.NetplayProbe;

internal sealed class ProbeOptions
{
    public required string DiscPath { get; init; }

    public required string BiosPath { get; init; }

    public string? Bios2Path { get; init; }

    public string? P1ScriptPath { get; init; }

    public string? P2ScriptPath { get; init; }

    public string? JsonPath { get; init; }

    public int Frames { get; init; } = 3600;

    public byte InputDelay { get; init; } = 2;

    public int Port { get; init; } = 5600;

    public int DropInputEvery { get; init; }

    public int LatencyMilliseconds { get; init; }

    public bool RunReference { get; init; } = true;

    public bool MeasureStateCost { get; init; }

    public string Profile { get; init; } = "compatible";

    public static ProbeOptions Parse(string[] args)
    {
        string? disc = null;
        string? bios = null;
        string? bios2 = null;
        string? p1 = null;
        string? p2 = null;
        string? json = null;
        var frames = 3600;
        var delay = 2;
        var port = 5600;
        var drop = 0;
        var latency = 0;
        var reference = true;
        var state = false;
        var profile = "compatible";

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--disc":
                    disc = Next(args, ref i);
                    break;
                case "--bios":
                    bios = Next(args, ref i);
                    break;
                case "--bios2":
                    bios2 = Next(args, ref i);
                    break;
                case "--p1-script":
                    p1 = Next(args, ref i);
                    break;
                case "--p2-script":
                    p2 = Next(args, ref i);
                    break;
                case "--json":
                    json = Next(args, ref i);
                    break;
                case "--frames":
                    frames = ParseInt(Next(args, ref i), "--frames");
                    break;
                case "--delay":
                    delay = ParseInt(Next(args, ref i), "--delay");
                    break;
                case "--port":
                    port = ParseInt(Next(args, ref i), "--port");
                    break;
                case "--drop-input-every":
                    drop = ParseInt(Next(args, ref i), "--drop-input-every");
                    break;
                case "--latency-ms":
                    latency = ParseInt(Next(args, ref i), "--latency-ms");
                    break;
                case "--no-reference":
                    reference = false;
                    break;
                case "--state-cost":
                    state = true;
                    break;
                case "--profile":
                    profile = Next(args, ref i);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{args[i]}'.\n{BuildUsage()}", nameof(args));
            }
        }

        if (string.IsNullOrWhiteSpace(disc))
        {
            throw new ArgumentException($"--disc is required.\n{BuildUsage()}", nameof(args));
        }

        if (string.IsNullOrWhiteSpace(bios))
        {
            throw new ArgumentException($"--bios is required.\n{BuildUsage()}", nameof(args));
        }

        if (frames <= 0)
        {
            throw new ArgumentException("--frames must be positive.", nameof(args));
        }

        if (delay is < 0 or > 120)
        {
            throw new ArgumentException("--delay must be between 0 and 120.", nameof(args));
        }

        if (port is < 1 or > 65535)
        {
            throw new ArgumentException("--port must be between 1 and 65535.", nameof(args));
        }

        if (drop is < 0 or > 100)
        {
            throw new ArgumentException("--drop-input-every must be between 0 and 100.", nameof(args));
        }

        if (latency is < 0 or > 1000)
        {
            throw new ArgumentException("--latency-ms must be between 0 and 1000.", nameof(args));
        }

        if (!string.Equals(profile, "compatible", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(profile, "accuracy", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(profile, "performance", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("--profile must be compatible, accuracy, or performance.", nameof(args));
        }

        return new ProbeOptions
        {
            DiscPath = disc,
            BiosPath = bios,
            Bios2Path = bios2,
            P1ScriptPath = p1,
            P2ScriptPath = p2,
            JsonPath = json,
            Frames = frames,
            InputDelay = (byte)delay,
            Port = port,
            DropInputEvery = drop,
            LatencyMilliseconds = latency,
            RunReference = reference,
            MeasureStateCost = state,
            Profile = profile.ToLowerInvariant(),
        };
    }

    public static string BuildUsage()
    {
        return """
            FiveDo.NetplayProbe runs two deterministic machine instances over UDP loopback.

            Usage:
              FiveDo.NetplayProbe --disc <disc> --bios <bios> [options]

            Options:
              --bios2 <path>             Optional secondary BIOS.
              --p1-script <path>         Host input script (default: built-in pattern).
              --p2-script <path>         Join input script (default: built-in pattern).
              --frames <n>               Frames to run (default: 3600).
              --delay <n>                Input lead in frames (default: 2).
              --port <n>                 UDP port for the hosting peer (default: 5600).
              --latency-ms <n>           One-way delivery delay for probe evidence.
              --drop-input-every <n>     Drop every nth input datagram.
              --no-reference             Skip the single-machine reference run.
              --state-cost               Measure snapshot and compressed-state cost.
              --profile <p>              compatible, accuracy, or performance.
              --json <path>              Write the full probe report as JSON.
              --help                     Show this help.
            """;
    }

    private static string Next(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value after '{args[index]}'.", nameof(args));
        }

        index++;
        return args[index];
    }

    private static int ParseInt(string value, string option)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw new ArgumentException($"{option} requires an integer, got '{value}'.", nameof(value));
        }

        return result;
    }
}
