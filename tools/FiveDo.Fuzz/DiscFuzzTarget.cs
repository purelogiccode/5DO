using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Fuzz;

/// <summary>Exercises ISO and CHD container openers and sector reads.</summary>
internal sealed class DiscFuzzTarget : IFuzzTarget
{
    /// <inheritdoc />
    public string Name => "disc";

    /// <inheritdoc />
    public FuzzCase CreateCase(FuzzContext context, int iteration)
    {
        ArgumentNullException.ThrowIfNull(context);
        var isIso = context.Random.Next(2) == 0;
        var extension = isIso ? ".iso" : ".chd";
        byte[] payload;
        string description;
        if (isIso)
        {
            var baseline = new byte[2048 * Mutation.RandomLength(context.Random, 1, 32)];
            payload = Mutation.Mutate(context.Random, baseline, 1, 16);
            description = "mutated zero ISO";
        }
        else
        {
            payload = Mutation.RandomBytes(context.Random, Mutation.RandomLength(context.Random, 0, 16384));
            description = "random CHD bytes";
        }

        return new FuzzCase(
            description,
            payload,
            new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["image" + extension] = payload });
    }

    /// <inheritdoc />
    public void Execute(FuzzCase fuzzCase, FuzzContext context)
    {
        ArgumentNullException.ThrowIfNull(fuzzCase);
        ArgumentNullException.ThrowIfNull(context);
        if (fuzzCase.Files is null || fuzzCase.Files.Count != 1)
        {
            throw new InvalidDataException("The disc target needs exactly one companion file.");
        }

        foreach ((var name, var contents) in fuzzCase.Files)
        {
            var path = Path.Combine(context.Workspace, name);
            File.WriteAllBytes(path, contents);
            try
            {
                using IDiscSource disc = DiscImageFactory.Open(path);
                var buffer = new byte[disc.SectorSize];
                if (disc.SectorCount > 0)
                {
                    disc.ReadSector(0, buffer);
                    disc.ReadSector(disc.SectorCount - 1, buffer);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
