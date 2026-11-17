using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FiveDo.Host;

namespace FiveDo.Cli;

/// <summary>
/// Implements <c>5do doctor</c>: one read-only host report for support requests and
/// Linux first-run troubleshooting.
/// </summary>
internal static class DoctorCommand
{
    internal static int Run(string[] args)
    {
        var json = HasFlag(args, "--json");
        var strict = HasFlag(args, "--strict");
        var recursive = HasFlag(args, "--recursive");
        List<string> folders = ReadFolders(args);

        BiosDiscoveryResult bios = folders.Count == 0
            ? BiosDiscovery.DiscoverDefault(recursive)
            : BiosDiscovery.Discover(
                folders.Select(folder => new BiosDiscoveryLocation(folder, "--folder")),
                recursive);

        DoctorResult result = DoctorReport.Collect(bios);
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(
                CliDoctorResult.From(result),
                CliJsonContext.Default.CliDoctorResult));
        }
        else
        {
            Console.WriteLine(DoctorReport.FormatText(result));
        }

        return strict && result.Summary is DoctorStatus.Warn or DoctorStatus.Fail ? 1 : 0;
    }

    private static List<string> ReadFolders(string[] args)
    {
        var folders = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument.StartsWith("--folder=", StringComparison.OrdinalIgnoreCase))
            {
                folders.Add(argument["--folder=".Length..]);
            }
            else if (string.Equals(argument, "--folder", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Length)
            {
                folders.Add(args[index + 1]);
                index++;
            }
        }

        return folders;
    }

    private static bool HasFlag(string[] args, string flag)
    {
        foreach (var argument in args)
        {
            if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
