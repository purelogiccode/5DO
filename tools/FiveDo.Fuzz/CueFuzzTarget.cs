using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Fuzz;

/// <summary>Mutates a valid CUE sheet and its companion BIN.</summary>
internal sealed class CueFuzzTarget : IFuzzTarget
{
    private const string BaselineCue = """
        FILE "disc.bin" BINARY
          TRACK 01 MODE1/2048
            INDEX 01 00:00:00
        """;

    /// <inheritdoc />
    public string Name => "cue";

    /// <inheritdoc />
    public FuzzCase CreateCase(FuzzContext context, int iteration)
    {
        ArgumentNullException.ThrowIfNull(context);
        var cue = context.Random.Next(4) == 0
            ? Encoding.ASCII.GetString(Mutation.RandomBytes(context.Random, Mutation.RandomLength(context.Random, 0, 256)))
            : MutateText(context.Random, BaselineCue);
        var bin = Mutation.Mutate(
            context.Random,
            new byte[2048 * Mutation.RandomLength(context.Random, 1, 8)],
            0,
            8);
        return new FuzzCase(
            "mutated CUE sheet",
            Encoding.UTF8.GetBytes(cue),
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["disc.cue"] = Encoding.UTF8.GetBytes(cue),
                ["disc.bin"] = bin,
            });
    }

    /// <inheritdoc />
    public void Execute(FuzzCase fuzzCase, FuzzContext context)
    {
        ArgumentNullException.ThrowIfNull(fuzzCase);
        ArgumentNullException.ThrowIfNull(context);
        if (fuzzCase.Files is null)
        {
            throw new InvalidDataException("The CUE target needs companion files.");
        }

        var cuePath = Path.Combine(context.Workspace, "disc.cue");
        var binPath = Path.Combine(context.Workspace, "disc.bin");
        try
        {
            foreach ((var name, var contents) in fuzzCase.Files)
            {
                File.WriteAllBytes(Path.Combine(context.Workspace, name), contents);
            }

            using IDiscSource disc = DiscImageFactory.Open(cuePath);
            var buffer = new byte[disc.SectorSize];
            if (disc.SectorCount > 0)
            {
                disc.ReadSector(0, buffer);
            }
        }
        finally
        {
            File.Delete(cuePath);
            File.Delete(binPath);
        }
    }

    private static string MutateText(Random random, string source)
    {
        var builder = new StringBuilder(source);
        int changes = random.Next(1, 7);
        for (var change = 0; change < changes; change++)
        {
            switch (random.Next(3))
            {
                case 0 when builder.Length > 0:
                    builder[random.Next(builder.Length)] = (char)random.Next(32, 127);
                    break;
                case 1:
                    builder.Insert(random.Next(builder.Length + 1), (char)random.Next(32, 127));
                    break;
                case 2 when builder.Length > 0:
                    builder.Remove(random.Next(builder.Length), 1);
                    break;
            }
        }

        return builder.ToString();
    }
}
