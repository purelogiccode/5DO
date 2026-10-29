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
