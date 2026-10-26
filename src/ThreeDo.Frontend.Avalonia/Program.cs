using System;
using System.IO;
using Avalonia;

namespace ThreeDo.Frontend.Avalonia;

sealed class Program
{
    /// <summary>
    /// Gets the disc path passed on the command line, if any. File associations use
    /// this to open a disc when the desktop launches the application with a path.
    /// </summary>
    public static string? StartupDiscPath { get; private set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        StartupDiscPath = FindDiscPath(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static string? FindDiscPath(string[] args)
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

            if (!argument.StartsWith('-') && File.Exists(argument))
            {
                return Path.GetFullPath(argument);
            }
        }

        return null;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                // Prefer desktop WGL so the GLSL 330 presenter is used. ANGLE remains a
                // fallback and will select the GLES shader path once it is available.
                RenderingMode = [Win32RenderingMode.Wgl, Win32RenderingMode.AngleEgl, Win32RenderingMode.Software],
            })
            .WithInterFont()
            .LogToTrace();
}
