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

    // Opera: CDROM_MAX_SECTOR_SIZE. A digital-audio transfer may carry the
    // 2352-byte sector plus 96 bytes of subcode (and one error byte), so the
    // maximum wire size is 2449 rather than the 2352-byte raw sector.
    private const int MaxSectorSize = 2449;

    private const int RawSectorSize = 2352;

    private const int SubcodeSize = 96;

    private const byte BlockFlagSubcode = 0x40;
    private const byte BlockFlagError = 0x80;

    private const byte CdCtlAudioTrack = 0x00;
    private const byte CdCtlQPosition = 0x10;

    private const byte DensityDigitalAudio = 0x82;

    private const double DefaultAudioVolume = 0.8;

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

    // Opera: MEI_CDROM_DOUBLE_SPEED. MODE SELECT page 3 stores this byte and
    // cdrom_mode_apply_drive_speed maps it to the XBUS 2x status bit.
    private const byte MeiDoubleSpeed = 0x80;

    private const byte CdCtlDataTrack = 0x04;

    private readonly byte[] _status = new byte[256];
    private readonly byte[] _data = new byte[MaxSectorSize];
    private readonly byte[] _trackSector = new byte[RawSectorSize];
    private readonly byte[] _audioSector = new byte[RawSectorSize];
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
    private int _totalSectorCount;
    private CdromTimingMode _timingMode = CdromTimingMode.FreeDoCompatible;
    private CdSpeed _configuredSpeed = CdSpeed.X2;
    private byte _driveSpeedByte = MeiDoubleSpeed;
    private int _armClock = EmulatorConstants.ArmClock;
    private ulong _cycles;
    private ulong _nextReadyCycle;
    private bool _readStatusPending;
    private int _readSectorSize = RequestSize;
    private byte _densityCode;
    private byte _blockFlags;
    private bool _audioPlaying;
    private int _audioStartLba;
    private int _audioEndLba;
    private ulong _audioStartCycle;
    private int _audioSampleLba;
    private int _audioSampleOffset;
    private double _audioVolume = DefaultAudioVolume;
    private bool _audioMuted;

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
    /// The guest's MODE SELECT page 3 speed byte sets the XBUS 2x status bit. Like Opera's
    /// <c>cdrom_effective_speed</c>, a configured speed above 1x drops to 1x when the guest
    /// has selected single speed. <see cref="CdSpeed.Unbounded"/> stays unbounded because the
    /// reference treats configured speed 0 as a host override that bypasses the guest request.
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
    /// <remarks>
    /// Opera: <c>cdrom_effective_speed</c>. The configured host speed is the ceiling; the
    /// guest's MODE SELECT page 3 request caps it at single speed until the 2x bit is set
    /// again. This keeps the stock 2x default while following a guest that explicitly selects
    /// single speed.
    /// </remarks>
    public int EffectiveSpeed
    {
        get
        {
            var configured = (int)_configuredSpeed;
            if (configured == 0 || configured == 1)
            {
                return configured;
            }

            return RequestedSpeed == 1 ? 1 : configured;
        }
    }

    private int RequestedSpeed => _driveSpeedByte == MeiDoubleSpeed ? 2 : 1;

    /// <summary>Gets the native sector size selected by the most recent MODE SET block page.</summary>
    /// <remarks>
    /// The default is 2048 (Mode 1 user data). A CD-DA player selects 2352-byte
    /// digital-audio sectors with density code <c>0x82</c>, so READ_DATA returns the raw
    /// audio frames from the disc track rather than a 2048-byte data payload.
    /// </remarks>
    public int ReadSectorSize => _readSectorSize;

    private int TransferSize
    {
        get
        {
            int size = _readSectorSize;
            if ((_blockFlags & BlockFlagSubcode) != 0)
            {
                size += SubcodeSize;
            }

            if ((_blockFlags & BlockFlagError) != 0)
            {
                size++;
            }

            return Math.Min(size, MaxSectorSize);
        }
    }

    /// <summary>Gets whether PLAY_AUDIO_MSF or PLAY_AUDIO_TRACK is currently active.</summary>
    public bool IsAudioPlaying => _audioPlaying;

    /// <summary>
    /// Gets or sets the CD-DA mix level from 0 (silent) to 1 (full scale).
    /// </summary>
    /// <remarks>
    /// This level is independent of the host output volume. It is persisted in the CD
    /// save-state section so a resumed CD-DA session keeps the same mix.
    /// </remarks>
    public double AudioVolume
    {
        get => _audioVolume;
        set => _audioVolume = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : DefaultAudioVolume;
    }

    /// <summary>Gets or sets a value indicating whether CD-DA mixing is muted.</summary>
    public bool AudioMuted
    {
        get => _audioMuted;
        set => _audioMuted = value;
    }

    /// <summary>Gets the current logical block address of the audio playback head.</summary>
    public int AudioPositionLba
    {
        get
        {
            if (!_audioPlaying)
            {
                return _audioStartLba;
            }

            var frames = (int)(((_cycles - _audioStartCycle) * FramesPerSecond) / (ulong)_armClock);
            int position = _audioStartLba + frames;
            return Math.Min(position, _audioEndLba);
        }
    }

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

    private uint OverheadCompensationX10Usec => EffectiveSpeed switch
    {
        1 => 1660,
        2 => 1670,
        4 => 1690,
        8 => 1695,
        16 => 1704,
        _ => 1695,
    };

    /// <summary>
    /// Gets or sets the callback used to raise the XBUS CD FIQ when a delayed data sector or
    /// status becomes ready. The machine wires this to CLIO.
    /// </summary>
    public Action? InterruptRequested { get; set; }

    /// <summary>Gets or sets an optional diagnostic writer for command and sector traces.</summary>
    public System.IO.TextWriter? TraceWriter { get; set; }

    /// <summary>
    /// Gets or sets an optional diagnostic writer for CD register accesses.
    /// </summary>
    /// <remarks>
    /// Each line is <c>cdreg f=&lt;frame&gt; cycle=&lt;cycle&gt; pc=&lt;pc&gt; &lt;op&gt; &lt;value&gt;</c>,
    /// where <c>op</c> is <c>cmdw</c>, <c>pollw</c>, <c>pollr</c>, <c>statusr</c>,
    /// <c>dataw</c>, or <c>datr_empty</c>. The trace is diagnostic-only and is not
    /// serialized in save states.
    /// </remarks>
    public System.IO.TextWriter? RegisterTraceWriter { get; set; }

    /// <summary>Gets or sets an optional provider for the current CPU program counter (diagnostics only).</summary>
    public Func<uint>? ProgramCounterProvider { get; set; }

    /// <summary>
    /// Gets or sets an optional provider for the retired ARM instruction count
    /// (diagnostics only). The differential harness records it with each CD command
    /// so a command-cycle delta can be separated into extra work versus extra idle time.
    /// </summary>
    public Func<long>? InstructionCountProvider { get; set; }

    /// <summary>Gets or sets an optional provider for the current emulated frame number (diagnostics only).</summary>
    public Func<long>? FrameProvider { get; set; }

    /// <summary>Gets or sets an optional observer invoked for each executed command (diagnostics only).</summary>
    public Action<byte>? CommandObserver { get; set; }

    /// <summary>
    /// Gets or sets an optional observer invoked when the first command byte (opcode)
    /// is written (diagnostics only).
    /// </summary>
    /// <remarks>
    /// This fires at the same protocol phase as the reference core's command hook,
    /// before the remaining six command bytes arrive. Use it for issue-time command
    /// counts and hashes; use <see cref="CommandObserver"/> for executed commands.
    /// </remarks>
    public Action<byte>? CommandOpcodeObserver { get; set; }

    /// <summary>Gets or sets an optional observer invoked after a command has completed (diagnostics only).</summary>
    /// <remarks>
    /// Unlike <see cref="CommandObserver"/>, this callback runs after the command handler
    /// has populated the status bytes, poll bits, and drive status, so it can capture a
    /// complete command/status transcript entry.
    /// </remarks>
    public Action? CommandCompleted { get; set; }

    /// <summary>Gets the most recent command bytes (opcode plus parameters). Diagnostics only.</summary>
    public ReadOnlySpan<byte> CommandBytes => _command;

    /// <summary>Gets the number of valid bytes in <see cref="StatusBytes"/>.</summary>
    public int StatusLength => _statusLength;

    /// <summary>Gets the current status response buffer. Diagnostics only.</summary>
    public ReadOnlySpan<byte> StatusBytes => _status.AsSpan(0, _statusLength);

    /// <summary>Initializes a new instance of the <see cref="CdromDevice"/> class.</summary>
    public CdromDevice()
    {
        Initialize();
    }

    /// <summary>Gets the currently inserted disc, if any.</summary>
    public IDiscSource? Disc => _disc;

    /// <summary>Gets the drive status byte. FreeDO: <c>XbusStatus</c>.</summary>
    public byte DriveStatus => _xbusStatus;

    /// <summary>Gets a value indicating whether the virtual tray is open.</summary>
    /// <remarks>
    /// The guest eject command clears <c>CdstTray</c>; a host insert or the guest
    /// inject command closes it again.
    /// </remarks>
    public bool TrayOpen => (_xbusStatus & CdstTray) == 0;

    /// <summary>Gets a value indicating whether the drive reports a disc.</summary>
    public bool DiscPresent => (_xbusStatus & CdstDisc) != 0;

    /// <summary>Gets the number of guest-issued eject commands.</summary>
    /// <remarks>
    /// The counter lets a frontend provide an ODE-style swap prompt. Host
    /// <see cref="EjectDisc"/> calls do not change it.
    /// </remarks>
    public int GuestEjectCount { get; private set; }

    /// <summary>Gets the device poll byte. FreeDO: <c>Poll</c>.</summary>
    public byte Poll => _poll;

    /// <summary>Gets the emulated frame of the most recent read-data request, or -1.</summary>
    public long LastReadFrame { get; private set; } = -1;

    /// <summary>
    /// Reads the next 44.1 kHz stereo CD-DA sample while PLAY_AUDIO is active.
    /// </summary>
    /// <param name="sample">The decoded signed 16-bit stereo sample.</param>
    /// <returns><see langword="true"/> when a sample was produced.</returns>
    /// <remarks>
    /// Red Book audio sectors carry 588 stereo frames (2352 bytes) at 44.1 kHz. The
    /// machine calls this once per DSP sample and mixes the result into its output.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A bad audio sector stops CD-DA playback; it must not fault emulation.")]
    public bool TryGetAudioSample(out AudioSample sample)
    {
        sample = default;
        if (!_audioPlaying || _disc is not ITrackedDiscSource tracked)
        {
            return false;
        }

        while (true)
        {
            if (_audioSampleLba >= _audioEndLba || _audioSampleLba < _audioStartLba)
            {
                _audioPlaying = false;
                return false;
            }

            if (_audioSampleOffset == 0)
            {
                try
                {
                    int read = tracked.ReadTrackSector(_audioSampleLba, _audioSector);
                    if (read < 4)
                    {
                        _audioPlaying = false;
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    TraceWriter?.WriteLine($"cd audio read failed lba={_audioSampleLba}: {exception.Message}");
                    _audioPlaying = false;
                    return false;
                }
            }

            if (_audioSampleOffset + 4 > MaxSectorSize)
            {
                _audioSampleLba++;
                _audioSampleOffset = 0;
                continue;
            }

            short left = (short)(_audioSector[_audioSampleOffset] | (_audioSector[_audioSampleOffset + 1] << 8));
            short right = (short)(_audioSector[_audioSampleOffset + 2] | (_audioSector[_audioSampleOffset + 3] << 8));
            _audioSampleOffset += 4;
            if (_audioSampleOffset >= MaxSectorSize)
            {
                _audioSampleOffset = 0;
                _audioSampleLba++;
            }

            sample = new AudioSample(left, right);
            return true;
        }
    }

    /// <summary>Gets the save-state section identifier.</summary>
    public uint Id => StateSectionIds.Cdrom;

    /// <summary>Gets the save-state section version.</summary>
    /// <remarks>
    /// Version 2 adds the cycle-based data-sector clock and deadline. Version 3 adds the
    /// MODE SET sector size/block flags and the PLAY_AUDIO playback position. Version 4
    /// adds the CD-DA mix volume and mute flag. Version 5 adds the guest drive-speed mode
    /// and the volume-header readable-disc limit.
    /// </remarks>
    public ushort Version => 5;

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
        _readSectorSize = RequestSize;
        _densityCode = 0;
        _blockFlags = 0;
        _audioPlaying = false;
        _audioStartLba = 0;
        _audioEndLba = 0;
        _audioStartCycle = 0;
        _audioSampleLba = 0;
        _audioSampleOffset = 0;
        _xbusStatus = 0;
        _totalSectorCount = 0;
        ResetPoll();
        // Opera's cdrom_mode_reset selects MEI_CDROM_DOUBLE_SPEED, so the stock
        // drive reports the XBUS 2x bit before the guest writes MODE SELECT.
        _driveSpeedByte = MeiDoubleSpeed;
        ApplyDriveSpeed();
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
        SetTocEntry(1, CdCtlDataTrack | CdCtlQPosition, track: 1, minute: 0, second: 2, frame: 0);

        _discState.TempLba = 150;
        LbaToMsf();
        CopyMsf(_discState.TotalMsf, _discState.TempMsf);
        _discState.TempLba = 0;
        LbaToMsf();
        CopyMsf(_discState.SessionMsf, _discState.TempMsf);

        if (_disc is ITrackedDiscSource tracked)
        {
            BuildToc(tracked);
        }
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
        if (_audioPlaying && AudioPositionLba >= _audioEndLba)
        {
            _audioPlaying = false;
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
        _readSectorSize = RequestSize;
        _densityCode = 0;
        _blockFlags = 0;
        _audioPlaying = false;
        _audioStartLba = 0;
        _audioEndLba = 0;
        _audioStartCycle = 0;
        _audioSampleLba = 0;
        _audioSampleOffset = 0;
        _xbusStatus = 0;
        _totalSectorCount = 0;
        ResetPoll();
        _driveSpeedByte = MeiDoubleSpeed;
        ApplyDriveSpeed();
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
        SetTocEntry(1, CdCtlDataTrack | CdCtlQPosition, track: 1, minute: 0, second: 2, frame: 0);

        // FreeDO's InitCD adds 150 to the sector count before LBA2MSF, but LBA2MSF
        // already adds the 150-frame lead-in. The double-add makes the reported
        // disc end 150 frames larger than the actual sector count. The CD DIPIR
        // rejects a disc when the reported end exceeds the volume block count by
        // more than 512 frames, which is why discs with less than ~360 blocks of
        // padding (for example Road Rash) were treated as invalid.
        //
        // Opera reports the 3DO volume header's volume_block_count (sector 0,
        // offset 0x50, big-endian), not the physical image size. The two differ on
        // pressed discs (this title: 327,168 declared vs 327,320 physical), and the
        // declared count is the reference's readable-disc limit.
        _totalSectorCount = ResolveSectorCount();
        var sectorCount = (uint)_totalSectorCount;
        _discState.TempLba = (int)sectorCount;
        LbaToMsf();
        CopyMsf(_discState.TotalMsf, _discState.TempMsf);

        _discState.TempLba = 0;
        LbaToMsf();
        CopyMsf(_discState.SessionMsf, _discState.TempMsf);

        if (_disc is ITrackedDiscSource tracked)
        {
            BuildToc(tracked);
        }
    }

    /// <inheritdoc />
    public void SetCommand(byte value)
    {
        TraceRegister("cmdw", value);

        if (_commandPointer == 0)
        {
            CommandOpcodeObserver?.Invoke(value);
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
        TraceRegister("dataw", value);
        // The CD device ignores the data FIFO in the reference.
        _ = value;
    }

    /// <inheritdoc />
    public uint GetData()
    {
        var hadData = _dataLength > 0;
        if (_dataLength == 0 && _requested > 0)
        {
            TryLoadSector();
        }

        if (!hadData && _dataLength == 0)
        {
            TraceRegister("datr_empty", 0);
        }

        uint result = 0;

        if (_dataLength > 0)
        {
            result = _data[_dataPointer];
            _dataLength--;
            _dataPointer++;
            TraceRegister("datr", result);

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

        TraceRegister("statusr", result);
        return result;
    }

    /// <inheritdoc />
    public void SetPoll(uint value)
    {
        TraceRegister("pollw", value);
        if (!UseOperaPollSemantics)
        {
            _poll &= 0xf0;
            _poll |= (byte)(value & 0x0f);
            return;
        }

        var control = (byte)(value & 0x0f);

        // Opera: asserting POLREMASK performs a local reset. The reset clears the
        // command/transfer FIFOs and leaves logical media presence untouched.
        if ((control & PollResetMask) != 0)
        {
            LocalReset();
        }

        // POLRE is a write-one-to-clear media-access latch.
        if ((value & PollReset) != 0)
        {
            _poll &= unchecked((byte)~PollReset);
        }

        // Opera keeps only the three interrupt enables in the low nibble. POLREMASK
        // is a write-one-to-reset control and must not persist, or every later
        // command/status/data access is gated until the guest writes poll again.
        _poll = (byte)((_poll & 0xf0) | (control & (PollStatusMask | PollDataMask | PollMediaMask)));
    }

    private void ApplyDriveSpeed()
    {
        if (_driveSpeedByte == MeiDoubleSpeed)
        {
            _xbusStatus |= Cdst2X;
        }
        else
        {
            _xbusStatus &= unchecked((byte)~Cdst2X);
        }
    }

    private void ResetPoll()
    {
        // Opera: 0x220/0x540 reset leaves all three interrupt enables set. POLRE is
        // the write-one-to-clear media-access latch; the reference latches it on the
        // boot disc access, so the first guest read-modify-write preserves 0x07.
        _poll = UseOperaPollSemantics
            ? (byte)(PollReset | PollMediaMask | PollStatusMask | PollDataMask)
            : (byte)0x0f;
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
        TraceRegister("pollr", _poll);
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
        writer.WriteInt32(_totalSectorCount);
        writer.WriteBytes(_status);
        writer.WriteBytes(_data);
        writer.WriteBytes(_command);
        writer.WriteUInt64(_cycles);
        writer.WriteUInt64(_nextReadyCycle);
        writer.WriteByte(_readStatusPending ? (byte)1 : (byte)0);
        writer.WriteInt32(_readSectorSize);
        writer.WriteByte(_densityCode);
        writer.WriteByte(_blockFlags);
        writer.WriteByte(_driveSpeedByte);
        writer.WriteByte(_audioPlaying ? (byte)1 : (byte)0);
        writer.WriteInt32(_audioStartLba);
        writer.WriteInt32(_audioEndLba);
        writer.WriteUInt64(_audioStartCycle);
        writer.WriteInt32(_audioSampleLba);
        writer.WriteInt32(_audioSampleOffset);
        writer.WriteDouble(_audioVolume);
        writer.WriteByte(_audioMuted ? (byte)1 : (byte)0);

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
        int version = reader is StateSectionReader header ? header.Version : int.MaxValue;
        _poll = reader.ReadByte();
        _xbusStatus = reader.ReadByte();
        _statusLength = reader.ReadByte();
        _dataLength = reader.ReadInt32();
        _dataPointer = reader.ReadInt32();
        _commandPointer = reader.ReadByte();
        _requested = reader.ReadInt32();
        _meiStatus = (MeiError)reader.ReadUInt32();
        _currentSector = reader.ReadInt32();
        _totalSectorCount = version >= 5 ? reader.ReadInt32() : ResolveSectorCount();
        if (_totalSectorCount < 0)
        {
            throw new InvalidStateException($"Invalid CD-ROM sector count {_totalSectorCount}.");
        }

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

        if (version >= 3)
        {
            _readSectorSize = reader.ReadInt32();
            if (_readSectorSize is < 1 or > MaxSectorSize)
            {
                throw new InvalidStateException($"Invalid CD-ROM sector size {_readSectorSize}.");
            }

            _densityCode = reader.ReadByte();
            _blockFlags = reader.ReadByte();
            if (version >= 5)
            {
                _driveSpeedByte = reader.ReadByte();
            }
            else
            {
                // Version 3/4 stored only the derived XBUS bit. The guest-selected
                // single-speed mode is lost while ejected, but the common case is
                // recoverable from the status byte.
                _driveSpeedByte = (_xbusStatus & Cdst2X) != 0 ? MeiDoubleSpeed : (byte)0;
            }

            byte audioPlaying = reader.ReadByte();
            if (audioPlaying > 1)
            {
                throw new InvalidStateException($"Invalid CD-ROM audio playback flag {audioPlaying}.");
            }

            _audioPlaying = audioPlaying != 0;
            _audioStartLba = reader.ReadInt32();
            _audioEndLba = reader.ReadInt32();
            _audioStartCycle = reader.ReadUInt64();
            _audioSampleLba = reader.ReadInt32();
            _audioSampleOffset = reader.ReadInt32();
        }
        else
        {
            _readSectorSize = RequestSize;
            _densityCode = 0;
            _blockFlags = 0;
            _audioPlaying = false;
            _audioStartLba = 0;
            _audioEndLba = 0;
            _audioStartCycle = 0;
            _audioSampleLba = 0;
            _audioSampleOffset = 0;
        }

        if (version >= 4)
        {
            double volume = reader.ReadDouble();
            if (!double.IsFinite(volume) || volume is < 0 or > 1)
            {
                throw new InvalidStateException($"Invalid CD-DA volume {volume}.");
            }

            byte muted = reader.ReadByte();
            if (muted > 1)
            {
                throw new InvalidStateException($"Invalid CD-DA mute flag {muted}.");
            }

            _audioVolume = volume;
            _audioMuted = muted != 0;
        }
        else
        {
            _audioVolume = DefaultAudioVolume;
            _audioMuted = false;
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

        // The old READ_ID idle heuristic set POLMA after two polls >=50 frames apart.
        // With the Opera poll enables restored (0x07), that bit raised a level-held
        // FIQ the reference never has and shifted Space Ace's 3,000-frame FMV hash.
        // The heuristic is retired; the reference latches POLRE on real media events.
        switch (_command[0])
        {
            case 0x01: // seek
                FailWithStatus(0x01, MeiError.IllegalRequest);
                break;
            case 0x04: // diagnostics
                FailWithStatus(0x04, MeiError.IllegalRequest);
                break;
            case 0x0A: // reset
                ResetCommand();
                break;
            case 0x86: // read header
                FailWithStatus(0x86, MeiError.IllegalRequest);
                break;
            case 0x88: // read UPC
                FailWithStatus(0x88, MeiError.IllegalRequest);
                break;
            case 0x89: // read ISRC
                FailWithStatus(0x89, MeiError.IllegalRequest);
                break;
            case 0x8E: // read device driver
                FailWithStatus(0x8E, MeiError.IllegalRequest);
                break;
            case 0x07: // inject disc
                InjectDiscCommand();
                break;
            case 0x02: // spin up
                Spin(true);
                break;
            case 0x03: // spin down
                Spin(false);
                break;
            case 0x06: // eject
                GuestEjectCount++;
                _audioPlaying = false;
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
                _audioPlaying = false;
                StatusWithTrailer(0x08, 31);
                _xbusStatus |= CdstReady;
                _meiStatus = MeiError.NoError;
                break;
            case 0x09: // mode set
                ModeSetCommand();
                break;
            case 0x0E: // play audio MSF
                PlayAudioMsfCommand();
                break;
            case 0x0F: // play audio track
                PlayAudioTrackCommand();
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
                if ((_xbusStatus & CdstTray) != 0 && (_xbusStatus & CdstDisc) != 0)
                {
                    _xbusStatus |= CdstReady;
                    _meiStatus = MeiError.NoError;
                    StatusWithPayload(0x84, ModeSensePayload());
                }
                else
                {
                    _xbusStatus |= CdstError;
                    _xbusStatus &= unchecked((byte)~CdstReady);
                    _statusLength = 2;
                    _status[0] = 0x84;
                    _status[1] = _xbusStatus;
                    _poll |= PollStatus;
                }

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
                if (!DiscReadyForRead())
                {
                    FailWithStatus(0x87);
                    break;
                }

                if (_disc is not ITrackedDiscSource tracked
                    || !TryBuildSubQPayload(tracked, out byte[] subQ))
                {
                    FailWithStatus(0x87);
                    _meiStatus = MeiError.TrackError;
                    break;
                }

                _xbusStatus |= CdstReady;
                _meiStatus = MeiError.NoError;
                StatusWithPayload(0x87, subQ);
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
                if (!DiscReadyForRead())
                {
                    FailWithStatus(0x8C);
                    _meiStatus = MeiError.ReceiveEcc;
                    break;
                }

                byte requestedTrack = _command[2];
                if (requestedTrack == 0xAA)
                {
                    _xbusStatus |= CdstReady;
                    _meiStatus = MeiError.NoError;
                    byte leadOutControl = _discState.Toc[_discState.LastTrack].Control;
                    StatusWithPayload(
                        0x8C,
                        [
                            0x00,
                            leadOutControl,
                            0xAA,
                            0x00,
                            _discState.TotalMsf[0],
                            _discState.TotalMsf[1],
                            _discState.TotalMsf[2],
                            0x00,
                        ]);
                }
                else if (requestedTrack >= _discState.FirstTrack
                    && requestedTrack <= _discState.LastTrack)
                {
                    TocEntry entry = _discState.Toc[requestedTrack];
                    _xbusStatus |= CdstReady;
                    _meiStatus = MeiError.NoError;
                    StatusWithPayload(
                        0x8C,
                        [
                            entry.Reserved0,
                            entry.Control,
                            entry.TrackNumber,
                            entry.Reserved1,
                            entry.Minute,
                            entry.Second,
                            entry.Frame,
                            entry.Reserved2,
                        ]);
                }
                else
                {
                    FailWithStatus(0x8C);
                    _meiStatus = MeiError.TrackError;
                }

                break;
            case 0x8D: // read session information
                // Opera: payload[0] = session_valid ? 0x80 : 0, then the session
                // MSF, then two zero bytes. 5DO does not model multi-session TOCs,
                // so the single-session value is all zero.
                _statusLength = 8;
                _status[0] = 0x8D;
                if ((_xbusStatus & CdstTray) != 0 && (_xbusStatus & CdstDisc) != 0)
                {
                    for (var i = 1; i <= 6; i++)
                    {
                        _status[i] = 0x00;
                    }

                    _xbusStatus |= CdstReady;
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
                FailWithStatus(_command[0], MeiError.IllegalRequest);
                break;
        }

        CommandCompleted?.Invoke();
    }

    private void ResetCommand()
    {
        _audioPlaying = false;
        _requested = 0;
        _dataLength = 0;
        _dataPointer = 0;
        _readStatusPending = false;
        _readSectorSize = RequestSize;
        _densityCode = 0;
        _blockFlags = 0;
        _xbusStatus |= CdstReady;
        _xbusStatus &= unchecked((byte)~CdstError);
        _meiStatus = MeiError.NoError;
        SetStatus(0x0A);
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

        // Opera: cdrom_cmd_read_data rejects an invalid MSF before changing the
        // transfer. Seconds and frames must be below 60 and 75 respectively.
        if (_command[2] >= 60 || _command[3] >= 75)
        {
            ClearTransfer();
            FailWithStatus(0x10, MeiError.AddressError);
            return;
        }

        _discState.CurrentAbsoluteMsf[0] = _command[1];
        _discState.CurrentAbsoluteMsf[1] = _command[2];
        _discState.CurrentAbsoluteMsf[2] = _command[3];
        CopyMsf(_discState.TempMsf, _discState.CurrentAbsoluteMsf);
        MsfToLba();
        _currentSector = _discState.TempLba;

        _requested = (_command[4] << 16) | (_command[5] << 8) | _command[6];

        // Opera: cdrom_cmd_read_data rejects a transfer that starts at or beyond
        // the last disc sector, or that runs past it, with MEI end_address.
        var discBlocks = TotalSectorCount;
        if (_requested != 0
            && (_currentSector >= discBlocks || _requested > discBlocks - _currentSector))
        {
            ClearTransfer();
            FailWithStatus(0x10, MeiError.EndAddress);
            return;
        }

        // A new READ_DATA aborts any transfer still in flight. Without this, the
        // previous sector can leak into the new transfer's FIFO.
        AbortReadAhead();

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
                _dataLength = TransferSize;
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

    private void ModeSetCommand()
    {
        bool accepted;
        switch (_command[1])
        {
            case 0:
                accepted = ApplyBlockMode(_command[2], _command[3], _command[4], _command[5]);
                break;
            case 1: // error recovery
            case 2: // stop time
            case 4: // chunk size
                accepted = true;
                break;
            case 3: // drive speed
                accepted = true;
                _driveSpeedByte = _command[2];
                ApplyDriveSpeed();
                break;
            default:
                accepted = false;
                break;
        }

        if (accepted)
        {
            _xbusStatus |= CdstReady;
            _xbusStatus &= unchecked((byte)~CdstError);
            _meiStatus = MeiError.NoError;
            _poll |= PollStatus;
            SetStatus(0x09);
        }
        else
        {
            FailWithStatus(0x09);
            _meiStatus = MeiError.ModeError;
        }
    }

    private bool ApplyBlockMode(byte density, byte lengthMsb, byte lengthLsb, byte flags)
    {
        int requested = (lengthMsb << 8) | lengthLsb;
        if (requested == 0)
        {
            requested = RequestSize;
        }

        if (requested > MaxSectorSize
            || (flags & ~(BlockFlagSubcode | BlockFlagError)) != 0
            || !IsDensitySupported(density))
        {
            return false;
        }

        int transfer = requested;
        if ((flags & BlockFlagSubcode) != 0)
        {
            transfer += SubcodeSize;
        }

        if ((flags & BlockFlagError) != 0)
        {
            transfer++;
        }

        if (transfer > MaxSectorSize)
        {
            return false;
        }

        _densityCode = density;
        _blockFlags = flags;
        _readSectorSize = requested;
        return true;
    }

    private static bool IsDensitySupported(byte density)
    {
        return density is 0x00 or 0x01 or 0x81 or DensityDigitalAudio;
    }

    private void PlayAudioMsfCommand()
    {
        int start = MsfToLba(_command[1], _command[2], _command[3]);
        bool noEnd = _command[4] == 0xFF && _command[5] == 0xFF && _command[6] == 0xFF;
        int end = noEnd ? TotalSectorCount : MsfToLba(_command[4], _command[5], _command[6]);
        PlayAudio(start, end, 0x0E);
    }

    private void PlayAudioTrackCommand()
    {
        if (_disc is not ITrackedDiscSource tracked)
        {
            PlayAudio(0, 0, 0x0F);
            return;
        }

        int startTrack = _command[1];
        int endTrack = _command[3];
        DiscTrack? start = FindTrackByNumber(tracked, startTrack);
        if (start is null)
        {
            FailWithStatus(0x0F);
            _meiStatus = MeiError.TrackError;
            return;
        }

        int end = tracked.TotalSectorCount;
        if (endTrack > 0 && endTrack < tracked.Tracks[^1].Number)
        {
            DiscTrack? next = FindTrackByNumber(tracked, endTrack + 1);
            if (next is not null)
            {
                end = next.StartLba;
            }
        }

        PlayAudio(start.StartLba, end, 0x0F);
    }

    private void PlayAudio(int start, int end, byte command)
    {
        if (start == 0 && end == 0)
        {
            _audioPlaying = false;
            _audioStartLba = 0;
            _audioEndLba = 0;
            if (DiscReadyForRead())
            {
                _xbusStatus |= CdstReady;
                _meiStatus = MeiError.NoError;
            }
            else
            {
                _meiStatus = MeiError.ReceiveEcc;
                _xbusStatus |= CdstError;
            }

            _poll |= PollStatus;
            SetStatus(command);
            return;
        }

        if (!DiscReadyForRead() || end <= start)
        {
            _audioPlaying = false;
            FailWithStatus(command);
            _meiStatus = MeiError.ReceiveEcc;
            return;
        }

        _audioStartLba = Math.Max(0, start);
        _audioEndLba = Math.Min(end, TotalSectorCount);
        _audioStartCycle = _cycles;
        _audioSampleLba = _audioStartLba;
        _audioSampleOffset = 0;
        _audioPlaying = _audioEndLba > _audioStartLba;
        _xbusStatus |= CdstReady;
        _xbusStatus &= unchecked((byte)~CdstError);
        _meiStatus = MeiError.NoError;
        _poll |= PollStatus;
        SetStatus(command);
    }

    private int TotalSectorCount => _totalSectorCount;

    private int ResolveSectorCount()
    {
        int physical = _disc?.SectorCount ?? 0;
        if (_disc is null || physical == 0)
        {
            return physical;
        }

        // Audio-only discs have no 3DO volume header; the TOC is the only limit.
        if (_disc is ITrackedDiscSource tracked
            && tracked.Tracks.Count > 0
            && tracked.Tracks[0].IsAudio)
        {
            return physical;
        }

        var header = new byte[RequestSize];
        _disc.ReadSector(0, header);
        int declared = (header[0x50] << 24)
            | (header[0x51] << 16)
            | (header[0x52] << 8)
            | header[0x53];
        return declared > 0 && declared <= physical ? declared : physical;
    }

    private static DiscTrack? FindTrackByNumber(ITrackedDiscSource tracked, int number)
    {
        foreach (DiscTrack track in tracked.Tracks)
        {
            if (track.Number == number)
            {
                return track;
            }
        }

        return null;
    }

    private static int MsfToLba(byte minute, byte second, byte frame)
    {
        int lba = ((minute * 60) + second) * 75 + frame - 150;
        return Math.Max(0, lba);
    }

    private void InjectDiscCommand()
    {
        _dataLength = 0;
        _dataPointer = 0;
        _requested = 0;
        _currentSector = 0;
        _audioPlaying = false;
        _xbusStatus |= CdstReady | CdstTray;
        if (_disc is not null)
        {
            _xbusStatus |= CdstDisc | CdstSpin;
        }
        else
        {
            _xbusStatus &= unchecked((byte)~CdstDisc);
            _xbusStatus &= unchecked((byte)~CdstSpin);
        }

        _xbusStatus &= unchecked((byte)~CdstError);
        _meiStatus = MeiError.NoError;
        // Opera: inject re-applies the stored drive-speed mode. Eject clears the
        // 2x status bit, so injecting must restore it from the guest's MODE SELECT.
        ApplyDriveSpeed();
        if (_disc is ITrackedDiscSource tracked)
        {
            BuildToc(tracked);
        }

        _poll |= PollStatus | PollMedia;
        SetStatus(0x07);
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

    private byte[] ModeSensePayload()
    {
        return _command[1] switch
        {
            0 => [_densityCode, (byte)(_readSectorSize >> 8), (byte)_readSectorSize],
            1 => [0x00, 0x08, 0x00],
            2 => [0x00, 0x00, 0x00],
            3 => [0x02, 0x00, 0x00],
            4 => [0x01, 0x00, 0x00],
            _ => [0x00, 0x00, 0x00],
        };
    }

    private void StatusWithPayload(byte command, ReadOnlySpan<byte> payload)
    {
        _status[0] = command;
        payload.CopyTo(_status.AsSpan(1));
        _statusLength = (byte)(payload.Length + 2);
        _status[payload.Length + 1] = _xbusStatus;
        _poll |= PollStatus;
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

    private void FailWithStatus(byte command, MeiError meiError = MeiError.ReceiveEcc)
    {
        _xbusStatus |= CdstError;
        _xbusStatus &= unchecked((byte)~CdstReady);
        _statusLength = 2;
        _status[0] = command;
        _status[1] = _xbusStatus;
        _poll |= PollStatus;
        _meiStatus = meiError;
    }

    private void AbortReadAhead()
    {
        _dataLength = 0;
        _dataPointer = 0;
        _poll &= unchecked((byte)~PollData);
    }

    private void ClearTransfer()
    {
        AbortReadAhead();
        _requested = 0;
        _readStatusPending = false;
        _nextReadyCycle = _cycles;
        Array.Clear(_data);
    }

    private bool DiscReadyForRead()
    {
        return (_xbusStatus & CdstTray) != 0
            && (_xbusStatus & CdstDisc) != 0
            && (_xbusStatus & CdstSpin) != 0;
    }

    private void TraceRegister(string operation, uint value)
    {
        RegisterTraceWriter?.WriteLine(
            $"cdreg f={FrameProvider?.Invoke() ?? -1} cycle={_cycles} "
            + $"pc={ProgramCounterProvider?.Invoke() ?? 0:x8} {operation} {value:x}");
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
        _dataLength = TransferSize;
        _dataPointer = 0;
        _poll |= PollData;
        PublishReadStatusIfReady();

        if (_timingMode == CdromTimingMode.OperaCycleBased)
        {
            ScheduleNextDataSector();
            if (FiqPending)
            {
                InterruptRequested?.Invoke();
            }
        }
    }

    /// <summary>
    /// Publishes the READ_DATA status as soon as the first requested sector is
    /// queued, matching the reference core (Opera: <c>cdrom_data_publish_read_status_if_ready</c>
    /// with <c>data_len != 0</c>). Portfolio's DIPIR consumes read data only when the
    /// data and status are both ready, so waiting for the whole transfer makes the
    /// guest spin longer than the hardware.
    /// </summary>
    private void PublishReadStatusIfReady()
    {
        if (!_readStatusPending)
        {
            return;
        }

        _readStatusPending = false;
        _poll |= PollStatus;
    }

    /// <summary>
    /// Schedules the next data sector from the previous deadline. Opera:
    /// <c>cdrom_data_schedule_next</c>. Adding to the existing deadline keeps the
    /// long-term transfer rate at the configured drive speed even when the guest
    /// consumes a sector late; re-basing to <c>now + sector_cycles</c> would silently
    /// slow every streaming title by the accumulated lateness.
    /// </summary>
    private void ScheduleNextDataSector()
    {
        ulong sectorCycles = DataSectorCycles;
        if (_nextReadyCycle == 0)
        {
            _nextReadyCycle = _cycles + sectorCycles;
            return;
        }

        _nextReadyCycle += sectorCycles;
        if ((long)(_nextReadyCycle - _cycles) < -(long)sectorCycles)
        {
            _nextReadyCycle = _cycles;
        }
    }

    private void ReadSectorIntoData()
    {
        Array.Clear(_data);
        if (_disc is null)
        {
            return;
        }

        int sector = _currentSector;
        if (sector < 0 || sector >= TotalSectorCount)
        {
            return;
        }

        TraceWriter?.WriteLine($"cd read sector {sector} size {_readSectorSize}");
        if (_disc is not ITrackedDiscSource tracked)
        {
            _disc.ReadSector(sector, _data);
            return;
        }

        DiscTrack? track = tracked.FindTrack(sector);
        if (track is null)
        {
            return;
        }

        int available = tracked.ReadTrackSector(sector, _trackSector);
        if (_readSectorSize <= RequestSize && !track.IsAudio)
        {
            int offset = track.PayloadOffset;
            int copy = Math.Min(_readSectorSize, Math.Min(available - offset, _data.Length));
            if (copy > 0)
            {
                _trackSector.AsSpan(offset, copy).CopyTo(_data);
            }
        }
        else if (available >= _readSectorSize)
        {
            _trackSector.AsSpan(0, _readSectorSize).CopyTo(_data);
        }
        else if (!track.IsAudio && available == RequestSize)
        {
            // A cooked 2048-byte track was asked for a raw 2352-byte sector. Build a
            // Mode 1 frame so command transcripts and disc tools see the expected shape.
            BuildRawDataSector(_trackSector, sector, _data);
        }
        else
        {
            _trackSector.AsSpan(0, Math.Min(available, _data.Length)).CopyTo(_data);
        }

        if ((_blockFlags & BlockFlagSubcode) != 0
            && _readSectorSize + SubcodeSize <= _data.Length
            && TryBuildSubQ(tracked, sector, out byte[] q))
        {
            FillSubcode(_data.AsSpan(_readSectorSize, SubcodeSize), q);
        }
    }

    /// <summary>
    /// Packs the 96-bit Q subchannel into the 96-byte subcode block: bit 6 of each
    /// byte carries one Q bit, most-significant bit first. Opera zeroes the subcode
    /// block; 5DO exposes the real Q position so a guest CD player can follow the
    /// track list and read-ahead state.
    /// </summary>
    private static void FillSubcode(Span<byte> destination, ReadOnlySpan<byte> q)
    {
        destination.Clear();
        for (var bit = 0; bit < SubcodeSize; bit++)
        {
            int source = bit >> 3;
            if (source < q.Length && ((q[source] >> (7 - (bit & 7))) & 1) != 0)
            {
                destination[bit] |= 0x40;
            }
        }
    }

    private static void BuildRawDataSector(ReadOnlySpan<byte> userData, int lba, Span<byte> destination)
    {
        destination.Clear();
        destination[0] = 0x00;
        for (var i = 1; i <= 10; i++)
        {
            destination[i] = 0xFF;
        }

        destination[11] = 0x00;
        int absoluteFrame = lba + 150;
        destination[12] = ToBcd(absoluteFrame / (60 * 75));
        destination[13] = ToBcd((absoluteFrame / 75) % 60);
        destination[14] = ToBcd(absoluteFrame % 75);
        destination[15] = 0x01;
        userData[..Math.Min(RequestSize, destination.Length - 16)].CopyTo(destination[16..]);
    }

    private static byte ToBcd(int value)
    {
        return (byte)(((value / 10) << 4) | (value % 10));
    }

    private void BuildToc(ITrackedDiscSource tracked)
    {
        if (tracked.Tracks.Count == 0)
        {
            return;
        }

        _discState.FirstTrack = (byte)tracked.Tracks[0].Number;
        _discState.LastTrack = (byte)tracked.Tracks[^1].Number;

        foreach (DiscTrack track in tracked.Tracks)
        {
            if (track.Number is < 1 or > 99)
            {
                continue;
            }

            int absoluteFrame = track.StartLba + 150;
            SetTocEntry(
                track.Number,
                (byte)((track.IsAudio ? CdCtlAudioTrack : CdCtlDataTrack) | CdCtlQPosition),
                (byte)track.Number,
                (byte)(absoluteFrame / (60 * 75)),
                (byte)((absoluteFrame / 75) % 60),
                (byte)(absoluteFrame % 75));
        }

        _discState.TempLba = _totalSectorCount;
        LbaToMsf();
        CopyMsf(_discState.TotalMsf, _discState.TempMsf);
    }

    private bool TryBuildSubQPayload(ITrackedDiscSource tracked, out byte[] payload)
    {
        int lba = _audioPlaying ? AudioPositionLba : _currentSector;
        if (!TryBuildSubQ(tracked, lba, out byte[] q))
        {
            payload = [];
            return false;
        }

        payload = q[..10];
        return true;
    }

    /// <summary>
    /// Builds the 12-byte Q subchannel for <paramref name="lba"/>: control/ADR,
    /// track, index, absolute MSF, relative MSF, and CRC-16. The READ SUB-Q command
    /// returns the first ten bytes; the READ_DATA subcode block carries all twelve.
    /// </summary>
    private static bool TryBuildSubQ(ITrackedDiscSource tracked, int lba, out byte[] payload)
    {
        DiscTrack? track = tracked.FindTrack(lba);
        if (track is null)
        {
            payload = [];
            return false;
        }

        int absoluteFrame = lba + 150;
        int relative = lba - track.StartLba;
        byte control = (byte)((track.IsAudio ? CdCtlAudioTrack : CdCtlDataTrack) | CdCtlQPosition);
        byte controlAddress = (byte)(((control << 4) & 0xF0) | ((control >> 4) & 0x0F));
        payload =
        [
            0x00,
            controlAddress,
            ToBcd(track.Number),
            (byte)(relative < 0 ? 0x00 : 0x01),
            ToBcd(absoluteFrame / (60 * 75)),
            ToBcd((absoluteFrame / 75) % 60),
            ToBcd(absoluteFrame % 75),
            ToBcd(Math.Max(0, relative) / (60 * 75)),
            ToBcd((Math.Max(0, relative) / 75) % 60),
            ToBcd(Math.Max(0, relative) % 75),
            0x00,
            0x00,
        ];
        ushort crc = ComputeSubQCrc(payload.AsSpan(0, 10));
        payload[10] = (byte)(crc >> 8);
        payload[11] = (byte)crc;
        return true;
    }

    /// <summary>
    /// CRC-16/CCITT (polynomial 0x1021, initial 0) over the ten Q data bytes. The
    /// CD Q subchannel transmits the complement of the CRC.
    /// </summary>
    private static ushort ComputeSubQCrc(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (byte value in data)
        {
            crc ^= (ushort)(value << 8);
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
            }
        }

        return (ushort)~crc;
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
