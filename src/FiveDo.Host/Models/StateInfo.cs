using System.Collections.Generic;

namespace FiveDo.Host;


/// <summary>The header and section table of a save-state file.</summary>
/// <param name="Version">State format version.</param>
/// <param name="EmulatorVersion">Writer's emulator version.</param>
/// <param name="ArmClock">Recorded ARM clock.</param>
/// <param name="FixMode">Recorded fix-mode flags.</param>
/// <param name="BiosHash">Recorded BIOS MD5, or empty.</param>
/// <param name="DiscHash">Recorded disc hash, or empty.</param>
/// <param name="FramesExecuted">Recorded frame count.</param>
/// <param name="Sections">Section table.</param>
/// <param name="Compressed">Whether at least one section payload is compressed.</param>
/// <param name="StoredLength">State length on disk in bytes.</param>
public sealed record StateInfo(
    ushort Version,
    string EmulatorVersion,
    int ArmClock,
    uint FixMode,
    string BiosHash,
    string DiscHash,
    long FramesExecuted,
    IReadOnlyList<StateSectionInfo> Sections,
    bool Compressed = false,
    long StoredLength = 0);
