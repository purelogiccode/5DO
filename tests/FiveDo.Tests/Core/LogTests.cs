using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Core;
using FiveDo.Diagnostics.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace FiveDo.Tests.Core;

public sealed class LogTests
{
    [Fact]
    public void InMemorySink_CapturesCategoryLevelAndMessage()
    {
        var sink = new InMemorySerilogSink();
        using Logger logger = CreateLogger(sink);

        logger.ForCategory(LogCategory.Cpu).Warning("timer {Value}", 42);

        LogEntry entry = Assert.Single(sink.Entries);
        Assert.Equal(LogEventLevel.Warning, entry.Level);
        Assert.Equal(LogCategory.Cpu, entry.Category);
        Assert.Equal("timer 42", entry.Message);
    }

    [Fact]
    public void ForCategory_OverridesTheDefaultCategory()
    {
        var sink = new InMemorySerilogSink();
        using Logger logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.WithProperty("Category", LogCategory.Host)
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.ForCategory(LogCategory.Audio).Information("ready");

        LogEntry entry = Assert.Single(sink.Entries);
        Assert.Equal(LogCategory.Audio, entry.Category);
    }

    [Fact]
    public void InMemorySink_RetainsOnlyCapacity()
    {
        var sink = new InMemorySerilogSink(3);
        using Logger logger = CreateLogger(sink);

        for (var i = 1; i <= 4; i++)
        {
            logger.Information("entry {Index}", i);
        }

        IReadOnlyList<LogEntry> entries = sink.Entries;
        Assert.Equal(3, entries.Count);
        Assert.Equal("entry 2", entries[0].Message);
        Assert.Equal("entry 4", entries[2].Message);
    }

    [Fact]
    public void InMemorySink_Clear_RemovesEntries()
    {
        var sink = new InMemorySerilogSink();
        using Logger logger = CreateLogger(sink);
        logger.Information("one");

        sink.Clear();

        Assert.Empty(sink.Entries);
    }

    [Fact]
    public void InMemorySink_InvalidCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemorySerilogSink(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemorySerilogSink(-1));
    }

    [Fact]
    public void InMemorySink_RecordsTimestamp()
    {
        var sink = new InMemorySerilogSink();
        using Logger logger = CreateLogger(sink);
        DateTimeOffset before = DateTimeOffset.UtcNow.AddSeconds(-1);

        logger.Information("loaded");

        DateTimeOffset after = DateTimeOffset.UtcNow.AddSeconds(1);
        LogEntry entry = Assert.Single(sink.Entries);
        Assert.InRange(entry.TimestampUtc, before, after);
    }

    [Fact]
    public void LoggingSetup_WiresTheInMemorySink()
    {
        var options = new LoggingOptions
        {
            ApplicationMode = "Test",
            WriteToConsole = false,
            WriteToFile = false,
            EnableInMemory = true,
            EnableBugReports = false,
            MinimumLevel = LogEventLevel.Debug,
        };

        using Logger logger = LoggingSetup.CreateLogger(options, out InMemorySerilogSink sink);
        logger.ForCategory(LogCategory.Host).Information("hello");

        LogEntry entry = Assert.Single(sink.Entries);
        Assert.Equal("hello", entry.Message);
        Assert.Equal(LogCategory.Host, entry.Category);
    }

    [Fact]
    public void LogViewFilter_AppliesLevelCategoryAndSearch()
    {
        LogEntry[] entries =
        [
            new(LogEventLevel.Debug, LogCategory.Cpu, "debug cpu"),
            new(LogEventLevel.Information, LogCategory.Cpu, "BIOS loaded"),
            new(LogEventLevel.Warning, LogCategory.Storage, "disc read retry"),
            new(LogEventLevel.Error, LogCategory.Storage, "disc read failed"),
        ];

        var filter = new LogViewFilter
        {
            MinimumLevel = LogEventLevel.Information,
            Category = LogCategory.Storage,
            Search = "failed",
        };

        IReadOnlyList<LogEntry> result = filter.Apply(entries);
        LogEntry entry = Assert.Single(result);
        Assert.Equal(LogEventLevel.Error, entry.Level);
        Assert.Equal(LogCategory.Storage, entry.Category);
    }

    [Fact]
    public void LogViewFilter_AllCategoryShowsEveryCategory()
    {
        LogEntry[] entries =
        [
            new(LogEventLevel.Information, LogCategory.Cpu, "cpu"),
            new(LogEventLevel.Information, LogCategory.Storage, "storage"),
        ];

        IReadOnlyList<LogEntry> result = new LogViewFilter
        {
            MinimumLevel = LogEventLevel.Verbose,
        }.Apply(entries);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void BugReportFormatter_IncludesEveryRequiredBlock()
    {
        var exception = new InvalidOperationException("boom");
        LogEvent logEvent = CreateEvent(LogEventLevel.Error, exception, "Failed load");

        BugReportPayload payload = BugReportFormatter.Create(logEvent, "CLI");

        Assert.Equal("5DO", payload.ApplicationName);
        Assert.Equal("1.0.0-rc.1", payload.Version);
        Assert.Contains(" / CLI", payload.Environment, StringComparison.Ordinal);
        Assert.Contains("=== Environment Details ===", payload.Message, StringComparison.Ordinal);
        Assert.Contains("Date:", payload.Message, StringComparison.Ordinal);
        Assert.Contains("Application Name: 5DO", payload.Message, StringComparison.Ordinal);
        Assert.Contains("Application Version:", payload.Message, StringComparison.Ordinal);
        Assert.Contains("OS Version:", payload.Message, StringComparison.Ordinal);
        Assert.Contains("Architecture:", payload.Message, StringComparison.Ordinal);
        Assert.Contains("Bitness:", payload.Message, StringComparison.Ordinal);
        Assert.Contains("Processor Count:", payload.Message, StringComparison.Ordinal);
        Assert.Contains("Base Directory:", payload.Message, StringComparison.Ordinal);
        Assert.Contains("Temp Path:", payload.Message, StringComparison.Ordinal);
        Assert.Contains("=== Error Details ===", payload.Message, StringComparison.Ordinal);
        Assert.Contains("Failed load", payload.Message, StringComparison.Ordinal);
        Assert.NotNull(payload.StackTrace);
        Assert.Contains("=== Exception Details ===", payload.StackTrace, StringComparison.Ordinal);
        Assert.Contains("Type: System.InvalidOperationException", payload.StackTrace, StringComparison.Ordinal);
        Assert.Contains("Message: boom", payload.StackTrace, StringComparison.Ordinal);
        Assert.Contains("Source:", payload.StackTrace, StringComparison.Ordinal);
        Assert.Contains("StackTrace:", payload.StackTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void BugReportFormatter_WithoutException_ReportsNone()
    {
        LogEvent logEvent = CreateEvent(LogEventLevel.Warning, exception: null, "disk nearly full");

        BugReportPayload payload = BugReportFormatter.Create(logEvent, "Desktop");

        Assert.Contains("=== Error Details ===", payload.Message, StringComparison.Ordinal);
        Assert.Equal("=== Exception Details ===" + Environment.NewLine + "None", payload.StackTrace);
    }

    [Fact]
    public async Task BugReportSink_PostsWarningToTheConfiguredEndpoint()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        using var sink = new BugReportSink(
            new BugReportOptions
            {
                Endpoint = "https://example.test/bug",
                ApiKey = "test-key",
                ApplicationMode = "CLI",
            },
            http);
        LogEvent logEvent = CreateEvent(LogEventLevel.Warning, exception: null, "disk nearly full");

        sink.Emit(logEvent);

        string body = await handler.Body.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("test-key", handler.ApiKey);
        Assert.Contains("\"applicationName\":\"5DO\"", body, StringComparison.Ordinal);
        Assert.Contains("\"environment\":\"", body, StringComparison.Ordinal);
        Assert.Contains("=== Environment Details ===", body, StringComparison.Ordinal);
        Assert.Contains("=== Error Details ===", body, StringComparison.Ordinal);
        Assert.Contains("disk nearly full", body, StringComparison.Ordinal);
        Assert.Contains("=== Exception Details ===", body, StringComparison.Ordinal);
    }

    [Fact]
    public void BugReportSink_IgnoresInformationAndBelow()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        using var sink = new BugReportSink(
            new BugReportOptions { Endpoint = "https://example.test/bug", ApiKey = "test-key" },
            http);

        sink.Emit(CreateEvent(LogEventLevel.Information, exception: null, "ordinary event"));

        Assert.False(handler.Body.IsCompleted);
    }

    private static Logger CreateLogger(InMemorySerilogSink sink)
        => new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();

    private static LogEvent CreateEvent(LogEventLevel level, Exception? exception, string message)
    {
        var sink = new CapturingSink();
        using Logger logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();
        switch (level)
        {
            case LogEventLevel.Warning:
                logger.Warning(exception, "{Message}", message);
                break;
            case LogEventLevel.Error:
                logger.Error(exception, "{Message}", message);
                break;
            case LogEventLevel.Information:
                logger.Information("{Message}", message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, "Unsupported test level.");
        }

        return sink.Last ?? throw new InvalidOperationException("No log event was captured.");
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public LogEvent? Last { get; private set; }

        public void Emit(LogEvent logEvent) => Last = logEvent;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<string> _body =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> Body => _body.Task;

        public string? ApiKey { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ApiKey = request.Headers.TryGetValues("X-API-KEY", out IEnumerable<string>? values)
                ? values.FirstOrDefault()
                : null;
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _body.TrySetResult(body);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
