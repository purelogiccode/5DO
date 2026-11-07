using System;
using System.IO;
using System.Threading.Tasks;

namespace FiveDo.CoverPackager;

/// <summary>Cover-pack builder entry point.</summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            CoverPackOptions options = CoverPackOptions.Parse(args);
            if (options.ShowHelp)
            {
                CoverPackOptions.PrintUsage();
                return 0;
            }

            var builder = new CoverPackBuilder(options);
            CoverPackReport report = await builder.BuildAsync().ConfigureAwait(false);
            Console.WriteLine();
            Console.WriteLine("Cover pack complete.");
            Console.WriteLine($"  catalog:      {Path.Combine(options.OutputDirectory, "catalog.json")}");
            Console.WriteLine($"  images:       {Path.Combine(options.OutputDirectory, "images")}");
            if (options.CreateZip)
            {
                Console.WriteLine($"  pack:         {Path.Combine(options.OutputDirectory, "packs", "covers-v1.zip")}");
            }

            Console.WriteLine($"  mapped games: {report.MappedGameIds}/{report.DatabaseGames}");
            Console.WriteLine($"  weak maps:    {report.WeakMappings}");
            Console.WriteLine($"  overrides:    {report.OverridesApplied}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or ArgumentException
            or UnauthorizedAccessException
            or NotSupportedException
            or FormatException)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }
    }
}
