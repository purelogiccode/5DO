using System;

namespace ThreeDo.Core;

/// <summary>
/// Supplies the 16-byte PBUS controller packet once per frame.
/// </summary>
public interface IPbusSource
{
    /// <summary>Copies the current PBUS packet into <paramref name="destination"/>.</summary>
    /// <param name="destination">Destination buffer, normally 16 bytes.</param>
    /// <returns>The number of bytes written.</returns>
    int GetPbusData(Span<byte> destination);
}
