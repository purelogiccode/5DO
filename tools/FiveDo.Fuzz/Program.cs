using System;
using System.Globalization;
using System.Text.Json;

namespace FiveDo.Fuzz;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static int Main(string[] args)
    {
        FuzzOptions options;
        try
        {
            options = FuzzOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine("error: " + exception.Message);
            PrintUsage();
            return 2;
        }

        if (options.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        FuzzReport report = FuzzRunner.Run(options);
        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        }
        else
        {
            WriteTextReport(report);
        }

        return report.UnexpectedFailures == 0 ? 0 : 1;
    }

    private static void WriteTextReport(FuzzReport report)
    {
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"5DO fuzz: seed={report.Seed} targets={string.Join(',', report.Targets)} "
                + $"started={report.Started:O} duration={report.DurationSeconds:F1}s "
                + $"iterations={report.TotalIterations}"));
        foreach (FuzzTargetReport target in report.TargetReports)
        {
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {target.Target,-14} iterations={target.Iterations,-6} "
                    + $"typed={target.TypedFailures,-6} unexpected={target.UnexpectedFailures}"));
            foreach (FuzzFailure failure in target.Failures)
            {
                Console.WriteLine(
                    $"    [{failure.Target}#{failure.Iteration}] {failure.ExceptionType}: "
                    + failure.Message
                    + (string.IsNullOrEmpty(failure.ReproductionPath)
                        ? string.Empty
                        : $" ({failure.ReproductionPath})"));
            }
        }

        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Typed rejections: {report.TypedFailures}; unexpected failures: {report.UnexpectedFailures}."));
        Console.WriteLine(
            report.UnexpectedFailures == 0
                ? "Fuzz run clean."
                : "Fuzz run failed; inspect the reproduction directories.");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("5DO fuzz harness");
        Console.WriteLine("Usage: FiveDo.Fuzz [--target all|state,disc,cue,opera-fs,cd,cpu] [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --target LIST       Targets to run (default: all).");
        Console.WriteLine("  --iterations N      Maximum iterations per target (default: time-limited).");
        Console.WriteLine("  --seconds N         Run duration per target (default: 30).");
        Console.WriteLine("  --seed N            Deterministic base seed (default: 1).");
        Console.WriteLine("  --out DIR           Reproduction directory (default: artifacts/fuzz).");
        Console.WriteLine("  --json              Emit a machine-readable report.");
        Console.WriteLine("  -h, --help          Show this help.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  FiveDo.Fuzz --seconds 60 --target all --json");
        Console.WriteLine("  FiveDo.Fuzz --iterations 500 --seed 42 --target state,cue");
    }
}
