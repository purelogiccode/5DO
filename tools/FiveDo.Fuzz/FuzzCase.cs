using System.Collections.Generic;

namespace FiveDo.Fuzz;

/// <summary>One generated input for a fuzz target.</summary>
/// <param name="Description">Human-readable description of the mutation.</param>
/// <param name="Payload">Primary input bytes.</param>
/// <param name="Files">Optional named companion files, such as a CUE sheet and BIN.</param>
internal sealed record FuzzCase(
    string Description,
    byte[] Payload,
    IReadOnlyDictionary<string, byte[]>? Files = null);
