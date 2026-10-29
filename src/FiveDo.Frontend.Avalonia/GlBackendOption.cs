namespace FiveDo.Frontend.Avalonia;

/// <summary>
/// Parses the <c>--gl-backend</c> command-line option.
/// </summary>
internal static class GlBackendOption
{
    /// <summary>Parses a backend name. A missing value means <see cref="GlBackend.Auto"/>.</summary>
    /// <param name="text">Option value, or <see langword="null"/> when not supplied.</param>
    /// <param name="backend">Parsed backend.</param>
    /// <returns><see langword="true"/> when the value is recognized.</returns>
    internal static bool TryParse(string? text, out GlBackend backend)
    {
        backend = GlBackend.Auto;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        switch (text.Trim().ToUpperInvariant())
        {
            case "AUTO":
            case "DEFAULT":
                backend = GlBackend.Auto;
                return true;
            case "WGL":
            case "DESKTOP":
            case "GL":
                backend = GlBackend.Wgl;
                return true;
            case "ANGLE":
            case "ANGLEEGL":
            case "EGL":
            case "GLES":
                backend = GlBackend.Angle;
                return true;
            case "SOFTWARE":
            case "CPU":
                backend = GlBackend.Software;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Gets the Avalonia rendering-mode preference list for a backend.</summary>
    /// <param name="backend">Requested backend.</param>
    /// <returns>The ordered rendering modes.</returns>
    public static global::Avalonia.Win32RenderingMode[] ToRenderingModes(GlBackend backend)
        => backend switch
        {
            GlBackend.Wgl => [global::Avalonia.Win32RenderingMode.Wgl],
            GlBackend.Angle => [global::Avalonia.Win32RenderingMode.AngleEgl],
            GlBackend.Software => [global::Avalonia.Win32RenderingMode.Software],
            _ => [global::Avalonia.Win32RenderingMode.Wgl, global::Avalonia.Win32RenderingMode.AngleEgl, global::Avalonia.Win32RenderingMode.Software],
        };
}
