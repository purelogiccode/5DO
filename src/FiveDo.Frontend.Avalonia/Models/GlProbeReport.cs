using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using FiveDo.Core;
using FiveDo.Diagnostics;
using FiveDo.Frontend.Avalonia.Controls;
using FiveDo.Video;

namespace FiveDo.Frontend.Avalonia;


/// <summary>The JSON result written by <see cref="GlProbeWindow"/>.</summary>
/// <param name="Ok">Whether the probe completed.</param>
/// <param name="Error">Failure message, if any.</param>
/// <param name="RequestedBackend">Renderer backend requested on the command line.</param>
/// <param name="Backend">Renderer backend name.</param>
/// <param name="Vendor">GL vendor string.</param>
/// <param name="Renderer">GL renderer string.</param>
/// <param name="Api">Graphics API name.</param>
/// <param name="MajorVersion">API major version.</param>
/// <param name="MinorVersion">API minor version.</param>
/// <param name="MaxTextureSize">Maximum texture dimension.</param>
/// <param name="SupportsIntegerTextures">Whether integer textures are available.</param>
/// <param name="SupportsVsync">Whether a swap-interval extension was found.</param>
/// <param name="VsyncRequested">Whether the probe requested vsync.</param>
/// <param name="VsyncActive">Whether the platform accepted the swap interval.</param>
/// <param name="VsyncBackend">Selected swap-interval extension.</param>
/// <param name="SourceWidth">Synthetic source width.</param>
/// <param name="SourceHeight">Synthetic source height.</param>
/// <param name="OutputWidth">Measured physical output width.</param>
/// <param name="OutputHeight">Measured physical output height.</param>
/// <param name="Filter">Selected filter.</param>
/// <param name="Offscreen">Whether the measured pass rendered to an offscreen target.</param>
/// <param name="CapturePath">Optional PNG capture path.</param>
/// <param name="Parity">CPU/GPU parity result when the capture is a 1:1 Accurate frame.</param>
/// <param name="Frames">Measured frames presented.</param>
/// <param name="WarmupFrames">Unmeasured warm-up frames.</param>
/// <param name="ElapsedSeconds">Elapsed wall-clock seconds.</param>
/// <param name="Fps">Presented frames per second.</param>
/// <param name="AllocatedBytes">Managed bytes allocated during the measured run.</param>
/// <param name="AllocatedBytesPerFrame">Managed bytes allocated per presented frame.</param>
/// <param name="RendererAllocatedBytes">Managed bytes allocated by the renderer/mailbox present call.</param>
/// <param name="RendererAllocatedBytesPerFrame">Renderer bytes per presented frame.</param>
/// <param name="RendererOnlyAllocatedBytes">Managed bytes allocated by the renderer present call alone.</param>
/// <param name="RendererOnlyAllocatedBytesPerFrame">Renderer-only bytes per presented frame.</param>
/// <param name="RendererMeasureAllocations">Whether the renderer stage counters were active.</param>
/// <param name="RendererPresentAllocatedBytes">Bytes allocated by the renderer's entire last present call.</param>
/// <param name="UploadAllocatedBytes">Bytes allocated by the last upload stage.</param>
/// <param name="PaletteAllocatedBytes">Bytes allocated by the last palette stage.</param>
/// <param name="ScaleAllocatedBytes">Bytes allocated by the last scale/filter stage.</param>
/// <param name="Present">CPU present timing.</param>
/// <param name="Gpu">GPU timing when available.</param>
public sealed record GlProbeReport(
    bool Ok,
    string? Error,
    string? RequestedBackend,
    string? Backend,
    string? Vendor,
    string? Renderer,
    string? Api,
    int MajorVersion,
    int MinorVersion,
    int MaxTextureSize,
    bool SupportsIntegerTextures,
    bool SupportsVsync,
    bool VsyncRequested,
    bool VsyncActive,
    string? VsyncBackend,
    int SourceWidth,
    int SourceHeight,
    int OutputWidth,
    int OutputHeight,
    string Filter,
    bool Offscreen,
    string? CapturePath,
    GlProbeParity? Parity,
    int Frames,
    int WarmupFrames,
    double ElapsedSeconds,
    double Fps,
    long AllocatedBytes,
    long AllocatedBytesPerFrame,
    long RendererAllocatedBytes,
    long RendererAllocatedBytesPerFrame,
    long RendererOnlyAllocatedBytes,
    long RendererOnlyAllocatedBytesPerFrame,
    bool RendererMeasureAllocations,
    long RendererPresentAllocatedBytes,
    long UploadAllocatedBytes,
    long PaletteAllocatedBytes,
    long ScaleAllocatedBytes,
    GlProbeTiming? Present,
    GlProbeTiming? Gpu);
