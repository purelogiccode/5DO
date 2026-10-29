using System;
using System.Runtime.InteropServices;

namespace FiveDo.Video.OpenGL;

/// <summary>
/// Resolves a platform swap-interval extension from a GL/EGL proc-address loader.
/// </summary>
public static class SwapInterval
{
    /// <summary>
    /// Creates the best available swap-interval backend.
    /// </summary>
    /// <param name="getProcAddress">Context proc-address loader, typically <c>GlInterface.GetProcAddress</c>.</param>
    /// <returns>The selected backend, or <see langword="null"/> when the platform exposes no supported extension.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="getProcAddress"/> is null.</exception>
    public static ISwapIntervalBackend? TryCreate(Func<string, nint> getProcAddress)
    {
        ArgumentNullException.ThrowIfNull(getProcAddress);

        // WGL_EXT_swap_control: BOOL wglSwapIntervalEXT(int interval).
        var wgl = getProcAddress("wglSwapIntervalEXT");
        if (wgl != 0)
        {
            return new WglSwapIntervalBackend(wgl);
        }

        // GLX_EXT_swap_control: void glXSwapIntervalEXT(Display*, GLXDrawable, int).
        var glxExt = getProcAddress("glXSwapIntervalEXT");
        var glxDisplay = getProcAddress("glXGetCurrentDisplay");
        var glxDrawable = getProcAddress("glXGetCurrentDrawable");
        if (glxExt != 0 && glxDisplay != 0 && glxDrawable != 0)
        {
            return new GlxExtSwapIntervalBackend(glxExt, glxDisplay, glxDrawable);
        }

        // GLX_MESA_swap_control / GLX_SGI_swap_control: int f(int interval).
        var glxMesa = getProcAddress("glXSwapIntervalMESA");
        if (glxMesa != 0)
        {
            return new GlxSimpleSwapIntervalBackend("GLX_MESA_swap_control", glxMesa, zeroIsSuccess: true);
        }

        var glxSgi = getProcAddress("glXSwapIntervalSGI");
        if (glxSgi != 0)
        {
            return new GlxSimpleSwapIntervalBackend("GLX_SGI_swap_control", glxSgi, zeroIsSuccess: true);
        }

        // EGL: eglSwapInterval(EGLDisplay, int) returns EGL_TRUE on success.
        var eglSwap = getProcAddress("eglSwapInterval");
        var eglDisplay = getProcAddress("eglGetCurrentDisplay");
        if (eglSwap != 0 && eglDisplay != 0)
        {
            return new EglSwapIntervalBackend(eglSwap, eglDisplay);
        }

        return null;
    }

    private sealed class WglSwapIntervalBackend(nint address) : ISwapIntervalBackend
    {
        private readonly WglSwapInterval _swap = Marshal.GetDelegateForFunctionPointer<WglSwapInterval>(address);

        public string Name => "WGL_EXT_swap_control";

        public bool Apply(int interval) => _swap(interval) != 0;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int WglSwapInterval(int interval);
    }

    private sealed class GlxExtSwapIntervalBackend(nint address, nint displayAddress, nint drawableAddress)
        : ISwapIntervalBackend
    {
        private readonly GlxSwapIntervalExt _swap = Marshal.GetDelegateForFunctionPointer<GlxSwapIntervalExt>(address);
        private readonly GlxGetCurrentDisplay _getDisplay = Marshal.GetDelegateForFunctionPointer<GlxGetCurrentDisplay>(displayAddress);
        private readonly GlxGetCurrentDrawable _getDrawable = Marshal.GetDelegateForFunctionPointer<GlxGetCurrentDrawable>(drawableAddress);

        public string Name => "GLX_EXT_swap_control";

        public bool Apply(int interval)
        {
            var display = _getDisplay();
            var drawable = _getDrawable();
            if (display == 0 || drawable == 0)
            {
                return false;
            }

            _swap(display, drawable, interval);
            return true;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GlxSwapIntervalExt(nint display, nint drawable, int interval);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate nint GlxGetCurrentDisplay();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate nint GlxGetCurrentDrawable();
    }

    private sealed class GlxSimpleSwapIntervalBackend(string name, nint address, bool zeroIsSuccess)
        : ISwapIntervalBackend
    {
        private readonly GlxSwapInterval _swap = Marshal.GetDelegateForFunctionPointer<GlxSwapInterval>(address);

        public string Name { get; } = name;

        public bool Apply(int interval)
        {
            var result = _swap(interval);
            return zeroIsSuccess ? result == 0 : result != 0;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int GlxSwapInterval(int interval);
    }

    private sealed class EglSwapIntervalBackend(nint swapAddress, nint displayAddress) : ISwapIntervalBackend
    {
        private readonly EglSwapInterval _swap = Marshal.GetDelegateForFunctionPointer<EglSwapInterval>(swapAddress);
        private readonly EglGetCurrentDisplay _getDisplay = Marshal.GetDelegateForFunctionPointer<EglGetCurrentDisplay>(displayAddress);

        public string Name => "EGL_KHR_swap_interval";

        public bool Apply(int interval)
        {
            var display = _getDisplay();
            return display != 0 && _swap(display, interval) != 0;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int EglSwapInterval(nint display, int interval);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate nint EglGetCurrentDisplay();
    }
}
