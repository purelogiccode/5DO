using System.Runtime.InteropServices;

namespace FiveDo.Frontend.Avalonia;

/// <summary>One timing summary in the GPU probe report.</summary>
/// <param name="MeanMs">Mean milliseconds.</param>
/// <param name="P50Ms">Median milliseconds.</param>
/// <param name="P95Ms">95th percentile.</param>
/// <param name="P99Ms">99th percentile.</param>
/// <param name="MaxMs">Maximum milliseconds.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct GlProbeTiming(
    double MeanMs,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    double MaxMs);
