using System;
using ThreeDo.Core;

namespace ThreeDo.Cdrom;

/// <summary>
/// The 3DO CD-ROM drive as an XBUS device. FreeDO: <c>Iso.cpp: cdrom_Device</c>.
/// </summary>
/// <remarks>
/// <para>
/// The device implements the MEI command set used by the 3DO BIOS and games. Sector data is
/// fetched through <see cref="IDiscSource"/>, which normalizes ISO/BIN/CUE/CHD containers to
/// 2048-byte user-data sectors. The command state machine, status layouts, and poll/FIQ
/// semantics follow the reference exactly, including its quirks (for example the "no disc"
/// initial state still reports the tray/disc bits with a 150-frame disc).
/// </para>
/// <para>
/// Commands 0x01, 0x04, 0x07, 0x0A, 0x86, 0x88, 0x89, and 0x8E are accepted but produce no
/// status in the reference; they are accepted here too.
/// </para>
/// </remarks>
public sealed class CdromDevice : IXbusDevice, IStateSection
{
    private const int StatusDelay = 100;
    private const int RequestSize = 2048;

    private const byte PollStatusMask = 0x01;
    private const byte PollDataMask = 0x02;
    private const byte PollMagicMask = 0x04;
    private const byte PollResultMask = 0x08;
    private const byte PollStatus = 0x10;
    private const byte PollData = 0x20;
    private const byte PollMagic = 0x40;
    private const byte PollResult = 0x80;

    private const byte CdstTray = 0x80;
    private const byte CdstDisc = 0x40;
    private const byte CdstSpin = 0x20;
    private const byte CdstError = 0x10;
    private const byte Cdst2X = 0x02;
    private const byte CdstReady = 0x01;

    private const byte CdCtlDataTrack = 0x04;

    private readonly byte[] _status = new byte[256];
    private readonly byte[] _data = new byte[RequestSize];
    private readonly byte[] _command = new byte[7];

    private IDiscSource? _disc;
    private byte _poll;
    private byte _xbusStatus;
    private byte _statusLength;
    private int _dataLength;
    private int _dataPointer;
    private byte _commandPointer;
    private int _requested;
    private MeiError _meiStatus;
    private DiscState _discState = new();
    private int _currentSector;

    /// <summary>Gets or sets an optional diagnostic writer for command and sector traces.</summary>
    public System.IO.TextWriter? TraceWriter { get; set; }

    /// <summary>Gets or sets an optional provider for the current CPU program counter (diagnostics only).</summary>
    public Func<uint>? ProgramCounterProvider { get; set; }

    /// <summary>Gets or sets an optional provider for the current emulated frame number (diagnostics only).</summary>
    public Func<long>? FrameProvider { get; set; }

    /// <summary>Gets or sets an optional observer invoked for each executed command (diagnostics only).</summary>
    public Action<byte>? CommandObserver { get; set; }

    /// <summary>Initializes a new instance of the <see cref="CdromDevice"/> class.</summary>
    public CdromDevice()
    {
        Initialize();
    }

    /// <summary>Gets the currently inserted disc, if any.</summary>
    public IDiscSource? Disc => _disc;

    /// <summary>Gets the drive status byte. FreeDO: <c>XbusStatus</c>.</summary>
    public byte DriveStatus => _xbusStatus;

    /// <summary>Gets the device poll byte. FreeDO: <c>Poll</c>.</summary>
    public byte Poll => _poll;

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Cdrom;

    /// <summary>Gets the save-state section version.</summary>
    public ushort Version => 1;

    /// <summary>Inserts a disc and initializes the drive for it.</summary>
    /// <param name="disc">Disc source. The device does not take ownership.</param>
    public void InsertDisc(IDiscSource disc)
    {
        ArgumentNullException.ThrowIfNull(disc);
        _disc = disc;
        InitCd();
    }

    /// <summary>Ejects the current disc and reinitializes the drive.</summary>
    public void EjectDisc()
    {
        _disc = null;
        Initialize();
    }

    /// <inheritdoc />
    public void Initialize()
    {
        _dataPointer = 0;
        _xbusStatus = 0;
        _poll = 0x0f;
        _xbusStatus |= CdstTray;
        _xbusStatus |= CdstReady;
        _xbusStatus |= CdstDisc;
        _xbusStatus |= CdstSpin;
        _meiStatus = MeiError.NoError;

        _discState = new DiscState();
        _discState.FirstTrack = 1;
        _discState.LastTrack = 1;
        SetMsf(_discState.CurrentAbsoluteMsf, 0, 2, 0);
        _discState.DiscId = 0;
        SetTocEntry(1, CdCtlDataTrack, track: 1, minute: 0, second: 2, frame: 0);

        _discState.TempLba = 150;
        LbaToMsf();
        CopyMsf(_discState.TotalMsf, _discState.TempMsf);
        _discState.TempLba = 0;
        LbaToMsf();
        CopyMsf(_discState.SessionMsf, _discState.TempMsf);
    }

    /// <inheritdoc />
    public void Reset()
    {
        Initialize();
        if (_disc is not null && _disc.SectorCount > 0)
        {
            InitCd();
        }
    }

    /// <summary>Initializes the drive for the inserted disc. FreeDO: <c>InitCD</c>.</summary>
    public void InitCd()
    {
        _currentSector = 0;
        _xbusStatus = 0;
        _poll = 0x0f;
        _xbusStatus |= CdstTray;
        _xbusStatus |= CdstReady;
        _xbusStatus |= CdstDisc;
        _xbusStatus |= CdstSpin;
        _meiStatus = MeiError.NoError;

        _discState = new DiscState();
        _discState.FirstTrack = 1;
        _discState.LastTrack = 1;
        SetMsf(_discState.CurrentAbsoluteMsf, 0, 2, 0);
        _discState.DiscId = 0;
        SetTocEntry(1, CdCtlDataTrack, track: 1, minute: 0, second: 2, frame: 0);

        var fileSize = (uint)(_disc?.SectorCount ?? 0) + 150;
        _discState.TempLba = (int)fileSize;
        LbaToMsf();
        CopyMsf(_discState.TotalMsf, _discState.TempMsf);

        _discState.TempLba = (int)fileSize - 150;
        LbaToMsf();
        CopyMsf(_discState.SessionMsf, _discState.TempMsf);
    }

    /// <inheritdoc />
    public void SetCommand(byte value)
    {
        if (_commandPointer < _command.Length)
        {
            _command[_commandPointer] = value;
            _commandPointer++;
        }

        if (_commandPointer >= _command.Length || _command[0] == 0x08)
        {
            ExecuteCommand();
            _commandPointer = 0;
        }
    }

    /// <inheritdoc />
    public bool FiqPending
    {
        get
        {
            return (((_poll & PollStatus) != 0) && ((_poll & PollStatusMask) != 0))
                || (((_poll & PollData) != 0) && ((_poll & PollDataMask) != 0));
        }
    }

    /// <inheritdoc />
    public void SetData(uint value)
    {
        // The CD device ignores the data FIFO in the reference.
        _ = value;
    }

    /// <inheritdoc />
    public uint GetData()
    {
        uint result = 0;

        if (_dataLength > 0)
        {
            result = _data[_dataPointer];
            _dataLength--;
            _dataPointer++;

            if (_dataLength == 0)
            {
                _dataPointer = 0;
                if (_requested != 0)
                {
                    ReadSectorIntoData();
                    _currentSector++;
                    _requested--;
                    _dataLength = RequestSize;
                }
                else
                {
                    _poll &= unchecked((byte)~PollData);
                    _requested = 0;
                    _dataLength = 0;
                    _dataPointer = 0;
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public uint GetStatus()
    {
        uint result = 0;
        if (_statusLength > 0)
        {
            result = _status[0];
            _statusLength--;
            if (_statusLength > 0)
            {
                Array.Copy(_status, 1, _status, 0, _statusLength);
            }
            else
            {
                _poll &= unchecked((byte)~PollStatus);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public void SetPoll(uint value)
    {
        _poll &= 0xf0;
        _poll |= (byte)(value & 0x0f);
    }

    /// <inheritdoc />
    public uint GetPoll() => _poll;

    /// <inheritdoc />
    public uint GetReserved() => 0;

    /// <summary>Writes the device save-state payload.</summary>
    /// <param name="writer">Destination writer.</param>
    public void Save(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteByte(_poll);
        writer.WriteByte(_xbusStatus);
        writer.WriteByte(_statusLength);
        writer.WriteInt32(_dataLength);
        writer.WriteInt32(_dataPointer);
        writer.WriteByte(_commandPointer);
        writer.WriteInt32(_requested);
        writer.WriteUInt32((uint)_meiStatus);
        writer.WriteInt32(_currentSector);
        writer.WriteBytes(_status);
        writer.WriteBytes(_data);
        writer.WriteBytes(_command);

        writer.WriteByte(_discState.FirstTrack);
        writer.WriteByte(_discState.LastTrack);
        writer.WriteBytes(_discState.CurrentAbsoluteMsf);
        writer.WriteBytes(_discState.NextMsf);
        writer.WriteBytes(_discState.TempMsf);
        writer.WriteInt32(_discState.TempBlock);
        writer.WriteInt32(_discState.TempLba);
        writer.WriteByte(_discState.CurrentError);
        writer.WriteByte(_discState.CurrentXbus);
        writer.WriteUInt32(_discState.CurrentOffset);
        writer.WriteUInt32(_discState.CurrentBlockSize);
        writer.WriteByte(_discState.CurrentSpeed);
        writer.WriteBytes(_discState.TotalMsf);
        writer.WriteByte(_discState.DiscId);
        writer.WriteBytes(_discState.SessionMsf);
        for (var i = 0; i < _discState.Toc.Length; i++)
        {
            var entry = _discState.Toc[i];
            writer.WriteByte(entry.Reserved0);
            writer.WriteByte(entry.Control);
            writer.WriteByte(entry.TrackNumber);
            writer.WriteByte(entry.Reserved1);
            writer.WriteByte(entry.Minute);
            writer.WriteByte(entry.Second);
            writer.WriteByte(entry.Frame);
            writer.WriteByte(entry.Reserved2);
        }
    }

    /// <summary>Reads the device save-state payload.</summary>
    /// <param name="reader">Source reader.</param>
    public void Load(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _poll = reader.ReadByte();
        _xbusStatus = reader.ReadByte();
        _statusLength = reader.ReadByte();
        _dataLength = reader.ReadInt32();
        _dataPointer = reader.ReadInt32();
        _commandPointer = reader.ReadByte();
        _requested = reader.ReadInt32();
        _meiStatus = (MeiError)reader.ReadUInt32();
        _currentSector = reader.ReadInt32();
        reader.ReadBytes(_status);
        reader.ReadBytes(_data);
        reader.ReadBytes(_command);

        _discState.FirstTrack = reader.ReadByte();
        _discState.LastTrack = reader.ReadByte();
        reader.ReadBytes(_discState.CurrentAbsoluteMsf);
        reader.ReadBytes(_discState.NextMsf);
        reader.ReadBytes(_discState.TempMsf);
        _discState.TempBlock = reader.ReadInt32();
        _discState.TempLba = reader.ReadInt32();
        _discState.CurrentError = reader.ReadByte();
        _discState.CurrentXbus = reader.ReadByte();
        _discState.CurrentOffset = reader.ReadUInt32();
        _discState.CurrentBlockSize = reader.ReadUInt32();
        _discState.CurrentSpeed = reader.ReadByte();
        reader.ReadBytes(_discState.TotalMsf);
        _discState.DiscId = reader.ReadByte();
        reader.ReadBytes(_discState.SessionMsf);
        for (var i = 0; i < _discState.Toc.Length; i++)
        {
            _discState.Toc[i].Reserved0 = reader.ReadByte();
            _discState.Toc[i].Control = reader.ReadByte();
            _discState.Toc[i].TrackNumber = reader.ReadByte();
            _discState.Toc[i].Reserved1 = reader.ReadByte();
            _discState.Toc[i].Minute = reader.ReadByte();
            _discState.Toc[i].Second = reader.ReadByte();
            _discState.Toc[i].Frame = reader.ReadByte();
            _discState.Toc[i].Reserved2 = reader.ReadByte();
        }
    }

    /// <inheritdoc />
    public void SaveState(IStateWriter writer) => Save(writer);

    /// <inheritdoc />
    public void LoadState(IStateReader reader) => Load(reader);

    private void ExecuteCommand()
    {
        _statusLength = 0;
        _poll &= unchecked((byte)~PollStatus);
        _poll &= unchecked((byte)~PollData);
        _xbusStatus &= unchecked((byte)~CdstError);
        _xbusStatus &= unchecked((byte)~CdstReady);

        TraceWriter?.WriteLine(
            $"cd f={FrameProvider?.Invoke() ?? -1} pc={ProgramCounterProvider?.Invoke() ?? 0:x8} "
            + $"cmd {_command[0]:x2} {_command[1]:x2} {_command[2]:x2} "
            + $"{_command[3]:x2} {_command[4]:x2} {_command[5]:x2} {_command[6]:x2}");
        CommandObserver?.Invoke(_command[0]);

        switch (_command[0])
        {
            case 0x01: // seek
            case 0x04: // diagnostics
            case 0x07: // inject disc
            case 0x0A: // reset
            case 0x86: // read header
            case 0x88: // read UPC
            case 0x89: // read ISRC
            case 0x8E: // read device driver
                break;
            case 0x02: // spin up
                Spin(true);
                break;
            case 0x03: // spin down
                Spin(false);
                break;
            case 0x06: // eject
                _xbusStatus &= unchecked((byte)~CdstTray);
                _xbusStatus &= unchecked((byte)~CdstDisc);
                _xbusStatus &= unchecked((byte)~CdstSpin);
                _xbusStatus &= unchecked((byte)~Cdst2X);
                _xbusStatus &= unchecked((byte)~CdstError);
                _xbusStatus |= CdstReady;
                _poll |= PollStatus;
                _poll &= unchecked((byte)~PollMagic);
                _meiStatus = MeiError.NoError;
                SetStatus(0x06);
                break;
            case 0x08: // abort
                StatusWithTrailer(0x08, 31);
                _xbusStatus |= CdstReady;
                _meiStatus = MeiError.NoError;
                break;
            case 0x09: // mode set
                _xbusStatus |= CdstReady;
                _meiStatus = MeiError.NoError;
                _poll |= PollStatus;
                SetStatus(0x09);
                break;
            case 0x0B: // flush
                _xbusStatus |= CdstReady;
                StatusWithTrailer(0x0B, 31);
                _meiStatus = MeiError.NoError;
                break;
            case 0x10: // read data
                ReadDataCommand();
                break;
            case 0x80: // data path check
                _xbusStatus |= CdstReady;
                _statusLength = 4;
                _status[0] = 0x80;
                _status[1] = 0xAA;
                _status[2] = 0x55;
                _status[3] = _xbusStatus;
                _poll |= PollStatus;
                _meiStatus = MeiError.NoError;
                break;
            case 0x82: // read error
                for (var i = 1; i <= 8; i++)
                {
                    _status[i] = (byte)_meiStatus;
                }

                _status[0] = 0x82;
                _xbusStatus |= CdstReady;
                _status[9] = _xbusStatus;
                _statusLength = 10;
                _poll |= PollStatus;
                break;
            case 0x83: // read id
                _xbusStatus |= CdstReady;
                _statusLength = 12;
                _status[0] = 0x83;
                _status[1] = 0x00;
                _status[2] = 0x10;
                _status[3] = 0x00;
                _status[4] = 0x01;
                _status[5] = 0;
                _status[6] = 0;
                _status[7] = 0;
                _status[8] = 0;
                _status[9] = 0x00;
                _status[10] = 0x00;
                _status[11] = _xbusStatus;
                _poll |= PollStatus;
                _meiStatus = MeiError.NoError;
                break;
            case 0x84: // mode sense
                _statusLength = 4;
                _status[0] = 0;
                _status[1] = 0;
                _status[2] = 0;
                if ((_xbusStatus & CdstTray) != 0 && (_xbusStatus & CdstDisc) != 0)
                {
                    _xbusStatus |= CdstReady;
                }
                else
                {
                    _xbusStatus |= CdstError;
                    _xbusStatus &= unchecked((byte)~CdstReady);
                }

                _poll |= PollStatus;
                _status[3] = _xbusStatus;
                break;
            case 0x85: // read capacity
                if (DiscReadyForRead())
                {
                    _statusLength = 8;
                    _status[0] = 0x85;
                    _status[1] = 0;
                    _status[2] = _discState.TotalMsf[0];
                    _status[3] = _discState.TotalMsf[1];
                    _status[4] = _discState.TotalMsf[2];
                    _status[5] = 0;
                    _status[6] = 0;
                    _xbusStatus |= CdstReady;
                    _status[7] = _xbusStatus;
                    _poll |= PollStatus;
                    _meiStatus = MeiError.NoError;
                }
                else
                {
                    FailWithStatus(0x85);
                }

                break;
            case 0x87: // read sub-Q
                if (DiscReadyForRead())
                {
                    _statusLength = 12;
                    _status[0] = 0x87;
                    for (var i = 1; i <= 5; i++)
                    {
                        _status[i] = 0;
                    }

                    _xbusStatus |= CdstReady;
                    for (var i = 6; i <= 10; i++)
                    {
                        _status[i] = 0;
                    }

                    _status[11] = _xbusStatus;
                    _poll |= PollStatus;
                    _meiStatus = MeiError.NoError;
                }
                else
                {
                    FailWithStatus(0x85);
                }

                break;
            case 0x8A: // read disc code
                if (DiscReadyForRead())
                {
                    _statusLength = 12;
                    _status[0] = 0x8A;
                    for (var i = 1; i <= 5; i++)
                    {
                        _status[i] = 0;
                    }

                    _xbusStatus |= CdstReady;
                    for (var i = 6; i <= 10; i++)
                    {
                        _status[i] = 0;
                    }

                    _status[11] = _xbusStatus;
                    _poll |= PollStatus;
                    _meiStatus = MeiError.NoError;
                }
                else
                {
                    FailWithStatus(0x85);
                }

                break;
            case 0x8B: // read disc information
                _statusLength = 8;
                _status[0] = 0x8B;
                if ((_xbusStatus & (CdstTray | CdstDisc | CdstSpin)) != 0)
                {
                    _status[1] = _discState.DiscId;
                    _status[2] = _discState.FirstTrack;
                    _status[3] = _discState.LastTrack;
                    _status[4] = _discState.TotalMsf[0];
                    _status[5] = _discState.TotalMsf[1];
                    _xbusStatus |= CdstReady;
                    _status[6] = _discState.TotalMsf[2];
                    _meiStatus = MeiError.NoError;
                    _status[7] = _xbusStatus;
                }
                else
                {
                    _statusLength = 2;
                    _xbusStatus |= CdstError;
                    _meiStatus = MeiError.ReceiveEcc;
                    _status[1] = _xbusStatus;
                }

                _poll |= PollStatus;
                break;
            case 0x8C: // read TOC
                _statusLength = 10;
                _status[0] = 0x8C;
                if ((_xbusStatus & (CdstTray | CdstDisc | CdstSpin)) != 0)
                {
                    var entry = _discState.Toc[_command[2]];
                    _status[1] = entry.Reserved0;
                    _status[2] = entry.Control;
                    _status[3] = entry.TrackNumber;
                    _status[4] = entry.Reserved1;
                    _status[5] = entry.Minute;
                    _xbusStatus |= CdstReady;
                    _status[6] = entry.Second;
                    _status[7] = entry.Frame;
                    _status[8] = entry.Reserved2;
                    _meiStatus = MeiError.NoError;
                    _status[9] = _xbusStatus;
                }
                else
                {
                    _statusLength = 2;
                    _xbusStatus |= CdstError;
                    _meiStatus = MeiError.ReceiveEcc;
                    _status[1] = _xbusStatus;
                }

                _poll |= PollStatus;
                break;
            case 0x8D: // read session information
                _statusLength = 8;
                _status[0] = 0x8D;
                if ((_xbusStatus & CdstTray) != 0 && (_xbusStatus & CdstDisc) != 0)
                {
                    _status[1] = 0x00;
                    _status[2] = 0x00;
                    _status[3] = 0x02;
                    _status[4] = 0x00;
                    _status[5] = 0x00;
                    _xbusStatus |= CdstReady;
                    _status[6] = 0x00;
                    _status[7] = _xbusStatus;
                    _meiStatus = MeiError.NoError;
                }
                else
                {
                    _statusLength = 2;
                    _xbusStatus |= CdstError;
                    _status[1] = _xbusStatus;
                    _meiStatus = MeiError.ReceiveEcc;
                }

                _poll |= PollStatus;
                break;
            case 0x93:
                _statusLength = 4;
                _status[0] = 0;
                _status[1] = 0;
                _status[2] = 0;
                if ((_xbusStatus & CdstTray) != 0 && (_xbusStatus & CdstDisc) != 0)
                {
                    _xbusStatus |= CdstReady;
                }
                else
                {
                    _xbusStatus |= CdstError;
                    _xbusStatus |= CdstReady;
                }

                _poll |= PollStatus;
                _status[3] = _xbusStatus;
                break;
            default:
                break;
        }
    }

    private void ReadDataCommand()
    {
        if (!DiscReadyForRead())
        {
            _xbusStatus |= CdstError;
            _xbusStatus &= unchecked((byte)~CdstReady);
            _poll |= PollStatus;
            SetStatus(0x10);
            _meiStatus = MeiError.ReceiveEcc;
            return;
        }

        _xbusStatus |= CdstReady;
        SetStatus(0x10);

        _discState.CurrentAbsoluteMsf[0] = _command[1];
        _discState.CurrentAbsoluteMsf[1] = _command[2];
        _discState.CurrentAbsoluteMsf[2] = _command[3];
        CopyMsf(_discState.TempMsf, _discState.CurrentAbsoluteMsf);
        MsfToLba();
        _currentSector = _discState.TempLba;

        _requested = (_command[5] << 8) + _command[6];
        if (_requested != 0)
        {
            ReadSectorIntoData();
            _currentSector++;
            _dataLength = RequestSize;
            _requested--;
        }
        else
        {
            _dataLength = 0;
        }

        _poll |= PollData;
        _poll |= PollStatus;
        _meiStatus = MeiError.NoError;
    }

    private void Spin(bool up)
    {
        if ((_xbusStatus & CdstTray) != 0 && (_xbusStatus & CdstDisc) != 0)
        {
            if (up)
            {
                _xbusStatus |= CdstSpin;
            }
            else
            {
                _xbusStatus &= unchecked((byte)~CdstSpin);
            }

            _xbusStatus |= CdstReady;
            _meiStatus = MeiError.NoError;
        }
        else
        {
            _xbusStatus |= CdstError;
            if (up)
            {
                _xbusStatus &= unchecked((byte)~CdstReady);
            }
            else
            {
                _xbusStatus |= CdstReady;
            }

            _meiStatus = MeiError.ReceiveEcc;
        }

        _poll |= PollStatus;
        SetStatus(_command[0]);
    }

    private void SetStatus(byte command)
    {
        _statusLength = 2;
        _status[0] = command;
        _status[1] = _xbusStatus;
    }

    private void StatusWithTrailer(byte command, int count)
    {
        _statusLength = (byte)(count + 2);
        _status[0] = command;
        for (var i = 1; i <= count; i++)
        {
            _status[i] = 0;
        }

        _status[count + 1] = _xbusStatus;
    }

    private void FailWithStatus(byte command)
    {
        _xbusStatus |= CdstError;
        _xbusStatus &= unchecked((byte)~CdstReady);
        _statusLength = 2;
        _status[0] = command;
        _status[1] = _xbusStatus;
        _poll |= PollStatus;
        _meiStatus = MeiError.ReceiveEcc;
    }

    private bool DiscReadyForRead()
    {
        return (_xbusStatus & CdstTray) != 0
            && (_xbusStatus & CdstDisc) != 0
            && (_xbusStatus & CdstSpin) != 0;
    }

    private void ReadSectorIntoData()
    {
        if (_disc is null)
        {
            Array.Clear(_data);
            return;
        }

        var sector = _currentSector;
        if (sector < 0 || sector >= _disc.SectorCount)
        {
            Array.Clear(_data);
            return;
        }

        TraceWriter?.WriteLine($"cd read sector {sector}");
        _disc.ReadSector(sector, _data);
    }

    private void SetTocEntry(int index, byte control, byte track, byte minute, byte second, byte frame)
    {
        _discState.Toc[index].Control = control;
        _discState.Toc[index].TrackNumber = track;
        _discState.Toc[index].Minute = minute;
        _discState.Toc[index].Second = second;
        _discState.Toc[index].Frame = frame;
    }

    private static void SetMsf(byte[] destination, byte minute, byte second, byte frame)
    {
        destination[0] = minute;
        destination[1] = second;
        destination[2] = frame;
    }

    private static void CopyMsf(byte[] destination, byte[] source)
    {
        destination[0] = source[0];
        destination[1] = source[1];
        destination[2] = source[2];
    }

    private static byte BcdToBin(byte value) => (byte)(((value >> 4) * 10) + (value & 0x0f));

    private static byte BinToBcd(byte value) => (byte)(((value / 10) << 4) | (value % 10));

    private void LbaToMsf()
    {
        _discState.TempLba += 150;
        _discState.TempMsf[0] = (byte)(_discState.TempLba / (60 * 75));
        _discState.TempLba %= 60 * 75;
        _discState.TempMsf[1] = (byte)(_discState.TempLba / 75);
        _discState.TempMsf[2] = (byte)(_discState.TempLba % 75);
    }

    private void MsfToLba()
    {
        _discState.TempLba = ((_discState.TempMsf[0] * 60) + _discState.TempMsf[1]) * 75 + _discState.TempMsf[2] - 150;
        if (_discState.TempLba < 0)
        {
            _discState.TempLba = 0;
        }
    }

    private enum MeiError : byte
    {
        NoError = 0x00,
        ReceiveRetry = 0x01,
        ReceiveEcc = 0x02,
        NotReady = 0x03,
        TocError = 0x04,
        UnreceiveError = 0x05,
        SeekError = 0x06,
        TrackError = 0x07,
        RamError = 0x08,
        DiagnosticError = 0x09,
        FocusError = 0x0A,
        ClvError = 0x0B,
        DataError = 0x0C,
        AddressError = 0x0D,
        CdbError = 0x0E,
        EndAddress = 0x0F,
        ModeError = 0x10,
        MediaChanged = 0x11,
        HardReset = 0x12,
        RomError = 0x13,
        CommandError = 0x14,
        DiscOut = 0x15,
        HardwareError = 0x16,
        IllegalRequest = 0x17,
    }

    private struct TocEntry
    {
        internal byte Reserved0;
        internal byte Control;
        internal byte TrackNumber;
        internal byte Reserved1;
        internal byte Minute;
        internal byte Second;
        internal byte Frame;
        internal byte Reserved2;
    }

    private sealed class DiscState
    {
        internal byte[] CurrentAbsoluteMsf { get; } = new byte[3];

        internal byte[] NextMsf { get; } = new byte[3];

        internal byte[] TempMsf { get; } = new byte[3];

        internal int TempBlock;

        internal int TempLba;

        internal byte CurrentError;

        internal byte CurrentXbus;

        internal uint CurrentOffset;

        internal uint CurrentBlockSize;

        internal byte CurrentSpeed;

        internal byte[] TotalMsf { get; } = new byte[3];

        internal byte FirstTrack;

        internal byte LastTrack;

        internal byte DiscId;

        internal byte[] SessionMsf { get; } = new byte[3];

        internal TocEntry[] Toc { get; } = new TocEntry[100];
    }
}
