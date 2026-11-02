using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace FiveDo.Fuzz;

/// <summary>Writes reproducible inputs for unexpected failures.</summary>
internal static class ReproWriter
{
    /// <summary>Writes the input and failure details into a new directory.</summary>
    /// <param name="outputDirectory">Root output directory.</param>
    /// <param name="target">Target name.</param>
    /// <param name="seed">Target seed.</param>
    /// <param name="iteration">Zero-based iteration.</param>
    /// <param name="fuzzCase">Generated input.</param>
    /// <param name="exception">Unexpected exception.</param>
    /// <returns>The reproduction directory.</returns>
    internal static string Write(
        string outputDirectory,
        string target,
        int seed,
        int iteration,
        FuzzCase fuzzCase,
        Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(fuzzCase);
        ArgumentNullException.ThrowIfNull(exception);

        var directory = Path.Combine(
            outputDirectory,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{Sanitize(target)}-seed{seed}-iter{iteration}"));
        Directory.CreateDirectory(directory);

        File.WriteAllBytes(Path.Combine(directory, "payload.bin"), fuzzCase.Payload);
        if (fuzzCase.Files is not null)
        {
            foreach ((var name, var contents) in fuzzCase.Files)
            {
                var path = Path.Combine(directory, Sanitize(name));
                File.WriteAllBytes(path, contents);
            }
        }

        var details = new StringBuilder();
        details.AppendLine("target: " + target);
        details.AppendLine("seed: " + seed.ToString(CultureInfo.InvariantCulture));
        details.AppendLine("iteration: " + iteration.ToString(CultureInfo.InvariantCulture));
        details.AppendLine("description: " + fuzzCase.Description);
        details.AppendLine("exception: " + exception.GetType().FullName);
        details.AppendLine("message: " + exception.Message);
        details.AppendLine();
        details.AppendLine(exception.ToString());
        File.WriteAllText(Path.Combine(directory, "failure.txt"), details.ToString());
        return directory;
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var buffer = value.ToCharArray();
        for (var index = 0; index < buffer.Length; index++)
        {
            if (Array.IndexOf(invalid, buffer[index]) >= 0 || buffer[index] is '/' or '\\')
            {
                buffer[index] = '_';
            }
        }

        return new string(buffer);
    }
}
