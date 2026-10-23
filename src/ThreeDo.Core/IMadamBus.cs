namespace ThreeDo.Core;

/// <summary>
/// The MADAM register window as seen by the CPU bus.
/// </summary>
/// <remarks>FreeDO: <c>_madam_Peek</c> and <c>_madam_Poke</c>.</remarks>
public interface IMadamBus
{
    /// <summary>Reads a MADAM register.</summary>
    /// <param name="offset">Byte offset within the MADAM window.</param>
    /// <returns>The register value.</returns>
    uint Peek(uint offset);

    /// <summary>Writes a MADAM register.</summary>
    /// <param name="offset">Byte offset within the MADAM window.</param>
    /// <param name="value">Register value.</param>
    void Poke(uint offset, uint value);
}
