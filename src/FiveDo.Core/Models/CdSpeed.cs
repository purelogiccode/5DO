namespace FiveDo.Core;

/// <summary>
/// The effective CD-ROM read speed used by the cycle-based drive model.
/// </summary>
/// <remarks>
/// Opera exposes the same set as a core option. The stock 3DO drive is 2x, so
/// <see cref="X2"/> is the default. <see cref="Unbounded"/> publishes each requested sector
/// immediately, matching Opera's host-speed override; it is a performance option, not an
/// accuracy mode. The speed only affects <see cref="CdromTimingMode.OperaCycleBased"/>;
/// <see cref="CdromTimingMode.FreeDoCompatible"/> always transfers immediately.
/// </remarks>
public enum CdSpeed
{
    /// <summary>No host-side limit: sectors are published as soon as they are requested.</summary>
    Unbounded = 0,

    /// <summary>1x (about 150 KB/s).</summary>
    X1 = 1,

    /// <summary>2x (about 300 KB/s), the stock 3DO drive speed.</summary>
    X2 = 2,

    /// <summary>4x (about 600 KB/s).</summary>
    X4 = 4,

    /// <summary>8x (about 1200 KB/s).</summary>
    X8 = 8,

    /// <summary>16x (about 2400 KB/s).</summary>
    X16 = 16,

    /// <summary>24x (about 3600 KB/s).</summary>
    X24 = 24,

    /// <summary>32x (about 4800 KB/s).</summary>
    X32 = 32,
}
