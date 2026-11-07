using System;

namespace FiveDo.Core;

/// <summary>
/// The 3DO video region selected for a machine.
/// </summary>
/// <remarks>
/// PAL1 is the 320-wide European mode; PAL2 is the 384-wide mode used by some titles.
/// Both PAL modes run at 50 fields per second and show 288 lines.
/// </remarks>
public enum VideoRegion
{
    /// <summary>NTSC: 320x240 at 60 fields per second.</summary>
    Ntsc,

    /// <summary>PAL1: 320x288 at 50 fields per second.</summary>
    Pal1,

    /// <summary>PAL2: 384x288 at 50 fields per second.</summary>
    Pal2,
}

/// <summary>
/// Geometry and timing derived from a <see cref="VideoRegion"/>.
/// </summary>
/// <remarks>
/// NTSC uses the exact 60000/1001 field rate (59.94 Hz) and Opera's 16-line visible
/// start. PAL follows Opera's 21/22-line starts and 50 Hz field clock. The nominal
/// <see cref="FieldRate"/> is kept for reporting; emulation uses the rational
/// <see cref="FieldRateNumerator"/>/<see cref="FieldRateDenominator"/> pair.
/// </remarks>
public sealed record VideoRegionProfile
{
    private VideoRegionProfile(
        VideoRegion region,
        int width,
        int height,
        int startScanline,
        int fieldLines,
        int fieldRate)
    {
        Region = region;
        Width = width;
        Height = height;
        StartScanline = startScanline;
        FieldLines = fieldLines;
        FieldRate = fieldRate;
    }

    /// <summary>Gets the NTSC profile.</summary>
    public static VideoRegionProfile Ntsc { get; } = new(
        VideoRegion.Ntsc,
        width: 320,
        height: 240,
        startScanline: 16,
        fieldLines: 263,
        fieldRate: 60);

    /// <summary>Gets the PAL1 profile.</summary>
    public static VideoRegionProfile Pal1 { get; } = new(
        VideoRegion.Pal1,
        width: 320,
        height: 288,
        startScanline: 21,
        fieldLines: 312,
        fieldRate: 50);

    /// <summary>Gets the PAL2 profile.</summary>
    public static VideoRegionProfile Pal2 { get; } = new(
        VideoRegion.Pal2,
        width: 384,
        height: 288,
        startScanline: 22,
        fieldLines: 312,
        fieldRate: 50);

    /// <summary>Gets the selected region.</summary>
    public VideoRegion Region { get; }

    /// <summary>Gets the visible width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the visible height in lines.</summary>
    public int Height { get; }

    /// <summary>Gets the first visible VDL line.</summary>
    public int StartScanline { get; }

    /// <summary>Gets the number of VDL lines in one field.</summary>
    public int FieldLines { get; }

    /// <summary>Gets the field rate in hertz.</summary>
    /// <remarks>
    /// This is the nominal rate used for reporting and host pacing. Emulation timing
    /// uses the exact rational <see cref="FieldRateNumerator"/>/<see cref="FieldRateDenominator"/>
    /// pair; NTSC is 60000/1001 (59.94 Hz), not 60 Hz.
    /// </remarks>
    public int FieldRate { get; }

    /// <summary>Gets the exact field-rate numerator.</summary>
    public int FieldRateNumerator => IsPal ? 50 : 60_000;

    /// <summary>Gets the exact field-rate denominator.</summary>
    public int FieldRateDenominator => IsPal ? 1 : 1_001;

    /// <summary>Gets the exact field rate in hertz (NTSC 59.94, PAL 50).</summary>
    public double ExactFieldRate => (double)FieldRateNumerator / FieldRateDenominator;

    /// <summary>Gets the number of VDL lines in one full two-field cycle.</summary>
    public int FrameLines => FieldLines * 2;

    /// <summary>Gets the VDL events per second.</summary>
    /// <remarks>
    /// Rounded to the nearest whole event. Emulation uses the exact rational
    /// field rate in the machine clock scheduler; this property is for reporting and tests.
    /// </remarks>
    public int VdlClock => (int)Math.Round(FieldLines * ExactFieldRate);

    /// <summary>Gets a value indicating whether this is a PAL region.</summary>
    public bool IsPal => Region is VideoRegion.Pal1 or VideoRegion.Pal2;

    /// <summary>Gets the ARM cycles in one emulated field.</summary>
    /// <param name="armClock">ARM clock in hertz.</param>
    /// <returns>The integer number of cycles in one field.</returns>
    /// <remarks>
    /// Uses the exact rational field rate. NTSC fields last 208,541.67 cycles at
    /// 12.5 MHz; the old 12,500,000/60 = 208,333 integer division ran 1 ms long per
    /// 60 fields and drifted away from the reference core's sample clock.
    /// </remarks>
    public int GetFrameCycles(int armClock) =>
        (int)(((long)armClock * FieldRateDenominator) / FieldRateNumerator);

    /// <summary>Returns the profile for a region.</summary>
    /// <param name="region">Region to resolve.</param>
    /// <returns>The matching profile.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for an unknown region.</exception>
    public static VideoRegionProfile For(VideoRegion region) => region switch
    {
        VideoRegion.Ntsc => Ntsc,
        VideoRegion.Pal1 => Pal1,
        VideoRegion.Pal2 => Pal2,
        _ => throw new ArgumentOutOfRangeException(nameof(region), region, "Unknown video region."),
    };
}
