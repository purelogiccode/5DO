namespace ThreeDo.Core;

/// <summary>
/// The XBUS controller as seen by CLIO's register window.
/// </summary>
/// <remarks>FreeDO: the <c>_xbus_*</c> functions called from <c>Clio.cpp</c>.</remarks>
public interface IClioXbus
{
    /// <summary>Gets the currently selected device slot.</summary>
    int SelectedDevice { get; }

    /// <summary>Selects a device. FreeDO: <c>_xbus_SetSEL</c>.</summary>
    /// <param name="value">Select register value.</param>
    void SetSelect(uint value);

    /// <summary>Writes the poll register. FreeDO: <c>_xbus_SetPoll</c>.</summary>
    /// <param name="value">Poll value.</param>
    void SetPoll(uint value);

    /// <summary>Writes one command byte. FreeDO: <c>_xbus_SetCommandFIFO</c>.</summary>
    /// <param name="value">Command byte.</param>
    void SetCommandFifo(uint value);

    /// <summary>Writes one data byte. FreeDO: <c>_xbus_SetDataFIFO</c>.</summary>
    /// <param name="value">Data byte.</param>
    void SetDataFifo(uint value);

    /// <summary>Reads the device reservation value. FreeDO: <c>_xbus_GetRes</c>.</summary>
    /// <returns>Reservation value.</returns>
    uint GetResult();

    /// <summary>Reads the poll register. FreeDO: <c>_xbus_GetPoll</c>.</summary>
    /// <returns>Poll value.</returns>
    uint GetPoll();

    /// <summary>Reads one status byte. FreeDO: <c>_xbus_GetStatusFIFO</c>.</summary>
    /// <returns>Status byte.</returns>
    uint GetStatusFifo();

    /// <summary>Reads one data byte. FreeDO: <c>_xbus_GetDataFIFO</c>.</summary>
    /// <returns>Data byte.</returns>
    uint GetDataFifo();

    /// <summary>Resets the controller and attached devices.</summary>
    void Reset();
}
