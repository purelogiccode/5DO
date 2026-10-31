namespace FiveDo.Core;

/// <summary>
/// Selects how the CD-ROM drive schedules data-sector availability.
/// </summary>
/// <remarks>
/// FreeDO returns the first sector immediately and reads every following sector as soon as
/// the previous one has been consumed. Opera instead schedules each 2048-byte data sector at
/// the configured drive speed (2x by default) and clears the data-poll bit while the drive is
/// still seeking or reading. The default remains the FreeDO-compatible model so existing
/// save states and golden frames are unchanged.
/// </remarks>
public enum CdromTimingMode
{
    /// <summary>FreeDO's immediate, frame-based data transfer. This is the default.</summary>
    FreeDoCompatible = 0,

    /// <summary>Opera's cycle-based data-sector readiness at the stock 2x drive speed.</summary>
    OperaCycleBased = 1,
}
