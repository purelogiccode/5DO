using System;
using System.Runtime.InteropServices;

namespace FiveDo.Video.OpenGL;


/// <summary>
/// Controls the platform swap interval used for vsync presentation.
/// </summary>
/// <remarks>
/// <para>
/// The renderer owns no windowing system. The host supplies the context's
/// <c>GetProcAddress</c> delegate, and this helper resolves the first available
/// platform extension: WGL on Windows, GLX on Linux, or EGL for ANGLE/GLES
/// contexts. The helper is deliberately small so the selection logic can be
/// tested without a live GL context.
/// </para>
/// <para>
/// A missing extension is not an error. The renderer reports
/// <see cref="RendererCapabilities.SupportsVsync"/> as false and continues with
/// the platform's default presentation behaviour.
/// </para>
/// </remarks>
public interface ISwapIntervalBackend
{
    /// <summary>Gets the platform extension name.</summary>
    string Name { get; }

    /// <summary>Applies a swap interval.</summary>
    /// <param name="interval">Zero disables vsync; one enables it.</param>
    /// <returns><see langword="true"/> when the platform accepted the request.</returns>
    bool Apply(int interval);
}
