using System;
using FiveDo.Storage;

namespace FiveDo.Fuzz;

/// <summary>Mutates synthetic Opera file-system images and walks the parsed tree.</summary>
internal sealed class OperaFileSystemFuzzTarget : IFuzzTarget
{
    private readonly byte[] _baseline = SyntheticOperaDisc.Build();

    /// <inheritdoc />
    public string Name => "opera-fs";

    /// <inheritdoc />
    public FuzzCase CreateCase(FuzzContext context, int iteration)
    {
        ArgumentNullException.ThrowIfNull(context);
        byte[] payload;
        string description;
        if (context.Random.Next(5) == 0)
        {
            payload = Mutation.RandomBytes(
                context.Random,
                Mutation.RandomLength(context.Random, 0, 16 * 2048));
            description = "random volume image";
        }
        else
        {
            payload = Mutation.Mutate(context.Random, _baseline, 1, 24);
            description = "mutated Opera volume";
        }

        return new FuzzCase(description, payload);
    }

    /// <inheritdoc />
    public void Execute(FuzzCase fuzzCase, FuzzContext context)
    {
        ArgumentNullException.ThrowIfNull(fuzzCase);
        ArgumentNullException.ThrowIfNull(context);
        using var disc = new MemoryDiscSource(fuzzCase.Payload);
        OperaFileSystem fileSystem = OperaFileSystem.Mount(disc);
        var buffer = new byte[64];
        var inspected = 0;
        foreach (OperaFileEntry entry in fileSystem.Entries)
        {
            if (entry.IsDirectory || entry.Size == 0)
            {
                continue;
            }

            long offset = entry.Size > 1
                ? (long)(context.Random.NextDouble() * Math.Min(entry.Size, 1024))
                : 0;
            _ = fileSystem.ReadFile(entry, offset, buffer);
            inspected++;
            if (inspected >= 32)
            {
                break;
            }
        }
    }
}
