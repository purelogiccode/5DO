namespace FiveDo.Core;

/// <summary>
/// Stable save-state section identifiers.
/// </summary>
/// <remarks>
/// Values are persisted in state files. Never renumber an existing section.
/// </remarks>
public static class StateSectionIds
{
    /// <summary>ARM60 CPU state.</summary>
    public const uint Cpu = 1;

    /// <summary>RAM, ROM, and NVRAM.</summary>
    public const uint Memory = 2;

    /// <summary>MADAM and CEL state.</summary>
    public const uint Madam = 3;

    /// <summary>CLIO state.</summary>
    public const uint Clio = 4;

    /// <summary>DSP state.</summary>
    public const uint Dsp = 5;

    /// <summary>SPORT state.</summary>
    public const uint Sport = 6;

    /// <summary>VDLP state.</summary>
    public const uint Vdlp = 7;

    /// <summary>XBUS state.</summary>
    public const uint Xbus = 8;

    /// <summary>CD-ROM device state.</summary>
    public const uint Cdrom = 9;

    /// <summary>Quartz scheduler state.</summary>
    public const uint Quartz = 10;

    /// <summary>Machine-level state.</summary>
    public const uint Machine = 11;

    /// <summary>Machine option snapshot (ARM timing and future accuracy options).</summary>
    public const uint Options = 12;
}
