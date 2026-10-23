namespace ThreeDo.Core;

/// <summary>
/// An address bus that can request the four-byte program-counter adjustment used by
/// FreeDOCore's CLIO register writes.
/// </summary>
/// <remarks>
/// FreeDO: <c>mwritew</c> calls <c>_clio_Poke</c> and does <c>REG_PC += 4</c> when it
/// returns <see langword="true"/>. The bus cannot see the CPU, so it records the request
/// and the CPU consumes it immediately after the write.
/// </remarks>
public interface IPcAdjustSource
{
    /// <summary>Consumes a pending four-byte program-counter adjustment.</summary>
    /// <returns><see langword="true"/> when a request was pending.</returns>
    bool ConsumePcAdjustment();
}
