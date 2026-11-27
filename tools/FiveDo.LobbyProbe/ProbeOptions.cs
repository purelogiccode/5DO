using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FiveDo.Lobby.Protocol;

namespace FiveDo.LobbyProbe;

[SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "Consumed by the probe runner in Program.cs.")]
internal sealed class ProbeOptions
{
    public const string Usage =
        "Usage:\n"
        + "  FiveDo.LobbyProbe host --lobby <url> [--emulator <tag>] [--content <id>] [--hash <hash>]\n"
        + "      [--name <name>] [--relay|--direct] [--no-ws] [--local-port N] [--pings N] [--interval MS]\n"
        + "  FiveDo.LobbyProbe join --lobby <url> --code <join-code> [--emulator <tag>]\n"
        + "      [--content <id>] [--hash <hash>] [--name <name>] [--relay|--direct] [--no-ws]\n"
        + "      [--local-port N] [--pings N] [--interval MS]";

    public required bool Host { get; init; }

    public required Uri Lobby { get; init; }

    public string Emulator { get; init; } = "probe";

    public string Content { get; init; } = "probe-content";

    public string? Hash { get; init; }

    public string? Name { get; init; }

    public string? Code { get; init; }

    public LobbyTransportMode Mode { get; init; } = LobbyTransportMode.Auto;

    public bool UseSignal { get; init; } = true;

    public int LocalPort { get; init; }

    public int Pings { get; init; } = 20;

    public int IntervalMilliseconds { get; init; } = 500;

    public static ProbeOptions Parse(string[] args)
    {
        if (args.Length == 0
            || (!string.Equals(args[0], "host", StringComparison.Ordinal)
                && !string.Equals(args[0], "join", StringComparison.Ordinal)))
        {
            throw new ArgumentException("Choose host or join.", nameof(args));
        }

        bool host = string.Equals(args[0], "host", StringComparison.Ordinal);
        Uri? lobby = null;
        string? code = null;
        var emulator = "probe";
        var content = "probe-content";
        string? hash = null;
        string? name = null;
        var mode = LobbyTransportMode.Auto;
        var useSignal = true;
        var localPort = 0;
        var pings = 20;
        var interval = 500;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--lobby":
                    lobby = ParseUri(Next(args, ref i));
                    break;
                case "--code":
                    code = Next(args, ref i);
                    break;
                case "--emulator":
                    emulator = Next(args, ref i);
                    break;
                case "--content":
                    content = Next(args, ref i);
                    break;
                case "--hash":
                    hash = Next(args, ref i);
                    break;
                case "--name":
                    name = Next(args, ref i);
                    break;
                case "--relay":
                    mode = LobbyTransportMode.Relay;
                    break;
                case "--direct":
                    mode = LobbyTransportMode.Direct;
                    break;
                case "--no-ws":
                    useSignal = false;
                    break;
                case "--local-port":
                    localPort = ParseInt(Next(args, ref i), "--local-port");
                    break;
                case "--pings":
                    pings = ParseInt(Next(args, ref i), "--pings");
                    break;
                case "--interval":
                    interval = ParseInt(Next(args, ref i), "--interval");
                    break;
                default:
                    throw new ArgumentException($"Unknown probe option '{args[i]}'.", nameof(args));
            }
        }

        if (lobby is null)
        {
            throw new ArgumentException("--lobby is required.", nameof(args));
        }

        if (!host && string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("join requires --code.", nameof(args));
        }

        if (pings is < 1 or > 10_000 || interval is < 10 or > 60_000
            || localPort is < 0 or > 65535)
        {
            throw new ArgumentException("--pings, --interval, or --local-port is out of range.", nameof(args));
        }

        return new ProbeOptions
        {
            Host = host,
            Lobby = lobby,
            Code = code,
            Emulator = emulator,
            Content = content,
            Hash = hash,
            Name = name,
            Mode = mode,
            UseSignal = useSignal,
            LocalPort = localPort,
            Pings = pings,
            IntervalMilliseconds = interval,
        };
    }

    private static Uri ParseUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            throw new ArgumentException("--lobby must be an absolute http or https URL.", nameof(value));
        }

        return uri;
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
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            throw new ArgumentException($"{option} requires an integer.", nameof(value));
        }

        return result;
    }
}
