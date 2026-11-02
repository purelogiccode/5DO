using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Machine;

namespace FiveDo.Fuzz;

/// <summary>Mutates a valid save state and exercises the versioned state loader.</summary>
internal sealed class StateFuzzTarget : IFuzzTarget
{
    private readonly byte[] _baseline;

    /// <summary>Initializes the target and captures a valid baseline state.</summary>
    internal StateFuzzTarget()
    {
        _baseline = CreateBaseline();
    }

    /// <inheritdoc />
    public string Name => "state";

    /// <inheritdoc />
    public FuzzCase CreateCase(FuzzContext context, int iteration)
    {
        ArgumentNullException.ThrowIfNull(context);
        byte[] payload;
        string description;
        if (context.Random.Next(5) == 0)
        {
            payload = Mutation.RandomBytes(context.Random, Mutation.RandomLength(context.Random, 0, 4096));
            description = "random state bytes";
        }
        else
        {
            payload = Mutation.Mutate(context.Random, _baseline, 1, 12);
            description = "mutated valid state";
        }

        return new FuzzCase(description, payload);
    }

    /// <inheritdoc />
    public void Execute(FuzzCase fuzzCase, FuzzContext context)
    {
        ArgumentNullException.ThrowIfNull(fuzzCase);
        ArgumentNullException.ThrowIfNull(context);
        var machine = new FiveDoMachine();
        machine.LoadBios(new byte[EmulatorConstants.RomBankSize]);
        machine.Reset();
        using var stream = new MemoryStream(fuzzCase.Payload, writable: false);
        machine.LoadState(stream);
        if (context.Random.Next(10) == 0)
        {
            machine.RunFrames(1);
        }
    }

    private static byte[] CreateBaseline()
    {
        var machine = new FiveDoMachine();
        machine.LoadBios(new byte[EmulatorConstants.RomBankSize]);
        machine.Reset();
        machine.RunFrames(2);
        using var stream = new MemoryStream();
        machine.SaveState(stream);
        return stream.ToArray();
    }
}
