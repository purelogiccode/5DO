using System;
using System.IO;
using Avalonia;
using ThreeDo.Core;

namespace ThreeDo.Frontend.Avalonia;

sealed class Program
{
    /// <summary>
    /// Gets the disc path passed on the command line, if any. File associations use
    /// this to open a disc when the desktop launches the application with a path.
    /// </summary>
    public static string? StartupDiscPath { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the process was started by the packaged
    /// frontend smoke test. The shell skips the welcome/config load and exits after
    /// the first frame so CI can validate a trimmed publish without a user session.
    /// </summary>
    public static bool SmokeTest { get; private set; }

    /// <summary>
    /// Gets the optional path that receives the smoke-test result marker.
    /// </summary>
    public static string? SmokeOutputPath { get; private set; }

    /// <summary>
    /// Gets the GPU presentation probe request, or <see langword="null"/> during normal
    /// startup. The probe opens the real OpenGL surface, presents synthetic frames, and
    /// writes timing JSON without loading a BIOS or disc.
    /// </summary>
    public static GlProbeOptions? GlProbe { get; private set; }

    /// <summary>
    /// Gets the renderer backend requested with <c>--gl-backend</c> (or the probe alias
    /// <c>--gl-probe-backend</c>). The default is <see cref="GlBackend.Auto"/>.
    /// </summary>
    public static GlBackend RequestedGlBackend { get; private set; } = GlBackend.Auto;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (HasFlag(args, "--version"))
        {
            Console.WriteLine(ThreeDoVersion.Version);
            return;
        }

        var backendText = FindOptionValue(args, "--gl-backend")
            ?? FindOptionValue(args, "--gl-probe-backend");
        if (!GlBackendOption.TryParse(backendText, out var backend))
        {
            Console.Error.WriteLine(
                $"--gl-backend does not recognize '{backendText}'. Use auto, wgl, angle, or software.");
            Environment.ExitCode = 1;
            return;
        }

        RequestedGlBackend = backend;

        SmokeTest = HasFlag(args, "--smoke-test");
        SmokeOutputPath = FindOptionValue(args, "--smoke-output");
        if (HasFlag(args, "--gl-probe"))
        {
            if (!GlProbeOptions.TryParse(args, out var probe, out var probeError))
            {
                Console.Error.WriteLine(probeError);
                Environment.ExitCode = 1;
                return;
            }

            GlProbe = probe;
        }

        StartupDiscPath = FindDiscPath(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Writes the smoke-test marker, if one was requested. Called from the UI thread
    /// once the main window has opened.
    /// </summary>
    internal static void CompleteSmokeTest()
    {
        if (!SmokeTest || string.IsNullOrWhiteSpace(SmokeOutputPath))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(SmokeOutputPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                SmokeOutputPath,
                $"FRONTEND SMOKE OK {ThreeDoVersion.Version}{Environment.NewLine}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A failed marker write must not turn a successful window launch into a
            // crash; the process exit code remains the authoritative smoke result.
            Console.Error.WriteLine($"Smoke marker failed: {exception.Message}");
        }
    }

    internal static bool HasFlag(string[] args, string flag)
    {
        foreach (var argument in args)
        {
            if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static string? FindOptionValue(string[] args, string option)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], option, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(args[i + 1]))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    internal static string? FindDiscPath(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];
            if (string.Equals(argument, "--disc", StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length
                && File.Exists(args[i + 1]))
            {
                return Path.GetFullPath(args[i + 1]);
            }

            if (argument.StartsWith('-') || IsOptionValue(args, i))
            {
                continue;
            }

            if (File.Exists(argument))
            {
                return Path.GetFullPath(argument);
            }
        }

        return null;
    }

    private static bool IsOptionValue(string[] args, int index)
    {
        if (index == 0)
        {
            return false;
        }

        var previous = args[index - 1];
        return string.Equals(previous, "--disc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--smoke-output", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-backend", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe-backend", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe-capture", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe-size", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe-frames", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe-warmup", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe-filter", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe-timeout", StringComparison.OrdinalIgnoreCase);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                // Desktop WGL is the primary path; ANGLE is the GLES fallback. The
                // --gl-backend option forces a single backend for validation.
                RenderingMode = GlBackendOption.ToRenderingModes(RequestedGlBackend),
            })
            .WithInterFont()
            .LogToTrace();
}
