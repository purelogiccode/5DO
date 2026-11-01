using System;
using System.IO;
using FiveDo.Host;
using FiveDo.Host.Osd;

namespace FiveDo.Tests.Host;

public sealed class OsdTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Enqueue_AddsATrimmedMessageWithAnExpiry()
    {
        var notifications = new OsdNotifications(maxMessages: 3, lifetime: TimeSpan.FromSeconds(2));

        OsdMessage? message = notifications.Enqueue("  State saved  ", OsdSeverity.Success, Start);

        Assert.NotNull(message);
        Assert.Equal("State saved", message.Text);
        Assert.Equal(OsdSeverity.Success, message.Severity);
        Assert.Equal(Start.AddSeconds(2), message.ExpiresAt);
        Assert.Single(notifications.Messages);
    }

    [Fact]
    public void Enqueue_IgnoresEmptyText()
    {
        var notifications = new OsdNotifications();
        var changes = 0;
        notifications.Changed += (_, _) => changes++;

        Assert.Null(notifications.Enqueue("   ", OsdSeverity.Info, Start));
        Assert.Null(notifications.Enqueue(null, OsdSeverity.Info, Start));
        Assert.Empty(notifications.Messages);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Enqueue_TrimsTheOldestMessageWhenTheQueueIsFull()
    {
        var notifications = new OsdNotifications(maxMessages: 2);

        notifications.Enqueue("first", OsdSeverity.Info, Start);
        notifications.Enqueue("second", OsdSeverity.Info, Start);
        notifications.Enqueue("third", OsdSeverity.Warning, Start);

        Assert.Equal(2, notifications.Count);
        Assert.Equal("second", notifications.Messages[0].Text);
        Assert.Equal("third", notifications.Messages[1].Text);
    }

    [Fact]
    public void RemoveExpired_RemovesOnlyElapsedMessages()
    {
        var notifications = new OsdNotifications(maxMessages: 3, lifetime: TimeSpan.FromSeconds(1));
        notifications.Enqueue("first", OsdSeverity.Info, Start);
        notifications.Enqueue("second", OsdSeverity.Info, Start.AddMilliseconds(400));

        Assert.Equal(0, notifications.RemoveExpired(Start.AddMilliseconds(999)));
        Assert.Equal(2, notifications.Count);

        Assert.Equal(1, notifications.RemoveExpired(Start.AddSeconds(1)));
        Assert.Single(notifications.Messages);
        Assert.Equal("second", notifications.Messages[0].Text);

        Assert.Equal(1, notifications.RemoveExpired(Start.AddMilliseconds(1400)));
        Assert.Empty(notifications.Messages);
    }

    [Fact]
    public void Clear_EmptiesTheQueueAndRaisesChanged()
    {
        var notifications = new OsdNotifications();
        notifications.Enqueue("first", OsdSeverity.Info, Start);
        var changes = 0;
        notifications.Changed += (_, _) => changes++;

        notifications.Clear();

        Assert.Empty(notifications.Messages);
        Assert.Equal(1, changes);

        notifications.Clear();
        Assert.Equal(1, changes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Constructor_RejectsAnInvalidMessageLimit(int maxMessages)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OsdNotifications(maxMessages));
    }

    [Theory]
    [InlineData(499)]
    [InlineData(10_001)]
    public void Lifetime_RejectsAnInvalidDuration(int milliseconds)
    {
        var notifications = new OsdNotifications();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => notifications.Lifetime = TimeSpan.FromMilliseconds(milliseconds));
    }

    [Fact]
    public void Options_ValidateAcceptsTheDefaults()
    {
        var options = new OsdOptions();

        options.Validate();
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(1.01)]
    public void Options_ValidateRejectsAnInvalidOpacity(double opacity)
    {
        var options = new OsdOptions { Opacity = opacity };

        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    [Theory]
    [InlineData(499)]
    [InlineData(10_001)]
    public void Options_ValidateRejectsAnInvalidDuration(int milliseconds)
    {
        var options = new OsdOptions { DurationMilliseconds = milliseconds };

        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    [Fact]
    public void Options_CloneIsDetachedAndValidated()
    {
        var options = new OsdOptions
        {
            Enabled = false,
            Position = OsdPosition.BottomCenter,
            Opacity = 0.5,
            DurationMilliseconds = 4000,
            MaxMessages = 5,
        };

        OsdOptions clone = options.Clone();
        clone.Position = OsdPosition.TopLeft;

        Assert.Equal(OsdPosition.BottomCenter, options.Position);
        Assert.False(clone.Enabled);
        Assert.Equal(0.5, clone.Opacity);
        Assert.Equal(4000, clone.DurationMilliseconds);
        Assert.Equal(5, clone.MaxMessages);
    }

    [Fact]
    public void Settings_ToOsdOptionsMapsThePersistedValues()
    {
        var settings = new AppSettings
        {
            OsdEnabled = false,
            OsdPosition = OsdPosition.BottomLeft,
            OsdOpacity = 0.75,
            OsdDurationMilliseconds = 3500,
        };

        OsdOptions options = settings.ToOsdOptions();

        Assert.False(options.Enabled);
        Assert.Equal(OsdPosition.BottomLeft, options.Position);
        Assert.Equal(0.75, options.Opacity);
        Assert.Equal(3500, options.DurationMilliseconds);
    }

    [Fact]
    public void Settings_ToOsdOptionsRejectsAnInvalidDuration()
    {
        var settings = new AppSettings { OsdDurationMilliseconds = 100 };

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ToOsdOptions());
    }

    [Fact]
    public void SettingsStore_RoundtripsOsdOptions()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-osd-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var settings = new AppSettings
            {
                OsdEnabled = false,
                OsdPosition = OsdPosition.BottomCenter,
                OsdOpacity = 0.6,
                OsdDurationMilliseconds = 4000,
                QuickMenuEnabled = false,
            };

            AppSettingsStore.Save(settings, path);
            AppSettings loaded = AppSettingsStore.Load(path);

            Assert.False(loaded.OsdEnabled);
            Assert.Equal(OsdPosition.BottomCenter, loaded.OsdPosition);
            Assert.Equal(0.6, loaded.OsdOpacity);
            Assert.Equal(4000, loaded.OsdDurationMilliseconds);
            Assert.False(loaded.QuickMenuEnabled);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void SettingsStore_NormalizesInvalidOsdRanges()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-osd-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                path,
                """
                {
                  "OsdPosition": "BottomLeft",
                  "OsdOpacity": 9.0,
                  "OsdDurationMilliseconds": 1
                }
                """);

            AppSettings loaded = AppSettingsStore.Load(path);

            Assert.Equal(OsdPosition.BottomLeft, loaded.OsdPosition);
            Assert.Equal(0.9, loaded.OsdOpacity);
            Assert.Equal(2200, loaded.OsdDurationMilliseconds);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
