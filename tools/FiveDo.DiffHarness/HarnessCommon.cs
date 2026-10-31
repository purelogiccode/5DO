using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.DiffHarness;

/// <summary>Shared filesystem, hashing, and process helpers.</summary>
internal static partial class HarnessCommon
{
    /// <summary>Runs a process and captures stdout/stderr.</summary>
    public static ProcessResult RunProcess(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        int timeoutSeconds)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var clock = Stopwatch.StartNew();
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{fileName}'.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        var completed = process.WaitForExit(timeoutSeconds * 1000);
        if (!completed)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process exited between the timeout and the kill.
            }

            process.WaitForExit(5000);
        }

        clock.Stop();
        return new ProcessResult(
            completed ? process.ExitCode : -1,
            !completed,
            stdout.GetAwaiter().GetResult(),
            stderr.GetAwaiter().GetResult(),
            clock.Elapsed.TotalSeconds);
    }

    /// <summary>Resolves the 5DO CLI to an executable and argument prefix.</summary>
    public static CliCommand ResolveCli(string repoRoot, string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var resolved = Path.GetFullPath(explicitPath);
            if (!File.Exists(resolved))
            {
                throw new FileNotFoundException("5DO CLI not found.", resolved);
            }

            return resolved.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? new CliCommand("dotnet", [resolved], resolved)
                : new CliCommand(resolved, [], resolved);
        }

        foreach (var candidate in new[]
        {
            Path.Combine(repoRoot, "src", "FiveDo.Cli", "bin", "Release", "net10.0", "FiveDo.Cli.exe"),
            Path.Combine(repoRoot, "src", "FiveDo.Cli", "bin", "Release", "net10.0", "FiveDo.Cli.dll"),
        })
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            return candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? new CliCommand("dotnet", [candidate], candidate)
                : new CliCommand(candidate, [], candidate);
        }

        throw new FileNotFoundException(
            "5DO CLI not found. Build src/FiveDo.Cli in Release or pass --cli.");
    }

    /// <summary>Computes a file's MD5.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA5351:Do not use broken cryptographic algorithms",
        Justification = "MD5 is the identity used by the 3DO BIOS catalog and the reference core bundle.")]
    public static string Md5File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(MD5.HashData(stream));
    }

    /// <summary>Creates a safe file-system name.</summary>
    public static string SafeName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsAsciiLetterOrDigit(character)
                || character is '.' or '_' or '-'
                ? character
                : '_');
        }

        var safe = builder.ToString().Trim('_');
        if (safe.Length == 0)
        {
            safe = "item";
        }

        return safe.Length <= 80 ? safe : safe[..80];
    }

    /// <summary>Deletes a file if it exists.</summary>
    public static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>Stages a file with a hard link when possible, otherwise a copy.</summary>
    public static void StageFile(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
        {
            return;
        }

        if (OperatingSystem.IsWindows()
            && CreateHardLinkW(destination, source, IntPtr.Zero))
        {
            return;
        }

        File.Copy(source, destination, overwrite: true);
    }

    /// <summary>Extracts the 2048-byte data sectors to an ISO.</summary>
    public static void ExtractIso(string discPath, string isoPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(isoPath)!);
        using IDiscSource disc = DiscImageFactory.Open(discPath);
        using var output = File.Create(isoPath);
        var sector = new byte[2048];
        for (var lba = 0; lba < disc.SectorCount; lba++)
        {
            disc.ReadSector(lba, sector);
            output.Write(sector);
        }
    }

    /// <summary>Gets the current git commit, when available.</summary>
    public static string? GitCommit(string repoRoot)
    {
        try
        {
            var result = RunProcess("git", ["rev-parse", "HEAD"], repoRoot, 10);
            return result.ExitCode == 0 ? result.StdOut.Trim() : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>Formats a hash for reports.</summary>
    public static string Hex(uint value)
    {
        return "0x" + value.ToString("x8", CultureInfo.InvariantCulture);
    }

    /// <summary>Writes an XRGB8888 frame as a bottom-up 32-bit BMP for visual comparison.</summary>
    public static void WriteBmp(string path, ReadOnlySpan<uint> pixels, int width, int height, int stride)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var rowBytes = width * 4;
        var imageSize = rowBytes * height;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0x4D42);
        writer.Write(54 + imageSize);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write(imageSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);
        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = pixels[(y * stride) + x];
                writer.Write((byte)(pixel & 0xff));
                writer.Write((byte)((pixel >> 8) & 0xff));
                writer.Write((byte)((pixel >> 16) & 0xff));
                writer.Write((byte)0);
            }
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(
        string newFileName,
        string existingFileName,
        IntPtr securityAttributes);
}

/// <summary>A resolved CLI executable.</summary>
internal sealed record CliCommand(string FileName, string[] Prefix, string Path);

/// <summary>A captured process result.</summary>
internal sealed record ProcessResult(
    int ExitCode,
    bool TimedOut,
    string StdOut,
    string StdErr,
    double ElapsedSeconds)
{
    /// <summary>Gets a short failure description.</summary>
    public string Describe()
    {
        if (TimedOut)
        {
            return "process timed out";
        }

        if (ExitCode != 0)
        {
            return $"process exited with code {ExitCode}";
        }

        return "process failed";
    }

    /// <summary>Gets the tail of stderr for diagnostics.</summary>
    public string StderrTail(int lines = 20)
    {
        var split = StdErr.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Math.Max(0, split.Length - lines);
        return string.Join('\n', split[start..]).Trim();
    }
}
