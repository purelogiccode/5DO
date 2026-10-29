using System;
using FiveDo.Core;

namespace FiveDo.Video;


/// <summary>Scaling/filter mode for the required OpenGL presenter.</summary>
public enum VideoFilter
{
    /// <summary>Nearest-neighbor, pixel-perfect, integer scaling; matches the CPU oracle.</summary>
    Accurate,

    /// <summary>Nearest-neighbor with arbitrary scale and letterboxing.</summary>
    Nearest,

    /// <summary>Sharp-bilinear; the default enhanced desktop mode.</summary>
    SharpBilinear,

    /// <summary>CRT scanlines and aperture mask.</summary>
    Crt,

    /// <summary>xBR/HQx-family smoothing (opt-in).</summary>
    Xbr,

    /// <summary>HQx 2x pixel-art smoothing followed by sharp-bilinear scaling (opt-in).</summary>
    Hqx,

    /// <summary>HQx 3x pixel-art smoothing followed by sharp-bilinear scaling (opt-in).</summary>
    Hqx3x,

    /// <summary>HQx 4x pixel-art smoothing followed by sharp-bilinear scaling (opt-in).</summary>
    Hqx4x,

    /// <summary>AMD FSR 1.0 EASU upscale plus RCAS sharpening (opt-in spatial post-process).</summary>
    Fsr,
}
