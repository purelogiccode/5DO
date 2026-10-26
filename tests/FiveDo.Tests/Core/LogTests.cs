using System;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Tests.Core;

public sealed class LogTests
{
    [Fact]
    public void DisabledLevel_DoesNotWrite()
    {
        var sink = new InMemoryLogSink();
        var log = new Log(sink, LogLevel.Warning);

        log.Write(LogLevel.Debug, LogCategory.Cpu, "hidden");
        log.Write(LogLevel.Warning, LogCategory.Cpu, "shown");

        var entries = sink.Entries;
        Assert.Single(entries);
        Assert.Equal("shown", entries[0].Message);
        Assert.Equal(LogLevel.Warning, entries[0].Level);
        Assert.Equal(LogCategory.Cpu, entries[0].Category);
    }

    [Fact]
    public void DisabledCategory_DoesNotWrite()
    {
        var sink = new InMemoryLogSink();
        var log = new Log(sink, LogLevel.Trace, LogCategory.Cpu);

        log.Write(LogLevel.Info, LogCategory.Madam, "hidden");
        log.Write(LogLevel.Info, LogCategory.Cpu, "shown");

        var entries = sink.Entries;
        Assert.Single(entries);
        Assert.Equal("shown", entries[0].Message);
    }

    [Fact]
    public void NullLog_IsDisabled()
    {
        Assert.False(Log.Null.IsEnabled(LogLevel.Critical, LogCategory.All));
        Log.Null.Write(LogLevel.Critical, LogCategory.All, "ignored");
    }

    [Fact]
    public void NullSink_DoesNotThrow()
    {
        NullLogSink.Instance.Write(LogLevel.Critical, LogCategory.All, "ignored");
    }

    [Fact]
    public void InterpolatedMessage_FormatsWhenEnabled()
    {
        var sink = new InMemoryLogSink();
        var log = new Log(sink);
        var value = 42;

        log.Write(LogLevel.Info, LogCategory.Machine, $"value={value}");

        Assert.Equal("value=42", sink.Entries[0].Message);
    }

    [Fact]
    public void InterpolatedMessage_DoesNotFormatWhenDisabled()
    {
        var sink = new InMemoryLogSink();
        var log = new Log(sink, LogLevel.Error);
        var value = 42;

        log.Write(LogLevel.Debug, LogCategory.Machine, $"value={value}");

        Assert.Empty(sink.Entries);
    }

    [Fact]
    public void InMemorySink_RetainsOnlyCapacity()
    {
        var sink = new InMemoryLogSink(3);

        sink.Write(LogLevel.Info, LogCategory.Machine, "1");
        sink.Write(LogLevel.Info, LogCategory.Machine, "2");
        sink.Write(LogLevel.Info, LogCategory.Machine, "3");
        sink.Write(LogLevel.Info, LogCategory.Machine, "4");

        var entries = sink.Entries;
        Assert.Equal(3, entries.Count);
        Assert.Equal("2", entries[0].Message);
        Assert.Equal("4", entries[2].Message);
    }

    [Fact]
    public void InMemorySink_Clear_RemovesEntries()
    {
        var sink = new InMemoryLogSink();
        sink.Write(LogLevel.Info, LogCategory.Machine, "1");

        sink.Clear();

        Assert.Empty(sink.Entries);
    }

    [Fact]
    public void InMemorySink_InvalidCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryLogSink(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryLogSink(-1));
    }

    [Fact]
    public void ConsoleSink_WritesFormattedLine()
    {
        using var writer = new StringWriter();
        var sink = new ConsoleLogSink(writer);

        sink.Write(LogLevel.Warning, LogCategory.Clio, "timer underflow");

        Assert.Equal("[Warning] Clio: timer underflow" + Environment.NewLine, writer.ToString());
    }

    [Fact]
    public void InMemorySink_RecordsTimestamp()
    {
        var sink = new InMemoryLogSink();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        sink.Write(LogLevel.Info, LogCategory.Host, "loaded");
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        var entry = Assert.Single(sink.Entries);
        Assert.InRange(entry.TimestampUtc, before, after);
    }

    [Fact]
    public void LogViewFilter_AppliesLevelCategoryAndSearch()
    {
        LogEntry[] entries =
        [
            new(LogLevel.Debug, LogCategory.Cpu, "debug cpu"),
            new(LogLevel.Info, LogCategory.Cpu, "BIOS loaded"),
            new(LogLevel.Warning, LogCategory.Storage, "disc read retry"),
            new(LogLevel.Error, LogCategory.Storage, "disc read failed"),
        ];

        var filter = new LogViewFilter
        {
            MinimumLevel = LogLevel.Info,
            Category = LogCategory.Storage,
            Search = "failed",
        };

        var result = filter.Apply(entries);
        var entry = Assert.Single(result);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(LogCategory.Storage, entry.Category);
    }

    [Fact]
    public void LogViewFilter_AllCategoryShowsEveryCategory()
    {
        LogEntry[] entries =
        [
            new(LogLevel.Info, LogCategory.Cpu, "cpu"),
            new(LogLevel.Info, LogCategory.Storage, "storage"),
        ];

        var result = new LogViewFilter { MinimumLevel = LogLevel.Trace }.Apply(entries);

        Assert.Equal(2, result.Count);
    }
}
