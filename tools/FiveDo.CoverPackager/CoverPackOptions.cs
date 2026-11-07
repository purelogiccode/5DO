using System;
using System.Globalization;
using System.IO;

namespace FiveDo.CoverPackager;

/// <summary>Command-line options for one cover-pack build.</summary>
internal sealed class CoverPackOptions
{
    /// <summary>Gets or sets the source image directory.</summary>
    public string SourceDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets the output directory.</summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional game-database XML path.</summary>
    public string? GameDatabasePath { get; set; }

    /// <summary>Gets or sets the pack version label.</summary>
    public string PackVersion { get; set; } = DateTime.UtcNow.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);

    /// <summary>Gets or sets the WebP quality (1-100).</summary>
    public int Quality { get; set; } = 85;

    /// <summary>Gets or sets the maximum encoded height.</summary>
    public int MaxHeight { get; set; } = 600;

    /// <summary>Gets or sets the minimum score for an automatic game-ID mapping.</summary>
    public double MapThreshold { get; set; } = 0.90;

    /// <summary>Gets or sets an optional override file.</summary>
    public string? OverridesPath { get; set; }

    /// <summary>Gets or sets the rights/notice URL written to the catalog.</summary>
    public string NoticeUrl { get; set; } = "https://github.com/purelogiccode/5DO";

    /// <summary>Gets or sets a value indicating whether a bulk ZIP is produced.</summary>
    public bool CreateZip { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the output directory is cleared first.</summary>
    public bool Clean { get; set; }

    /// <summary>Gets or sets a value indicating whether extra detail is printed.</summary>
    public bool Verbose { get; set; }

    /// <summary>Gets or sets a value indicating whether help was requested.</summary>
    public bool ShowHelp { get; set; }

    /// <summary>Parses command-line arguments.</summary>
    /// <param name="args">Raw arguments.</param>
    /// <returns>The parsed options.</returns>
    public static CoverPackOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var root = FindRepositoryRoot();
        var options = new CoverPackOptions
        {
            SourceDirectory = Path.Combine(root, "References", "Screenshots"),
            OutputDirectory = Path.Combine(root, "artifacts", "covers", "v1"),
            OverridesPath = Path.Combine(root, "packaging", "cover-overrides.json"),
        };

        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];
            string? inlineValue = null;
            var equalsIndex = argument.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex > 0)
            {
                inlineValue = argument[(equalsIndex + 1)..];
                argument = argument[..equalsIndex];
            }

            switch (argument)
            {
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                case "--source":
                    options.SourceDirectory = Next();
                    break;
                case "--output":
                    options.OutputDirectory = Next();
                    break;
                case "--game-db":
                    options.GameDatabasePath = Next();
                    break;
                case "--pack-version":
                    options.PackVersion = Next();
                    break;
                case "--quality":
                    options.Quality = ParseInt(Next(), "--quality");
                    break;
                case "--max-height":
                    options.MaxHeight = ParseInt(Next(), "--max-height");
                    break;
                case "--map-threshold":
                    options.MapThreshold = ParseDouble(Next(), "--map-threshold");
                    break;
                case "--overrides":
                    options.OverridesPath = Next();
                    break;
                case "--notice-url":
                    options.NoticeUrl = Next();
                    break;
                case "--no-zip":
                    options.CreateZip = false;
                    break;
                case "--clean":
                    options.Clean = true;
                    break;
                case "--verbose":
                    options.Verbose = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{argument}'. Use --help for usage.", nameof(args));
            }

            string Next()
            {
                if (inlineValue is not null)
                {
                    return inlineValue;
                }

                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"Option '{argument}' needs a value.", nameof(args));
                }

                i++;
                return args[i];
            }
        }

        if (options.ShowHelp)
        {
            return options;
        }

        options.SourceDirectory = Path.GetFullPath(options.SourceDirectory);
        options.OutputDirectory = Path.GetFullPath(options.OutputDirectory);
        if (!Directory.Exists(options.SourceDirectory))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {options.SourceDirectory}");
        }

        if (options.GameDatabasePath is not null)
        {
            options.GameDatabasePath = Path.GetFullPath(options.GameDatabasePath);
            if (!File.Exists(options.GameDatabasePath))
            {
                throw new FileNotFoundException("Game database not found.", options.GameDatabasePath);
            }
        }

        if (options.OverridesPath is not null)
        {
            options.OverridesPath = Path.GetFullPath(options.OverridesPath);
        }

        if (options.Quality is < 1 or > 100)
        {
            throw new ArgumentException("--quality must be between 1 and 100.", nameof(args));
        }

        if (options.MaxHeight < 100)
        {
            throw new ArgumentException("--max-height must be at least 100.", nameof(args));
        }

        if (!double.IsFinite(options.MapThreshold)
            || options.MapThreshold is < 0.50 or > 1.0)
        {
            throw new ArgumentException("--map-threshold must be between 0.50 and 1.00.", nameof(args));
        }

        if (string.IsNullOrWhiteSpace(options.PackVersion))
        {
            throw new ArgumentException("--pack-version must not be empty.", nameof(args));
        }

        return options;
    }

    /// <summary>Prints command-line usage.</summary>
    public static void PrintUsage()
    {
        Console.WriteLine("FiveDo.CoverPackager - build the 5DO online cover catalog and pack.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --source <dir>          Source image directory (default: References/Screenshots).");
        Console.WriteLine("  --output <dir>          Output directory (default: artifacts/covers/v1).");
        Console.WriteLine("  --game-db <file>        Optional GameDatabase.xml; defaults to the embedded database.");
        Console.WriteLine("  --pack-version <text>   Catalog pack version (default: current UTC date).");
        Console.WriteLine("  --quality <1-100>       WebP quality (default: 85).");
        Console.WriteLine("  --max-height <pixels>   Maximum image height (default: 600).");
        Console.WriteLine("  --map-threshold <0.5-1> Minimum game-ID mapping score (default: 0.90).");
        Console.WriteLine("  --overrides <file>      Optional game-ID override JSON.");
        Console.WriteLine("  --notice-url <url>      Rights/notice URL written to the catalog.");
        Console.WriteLine("  --no-zip                Do not create the bulk cover pack.");
        Console.WriteLine("  --clean                 Delete the output directory before writing.");
        Console.WriteLine("  --verbose               Print duplicate and mapping detail.");
        Console.WriteLine("  --help                  Show this help.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "5DO.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Environment.CurrentDirectory;
    }

    private static int ParseInt(string value, string option)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new ArgumentException($"{option} expects an integer.", nameof(value));
    }

    private static double ParseDouble(string value, string option)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new ArgumentException($"{option} expects a number.", nameof(value));
    }
}
