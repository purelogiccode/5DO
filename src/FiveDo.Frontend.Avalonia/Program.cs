using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using FiveDo.Core;
using FiveDo.Diagnostics.Logging;
using FiveDo.Diagnostics.Statistics;

namespace FiveDo.Frontend.Avalonia;

internal static class Program
{
    /// <summary>
    /// Gets the disc path passed on the command line, if any. File associations use
    /// this to open a disc when the desktop launches the application with a path.
    /// </summary>
    public static string? StartupDiscPath { get; private set; }

    /// <summary>
    /// Gets the optional `--bios` path used for this launch. External frontends can
    /// provide the BIOS without changing the saved desktop configuration.
    /// </summary>
    public static string? StartupBiosPath { get; private set; }

    /// <summary>
    /// Gets a value indicating whether `--fullscreen` (or `-f`) requested a fullscreen
    /// launch. Used by launchers and emulator frontends.
    /// </summary>
    public static bool StartFullscreen { get; private set; }

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
    /// Gets the optional Vulkan probe request. When set, the process runs the offscreen
    /// pipeline (or the native Win32 swapchain for <c>--vk-window-probe</c>) and exits
    /// without initializing Avalonia.
    /// </summary>
    public static VkProbeOptions? VkProbe { get; private set; }

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
        if (Array.Exists(args, arg => string.Equals(arg, "--portable", StringComparison.OrdinalIgnoreCase)))
        {
            Environment.SetEnvironmentVariable("FIVEDO_PORTABLE", "1");
            args = Array.FindAll(args, arg => !string.Equals(arg, "--portable", StringComparison.OrdinalIgnoreCase));
        }

        try
        {
            AppLogging.Initialize(new LoggingOptions
            {
                ApplicationMode = "Desktop",
                WriteToConsole = true,
                WriteToFile = true,
                // Automated smoke/probe runs must never create bug reports; they run
                // without a user session and their environment warnings are expected.
                EnableBugReports = !IsAutomatedRun(args),
            });
        }
        catch (Exception exception)
        {
            // Logging must never prevent the application from starting.
            Console.Error.WriteLine($"Logging initialization failed: {exception.Message}");
        }

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                Serilog.Log.Fatal(exception, "Unhandled application exception.");
            }
            else
            {
                Serilog.Log.Fatal(
                    "Unhandled application exception: {ExceptionObject}.",
                    eventArgs.ExceptionObject);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            Serilog.Log.Error(eventArgs.Exception, "Unobserved task exception.");
            eventArgs.SetObserved();
        };

        try
        {
            Serilog.Log.Information(
                "Desktop frontend starting ({Version}); {ArgumentCount} argument(s).",
                FiveDoVersion.Version,
                args.Length);

            if (HasFlag(args, "--version"))
            {
                Console.WriteLine(FiveDoVersion.Version);
                return;
            }

            if (HasFlag(args, "--help") || HasFlag(args, "-h") || HasFlag(args, "help"))
            {
                PrintUsage();
                return;
            }

            var backendText = FindOptionValue(args, "--gl-backend")
                ?? FindOptionValue(args, "--gl-probe-backend");
            if (!GlBackendOption.TryParse(backendText, out GlBackend backend))
            {
                Serilog.Log.Warning(
                    "Unrecognized --gl-backend value {Backend}; use auto, wgl, angle, software, or vulkan.",
                    backendText);
                Environment.ExitCode = 1;
                return;
            }

            RequestedGlBackend = backend;

            if (HasFlag(args, "--vk-window-probe"))
            {
                if (!VkProbeOptions.TryParseWindow(args, out VkProbeOptions? windowProbe, out var windowProbeError))
                {
                    Serilog.Log.Warning("Invalid Vulkan window probe options: {Error}.", windowProbeError);
                    Environment.ExitCode = 1;
                    return;
                }

                VkProbe = windowProbe;
                Environment.ExitCode = VkProbeRunner.Run(windowProbe!);
                return;
            }

            if (HasFlag(args, "--vk-probe"))
            {
                if (!VkProbeOptions.TryParse(args, out VkProbeOptions? vkProbe, out var vkProbeError))
                {
                    Serilog.Log.Warning("Invalid Vulkan probe options: {Error}.", vkProbeError);
                    Environment.ExitCode = 1;
                    return;
                }

                VkProbe = vkProbe;
                Environment.ExitCode = VkProbeRunner.Run(vkProbe!);
                return;
            }

            SmokeTest = HasFlag(args, "--smoke-test");
            SmokeOutputPath = FindOptionValue(args, "--smoke-output");
            if (HasFlag(args, "--gl-probe"))
            {
                if (!GlProbeOptions.TryParse(args, out GlProbeOptions? probe, out var probeError))
                {
                    Serilog.Log.Warning("Invalid GPU probe options: {Error}.", probeError);
                    Environment.ExitCode = 1;
                    return;
                }

                GlProbe = probe;
            }

            StartupDiscPath = FindDiscPath(args);
            StartupBiosPath = FindBiosPath(args);
            StartFullscreen = HasFlag(args, "--fullscreen") || HasFlag(args, "-f");
            var biosOption = FindOptionValue(args, "--bios");
            if (biosOption is not null && StartupBiosPath is null)
            {
                Console.Error.WriteLine($"BIOS image not found: {biosOption}");
                Environment.ExitCode = 1;
                return;
            }

            if (GlProbe is null && VkProbe is null && !SmokeTest)
            {
                ApplicationStatsClient.TrackLaunch();
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            Serilog.Log.Fatal(exception, "Desktop frontend terminated unexpectedly.");
            Environment.ExitCode = 1;
        }
        finally
        {
            ApplicationStatsClient.WaitForPending(TimeSpan.FromSeconds(2));
            AppLogging.Shutdown();
        }
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
                $"FRONTEND SMOKE OK {FiveDoVersion.Version}{Environment.NewLine}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A failed marker write must not turn a successful window launch into a
            // crash; the process exit code remains the authoritative smoke result.
            Serilog.Log.Error(exception, "Smoke marker write failed.");
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("5DO desktop frontend");
        Console.WriteLine("Usage: FiveDo.Frontend.Avalonia [disc-path] [options]");
        Console.WriteLine();
        Console.WriteLine("  <disc-path>             Open and auto-start a disc image (ISO, BIN, CUE, CHD).");
        Console.WriteLine("  --disc <path>           Same as the positional disc path.");
        Console.WriteLine("  --bios <path>           Use this BIOS image for this launch.");
        Console.WriteLine("  -f, --fullscreen        Start in fullscreen.");
        Console.WriteLine("  --portable              Store settings, NVRAM, cheats, and logs beside the executable.");
        Console.WriteLine("  --gl-backend <backend>  auto (default), wgl, angle, software, or vulkan.");
        Console.WriteLine("  --version               Print the version and exit.");
        Console.WriteLine("  -h, --help              Show this help and exit.");
        Console.WriteLine();
        Console.WriteLine("Developer GPU probes (see docs/Performance.md):");
        Console.WriteLine("  --gl-probe <json> [--gl-probe-size WxH] [--gl-probe-frames N] ...");
        Console.WriteLine("  --vk-probe <json> | --vk-window-probe <json> ...");
        Console.WriteLine("  --smoke-test [--smoke-output <path>]");
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

    /// <summary>
    /// Gets a value indicating whether the process is an automated smoke or probe run.
    /// These runs disable bug reporting and launch statistics.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns><see langword="true"/> for smoke, OpenGL probe, or Vulkan probe runs.</returns>
    internal static bool IsAutomatedRun(string[] args)
    {
        return HasFlag(args, "--smoke-test")
               || HasFlag(args, "--gl-probe")
               || HasFlag(args, "--vk-probe")
               || HasFlag(args, "--vk-window-probe");
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

    /// <summary>
    /// Finds an existing `--bios` path and returns its absolute path.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The absolute BIOS path, or <see langword="null"/> when absent or missing.</returns>
    internal static string? FindBiosPath(string[] args)
    {
        var path = FindOptionValue(args, "--bios");
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path)
            ? Path.GetFullPath(path)
            : null;
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
            || string.Equals(previous, "--bios", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--smoke-output", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-backend", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe-backend", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe-capture", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe-size", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe-frames", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe-warmup", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe-filter", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--gl-probe-timeout", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--vk-probe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--vk-window-probe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--vk-probe-capture", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--vk-probe-size", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--vk-probe-frames", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--vk-probe-warmup", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--vk-probe-filter", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--vk-probe-timeout", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--vk-window-probe-capture", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--vk-window-probe-size", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--vk-window-probe-frames", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--vk-window-probe-warmup", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--vk-window-probe-filter", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "--vk-window-probe-timeout", StringComparison.OrdinalIgnoreCase);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
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
}
