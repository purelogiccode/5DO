using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FiveDo.Core;
using FiveDo.Diagnostics.Logging;
using FiveDo.Diagnostics.Statistics;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace FiveDo.Tests.Diagnostics;

/// <summary>
/// Covers the shared Serilog pipeline, the bug-report formatter/sink, and the
/// environment block required by the bug-report service.
/// </summary>
public sealed class LoggingInfrastructureTests
{
    [Fact]
    public void EnvironmentDetails_Format_IncludesEveryRequiredField()
    {
        var details = EnvironmentDetails.Format();

        Assert.Contains("=== Environment Details ===", details, StringComparison.Ordinal);
        Assert.Contains("Date:", details, StringComparison.Ordinal);
        Assert.Contains($"Application Name: {FiveDoVersion.ProductName}", details, StringComparison.Ordinal);
        Assert.Contains($"Application Version: {FiveDoVersion.Version}", details, StringComparison.Ordinal);
        Assert.Contains("OS Version:", details, StringComparison.Ordinal);
        Assert.Contains("Architecture:", details, StringComparison.Ordinal);
        Assert.Contains("Bitness:", details, StringComparison.Ordinal);
        Assert.Contains("Processor Count:", details, StringComparison.Ordinal);
        Assert.Contains("Base Directory:", details, StringComparison.Ordinal);
        Assert.Contains("Temp Path:", details, StringComparison.Ordinal);
        Assert.Contains($"{EnvironmentDetails.PlatformName} Version:", details, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvironmentDetails_PlatformName_IsOneOfTheSupportedLabels()
    {
        string[] supported = ["Windows", "Linux", "MacOSX", "Unknown"];

        Assert.Contains(
            supported,
            item => string.Equals(item, EnvironmentDetails.PlatformName, StringComparison.Ordinal));
    }

    [Fact]
    public void EnvironmentDetails_PlatformName_MatchesTheRunningOperatingSystem()
    {
        var actual = EnvironmentDetails.PlatformName;
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("Windows", actual);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.Equal("Linux", actual);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.Equal("MacOSX", actual);
        }
        else
        {
            Assert.Equal("Unknown", actual);
        }
    }

    [Fact]
    public void BugReportFormatter_WithoutException_ReportsNoneAndKeepsTheBlock()
    {
        var details = BugReportFormatter.FormatException(null);

        Assert.Contains("=== Exception Details ===", details, StringComparison.Ordinal);
        Assert.Contains("None", details, StringComparison.Ordinal);
    }

    [Fact]
    public void BugReportFormatter_FormatException_IncludesTypeMessageSourceAndStack()
    {
        Action thrower = static () => throw new InvalidOperationException("inner boom");
        InvalidOperationException captured = Assert.Throws<InvalidOperationException>(thrower);

        var details = BugReportFormatter.FormatException(captured);

        Assert.Contains("Type: System.InvalidOperationException", details, StringComparison.Ordinal);
        Assert.Contains("Message: inner boom", details, StringComparison.Ordinal);
        Assert.Contains("Source:", details, StringComparison.Ordinal);
        Assert.Contains("StackTrace:", details, StringComparison.Ordinal);
    }

    [Fact]
    public void BugReportFormatter_FormatException_WalksTheInnerExceptionChain()
    {
        var exception = new AggregateException(
            "outer",
            new InvalidOperationException("middle", new NotSupportedException("leaf")));

        var details = BugReportFormatter.FormatException(exception);

        Assert.Contains("Type: System.AggregateException", details, StringComparison.Ordinal);
        Assert.Contains("Message: outer", details, StringComparison.Ordinal);
        Assert.Contains("Inner Exception 1: Type: System.InvalidOperationException", details, StringComparison.Ordinal);
        Assert.Contains("Inner Exception 2: Type: System.NotSupportedException", details, StringComparison.Ordinal);
        Assert.Contains("Message: leaf", details, StringComparison.Ordinal);
    }

    [Fact]
    public void BugReportFormatter_Create_ThrowsForNullEvent()
    {
        Assert.Throws<ArgumentNullException>(() => BugReportFormatter.Create(null!, "CLI"));
    }

    [Fact]
    public void BugReportFormatter_Create_TruncatesOverlongMessages()
    {
        var message = new string('x', 12_000);
        LogEvent logEvent = CaptureEvent(LogEventLevel.Error, exception: null, message);

        BugReportPayload payload = BugReportFormatter.Create(logEvent, "CLI");

        Assert.Equal(4000, payload.Message.Length);
        Assert.EndsWith("...", payload.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BugReportFormatter_Create_TruncatesOverlongStackTrace()
    {
        var exception = new InvalidOperationException(new string('y', 20_000));
        LogEvent logEvent = CaptureEvent(LogEventLevel.Error, exception, "boom");

        BugReportPayload payload = BugReportFormatter.Create(logEvent, "CLI");

        Assert.NotNull(payload.StackTrace);
        Assert.Equal(8000, payload.StackTrace!.Length);
        Assert.EndsWith("...", payload.StackTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void BugReportFormatter_Create_KeepsEnvironmentWithinTheServiceLimit()
    {
        LogEvent logEvent = CaptureEvent(LogEventLevel.Warning, exception: null, "warn");

        BugReportPayload payload = BugReportFormatter.Create(logEvent, new string('m', 200));

        Assert.True(payload.Environment.Length <= 50);
        Assert.True(payload.ApplicationName.Length <= 100);
        Assert.True(payload.Version.Length <= 20);
    }

    [Fact]
    public void BugReportSink_RejectsInvalidOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new BugReportSink(null!));
        Assert.Throws<ArgumentException>(() => new BugReportSink(new BugReportOptions { Endpoint = " " }));
        Assert.Throws<ArgumentException>(() => new BugReportSink(new BugReportOptions { ApiKey = " " }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BugReportSink(new BugReportOptions { MaxRequestsPerMinute = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BugReportSink(new BugReportOptions { QueueCapacity = 0 }));
    }

    [Fact]
    public void BugReportSink_Emit_ThrowsForNullEvent()
    {
        using var sink = new BugReportSink(CreateOptions(), new HttpClient(new CountingHandler()));

        Assert.Throws<ArgumentNullException>(() => sink.Emit(null!));
    }

    [Fact]
    public async Task BugReportSink_DeduplicatesIdenticalMessages()
    {
        var handler = new CountingHandler();
        using var sink = new BugReportSink(CreateOptions(), new HttpClient(handler));
        LogEvent logEvent = CaptureEvent(LogEventLevel.Warning, exception: null, "same warning");

        sink.Emit(logEvent);
        sink.Emit(logEvent);
        await handler.WaitForCountAsync(1);
        await Task.Delay(250);

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task BugReportSink_SubmitsDistinctMessages()
    {
        var handler = new CountingHandler();
        using var sink = new BugReportSink(CreateOptions(), new HttpClient(handler));

        sink.Emit(CaptureEvent(LogEventLevel.Warning, exception: null, "first warning"));
        sink.Emit(CaptureEvent(LogEventLevel.Warning, exception: null, "second warning"));
        await handler.WaitForCountAsync(2);

        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task BugReportSink_HonorsThePerMinuteLimit()
    {
        var handler = new CountingHandler();
        using var sink = new BugReportSink(
            new BugReportOptions
            {
                Endpoint = "https://example.test/bug",
                ApiKey = "test-key",
                MaxRequestsPerMinute = 1,
                DedupeWindow = TimeSpan.Zero,
            },
            new HttpClient(handler));

        sink.Emit(CaptureEvent(LogEventLevel.Warning, exception: null, "first warning"));
        sink.Emit(CaptureEvent(LogEventLevel.Error, exception: null, "second warning"));
        await handler.WaitForCountAsync(1);
        await Task.Delay(250);

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public void BugReportSink_Dispose_IsIdempotent()
    {
        var sink = new BugReportSink(CreateOptions(), new HttpClient(new CountingHandler()));

        sink.Dispose();
        sink.Dispose();
    }

    [Fact]
    public void LoggingSetup_ThrowsForNullOptions()
    {
        Assert.Throws<ArgumentNullException>(() => LoggingSetup.CreateLogger(null!, out _));
    }

    [Fact]
    public void LoggingSetup_WritesTheRollingFileSink()
    {
        using var directory = new TempDirectory();
        var options = new LoggingOptions
        {
            ApplicationMode = "Test",
            WriteToConsole = false,
            WriteToFile = true,
            EnableInMemory = true,
            EnableBugReports = false,
            LogDirectory = directory.Path,
            MinimumLevel = LogEventLevel.Debug,
        };

        using (Logger logger = LoggingSetup.CreateLogger(options, out InMemorySerilogSink sink))
        {
            logger.ForCategory(LogCategory.Host).Warning("file sink check");
            Assert.Single(sink.Entries);
        }

        string[] files = Directory.GetFiles(directory.Path, "5do-*.log");
        Assert.Single(files);
        Assert.Contains("file sink check", File.ReadAllText(files[0]), StringComparison.Ordinal);
    }

    [Fact]
    public void LoggingSetup_RespectsTheMinimumLevel()
    {
        var options = new LoggingOptions
        {
            ApplicationMode = "Test",
            WriteToConsole = false,
            WriteToFile = false,
            EnableInMemory = true,
            EnableBugReports = false,
            MinimumLevel = LogEventLevel.Warning,
        };

        using Logger logger = LoggingSetup.CreateLogger(options, out InMemorySerilogSink sink);
        logger.Debug("dropped");
        logger.Warning("kept");

        LogEntry entry = Assert.Single(sink.Entries);
        Assert.Equal("kept", entry.Message);
    }

    [Fact]
    public void LoggingExtensions_ThrowForNullLogger()
    {
        Assert.Throws<ArgumentNullException>(() => LoggingExtensions.ForCategory(null!, LogCategory.Host));
        Assert.Throws<ArgumentNullException>(() => LoggingExtensions.WithoutBugReport(null!));
    }

    [Fact]
    public void LogPaths_ReturnsTheRollingLogNameBelowTheDirectory()
    {
        var directory = LogPaths.GetLogDirectory();
        var file = LogPaths.GetLogFilePath();

        Assert.False(string.IsNullOrWhiteSpace(directory));
        Assert.EndsWith("5do-.log", file, StringComparison.Ordinal);
        Assert.StartsWith(directory, file, StringComparison.Ordinal);
    }

    [Fact]
    public void LogPaths_RejectsAnEmptyApplicationName()
    {
        Assert.Throws<ArgumentException>(() => LogPaths.GetLogDirectory(" "));
        Assert.Throws<ArgumentException>(() => LogPaths.GetLogFilePath(string.Empty));
    }

    [Fact]
    public void ApplicationStatsOptions_ExposeTheDocumentedDefaults()
    {
        var options = new ApplicationStatsOptions();

        Assert.Equal("https://www.purelogiccode.com/ApplicationStats/stats", options.Endpoint);
        Assert.Equal("5do", options.ApplicationId);
        Assert.Equal(FiveDoVersion.Version, options.Version);
        Assert.False(string.IsNullOrWhiteSpace(options.ApiKey));
    }

    [Fact]
    public void ApplicationStatsClient_IsDisabledDuringTestRuns()
    {
        // TestEnvironment sets FIVEDO_STATS=0 so no test run can record a launch.
        Assert.False(ApplicationStatsClient.IsEnabled());
    }

    [Fact]
    public void ApplicationStatsClient_WaitForPendingWithoutARequestReturnsImmediately()
    {
        var stopwatch = Stopwatch.StartNew();

        ApplicationStatsClient.WaitForPending(TimeSpan.FromSeconds(5));

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void BugReportOptions_ExposeTheDocumentedDefaults()
    {
        var options = new BugReportOptions();

        Assert.Equal("https://www.purelogiccode.com/bugreport/api/send-bug-report", options.Endpoint);
        Assert.Equal("Desktop", options.ApplicationMode);
        Assert.Equal(10, options.MaxRequestsPerMinute);
        Assert.Equal(TimeSpan.FromMinutes(5), options.DedupeWindow);
        Assert.Equal(64, options.QueueCapacity);
        Assert.True(options.HttpTimeout > TimeSpan.Zero);
    }

    [Fact]
    public void LoggingOptions_ExposeTheDocumentedDefaults()
    {
        var options = new LoggingOptions();

        Assert.Equal("Desktop", options.ApplicationMode);
        Assert.Null(options.LogDirectory);
        Assert.True(options.WriteToConsole);
        Assert.True(options.WriteToFile);
        Assert.True(options.EnableInMemory);
        Assert.Equal(4000, options.InMemoryCapacity);
        Assert.True(options.EnableBugReports);
        Assert.Equal(LogEventLevel.Debug, options.MinimumLevel);
    }

    [Fact]
    public void BugReportJsonContext_UsesTheDocumentedFieldNames()
    {
        var payload = new BugReportPayload(
            Message: "message",
            ApplicationName: "5DO",
            Version: "1.2.3",
            UserInfo: null,
            Environment: "Windows / Desktop",
            StackTrace: "stack");

        var json = System.Text.Json.JsonSerializer.Serialize(
            payload,
            BugReportJsonContext.Default.BugReportPayload);

        Assert.Contains("\"message\":\"message\"", json, StringComparison.Ordinal);
        Assert.Contains("\"applicationName\":\"5DO\"", json, StringComparison.Ordinal);
        Assert.Contains("\"version\":\"1.2.3\"", json, StringComparison.Ordinal);
        Assert.Contains("\"environment\":\"Windows / Desktop\"", json, StringComparison.Ordinal);
        Assert.Contains("\"stackTrace\":\"stack\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationStatsJsonContext_UsesTheDocumentedFieldNames()
    {
        var payload = new ApplicationStatsPayload("5do", "1.2.3");

        var json = System.Text.Json.JsonSerializer.Serialize(
            payload,
            ApplicationStatsJsonContext.Default.ApplicationStatsPayload);

        Assert.Equal("{\"applicationId\":\"5do\",\"version\":\"1.2.3\"}", json);
    }

    [Fact]
    public void LogEntry_DefaultsTheTimestampAndKeepsTheValues()
    {
        var entry = new LogEntry(LogEventLevel.Warning, LogCategory.Audio, "message");

        Assert.Equal(LogEventLevel.Warning, entry.Level);
        Assert.Equal(LogCategory.Audio, entry.Category);
        Assert.Equal("message", entry.Message);
        Assert.Equal(default, entry.TimestampUtc);
    }

    [Fact]
    public void LogViewFilter_MaskMatchesAnyOverlappingCategory()
    {
        LogEntry[] entries =
        [
            new(LogEventLevel.Information, LogCategory.Cpu, "cpu"),
            new(LogEventLevel.Information, LogCategory.Storage, "storage"),
            new(LogEventLevel.Information, LogCategory.Audio, "audio"),
        ];

        IReadOnlyList<LogEntry> result = new LogViewFilter
        {
            MinimumLevel = LogEventLevel.Verbose,
            Category = LogCategory.Cpu | LogCategory.Audio,
        }.Apply(entries);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void LogViewFilter_Apply_ThrowsForNull()
    {
        Assert.Throws<ArgumentNullException>(() => new LogViewFilter().Apply(null!));
    }

    private static BugReportOptions CreateOptions()
    {
        return new BugReportOptions
        {
            Endpoint = "https://example.test/bug",
            ApiKey = "test-key",
        };
    }

    private static LogEvent CaptureEvent(LogEventLevel level, Exception? exception, string message)
    {
        var sink = new CapturingSink();
        using Logger logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();
        if (level == LogEventLevel.Warning)
        {
            logger.Warning(exception, "{Message}", message);
        }
        else
        {
            logger.Error(exception, "{Message}", message);
        }

        return sink.Last ?? throw new InvalidOperationException("No log event was captured.");
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public LogEvent? Last { get; private set; }

        public void Emit(LogEvent logEvent)
        {
            Last = logEvent;
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Channel<int> _signals = Channel.CreateUnbounded<int>();
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public Task WaitForCountAsync(int expected)
        {
            return WaitCoreAsync(expected);

            async Task WaitCoreAsync(int target)
            {
                while (Count < target)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    bool available;
                    try
                    {
                        available = await _signals.Reader.WaitToReadAsync(timeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        available = false;
                    }

                    if (!available)
                    {
                        throw new TimeoutException($"Only {Count} of {target} bug reports arrived.");
                    }

                    _ = _signals.Reader.TryRead(out _);
                }
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _count);
            _ = _signals.Writer.TryWrite(Count);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"fivedo-logs-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
