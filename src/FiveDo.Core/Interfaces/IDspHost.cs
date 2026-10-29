namespace FiveDo.Core;

/// <summary>
/// The CLIO services the audio DSP calls while executing.
/// </summary>
/// <remarks>
/// FreeDO: the <c>_clio_EIFIFO*</c>/<c>_clio_EOFIFO</c>/<c>_clio_GenerateFiq</c> calls inside
/// <c>DSP.cpp</c>. Implemented by CLIO; the DSP project depends only on Core.
/// </remarks>
public interface IDspHost
{
    /// <summary>Reads one word from an input FIFO and advances it. FreeDO: <c>_clio_EIFIFO</c>.</summary>
    /// <param name="channel">FIFO channel.</param>
    /// <returns>The FIFO word.</returns>
    ushort ReadInputFifo(ushort channel);

    /// <summary>Reads one word from an input FIFO without advancing. FreeDO: <c>_clio_EIFIFONI</c>.</summary>
    /// <param name="channel">FIFO channel.</param>
    /// <returns>The FIFO word.</returns>
    ushort ReadInputFifoNoIncrement(ushort channel);

    /// <summary>Reads the input FIFO status. FreeDO: <c>_clio_GetEIFIFOStat</c>.</summary>
    /// <param name="channel">FIFO channel.</param>
    /// <returns>Status value.</returns>
    ushort GetInputFifoStatus(byte channel);

    /// <summary>Reads the output FIFO status. FreeDO: <c>_clio_GetEOFIFOStat</c>.</summary>
    /// <param name="channel">FIFO channel.</param>
    /// <returns>Status value.</returns>
    ushort GetOutputFifoStatus(byte channel);

    /// <summary>Writes one word to an output FIFO. FreeDO: <c>_clio_EOFIFO</c>.</summary>
    /// <param name="channel">FIFO channel.</param>
    /// <param name="value">Word value.</param>
    void WriteOutputFifo(ushort channel, ushort value);

    /// <summary>Raises a FIQ. FreeDO: <c>_clio_GenerateFiq</c>.</summary>
    /// <param name="reason1">First reason mask.</param>
    /// <param name="reason2">Second reason mask.</param>
    void GenerateFiq(uint reason1, uint reason2);
}
