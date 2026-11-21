using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Diagnostics.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace FiveDo.Tests.Diagnostics;

/// <summary>
/// Locks the bug-report contract: every submitted report must carry the environment,
/// error, and exception blocks with the field names the service expects.
/// </summary>
public sealed class BugReportContractTests
{
    [Fact]
    public void EnvironmentBlock_ContainsEachRequiredFieldExactlyOnce()
    {
        var details = EnvironmentDetails.Format();
        string[] required =
        [
            "=== Environment Details ===",
            "Date:",
            "Application Name:",
            "Application Version:",
            "OS Version:",
            "Architecture:",
            "Bitness:",
            "Processor Count:",
            "Base Directory:",
            "Temp Path:",
        ];

        foreach (var field in required)
        {
            Assert.Equal(1, CountOccurrences(details, field));
        }

        Assert.Equal(1, CountOccurrences(details, $"{EnvironmentDetails.PlatformName} Version:"));
    }

    [Fact]
    public void MessageBlock_ContainsTheErrorDetailsAndSource()
    {
        LogEvent logEvent = CaptureEvent(
            LogEventLevel.Error,
            new InvalidOperationException("boom"),
            "Disc read failed");

        BugReportPayload payload = BugReportFormatter.Create(logEvent, "Desktop");

        Assert.Contains("=== Error Details ===", payload.Message, StringComparison.Ordinal);
        Assert.Contains("Level: Error", payload.Message, StringComparison.Ordinal);
        Assert.Contains("Source:", payload.Message, StringComparison.Ordinal);
        Assert.Contains("Message: Disc read failed", payload.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExceptionBlock_ContainsTypeMessageSourceAndStackTrace()
    {
        Exception exception;
        try
        {
            throw new InvalidOperationException("inner failure");
        }
        catch (InvalidOperationException caught)
        {
            exception = caught;
        }

        var block = BugReportFormatter.FormatException(exception);

        Assert.Contains("=== Exception Details ===", block, StringComparison.Ordinal);
        Assert.Contains("Type: System.InvalidOperationException", block, StringComparison.Ordinal);
        Assert.Contains("Message: inner failure", block, StringComparison.Ordinal);
        Assert.Contains("Source:", block, StringComparison.Ordinal);
        Assert.Contains("StackTrace:", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Payload_ContainsEveryFieldTheServiceAccepts()
    {
        LogEvent logEvent = CaptureEvent(
            LogEventLevel.Warning,
            new FormatException("bad disc"),
            "The disc could not be opened");

        BugReportPayload payload = BugReportFormatter.Create(logEvent, "CLI");
        var json = JsonSerializer.Serialize(payload, BugReportJsonContext.Default.BugReportPayload);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.True(root.TryGetProperty("message", out _));
        Assert.True(root.TryGetProperty("applicationName", out _));
        Assert.True(root.TryGetProperty("version", out _));
        Assert.True(root.TryGetProperty("userInfo", out _));
        Assert.True(root.TryGetProperty("environment", out _));
        Assert.True(root.TryGetProperty("stackTrace", out _));
        Assert.Contains("=== Environment Details ===", root.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("=== Error Details ===", root.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("=== Exception Details ===", root.GetProperty("stackTrace").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sink_SurvivesARejectedSubmission()
    {
        var handler = new CountingHandler(HttpStatusCode.InternalServerError);
        using var http = new HttpClient(handler);
        using var sink = new BugReportSink(Options(), http);

        sink.Emit(CaptureEvent(LogEventLevel.Error, exception: null, "rejected report"));
        await handler.WaitForCountAsync(1);

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task Sink_SurvivesATransportFailure()
    {
        var handler = new ThrowingHandler();
        using var http = new HttpClient(handler);
        using var sink = new BugReportSink(Options(), http);

        sink.Emit(CaptureEvent(LogEventLevel.Error, exception: null, "offline report"));
        await handler.WaitForCallAsync();

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task Sink_DisposeDrainsAQueuedReportAsync()
    {
        var handler = new DelayedHandler();
        using var http = new HttpClient(handler);
        var sink = new BugReportSink(Options(), http);
        try
        {
            sink.Emit(CaptureEvent(LogEventLevel.Warning, exception: null, "queued shutdown report"));
            // Windows CI runners can briefly starve the sink worker while the rest of
            // the suite is running; keep the drain wait consistent with the other
            // slow-worker hardening.
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));

            sink.Dispose();

            string body = await handler.Body.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Contains("queued shutdown report", body, StringComparison.Ordinal);
        }
        finally
        {
            sink.Dispose();
        }
    }

    private static BugReportOptions Options()
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
        logger.Write(level, exception, "{Message:l}", message);
        return sink.Last ?? throw new InvalidOperationException("No log event was captured.");
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public LogEvent? Last { get; private set; }

        public void Emit(LogEvent logEvent)
        {
            Last = logEvent;
        }
    }

    private sealed class DelayedHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<string> Body { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Body.TrySetResult(body);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class CountingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public async Task WaitForCountAsync(int expected)
        {
            try
            {
                await _done.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Assert.Fail($"Only {Count} of {expected} reports arrived.");
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _count);
            _done.TrySetResult();
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public async Task WaitForCallAsync()
        {
            try
            {
                await _done.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Assert.Fail("The sink did not attempt a submission.");
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _count);
            _done.TrySetResult();
            throw new HttpRequestException("offline");
        }
    }
}
