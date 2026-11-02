using System;

namespace FiveDo.Fuzz;

/// <summary>Creates the concrete target for a selected kind.</summary>
internal static class FuzzTargetFactory
{
    /// <summary>Creates a target.</summary>
    /// <param name="kind">Selected target.</param>
    /// <returns>The target instance.</returns>
    internal static IFuzzTarget Create(FuzzTargetKind kind)
    {
        return kind switch
        {
            FuzzTargetKind.State => new StateFuzzTarget(),
            FuzzTargetKind.Disc => new DiscFuzzTarget(),
            FuzzTargetKind.Cue => new CueFuzzTarget(),
            FuzzTargetKind.OperaFileSystem => new OperaFileSystemFuzzTarget(),
            FuzzTargetKind.CdCommand => new CdCommandFuzzTarget(),
            FuzzTargetKind.Cpu => new CpuFuzzTarget(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown fuzz target."),
        };
    }
}
