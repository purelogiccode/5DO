using System;

namespace FiveDo.Fuzz;

/// <summary>Selects one or more parser and command-stream fuzz targets.</summary>
[Flags]
internal enum FuzzTargetKind
{
    None = 0,
    State = 1 << 0,
    Disc = 1 << 1,
    Cue = 1 << 2,
    OperaFileSystem = 1 << 3,
    CdCommand = 1 << 4,
    Cpu = 1 << 5,
    All = State | Disc | Cue | OperaFileSystem | CdCommand | Cpu,
}
