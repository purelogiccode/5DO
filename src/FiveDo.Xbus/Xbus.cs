using System;
using FiveDo.Core;

namespace FiveDo.Xbus;

/// <summary>
/// The XBUS controller. FreeDO: <c>XBUS.cpp</c>.
/// </summary>
/// <remarks>
/// M3 provides the bus controller, the built-in device-15 responder, and device slots.
/// The CD-ROM device and the remaining command set arrive in M6.
/// </remarks>
public sealed class Xbus : IClioXbus, IStateSection
{
    /// <summary>Number of XBUS device slots.</summary>
    public const int DeviceCount = 16;

    /// <summary>Device slot of the built-in no-device responder.</summary>
    public const int BuiltInDevice = 0xf;

    private const byte PolStatusMask = 0x01;
    private const byte PolDataMask = 0x02;
    private const byte PolStatus = 0x10;
    private const byte PolData = 0x20;

    private readonly IXbusDevice?[] _devices = new IXbusDevice?[DeviceCount];
    private readonly byte[] _statusFifo = new byte[255];
    private readonly byte[] _commandFifo = new byte[7];

    private byte _select;
    private byte _selectHigh;
    private byte _poll;
    private byte _pollDevice;
    private byte _statusLength;
    private byte _commandPointer;
    private IInterruptSink? _interrupts;

    /// <summary>Initializes a new instance of the <see cref="Xbus"/> class.</summary>
    public Xbus()
    {
        Reset();
    }

    /// <summary>Gets or sets the FIQ controller.</summary>
    public IInterruptSink? Interrupts
    {
        get => _interrupts;
        set => _interrupts = value;
    }

    /// <summary>Gets the currently selected device slot.</summary>
    public int SelectedDevice => _select;

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Xbus;

    /// <summary>Gets the save-state section version.</summary>
    public ushort Version => 1;

    /// <summary>Attaches a device to the first free slot. FreeDO: <c>_xbus_Attach</c>.</summary>
    /// <param name="device">Device to attach.</param>
    /// <returns>The slot index, or -1 when all slots are occupied.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="device"/> is null.</exception>
    public int Attach(IXbusDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        for (var i = 0; i < DeviceCount - 1; i++)
        {
            if (_devices[i] is not null)
            {
                continue;
            }

            _devices[i] = device;
            device.Initialize();
            return i;
        }

        return -1;
    }

    /// <summary>Resets the controller and all attached devices.</summary>
    public void Reset()
    {
        _select = 0;
        _selectHigh = 0;
        _poll = 0xf;
        _pollDevice = 0;
        _statusLength = 0;
        _commandPointer = 0;
        Array.Clear(_statusFifo);
        Array.Clear(_commandFifo);

        for (var i = 0; i < DeviceCount; i++)
        {
            _devices[i]?.Reset();
        }
    }

    /// <inheritdoc />
    public void SetSelect(uint value)
    {
        _select = (byte)(value & 0xf);
        _selectHigh = (byte)(value & 0xf0);
    }

    /// <inheritdoc />
    public void SetPoll(uint value)
    {
        if (_select == BuiltInDevice)
        {
            _poll = (byte)((_poll & 0xf0) | (int)(value & 0xf));
        }

        var device = GetSelectedDevice();
        if (device is null)
        {
            return;
        }

        device.SetPoll(value);
        if (device.FiqPending)
        {
            _interrupts?.GenerateFiq(4, 0);
        }
    }

    /// <inheritdoc />
    public void SetCommandFifo(uint value)
    {
        var device = GetSelectedDevice();
        if (device is not null)
        {
            device.SetCommand((byte)value);
            if (device.FiqPending)
            {
                _interrupts?.GenerateFiq(4, 0);
            }

            return;
        }

        if (_select != BuiltInDevice)
        {
            return;
        }

        if (_commandPointer < _commandFifo.Length)
        {
            _commandFifo[_commandPointer] = (byte)value;
            _commandPointer++;
        }

        if (_commandPointer >= _commandFifo.Length)
        {
            ExecuteBuiltInCommand();
            _commandPointer = 0;
        }
    }

    /// <inheritdoc />
    public void SetDataFifo(uint value)
    {
        GetSelectedDevice()?.SetData(value);
    }

    /// <inheritdoc />
    public uint GetResult() => GetSelectedDevice()?.GetReserved() ?? 0;

    /// <inheritdoc />
    public uint GetPoll()
    {
        uint result;
        if (_select == BuiltInDevice)
        {
            result = _poll;
        }
        else
        {
            var device = GetSelectedDevice();
            result = device?.GetPoll() ?? 0x30;
        }

        if ((_selectHigh & 0x80) != 0)
        {
            result &= 0xf;
        }

        return result;
    }

    /// <inheritdoc />
    public uint GetStatusFifo()
    {
        var device = GetSelectedDevice();
        if (device is not null)
        {
            return device.GetStatus();
        }

        if (_select != BuiltInDevice || _statusLength == 0)
        {
            return 0;
        }

        var result = _statusFifo[0];
        _statusLength--;
        if (_statusLength > 0)
        {
            Array.Copy(_statusFifo, 1, _statusFifo, 0, _statusLength);
        }
        else
        {
            _pollDevice = (byte)(_pollDevice & ~PolStatus);
        }

        return result;
    }

    /// <inheritdoc />
    public uint GetDataFifo() => GetSelectedDevice()?.GetData() ?? 0;

    /// <summary>Writes the XBUS save-state payload. FreeDO: <c>_xbus_Save</c>.</summary>
    /// <param name="writer">Destination writer.</param>
    public void Save(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteByte(_select);
        writer.WriteByte(_selectHigh);
        writer.WriteByte(_poll);
        writer.WriteByte(_pollDevice);
        writer.WriteByte(_statusLength);
        writer.WriteByte(_commandPointer);
        writer.WriteBytes(_statusFifo);
        writer.WriteBytes(_commandFifo);

        for (var i = 0; i < DeviceCount; i++)
        {
            writer.WriteBoolean(_devices[i] is not null);
            _devices[i]?.SaveState(writer);
        }
    }

    /// <summary>Reads the XBUS save-state payload. FreeDO: <c>_xbus_Load</c>.</summary>
    /// <param name="reader">Source reader.</param>
    public void Load(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _select = reader.ReadByte();
        _selectHigh = reader.ReadByte();
        _poll = reader.ReadByte();
        _pollDevice = reader.ReadByte();
        _statusLength = reader.ReadByte();
        _commandPointer = reader.ReadByte();
        reader.ReadBytes(_statusFifo);
        reader.ReadBytes(_commandFifo);

        for (var i = 0; i < DeviceCount; i++)
        {
            var attached = reader.ReadBoolean();
            if (attached)
            {
                _devices[i]?.LoadState(reader);
            }
        }
    }

    private IXbusDevice? GetSelectedDevice() => _select < DeviceCount ? _devices[_select] : null;

    private void ExecuteBuiltInCommand()
    {
        if (_commandFifo[0] != 0x83)
        {
            return;
        }

        _statusLength = 12;
        _statusFifo[0] = 0x83;
        for (var i = 1; i < 12; i++)
        {
            _statusFifo[i] = 0x01;
        }

        _pollDevice |= PolStatus;
        if (((_pollDevice & PolStatus) != 0 && (_pollDevice & PolStatusMask) != 0) ||
            ((_pollDevice & PolData) != 0 && (_pollDevice & PolDataMask) != 0))
        {
            _interrupts?.GenerateFiq(4, 0);
        }
    }
}
