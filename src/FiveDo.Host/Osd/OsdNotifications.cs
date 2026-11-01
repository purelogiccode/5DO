using System;
using System.Collections.Generic;

namespace FiveDo.Host.Osd;

/// <summary>Severity used to colour an on-screen notification.</summary>
public enum OsdSeverity
{
    /// <summary>Neutral information.</summary>
    Info = 0,

    /// <summary>A completed action.</summary>
    Success,

    /// <summary>A recoverable problem or warning.</summary>
    Warning,

    /// <summary>A failed action.</summary>
    Error,
}

/// <summary>A notification currently shown by the on-screen display.</summary>
/// <param name="Text">Display text.</param>
/// <param name="Severity">Notification severity.</param>
/// <param name="ExpiresAt">UTC time after which the notification is removed.</param>
public sealed record OsdMessage(string Text, OsdSeverity Severity, DateTimeOffset ExpiresAt);

/// <summary>
/// Bounded, time-based queue of on-screen notifications.
/// </summary>
/// <remarks>
/// The queue is deterministic and UI-framework independent so its expiry and capacity
/// behaviour can be tested without starting Avalonia. The caller owns the clock and
/// supplies <see cref="DateTimeOffset.UtcNow"/> to <see cref="Enqueue"/> and
/// <see cref="RemoveExpired"/>.
/// </remarks>
public sealed class OsdNotifications
{
    private readonly List<OsdMessage> _messages = [];
    private TimeSpan _lifetime;

    /// <summary>Initializes a new instance of the <see cref="OsdNotifications"/> class.</summary>
    /// <param name="maxMessages">Maximum simultaneous messages (1-8).</param>
    /// <param name="lifetime">How long each message stays visible.</param>
    public OsdNotifications(int maxMessages = 3, TimeSpan? lifetime = null)
    {
        if (maxMessages is < 1 or > OsdOptions.MaxMessagesLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxMessages),
                maxMessages,
                $"The message limit must be between 1 and {OsdOptions.MaxMessagesLimit}.");
        }

        MaxMessages = maxMessages;
        _lifetime = lifetime ?? TimeSpan.FromMilliseconds(2200);
        ValidateLifetime(_lifetime);
    }

    /// <summary>Raised whenever the visible message list changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the maximum number of simultaneous messages.</summary>
    public int MaxMessages { get; }

    /// <summary>Gets or sets how long new messages stay visible.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the lifetime is outside 0.5-10 seconds.</exception>
    public TimeSpan Lifetime
    {
        get => _lifetime;
        set
        {
            ValidateLifetime(value);
            _lifetime = value;
        }
    }

    /// <summary>Gets the visible messages, oldest first.</summary>
    public IReadOnlyList<OsdMessage> Messages => _messages;

    /// <summary>Gets the number of visible messages.</summary>
    public int Count => _messages.Count;

    /// <summary>Adds a message, trimming the oldest entries when the queue is full.</summary>
    /// <param name="text">Display text. Empty or whitespace text is ignored.</param>
    /// <param name="severity">Notification severity.</param>
    /// <param name="now">Current UTC time.</param>
    /// <returns>The queued message, or <see langword="null"/> when the text was empty.</returns>
    public OsdMessage? Enqueue(string? text, OsdSeverity severity, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        RemoveExpired(now);
        var message = new OsdMessage(text.Trim(), severity, now + _lifetime);
        _messages.Add(message);
        while (_messages.Count > MaxMessages)
        {
            _messages.RemoveAt(0);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return message;
    }

    /// <summary>Removes every message whose lifetime has elapsed.</summary>
    /// <param name="now">Current UTC time.</param>
    /// <returns>The number of messages removed.</returns>
    public int RemoveExpired(DateTimeOffset now)
    {
        var removed = _messages.RemoveAll(message => message.ExpiresAt <= now);
        if (removed > 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    /// <summary>Removes every message.</summary>
    public void Clear()
    {
        if (_messages.Count == 0)
        {
            return;
        }

        _messages.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static void ValidateLifetime(TimeSpan lifetime)
    {
        if (lifetime < TimeSpan.FromMilliseconds(OsdOptions.MinDurationMilliseconds)
            || lifetime > TimeSpan.FromMilliseconds(OsdOptions.MaxDurationMilliseconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                lifetime,
                $"The notification lifetime must be between {OsdOptions.MinDurationMilliseconds} and {OsdOptions.MaxDurationMilliseconds} milliseconds.");
        }
    }
}
