namespace ThreeDo.Core;

/// <summary>
/// The diagnostic port as seen by the CPU bus.
/// </summary>
/// <remarks>FreeDO: <c>_diag_Get</c> and <c>_diag_Send</c>.</remarks>
public interface IDiagPort
{
    /// <summary>Reads the next diagnostic word.</summary>
    /// <returns>The diagnostic value.</returns>
    uint Get();

    /// <summary>Sends one diagnostic word.</summary>
    /// <param name="value">Value to send.</param>
    void Send(uint value);
}
