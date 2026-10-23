using System;
using ThreeDo.Core;

namespace ThreeDo.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        switch (args[0])
        {
            case "--version":
            case "-v":
                Console.WriteLine($"{ThreeDoVersion.ProductName} {ThreeDoVersion.Version} ({ThreeDoVersion.NamespacePrefix})");
                return 0;

            case "--help":
            case "-h":
            case "help":
                PrintUsage();
                return 0;

            default:
                Console.Error.WriteLine($"Unknown command: {args[0]}");
                PrintUsage();
                return 2;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine($"{ThreeDoVersion.ProductName} {ThreeDoVersion.Version}");
        Console.WriteLine("Usage: 3do <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  run <disc>     Run a disc image (available after M6)");
        Console.WriteLine("  info <disc>    Show disc metadata (available after M6)");
        Console.WriteLine("  bench          Run benchmarks (available after M8)");
        Console.WriteLine("  selftest       Run built-in checks (available after M8)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -v, --version  Show version");
        Console.WriteLine("  -h, --help     Show help");
    }
}
