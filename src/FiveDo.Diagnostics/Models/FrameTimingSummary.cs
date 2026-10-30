using System.Runtime.InteropServices;

namespace FiveDo.Diagnostics;


/// <summary>Summary statistics for a sequence of per-frame timings in milliseconds.</summary>
/// <param name="MeanMs">Arithmetic mean.</param>
/// <param name="P50Ms">Median (50th percentile).</param>
/// <param name="P95Ms">95th percentile.</param>
/// <param name="P99Ms">99th percentile.</param>
/// <param name="MaxMs">Largest sample.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct FrameTimingSummary(
    double MeanMs,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    double MaxMs);
