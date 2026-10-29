namespace FiveDo.Core;

/// <summary>
/// Receives FIQ requests from devices.
/// </summary>
/// <remarks>Implemented by CLIO; no device calls CLIO directly.</remarks>
public interface IInterruptSink
{
    /// <summary>Gets a value indicating whether a FIQ is currently pending.</summary>
    bool NeedFiq { get; }

    /// <summary>Raises a FIQ with the reference's two reason masks.</summary>
    /// <param name="reason1">First reason mask. FreeDO: <c>_clio_GenerateFiq(reason1, reason2)</c>.</param>
    /// <param name="reason2">Second reason mask.</param>
    void GenerateFiq(uint reason1, uint reason2);
}
