using System.Collections.Generic;

namespace FiveDo.Cli;

/// <summary>Result of a 4DO state import or export.</summary>
/// <param name="Ok">Whether the conversion completed.</param>
/// <param name="InputPath">Source file.</param>
/// <param name="OutputPath">Destination file.</param>
/// <param name="Bytes">Size of the written file in bytes.</param>
/// <param name="Warnings">Conversion notes and unsupported fields.</param>
internal sealed record CliFourDoStateResult(
    bool Ok,
    string InputPath,
    string OutputPath,
    long Bytes,
    IReadOnlyList<string> Warnings);
