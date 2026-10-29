using System;

namespace FiveDo.Core;

/// <summary>
/// Diagnostic log categories. Categories are flags so a sink can filter several at once.
/// </summary>
[Flags]
public enum LogCategory
{
    /// <summary>No category.</summary>
    None = 0,

    /// <summary>ARM60 CPU.</summary>
    Cpu = 1 << 0,

    /// <summary>Memory and bus.</summary>
    Memory = 1 << 1,

    /// <summary>MADAM and CEL.</summary>
    Madam = 1 << 2,

    /// <summary>CLIO.</summary>
    Clio = 1 << 3,

    /// <summary>Audio DSP.</summary>
    Dsp = 1 << 4,

    /// <summary>SPORT.</summary>
    Sport = 1 << 5,

    /// <summary>VDLP and frame assembly.</summary>
    Video = 1 << 6,

    /// <summary>XBUS.</summary>
    Xbus = 1 << 7,

    /// <summary>CD-ROM device.</summary>
    Cdrom = 1 << 8,

    /// <summary>Disc and BIOS storage.</summary>
    Storage = 1 << 9,

    /// <summary>Audio pipeline.</summary>
    Audio = 1 << 10,

    /// <summary>Input and PBUS.</summary>
    Input = 1 << 11,

    /// <summary>Machine composition and frame loop.</summary>
    Machine = 1 << 12,

    /// <summary>Host services.</summary>
    Host = 1 << 13,

    /// <summary>Save states.</summary>
    State = 1 << 14,

    /// <summary>Diagnostics itself.</summary>
    Diagnostics = 1 << 15,

    /// <summary>All categories.</summary>
    All = Cpu | Memory | Madam | Clio | Dsp | Sport | Video | Xbus | Cdrom | Storage | Audio | Input | Machine | Host | State | Diagnostics,
}
