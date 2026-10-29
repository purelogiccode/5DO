namespace FiveDo.Core;

/// <summary>
/// The CLIO FIFO register window shared with MADAM.
/// </summary>
/// <remarks>
/// FreeDO: <c>_clio_SetFIFO</c> and <c>_clio_FIFOStruct</c>. MADAM's <c>0x400-0x53F</c>
/// window forwards to CLIO, so the two chips communicate through this small contract rather
/// than a direct project reference.
/// </remarks>
public interface IClioFifo
{
    /// <summary>Writes a FIFO descriptor register. FreeDO: <c>_clio_SetFIFO</c>.</summary>
    /// <param name="address">Register offset within the CLIO window.</param>
    /// <param name="value">Register value.</param>
    void SetFifo(uint address, uint value);

    /// <summary>Reads a FIFO descriptor register. FreeDO: <c>_clio_FIFOStruct</c>.</summary>
    /// <param name="address">Register offset within the CLIO window.</param>
    /// <returns>Register value.</returns>
    uint FifoStruct(uint address);
}
