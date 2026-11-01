using System;
using System.Runtime.InteropServices;
using FiveDo.Core;

namespace FiveDo.Cdrom;

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
    private const int RequestSize = 2048;

    private const ulong FramesPerSecond = 75;

    private const byte PollStatusMask = 0x01;
    private const byte PollDataMask = 0x02;
    private const byte PollMediaMask = 0x04;
    private const byte PollResetMask = 0x08;
    private const byte PollStatus = 0x10;
    private const byte PollData = 0x20;
    private const byte PollMedia = 0x40;
    private const byte PollReset = 0x80;

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
    private byte _lastCommandOpcode;
    private long _lastCommandFrame = -1;
    private MeiError _meiStatus;
    private DiscState _discState = new();
    private int _currentSector;
    private CdromTimingMode _timingMode = CdromTimingMode.FreeDoCompatible;
    private CdSpeed _configuredSpeed = CdSpeed.X2;
    private int _armClock = EmulatorConstants.ArmClock;
    private ulong _cycles;
    private ulong _nextReadyCycle;
    private bool _readStatusPending;

    /// <summary>Gets or sets the data-sector timing model.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is unknown.</exception>
    public CdromTimingMode TimingMode
    {
        get => _timingMode;
        set
        {
            if (value is not CdromTimingMode.FreeDoCompatible and not CdromTimingMode.OperaCycleBased)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown CD-ROM timing mode.");
            }

            _timingMode = value;
        }
    }

    /// <summary>
    /// Gets or sets the effective CD-ROM read speed used by
    /// <see cref="CdromTimingMode.OperaCycleBased"/>. The stock 3DO drive is 2x.
    /// </summary>
    /// <remarks>
    /// The drive status does not advertise the 2x bit in 5DO, so the configured speed is
    /// treated as the drive's effective speed instead of being reduced to 1x. This keeps the
    /// default CORE-04 timing unchanged and makes the speed option measurable; the status-bit
    /// divergence is recorded in <c>docs/PortingNotes.md</c>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is unknown.</exception>
    public CdSpeed ConfiguredSpeed
    {
        get => _configuredSpeed;
        set
        {
            if (value is not CdSpeed.Unbounded
                and not CdSpeed.X1
                and not CdSpeed.X2
                and not CdSpeed.X4
                and not CdSpeed.X8
                and not CdSpeed.X16
                and not CdSpeed.X24
                and not CdSpeed.X32)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown CD-ROM speed.");
            }

            _configuredSpeed = value;
        }
    }

    /// <summary>Gets the effective speed multiplier, or 0 for <see cref="CdSpeed.Unbounded"/>.</summary>
    public int EffectiveSpeed => (int)_configuredSpeed;

    /// <summary>Gets or sets the ARM clock used to convert sector time to cycles.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not positive.</exception>
    public int ArmClock
    {
        get => _armClock;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The ARM clock must be positive.");
            }

            _armClock = value;
        }
    }

    /// <summary>Gets the total ARM cycles observed by the drive.</summary>
    public ulong Cycles => _cycles;

    /// <summary>Gets the cycle at which the next data sector becomes available.</summary>
    public ulong NextReadyCycle => _nextReadyCycle;

    /// <summary>
    /// Gets the ARM cycles one 2048-byte data sector takes at the configured speed,
    /// including Opera's measured overhead compensation. <see cref="CdSpeed.Unbounded"/>
    /// returns zero so the sector is published immediately.
    /// </summary>
    public ulong DataSectorCycles
    {
        get
        {
            var speed = (ulong)EffectiveSpeed;
            if (speed == 0)
            {
                return 0;
            }

            var baseCycles = (ulong)_armClock / (FramesPerSecond * speed);
            var compensation = (ulong)_armClock * OverheadCompensationX10Usec / 10_000_000;
            return baseCycles > compensation ? baseCycles - compensation : baseCycles;
        }
    }

    /// <summary>Gets whether the Opera poll/reset model is active.</summary>
    /// <remarks>
    /// The accuracy profile selects <see cref="CdromTimingMode.OperaCycleBased"/>. The
    /// default FreeDO-compatible mode keeps the historical poll register behavior so
    /// existing save states and title behavior do not change.
    /// </remarks>
    private bool UseOperaPollSemantics => _timingMode == CdromTimingMode.OperaCycleBased;

    private uint OverheadCompensationX10Usec => _configuredSpeed switch
    {
        CdSpeed.X1 => 1660,
        CdSpeed.X2 => 1670,
        CdSpeed.X4 => 1690,
        CdSpeed.X8 => 1695,
        CdSpeed.X16 => 1704,
        _ => 1695,
    };

    /// <summary>
    /// Gets or sets the callback used to raise the XBUS CD FIQ when a delayed data sector or
    /// status becomes ready. The machine wires this to CLIO.
    /// </summary>
    public Action? InterruptRequested { get; set; }

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

    /// <summary>Gets the emulated frame of the most recent read-data request, or -1.</summary>
    public long LastReadFrame { get; private set; } = -1;

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Cdrom;

    /// <summary>Gets the save-state section version.</summary>
    /// <remarks>Version 2 adds the cycle-based data-sector clock and deadline.</remarks>
    public ushort Version => 2;

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
        _cycles = 0;
        _nextReadyCycle = 0;
        _readStatusPending = false;
        _xbusStatus = 0;
        ResetPoll();
        if (UseOperaPollSemantics)
        {
            // Opera: no media callback means the door is closed and the drive is
            // ready, but no disc is present.
            _xbusStatus |= CdstReady;
        }
        else
        {
            // FreeDO's initial state reports a present disc even with no image.
            _xbusStatus |= CdstTray;
            _xbusStatus |= CdstReady;
            _xbusStatus |= CdstDisc;
            _xbusStatus |= CdstSpin;
        }

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

    /// <summary>
    /// Advances the drive clock. In <see cref="CdromTimingMode.OperaCycleBased"/> mode this
    /// publishes the next data sector when its scheduled cycle arrives.
    /// </summary>
    /// <param name="cycles">ARM cycles executed since the previous tick.</param>
    public void Tick(uint cycles)
    {
        _cycles += cycles;
        if (UseOperaPollSemantics && (_poll & PollResetMask) != 0)
        {
            return;
        }

        if (_timingMode == CdromTimingMode.OperaCycleBased
            && _dataLength == 0
            && _requested > 0
            && (_poll & PollData) == 0)
        {
            TryLoadSector();
        }
    }

    /// <summary>Initializes the drive for the inserted disc. FreeDO: <c>InitCD</c>.</summary>
    public void InitCd()
    {
        _currentSector = 0;
        _nextReadyCycle = 0;
        _readStatusPending = false;
        _xbusStatus = 0;
        ResetPoll();
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

        // FreeDO's InitCD adds 150 to the sector count before LBA2MSF, but LBA2MSF
        // already adds the 150-frame lead-in. The double-add makes the reported
        // disc end 150 frames larger than the actual sector count. The CD DIPIR
        // rejects a disc when the reported end exceeds the volume block count by
        // more than 512 frames, which is why discs with less than ~360 blocks of
        // padding (for example Road Rash) were treated as invalid. Report the real
        // end instead: TotalFrames = SectorCount, i.e. MSF = SectorCount + 150.
        var sectorCount = (uint)(_disc?.SectorCount ?? 0);
        _discState.TempLba = (int)sectorCount;
        LbaToMsf();
        CopyMsf(_discState.TotalMsf, _discState.TempMsf);

        _discState.TempLba = 0;
        LbaToMsf();
        CopyMsf(_discState.SessionMsf, _discState.TempMsf);
    }

    /// <inheritdoc />
    public void SetCommand(byte value)
    {
        if (UseOperaPollSemantics && (_poll & PollResetMask) != 0)
        {
            return;
        }

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

    /// <summary>Gets whether a media-access event is latched. Opera: <c>poll &amp; POLRE</c>.</summary>
    public bool HasMediaAccess => (_poll & PollReset) != 0;

    /// <inheritdoc />
    public bool FiqPending =>
        (((_poll & PollStatus) != 0) && ((_poll & PollStatusMask) != 0))
        || (((_poll & PollData) != 0) && ((_poll & PollDataMask) != 0))
        || (UseOperaPollSemantics
            && ((_poll & PollMedia) != 0)
            && ((_poll & PollMediaMask) != 0));

    /// <inheritdoc />
    public void SetData(uint value)
    {
        // The CD device ignores the data FIFO in the reference.
        _ = value;
    }

    /// <inheritdoc />
    public uint GetData()
    {
        if (UseOperaPollSemantics && (_poll & PollResetMask) != 0)
        {
            return 0;
        }

        if (_dataLength == 0 && _requested > 0)
        {
            TryLoadSector();
        }

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
                    TryLoadSector();
                }
                else
                {
                    _poll &= unchecked((byte)~PollData);
                    _requested = 0;
                    _dataLength = 0;
                    _dataPointer = 0;
                    if (_readStatusPending)
                    {
                        _readStatusPending = false;
                        _poll |= PollStatus;
                        if (FiqPending)
                        {
                            InterruptRequested?.Invoke();
                        }
                    }
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public uint GetStatus()
    {
        if (UseOperaPollSemantics && (_poll & PollResetMask) != 0)
        {
            return 0;
        }

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
        if (!UseOperaPollSemantics)
        {
            _poll &= 0xf0;
            _poll |= (byte)(value & 0x0f);
            return;
        }

        var control = (byte)(value & 0x0f);
        var resetWasAsserted = (_poll & PollResetMask) != 0;

        // Opera: asserting POLREMASK performs a local reset. The reset clears the
        // command/transfer FIFOs and leaves logical media presence untouched.
        if (!resetWasAsserted && (control & PollResetMask) != 0)
        {
            LocalReset();
        }

        // POLRE is a write-one-to-clear media-access latch.
        if ((value & PollReset) != 0)
        {
            _poll &= unchecked((byte)~PollReset);
        }

        _poll = (byte)((_poll & 0xf0) | control);

        // Releasing local reset is itself a media-change notification.
        if (resetWasAsserted && (control & PollResetMask) == 0)
        {
            _poll |= PollReset;
        }
    }

    private void ResetPoll()
    {
        _poll = UseOperaPollSemantics ? PollReset : (byte)0x0f;
    }

    private void LocalReset()
    {
        _dataLength = 0;
        _dataPointer = 0;
        _requested = 0;
        _statusLength = 0;
        _readStatusPending = false;
        _commandPointer = 0;
        _poll &= unchecked((byte)~(PollStatus | PollData));
        _xbusStatus |= CdstReady;
        _xbusStatus &= unchecked((byte)~CdstError);
        _currentSector = 0;
        _nextReadyCycle = _cycles;
        _meiStatus = MeiError.NoError;
        Array.Clear(_command);
        Array.Clear(_status);
        Array.Clear(_data);
        SetMsf(_discState.CurrentAbsoluteMsf, 0, 2, 0);
    }

    /// <inheritdoc />
    public uint GetPoll()
    {
        return _poll;
    }

    /// <inheritdoc />
    public uint GetReserved()
    {
        return 0;
    }

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
        writer.WriteUInt64(_cycles);
        writer.WriteUInt64(_nextReadyCycle);
        writer.WriteByte(_readStatusPending ? (byte)1 : (byte)0);

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
            TocEntry entry = _discState.Toc[i];
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

        if (reader is not StateSectionReader sectionReader || sectionReader.Version >= 2)
        {
            _cycles = reader.ReadUInt64();
            _nextReadyCycle = reader.ReadUInt64();
            var readStatusPending = reader.ReadByte();
            if (readStatusPending > 1)
            {
                throw new InvalidStateException($"Invalid CD-ROM pending status flag {readStatusPending}.");
            }

            _readStatusPending = readStatusPending != 0;
        }
        else
        {
            _cycles = 0;
            _nextReadyCycle = 0;
            _readStatusPending = false;
        }

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

        // Opera applies the local reset after loading a state that was saved with
        // the media-access/reset latch asserted.
        if (UseOperaPollSemantics && (_poll & PollResetMask) != 0)
        {
            LocalReset();
        }
    }

    /// <inheritdoc />
    public void SaveState(IStateWriter writer)
    {
        Save(writer);
    }

    /// <inheritdoc />
    public void LoadState(IStateReader reader)
    {
        Load(reader);
    }

    private void ExecuteCommand()
    {
        _statusLength = 0;
        _readStatusPending = false;
        _poll &= unchecked((byte)~PollStatus);
        _poll &= unchecked((byte)~PollData);
        _xbusStatus &= unchecked((byte)~CdstError);
        _xbusStatus &= unchecked((byte)~CdstReady);

        var frame = FrameProvider?.Invoke() ?? -1;
        TraceWriter?.WriteLine(
            $"cd f={frame} pc={ProgramCounterProvider?.Invoke() ?? 0:x8} "
            + $"cmd {_command[0]:x2} {_command[1]:x2} {_command[2]:x2} "
            + $"{_command[3]:x2} {_command[4]:x2} {_command[5]:x2} {_command[6]:x2}");
        CommandObserver?.Invoke(_command[0]);

        if (_command[0] == 0x83
            && _lastCommandOpcode == 0x83
            && _lastCommandFrame >= 0
            && frame >= 0
            && frame - _lastCommandFrame >= 50)
        {
            // Two consecutive READ_ID polls with no intervening command mean the
            // host is idle-waiting on the drive. Portfolio expects a media-access
            // event to finish a post-race/post-load transition; boot traffic never
            // has this pattern. Setting POLMA (0x40) wakes the waiting task.
            _poll |= PollMedia;
            _lastCommandOpcode = 0;
        }
        else
        {
            _lastCommandOpcode = _command[0];
        }

        _lastCommandFrame = frame;

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
                _poll &= unchecked((byte)~PollMedia);
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
                    TocEntry entry = _discState.Toc[_command[2]];
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
        LastReadFrame = FrameProvider?.Invoke() ?? -1;
        SetStatus(0x10);

        _discState.CurrentAbsoluteMsf[0] = _command[1];
        _discState.CurrentAbsoluteMsf[1] = _command[2];
        _discState.CurrentAbsoluteMsf[2] = _command[3];
        CopyMsf(_discState.TempMsf, _discState.CurrentAbsoluteMsf);
        MsfToLba();
        _currentSector = _discState.TempLba;

        _requested = (_command[5] << 8) + _command[6];
        if (_timingMode == CdromTimingMode.OperaCycleBased)
        {
            // Opera schedules the first sector one sector-time after the command and
            // clears both poll bits until the transfer is ready. The 0x10 status response
            // is published after the last requested sector has been consumed, matching
            // cdrom_data_publish_read_status_if_ready.
            _poll &= unchecked((byte)~PollData);
            _poll &= unchecked((byte)~PollStatus);
            _dataLength = 0;
            _dataPointer = 0;
            _readStatusPending = _requested != 0;
            if (_requested != 0)
            {
                _nextReadyCycle = _cycles + DataSectorCycles;
                TryLoadSector();
            }
            else
            {
                _poll |= PollStatus;
            }
        }
        else
        {
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
        }

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

    private void TryLoadSector()
    {
        if (UseOperaPollSemantics && (_poll & PollResetMask) != 0)
        {
            _poll &= unchecked((byte)~PollData);
            return;
        }

        if (_requested <= 0)
        {
            _poll &= unchecked((byte)~PollData);
            return;
        }

        if (_timingMode == CdromTimingMode.OperaCycleBased && _cycles < _nextReadyCycle)
        {
            _poll &= unchecked((byte)~PollData);
            return;
        }

        ReadSectorIntoData();
        _currentSector++;
        _requested--;
        _dataLength = RequestSize;
        _dataPointer = 0;
        _poll |= PollData;

        if (_timingMode == CdromTimingMode.OperaCycleBased)
        {
            _nextReadyCycle = _cycles + DataSectorCycles;
            if (FiqPending)
            {
                InterruptRequested?.Invoke();
            }
        }
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "UnusedMember.Local",
        Justification = "The MEI error code table is kept complete for hardware reference.")]
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

    [StructLayout(LayoutKind.Sequential)]
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
