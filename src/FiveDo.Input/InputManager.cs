using System;
using System.Threading;
using FiveDo.Core;

namespace FiveDo.Input;

/// <summary>
/// A keyboard-to-controller mapper driven by <see cref="PlayerProfile"/> bindings.
/// </summary>
/// <remarks>
/// The host (Avalonia) forwards key down/up events by key name; this class owns only the
/// emulation-facing state and is independent of the UI toolkit. Player indices are zero-based.
/// </remarks>
public sealed class KeyboardInputMapper
{
    private readonly PlayerProfile[] _profiles;
    private readonly ControllerState[] _states;
    private readonly Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="KeyboardInputMapper"/> class.</summary>
    /// <param name="profiles">Player profiles, up to six.</param>
    public KeyboardInputMapper(ReadOnlySpan<PlayerProfile> profiles)
    {
        _states = new ControllerState[PbusEncoder.MaxControllers];
        _profiles = new PlayerProfile[_states.Length];
        for (var i = 0; i < _profiles.Length; i++)
        {
            _profiles[i] = i < profiles.Length ? profiles[i] : PlayerProfile.CreateDefault(i + 1);
        }
    }

    /// <summary>Gets the state for a player. Safe to call from the emulation thread.</summary>
    /// <param name="player">Zero-based player index.</param>
    /// <returns>The controller state.</returns>
    public ControllerState GetState(int player)
    {
        lock (_gate)
        {
            return _states[player];
        }
    }

    /// <summary>Applies a key event to every player profile that binds the key.</summary>
    /// <param name="key">Key name, case-insensitive.</param>
    /// <param name="pressed">Whether the key is down.</param>
    /// <returns><see langword="true"/> when at least one binding matched.</returns>
    public bool SetKey(string key, bool pressed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            var matched = false;
            for (var player = 0; player < _profiles.Length; player++)
            {
                if (!_profiles[player].Keyboard.TryGetValue(key, out ControllerButton button))
                {
                    foreach ((var name, ControllerButton value) in _profiles[player].Keyboard)
                    {
                        if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                        {
                            button = value;
                            break;
                        }
                    }
                }

                if (button == ControllerButton.None)
                {
                    continue;
                }

                _states[player] = _states[player].Set(button, pressed);
                matched = true;
            }

            return matched;
        }
    }

    /// <summary>Releases every button.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _states.AsSpan().Clear();
        }
    }

    /// <summary>Gets a read-only view of all controller states.</summary>
    public ReadOnlySpan<ControllerState> States
    {
        get
        {
            lock (_gate)
            {
                return _states;
            }
        }
    }
}

/// <summary>
/// A gamepad backend with no connected devices.
/// </summary>
public sealed class NullGamepadBackend : IGamepadBackend
{
    /// <summary>Gets the shared null backend.</summary>
    public static NullGamepadBackend Instance { get; } = new();

    /// <inheritdoc />
    public int ControllerCount => 0;

    /// <inheritdoc />
    public void Poll()
    {
    }

    /// <inheritdoc />
    public ControllerState GetState(int player)
    {
        return ControllerState.Empty;
    }
}

/// <summary>
/// A PBUS source that combines keyboard and gamepad state for up to six players.
/// </summary>
public sealed class InputManager : IPbusSource
{
    private readonly KeyboardInputMapper _keyboard;
    private readonly IGamepadBackend _gamepad;
    private readonly InputDeviceSet _devices = new();
    private int _controllerCount = 1;
    private InputRecorder? _recorder;
    private Func<long>? _frameProvider;

    /// <summary>Initializes a new instance of the <see cref="InputManager"/> class.</summary>
    /// <param name="profile">Input profile whose player bindings drive the keyboard.</param>
    /// <param name="gamepad">Optional gamepad backend.</param>
    public InputManager(InputProfile profile, IGamepadBackend? gamepad = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _keyboard = new KeyboardInputMapper(profile.Players);
        _gamepad = gamepad ?? NullGamepadBackend.Instance;
    }

    /// <summary>Gets the keyboard mapper for the UI layer.</summary>
    public KeyboardInputMapper Keyboard => _keyboard;

    /// <summary>Gets the per-port device selection and peripheral state.</summary>
    public InputDeviceSet Devices => _devices;

    /// <summary>Sets the device type on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <param name="device">Device type.</param>
    public void SetDevice(int port, InputDeviceType device)
    {
        _devices.SetDevice(port, device);
        ControllerCount = Math.Max(ControllerCount, port + 1);
    }

    /// <summary>Gets or sets an optional recorder for player 1 input.</summary>
    /// <remarks>
    /// The recorder is invoked from the emulation thread once per <see cref="GetPbusData"/>
    /// call. Set it before starting emulation and clear it before disposing the recorder.
    /// </remarks>
    public InputRecorder? Recorder
    {
        get => Volatile.Read(ref _recorder);
        set => Volatile.Write(ref _recorder, value);
    }

    /// <summary>Gets or sets the frame number provider used by <see cref="Recorder"/>.</summary>
    public Func<long>? FrameProvider
    {
        get => Volatile.Read(ref _frameProvider);
        set => Volatile.Write(ref _frameProvider, value);
    }

    /// <summary>Gets or sets the connected controller count.</summary>
    public int ControllerCount
    {
        get => Volatile.Read(ref _controllerCount);
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, PbusEncoder.MaxControllers);
            _devices.SetPortCount(value);
            Volatile.Write(ref _controllerCount, value);
        }
    }

    /// <summary>Polls the gamepad and merges it with the keyboard state.</summary>
    public void Update()
    {
        _gamepad.Poll();
        for (var player = 0; player < PbusEncoder.MaxControllers; player++)
        {
            ControllerState pad = player < _gamepad.ControllerCount ? _gamepad.GetState(player) : ControllerState.Empty;
            ControllerState state = new(_keyboard.GetState(player).Buttons | pad.Buttons);
            _devices.SetJoypad(player, state);
            if (_devices.GetDevice(player) == InputDeviceType.Flightstick)
            {
                _devices.SetFlightstick(
                    player,
                    new FlightstickState(
                        state.IsDown(ControllerButton.Left) ? -32768 : state.IsDown(ControllerButton.Right) ? 32767 : 0,
                        state.IsDown(ControllerButton.Up) ? -32768 : state.IsDown(ControllerButton.Down) ? 32767 : 0,
                        0,
                        state.Buttons,
                        state.IsDown(ControllerButton.A)));
            }
        }
    }

    /// <inheritdoc />
    public int GetPbusData(Span<byte> destination)
    {
        Update();
        InputRecorder? recorder = Volatile.Read(ref _recorder);
        if (recorder is not null)
        {
            Func<long>? frameProvider = Volatile.Read(ref _frameProvider);
            recorder.Record(frameProvider?.Invoke() ?? 0, _devices.GetJoypad(0));
        }

        var length = _devices.HasPeripheral
            ? PbusChainEncoder.Encode(_devices, destination)
            : LegacyPacket(destination);
        _devices.ClearTransientMotion();
        return length;
    }

    private int LegacyPacket(Span<byte> destination)
    {
        PbusEncoder.Encode(_devices.Joypads, destination);
        return PbusEncoder.PacketSize;
    }
}
