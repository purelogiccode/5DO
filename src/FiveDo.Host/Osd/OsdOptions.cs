using System;

namespace FiveDo.Host.Osd;

/// <summary>
/// Validated presentation options for the on-screen display and quick menu.
/// </summary>
public sealed class OsdOptions
{
    /// <summary>Gets the smallest supported notification lifetime in milliseconds.</summary>
    public const int MinDurationMilliseconds = 500;

    /// <summary>Gets the largest supported notification lifetime in milliseconds.</summary>
    public const int MaxDurationMilliseconds = 10_000;

    /// <summary>Gets the largest supported number of simultaneous notifications.</summary>
    public const int MaxMessagesLimit = 8;

    /// <summary>Gets or sets a value indicating whether notifications are shown.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the overlay anchor position.</summary>
    public OsdPosition Position { get; set; } = OsdPosition.TopRight;

    /// <summary>Gets or sets the overlay opacity (0.1-1).</summary>
    public double Opacity { get; set; } = 0.9;

    /// <summary>Gets or sets how long a notification stays visible, in milliseconds.</summary>
    public int DurationMilliseconds { get; set; } = 2200;

    /// <summary>Gets or sets the maximum number of simultaneous notifications.</summary>
    public int MaxMessages { get; set; } = 3;

    /// <summary>Validates the options.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an option is outside its supported range.</exception>
    public void Validate()
    {
        if (!Enum.IsDefined(Position))
        {
            throw new ArgumentOutOfRangeException(
                paramName: null,
                actualValue: Position,
                message: "The OSD position is not a supported value.");
        }

        if (!double.IsFinite(Opacity) || Opacity is < 0.1 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName: null,
                actualValue: Opacity,
                message: "The OSD opacity must be between 0.1 and 1.0.");
        }

        if (DurationMilliseconds is < MinDurationMilliseconds or > MaxDurationMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                paramName: null,
                actualValue: DurationMilliseconds,
                message: $"The OSD duration must be between {MinDurationMilliseconds} and {MaxDurationMilliseconds} milliseconds.");
        }

        if (MaxMessages is < 1 or > MaxMessagesLimit)
        {
            throw new ArgumentOutOfRangeException(
                paramName: null,
                actualValue: MaxMessages,
                message: $"The OSD message limit must be between 1 and {MaxMessagesLimit}.");
        }
    }

    /// <summary>Creates a copy of the validated options.</summary>
    /// <returns>A detached copy.</returns>
    public OsdOptions Clone()
    {
        Validate();
        return new OsdOptions
        {
            Enabled = Enabled,
            Position = Position,
            Opacity = Opacity,
            DurationMilliseconds = DurationMilliseconds,
            MaxMessages = MaxMessages,
        };
    }
}
