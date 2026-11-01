using System;
using System.Runtime.InteropServices;

namespace FiveDo.Input;

/// <summary>
/// A device type on one PBUS port.
/// </summary>
public enum InputDeviceType
{
    /// <summary>Standard 3DO joypad. This is the default.</summary>
    Joypad = 0,

    /// <summary>3DO flightstick with three analog axes and fire.</summary>
    Flightstick = 1,

    /// <summary>3DO mouse with relative X/Y motion and four buttons.</summary>
    Mouse = 2,

    /// <summary>3DO lightgun with an absolute screen position.</summary>
    Lightgun = 3,

    /// <summary>Arcade lightgun with coin, start, service, and holster inputs.</summary>
    ArcadeLightgun = 4,

    /// <summary>Orbatak trackball with relative X/Y motion.</summary>
    OrbatakTrackball = 5,

    /// <summary>No device on this port.</summary>
    None = 6,
}

/// <summary>
/// The state of one 3DO flightstick for a single frame.
/// </summary>
/// <param name="Horizontal">Horizontal axis, -32768 (left) to 32767 (right).</param>
/// <param name="Vertical">Vertical axis, -32768 (up) to 32767 (down).</param>
/// <param name="Twist">Twist axis, -32768 to 32767.</param>
/// <param name="Buttons">Digital buttons.</param>
/// <param name="Fire">The trigger button.</param>
public readonly record struct FlightstickState(
    int Horizontal,
    int Vertical,
    int Twist,
    ControllerButton Buttons,
    bool Fire)
{
    /// <summary>Gets a centered flightstick with no buttons pressed.</summary>
    public static FlightstickState Empty => default;

    /// <summary>Gets a value indicating whether the fire trigger is pressed.</summary>
    public bool IsPressed => Fire || Buttons != ControllerButton.None;

    /// <summary>Tests whether a digital button is pressed.</summary>
    /// <param name="button">Button to test.</param>
    /// <returns><see langword="true"/> when pressed.</returns>
    public bool IsDown(ControllerButton button)
    {
        return (Buttons & button) != ControllerButton.None;
    }
}

/// <summary>
/// The state of one 3DO mouse for a single frame.
/// </summary>
/// <param name="DeltaX">Relative X motion, -512 to 511.</param>
/// <param name="DeltaY">Relative Y motion, -512 to 511.</param>
/// <param name="Left">Left button.</param>
/// <param name="Middle">Middle button.</param>
/// <param name="Right">Right button.</param>
/// <param name="Shift">Shift modifier button.</param>
public readonly record struct MouseState(
    int DeltaX,
    int DeltaY,
    bool Left,
    bool Middle,
    bool Right,
    bool Shift)
{
    /// <summary>Gets a mouse with no motion and no buttons pressed.</summary>
    public static MouseState Empty => default;

    /// <summary>Clamps and returns a mouse state with motion limited to the 10-bit range.</summary>
    /// <returns>A state safe for the PBUS encoder.</returns>
    public MouseState Clamped()
    {
        return this with
        {
            DeltaX = Math.Clamp(DeltaX, -512, 511),
            DeltaY = Math.Clamp(DeltaY, -512, 511),
        };
    }
}

/// <summary>
/// The state of one 3DO lightgun for a single frame.
/// </summary>
/// <param name="X">Horizontal screen position in pixels, 0-319.</param>
/// <param name="Y">Vertical screen position in pixels, 0-239.</param>
/// <param name="Trigger">The trigger.</param>
/// <param name="Option">The option/select button.</param>
/// <param name="Reload">The reload button; zeroes the hit counter.</param>
/// <param name="OffScreen">Whether the gun is aimed outside the screen.</param>
public readonly record struct LightgunState(
    int X,
    int Y,
    bool Trigger,
    bool Option,
    bool Reload,
    bool OffScreen)
{
    /// <summary>Gets a lightgun at the top-left with no buttons pressed.</summary>
    public static LightgunState Empty => default;

    /// <summary>Applies Opera's off-screen mapping.</summary>
    /// <returns>The state the PBUS encoder should use.</returns>
    public LightgunState Normalized()
    {
        return OffScreen
            ? this with { Trigger = false, Reload = Trigger || Reload }
            : this;
    }
}

/// <summary>
/// The state of one arcade lightgun for a single frame.
/// </summary>
/// <param name="X">Horizontal screen position in pixels, 0-319.</param>
/// <param name="Y">Vertical screen position in pixels, 0-239.</param>
/// <param name="Trigger">The trigger.</param>
/// <param name="Service">The service button.</param>
/// <param name="Coins">The coin button.</param>
/// <param name="Start">The start button.</param>
/// <param name="Holster">The holster button; reports a no-hit counter.</param>
/// <param name="OffScreen">Whether the gun is aimed outside the screen.</param>
public readonly record struct ArcadeLightgunState(
    int X,
    int Y,
    bool Trigger,
    bool Service,
    bool Coins,
    bool Start,
    bool Holster,
    bool OffScreen)
{
    /// <summary>Gets an arcade lightgun at the top-left with no buttons pressed.</summary>
    public static ArcadeLightgunState Empty => default;

    /// <summary>Applies Opera's off-screen mapping.</summary>
    /// <returns>The state the PBUS encoder should use.</returns>
    public ArcadeLightgunState Normalized()
    {
        return OffScreen
            ? this with { Trigger = false, Holster = Trigger || Holster }
            : this;
    }
}

/// <summary>
/// The state of one Orbatak trackball for a single frame.
/// </summary>
/// <param name="DeltaX">Relative X motion, -24 to 24.</param>
/// <param name="DeltaY">Relative Y motion, -24 to 24.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct OrbatakTrackballState(int DeltaX, int DeltaY)
{
    /// <summary>Gets a trackball with no motion.</summary>
    public static OrbatakTrackballState Empty => default;

    /// <summary>Clamps and returns a trackball state.</summary>
    /// <returns>A state safe for the PBUS encoder.</returns>
    public OrbatakTrackballState Clamped()
    {
        return this with
        {
            DeltaX = Math.Clamp(DeltaX, -24, 24),
            DeltaY = Math.Clamp(DeltaY, -24, 24),
        };
    }
}

/// <summary>
/// The Orbatak cabinet buttons.
/// </summary>
/// <param name="StartP1">Player-1 start.</param>
/// <param name="StartP2">Player-2 start.</param>
/// <param name="CoinP1">Player-1 coin.</param>
/// <param name="CoinP2">Player-2 coin.</param>
/// <param name="Service">Service/credit button.</param>
public readonly record struct OrbatakButtonsState(
    bool StartP1,
    bool StartP2,
    bool CoinP1,
    bool CoinP2,
    bool Service)
{
    /// <summary>Gets an empty button state.</summary>
    public static OrbatakButtonsState Empty => default;
}

/// <summary>
/// A per-port device selection and the state of every supported peripheral.
/// </summary>
/// <remarks>
/// The set owns six ports in wired PBUS order. Joypads remain the default, so a
/// frontend that never touches this type produces exactly the historical packet.
/// </remarks>
public sealed class InputDeviceSet
{
    private readonly InputDeviceType[] _devices = new InputDeviceType[PbusEncoder.MaxControllers];
    private readonly ControllerState[] _joypads = new ControllerState[PbusEncoder.MaxControllers];
    private readonly FlightstickState[] _flightsticks = new FlightstickState[PbusEncoder.MaxControllers];
    private readonly MouseState[] _mice = new MouseState[PbusEncoder.MaxControllers];
    private readonly LightgunState[] _lightguns = new LightgunState[PbusEncoder.MaxControllers];
    private readonly ArcadeLightgunState[] _arcadeLightguns = new ArcadeLightgunState[PbusEncoder.MaxControllers];
    private readonly OrbatakTrackballState[] _trackballs = new OrbatakTrackballState[PbusEncoder.MaxControllers];
    private readonly OrbatakButtonsState[] _trackballButtons = new OrbatakButtonsState[PbusEncoder.MaxControllers];
    private int _portCount = 1;

    /// <summary>Gets the number of active ports, 1-6.</summary>
    public int PortCount => _portCount;

    /// <summary>Sets the number of active ports.</summary>
    /// <param name="count">Port count, 1-6.</param>
    public void SetPortCount(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, PbusEncoder.MaxControllers);
        _portCount = count;
    }

    /// <summary>Gets the joypad states for all active ports.</summary>
    public ReadOnlySpan<ControllerState> Joypads => _joypads.AsSpan(0, _portCount);

    /// <summary>Gets the device type on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <returns>The configured device.</returns>
    public InputDeviceType GetDevice(int port)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(port, PbusEncoder.MaxControllers);
        return _devices[port];
    }

    /// <summary>Sets the device type on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <param name="device">Device type.</param>
    public void SetDevice(int port, InputDeviceType device)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(port, PbusEncoder.MaxControllers);
        _devices[port] = device;
        _portCount = Math.Max(_portCount, port + 1);
    }

    /// <summary>Gets a value indicating whether any port uses a non-joypad device.</summary>
    public bool HasPeripheral => Array.Exists(
        _devices,
        static device => device != InputDeviceType.Joypad);

    /// <summary>Gets the joypad state on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <returns>The joypad state.</returns>
    public ControllerState GetJoypad(int port)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(port, PbusEncoder.MaxControllers);
        return _joypads[port];
    }

    /// <summary>Sets the joypad state on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <param name="state">Joypad state.</param>
    public void SetJoypad(int port, ControllerState state)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(port, PbusEncoder.MaxControllers);
        _joypads[port] = state;
    }

    /// <summary>Gets the flightstick state on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <returns>The flightstick state.</returns>
    public FlightstickState GetFlightstick(int port) => _flightsticks[ValidatePort(port)];

    /// <summary>Sets the flightstick state on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <param name="state">Flightstick state.</param>
    public void SetFlightstick(int port, FlightstickState state) => _flightsticks[ValidatePort(port)] = state;

    /// <summary>Gets the mouse state on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <returns>The mouse state.</returns>
    public MouseState GetMouse(int port) => _mice[ValidatePort(port)];

    /// <summary>Sets the mouse state on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <param name="state">Mouse state.</param>
    public void SetMouse(int port, MouseState state) => _mice[ValidatePort(port)] = state;

    /// <summary>Gets the lightgun state on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <returns>The lightgun state.</returns>
    public LightgunState GetLightgun(int port) => _lightguns[ValidatePort(port)];

    /// <summary>Sets the lightgun state on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <param name="state">Lightgun state.</param>
    public void SetLightgun(int port, LightgunState state) => _lightguns[ValidatePort(port)] = state;

    /// <summary>Gets the arcade lightgun state on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <returns>The arcade lightgun state.</returns>
    public ArcadeLightgunState GetArcadeLightgun(int port) => _arcadeLightguns[ValidatePort(port)];

    /// <summary>Sets the arcade lightgun state on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <param name="state">Arcade lightgun state.</param>
    public void SetArcadeLightgun(int port, ArcadeLightgunState state) => _arcadeLightguns[ValidatePort(port)] = state;

    /// <summary>Gets the Orbatak trackball state on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <returns>The trackball state.</returns>
    public OrbatakTrackballState GetTrackball(int port) => _trackballs[ValidatePort(port)];

    /// <summary>Sets the Orbatak trackball state on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <param name="state">Trackball state.</param>
    public void SetTrackball(int port, OrbatakTrackballState state) => _trackballs[ValidatePort(port)] = state;

    /// <summary>Gets the Orbatak cabinet buttons on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <returns>The cabinet button state.</returns>
    public OrbatakButtonsState GetTrackballButtons(int port) => _trackballButtons[ValidatePort(port)];

    /// <summary>Sets the Orbatak cabinet buttons on a port.</summary>
    /// <param name="port">Zero-based port index.</param>
    /// <param name="state">Cabinet button state.</param>
    public void SetTrackballButtons(int port, OrbatakButtonsState state) => _trackballButtons[ValidatePort(port)] = state;

    /// <summary>Releases every input state without changing the device selection.</summary>
    public void Reset()
    {
        _joypads.AsSpan().Clear();
        _flightsticks.AsSpan().Clear();
        _mice.AsSpan().Clear();
        _lightguns.AsSpan().Clear();
        _arcadeLightguns.AsSpan().Clear();
        _trackballs.AsSpan().Clear();
        _trackballButtons.AsSpan().Clear();
    }

    /// <summary>Clears one-frame relative motion after a packet has been sampled.</summary>
    public void ClearTransientMotion()
    {
        for (var i = 0; i < _mice.Length; i++)
        {
            _mice[i] = _mice[i] with { DeltaX = 0, DeltaY = 0 };
        }

        for (var i = 0; i < _trackballs.Length; i++)
        {
            _trackballs[i] = _trackballs[i] with { DeltaX = 0, DeltaY = 0 };
        }
    }

    /// <summary>Encodes the port chain into a PBUS packet.</summary>
    /// <param name="destination">Destination span of at least <see cref="PbusChainEncoder.MaxPacketSize"/> bytes.</param>
    /// <returns>The encoded length, including padding.</returns>
    public int Encode(Span<byte> destination)
    {
        if (destination.Length < PbusChainEncoder.MaxPacketSize)
        {
            throw new ArgumentException(
                $"The PBUS chain packet needs at least {PbusChainEncoder.MaxPacketSize} bytes.",
                nameof(destination));
        }

        var length = 0;
        for (var port = 0; port < _portCount; port++)
        {
            switch (_devices[port])
            {
                case InputDeviceType.Joypad:
                    PbusChainEncoder.WriteJoypad(_joypads[port], destination, ref length);
                    break;
                case InputDeviceType.Flightstick:
                    PbusChainEncoder.WriteFlightstick(_flightsticks[port], destination, ref length);
                    break;
                case InputDeviceType.Mouse:
                    PbusChainEncoder.WriteMouse(_mice[port].Clamped(), destination, ref length);
                    break;
                case InputDeviceType.Lightgun:
                    PbusChainEncoder.WriteLightgun(_lightguns[port].Normalized(), destination, ref length);
                    break;
                case InputDeviceType.ArcadeLightgun:
                    PbusChainEncoder.WriteArcadeLightgun(_arcadeLightguns[port].Normalized(), destination, ref length);
                    break;
                case InputDeviceType.OrbatakTrackball:
                    PbusChainEncoder.WriteOrbatakTrackball(_trackballs[port].Clamped(), destination, ref length);
                    PbusChainEncoder.WriteOrbatakButtons(_trackballButtons[port], destination, ref length);
                    break;
                case InputDeviceType.None:
                    break;
            }
        }

        return PbusChainEncoder.Pad(destination, length);
    }

    private static int ValidatePort(int port)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(port, PbusEncoder.MaxControllers);
        return port;
    }
}
