namespace FiveDo.Core;

/// <summary>
/// A device attached to one of the 16 XBUS slots.
/// </summary>
/// <remarks>
/// The method set mirrors FreeDOCore's <c>XBP_*</c> plugin operations without the
/// integer-procedure ABI.
/// </remarks>
public interface IXbusDevice
{
    /// <summary>Initializes the device after attachment.</summary>
    void Initialize();

    /// <summary>Resets the device and ejects any media.</summary>
    void Reset();

    /// <summary>Passes one command byte to the device.</summary>
    /// <param name="value">Command byte.</param>
    void SetCommand(byte value);

    /// <summary>Gets a value indicating whether the device has a pending FIQ.</summary>
    bool FiqPending { get; }

    /// <summary>Passes one data word to the device.</summary>
    /// <param name="value">Data word.</param>
    void SetData(uint value);

    /// <summary>Reads one data word from the device.</summary>
    /// <returns>The next data word.</returns>
    uint GetData();

    /// <summary>Reads one status word from the device.</summary>
    /// <returns>The next status word.</returns>
    uint GetStatus();

    /// <summary>Writes the poll register.</summary>
    /// <param name="value">Poll value.</param>
    void SetPoll(uint value);

    /// <summary>Reads the poll register.</summary>
    /// <returns>The poll value.</returns>
    uint GetPoll();

    /// <summary>Reads the reserved register.</summary>
    /// <returns>The reserved value.</returns>
    uint GetReserved();

    /// <summary>Saves device-specific state.</summary>
    /// <param name="writer">State writer.</param>
    void SaveState(IStateWriter writer)
    {
    }

    /// <summary>Loads device-specific state.</summary>
    /// <param name="reader">State reader.</param>
    void LoadState(IStateReader reader)
    {
    }
}
