namespace FiveDo.Core;

/// <summary>
/// The SPORT window as seen by the CPU bus.
/// </summary>
/// <remarks>FreeDO: <c>_sport_SetSource</c> and <c>_sport_WriteAccess</c>.</remarks>
public interface ISportBus
{
    /// <summary>Selects a source address for a later copy operation.</summary>
    /// <param name="offset">Byte offset within the SPORT window.</param>
    /// <returns>The value returned to the CPU.</returns>
    uint SetSource(uint offset);

    /// <summary>Performs a SPORT fill, copy, or color write.</summary>
    /// <param name="offset">Byte offset within the SPORT window.</param>
    /// <param name="value">Value written by the CPU.</param>
    void WriteAccess(uint offset, uint value);
}
