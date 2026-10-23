namespace ThreeDo.Core;

/// <summary>
/// The CLIO register window as seen by the CPU bus.
/// </summary>
/// <remarks>FreeDO: <c>_clio_Peek</c> and <c>_clio_Poke</c>.</remarks>
public interface IClioBus
{
    /// <summary>Reads a CLIO register.</summary>
    /// <param name="offset">Byte offset within the CLIO window.</param>
    /// <returns>The register value.</returns>
    uint Peek(uint offset);

    /// <summary>Writes a CLIO register.</summary>
    /// <param name="offset">Byte offset within the CLIO window.</param>
    /// <param name="value">Register value.</param>
    /// <returns><see langword="true"/> when the write requests a four-byte PC advance.</returns>
    bool Poke(uint offset, uint value);
}
