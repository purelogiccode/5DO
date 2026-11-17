using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
/// Windows uses <c>SetThreadExecutionState</c> and Linux holds a freedesktop
/// screen-saver inhibitor for the duration of play. macOS returns
/// <see cref="NullDisplaySleepBackend"/> as a documented best-effort limitation.
/// </remarks>
public static class DisplaySleepBackend
{
    /// <summary>Creates the backend for the current platform.</summary>
    /// <returns>A supported backend, or <see cref="NullDisplaySleepBackend.Instance"/>.</returns>
    public static IDisplaySleepBackend Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsDisplaySleepBackend.Instance;
        }

        if (OperatingSystem.IsLinux())
        {
            return LinuxDisplaySleepBackend.Instance;
        }

        return NullDisplaySleepBackend.Instance;
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
/// Linux implementation using the freedesktop screen-saver D-Bus API.
/// </summary>
/// <remarks>
/// <c>org.freedesktop.ScreenSaver.Inhibit</c> returns a cookie that suppresses screen
/// blanking, the lock screen, and idle suspend while it is held; <c>UnInhibit</c>
/// releases it. The inhibitor is tied to the D-Bus connection, so the call runs in a
/// small persistent Python helper that holds the session-bus connection for as long as
/// the game is running. The helper exits (and the lock is released) when the parent
/// closes its stdin or the process ends. Hosts without <c>python3-dbus</c> fall back to
/// the null backend.
/// </remarks>
internal sealed class LinuxDisplaySleepBackend : IDisplaySleepBackend
{
    private const string Python = "python3";
    private const string Script =
        "import sys, dbus\n" +
        "bus = dbus.SessionBus()\n" +
        "proxy = bus.get_object('org.freedesktop.ScreenSaver', '/org/freedesktop/ScreenSaver')\n" +
        "cookie = int(proxy.Inhibit('5DO', 'Game running', dbus_interface='org.freedesktop.ScreenSaver'))\n" +
        "print(cookie, flush=True)\n" +
        "sys.stdin.readline()\n" +
        "try:\n" +
        "    proxy.UnInhibit(cookie, dbus_interface='org.freedesktop.ScreenSaver')\n" +
        "except Exception:\n" +
        "    pass\n";

    private static readonly Lazy<bool> Available =
        new(() => OperatingSystem.IsLinux() && FindOnPath(Python));

    private Process? _process;

    private LinuxDisplaySleepBackend()
    {
    }

    internal static LinuxDisplaySleepBackend Instance { get; } = new();

    /// <summary>Gets a value indicating whether <c>python3</c> is installed.</summary>
    internal static bool IsAvailable => Available.Value;

    /// <inheritdoc />
    public bool IsSupported => IsAvailable;

    /// <inheritdoc />
    public bool Acquire()
    {
        if (_process is { HasExited: false })
        {
            return true;
        }

        Release();
        if (!IsSupported)
        {
            return false;
        }

        Process? process = null;
        try
        {
            ProcessStartInfo start = new()
            {
                FileName = Python,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(Script);
            process = Process.Start(start);
            if (process is null)
            {
                return false;
            }

            Task<string?> reply = process.StandardOutput.ReadLineAsync();
            if (!reply.Wait(TimeSpan.FromSeconds(5))
                || reply.Result is null
                || !uint.TryParse(reply.Result.Trim(), out uint cookie)
                || cookie == 0)
            {
                process.Kill(entireProcessTree: true);
                process.Dispose();
                return false;
            }

            _process = process;
            Serilog.Log.Information(
                "Display-sleep inhibitor acquired (cookie {Cookie}).",
                cookie);
            return true;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
            or InvalidOperationException
            or IOException)
        {
            Debug.WriteLine($"Screen-saver inhibit failed: {exception.Message}");
            process?.Dispose();
            return false;
        }
    }

    /// <inheritdoc />
    public void Release()
    {
        Process? process = _process;
        _process = null;
        if (process is null)
        {
            return;
        }

        Serilog.Log.Information("Display-sleep inhibitor released.");
        try
        {
            if (!process.HasExited)
            {
                process.StandardInput.WriteLine("release");
                process.StandardInput.Flush();
                process.StandardInput.Close();
                if (!process.WaitForExit(2000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or NotSupportedException
            or IOException)
        {
            Debug.WriteLine($"Screen-saver release failed: {exception.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }

    private static bool FindOnPath(string name)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        foreach (string directory in path.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(directory, name)))
                {
                    return true;
                }
            }
            catch (Exception exception) when (exception is ArgumentException
                or IOException
                or UnauthorizedAccessException)
            {
                Debug.WriteLine($"PATH entry rejected: {exception.Message}");
            }
        }

        return false;
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
