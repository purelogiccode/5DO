using System;
using System.Globalization;
using System.Text.RegularExpressions;
using ThreeDo.Video;

namespace ThreeDo.Video.OpenGL;

/// <summary>
/// Parses OpenGL version strings and reports honest renderer capabilities.
/// </summary>
/// <remarks>
/// This helper is deliberately independent of a live GL context so the version and
/// feature rules can be unit tested on headless CI. The renderer still queries
/// <c>GL_MAX_TEXTURE_SIZE</c> from the current context.
/// </remarks>
public static class OpenGlCapabilities
{
    private static readonly Regex VersionPattern = new(
        @"(\d+)\.(\d+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Detects capabilities from a GL version string and maximum texture size.</summary>
    /// <param name="version">The string returned by <c>GL_VERSION</c>, or null/empty.</param>
    /// <param name="maxTextureSize">The value returned by <c>GL_MAX_TEXTURE_SIZE</c>.</param>
    /// <param name="supportsVsync">
    /// Whether the host resolved a platform swap-interval extension for this context.
    /// </param>
    /// <returns>Capabilities for the reported API and version.</returns>
    public static RendererCapabilities Detect(string? version, int maxTextureSize, bool supportsVsync = false)
    {
        var text = version ?? string.Empty;
        var isOpenGlEs = text.Contains("OpenGL ES", StringComparison.OrdinalIgnoreCase);
        var api = isOpenGlEs ? RendererApi.OpenGlEs : RendererApi.OpenGl;

        var major = 0;
        var minor = 0;
        var match = VersionPattern.Match(text);
        if (match.Success)
        {
            _ = int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out major);
            _ = int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out minor);
        }

        // R16UI, usampler2D, and texelFetch are core in desktop GL 3.0 and GLES 3.0.
        var supportsIntegerTextures = major >= 3;

        return new RendererCapabilities(
            isOpenGlEs ? "OpenGL ES" : OpenGlRendererInfo.BackendName,
            major,
            minor,
            maxTextureSize,
            supportsIntegerTextures,
            supportsVsync,
            api);
    }
}
