using System;
using System.Threading;
using ThreeDo.Input;

namespace ThreeDo.Frontend.Avalonia.Input;

/// <summary>
/// A raw platform gamepad source. Implementations translate native devices into
/// <see cref="GamepadState"/> samples and are polled once per emulated frame.
/// </summary>
internal interface IRawGamepadSource : IDisposable
{
    /// <summary>Gets the number of connected controllers.</summary>
    int Count { get; }

    /// <summary>Polls the platform for new input.</summary>
    void Poll();

    /// <summary>Reads a controller sample by compact index.</summary>
    /// <param name="index">Zero-based connected-controller index.</param>
    /// <returns>The current sample, or <see cref="GamepadState.Empty"/>.</returns>
    GamepadState Read(int index);
}

/// <summary>
/// The platform gamepad backend used by the desktop shell. It owns the native source and
/// applies the current six-player input profile.
/// </summary>
public sealed class PlatformGamepadBackend : IGamepadBackend, IDisposable
{
    private readonly IRawGamepadSource _source;
    private readonly ControllerState[] _states = new ControllerState[PbusEncoder.MaxControllers];
    private InputProfile _profile;
    private int _count;
    private bool _disposed;

    private PlatformGamepadBackend(IRawGamepadSource source, InputProfile profile)
    {
        _source = source;
        _profile = profile;
    }

    /// <summary>Creates the backend for the current platform.</summary>
    /// <param name="profile">Initial input profile.</param>
    /// <returns>The backend, or <see langword="null"/> when platform initialization failed.</returns>
    public static PlatformGamepadBackend? TryCreate(InputProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        try
        {
            return new PlatformGamepadBackend(PlatformRawGamepadSource.Create(), profile);
        }
        catch (Exception exception) when (exception is DllNotFoundException
            or EntryPointNotFoundException
            or InvalidOperationException
            or PlatformNotSupportedException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public int ControllerCount => _count;

    /// <summary>Gets the number of connected physical controllers.</summary>
    public int ConnectedControllers => _source.Count;

    /// <summary>Gets the platform source name for diagnostics.</summary>
    public string SourceName => _source.GetType().Name;

    /// <summary>Replaces the profile used to map physical inputs.</summary>
    /// <param name="profile">New profile. The backend does not take ownership.</param>
    public void UpdateProfile(InputProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Volatile.Write(ref _profile, profile);
    }

    /// <inheritdoc />
    public void Poll()
    {
        _source.Poll();
        var profile = Volatile.Read(ref _profile);
        var available = Math.Min(_source.Count, _states.Length);
        for (var player = 0; player < _states.Length; player++)
        {
            if (player >= available || player >= profile.Players.Length)
            {
                _states[player] = ControllerState.Empty;
                continue;
            }

            _states[player] = GamepadMapper.Map(profile.Players[player], _source.Read(player));
        }

        _count = available;
    }

    /// <inheritdoc />
    public ControllerState GetState(int player)
    {
        return player >= 0 && player < _states.Length ? _states[player] : ControllerState.Empty;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _source.Dispose();
    }
}

/// <summary>Selects the native gamepad source for the current operating system.</summary>
internal static class PlatformRawGamepadSource
{
    internal static IRawGamepadSource Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return XInputGamepadSource.TryCreate() ?? (IRawGamepadSource)NullRawGamepadSource.Instance;
        }

        if (OperatingSystem.IsLinux())
        {
            return EvdevGamepadSource.TryCreate() ?? (IRawGamepadSource)NullRawGamepadSource.Instance;
        }

        return NullRawGamepadSource.Instance;
    }
}

/// <summary>A source with no connected devices.</summary>
internal sealed class NullRawGamepadSource : IRawGamepadSource
{
    internal static NullRawGamepadSource Instance { get; } = new();

    public int Count => 0;

    public void Poll()
    {
    }

    public GamepadState Read(int index) => GamepadState.Empty;

    public void Dispose()
    {
    }
}
