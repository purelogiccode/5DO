using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace FiveDo.Host;

/// <summary>
/// Platform hook that keeps the display and the system awake while a game is running.
/// </summary>
public interface IDisplaySleepBackend
{
    /// <summary>Gets a value indicating whether the platform can inhibit display sleep.</summary>
    bool IsSupported { get; }

    /// <summary>Requests that the display and system stay awake until <see cref="Release"/>.</summary>
    /// <returns><see langword="true"/> when the request was accepted.</returns>
    bool Acquire();

    /// <summary>Releases a previous <see cref="Acquire"/> request.</summary>
    void Release();
}

/// <summary>Creates the display-sleep backend for the current platform.</summary>
/// <remarks>
/// Windows is fully supported through <c>SetThreadExecutionState</c>. Linux and macOS
/// return <see cref="NullDisplaySleepBackend"/> as a documented best-effort limitation:
/// their compositors may still blank the display during play.
/// </remarks>
public static class DisplaySleepBackend
{
    /// <summary>Creates the backend for the current platform.</summary>
    /// <returns>A supported backend, or <see cref="NullDisplaySleepBackend.Instance"/>.</returns>
    public static IDisplaySleepBackend Create()
    {
        return OperatingSystem.IsWindows()
            ? WindowsDisplaySleepBackend.Instance
            : NullDisplaySleepBackend.Instance;
    }
}

/// <summary>A backend for platforms without an implemented display-sleep request.</summary>
public sealed class NullDisplaySleepBackend : IDisplaySleepBackend
{
    /// <summary>Gets the shared null backend.</summary>
    public static NullDisplaySleepBackend Instance { get; } = new();

    private NullDisplaySleepBackend()
    {
    }

    /// <inheritdoc />
    public bool IsSupported => false;

    /// <inheritdoc />
    public bool Acquire()
    {
        return false;
    }

    /// <inheritdoc />
    public void Release()
    {
    }
}

/// <summary>Windows implementation using <c>SetThreadExecutionState</c>.</summary>
internal sealed class WindowsDisplaySleepBackend : IDisplaySleepBackend
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsDisplayRequired = 0x00000002;
    private const uint EsSystemRequired = 0x00000001;

    private static readonly WindowsDisplaySleepBackend Shared = new();

    private WindowsDisplaySleepBackend()
    {
    }

    internal static WindowsDisplaySleepBackend Instance => Shared;

    /// <inheritdoc />
    public bool IsSupported => OperatingSystem.IsWindows();

    [SuppressMessage(
        "Interoperability",
        "SYSLIB1054:Use 'LibraryImportAttribute' instead of 'DllImportAttribute'",
        Justification = "The test and desktop projects do not enable unsafe code for the source generator.")]
    [DllImport("kernel32.dll", SetLastError = false)]
    private static extern uint SetThreadExecutionState(uint flags);

    /// <inheritdoc />
    public bool Acquire()
    {
        return OperatingSystem.IsWindows()
            && SetThreadExecutionState(EsContinuous | EsDisplayRequired | EsSystemRequired) != 0;
    }

    /// <inheritdoc />
    public void Release()
    {
        if (OperatingSystem.IsWindows())
        {
            _ = SetThreadExecutionState(EsContinuous);
        }
    }
}

/// <summary>
/// Applies run/pause transitions to a display-sleep backend with no toolkit dependency.
/// </summary>
/// <remarks>
/// The desktop shell calls <see cref="SetRunning"/> whenever the emulation play state
/// changes. A paused or stopped machine releases the request; resuming reacquires it.
/// </remarks>
public sealed class DisplaySleepPolicy
{
    private readonly IDisplaySleepBackend _backend;
    private bool _running;
    private bool _paused;

    /// <summary>Initializes a new instance of the <see cref="DisplaySleepPolicy"/> class.</summary>
    /// <param name="backend">Platform backend.</param>
    public DisplaySleepPolicy(IDisplaySleepBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        _backend = backend;
    }

    /// <summary>Gets a value indicating whether the platform supports display-sleep inhibition.</summary>
    public bool IsSupported => _backend.IsSupported;

    /// <summary>Gets a value indicating whether the display-sleep request is currently held.</summary>
    public bool IsInhibited { get; private set; }

    /// <summary>Records whether a game is running.</summary>
    /// <param name="running">Whether frames are executing.</param>
    public void SetRunning(bool running)
    {
        if (_running == running)
        {
            return;
        }

        _running = running;
        Update();
    }

    /// <summary>Records whether the running game is paused.</summary>
    /// <param name="paused">Whether emulation is paused.</param>
    public void SetPaused(bool paused)
    {
        if (_paused == paused)
        {
            return;
        }

        _paused = paused;
        Update();
    }

    /// <summary>Releases the request and clears the run/pause state.</summary>
    public void Release()
    {
        _running = false;
        _paused = false;
        if (IsInhibited)
        {
            _backend.Release();
            IsInhibited = false;
        }
    }

    private void Update()
    {
        var shouldInhibit = _running && !_paused;
        if (shouldInhibit)
        {
            if (!IsInhibited)
            {
                IsInhibited = _backend.Acquire();
            }

            return;
        }

        if (IsInhibited)
        {
            _backend.Release();
            IsInhibited = false;
        }
    }
}
