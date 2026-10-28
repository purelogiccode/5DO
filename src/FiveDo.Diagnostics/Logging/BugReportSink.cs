using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace FiveDo.Diagnostics.Logging;

/// <summary>
/// Forwards warning-or-higher Serilog events to the bug-report service.
/// </summary>
/// <remarks>
/// <para>
/// Submission happens on a background worker over a bounded channel, so a slow or
/// unreachable endpoint never blocks the emulator, renderer, or audio thread. Identical
/// messages are suppressed for <see cref="BugReportOptions.DedupeWindow"/> and submissions
/// are capped at the service's ten-per-minute limit.
/// </para>
/// <para>
/// The payload includes the required environment, error, and exception blocks; see
/// <see cref="BugReportFormatter"/>.
/// </para>
/// </remarks>
public sealed class BugReportSink : ILogEventSink, IDisposable
{
    private readonly BugReportOptions _options;
    private readonly Channel<BugReportPayload> _queue;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _recent = new(StringComparer.Ordinal);
    private readonly Queue<DateTimeOffset> _sent = new();
    private readonly Lock _rateGate = new();
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="BugReportSink"/> class.</summary>
    /// <param name="options">Endpoint, credentials, and throttling options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the endpoint or API key is empty.</exception>
    public BugReportSink(BugReportOptions options)
        : this(options, httpClient: null)
    {
    }

    internal BugReportSink(BugReportOptions options, HttpClient? httpClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Endpoint, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ApiKey, nameof(options));
        if (options.MaxRequestsPerMinute <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The rate limit must be positive.");
        }

        if (options.QueueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The queue capacity must be positive.");
        }

        _options = options;
        _http = httpClient ?? new HttpClient { Timeout = options.HttpTimeout };
        _ownsHttp = httpClient is null;
        _queue = Channel.CreateBounded<BugReportPayload>(new BoundedChannelOptions(options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        _worker = Task.Run(ProcessAsync);
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        if (logEvent.Level < LogEventLevel.Warning)
        {
            return;
        }

        var key = CreateDedupeKey(logEvent);
        var now = DateTimeOffset.UtcNow;
        if (_recent.TryGetValue(key, out DateTimeOffset last) && now - last < _options.DedupeWindow)
        {
            return;
        }

        _recent[key] = now;
        if (_recent.Count > 256)
        {
            Prune(now);
        }

        _queue.Writer.TryWrite(BugReportFormatter.Create(logEvent, _options.ApplicationMode));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.Writer.TryComplete();
        _stop.Cancel();
        try
        {
            _ = _worker.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The worker is best-effort; shutdown must not throw.
        }

        if (_ownsHttp)
        {
            _http.Dispose();
        }

        _stop.Dispose();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The reporting worker must never crash the emulator or host application.")]
    private async Task ProcessAsync()
    {
        try
        {
            await foreach (BugReportPayload payload in _queue.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false))
            {
                if (!TryAcquireRateSlot())
                {
                    SelfLog.WriteLine("Bug report dropped: the {0}-per-minute limit was reached.", _options.MaxRequestsPerMinute);
                    continue;
                }

                await SendAsync(payload).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SelfLog.WriteLine("Bug report worker stopped: {0}", exception);
        }
    }

    private async Task SendAsync(BugReportPayload payload)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
            {
                Content = JsonContent.Create(payload, BugReportJsonContext.Default.BugReportPayload),
            };
            request.Headers.TryAddWithoutValidation("X-API-KEY", _options.ApiKey);
            using HttpResponseMessage response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _stop.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                SelfLog.WriteLine(
                    "Bug report rejected with HTTP {0} ({1}).",
                    (int)response.StatusCode,
                    response.ReasonPhrase);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException
            or InvalidOperationException
            or NotSupportedException)
        {
            SelfLog.WriteLine("Bug report submission failed: {0}", exception);
        }
    }

    private bool TryAcquireRateSlot()
    {
        lock (_rateGate)
        {
            var now = DateTimeOffset.UtcNow;
            while (_sent.Count > 0 && now - _sent.Peek() >= TimeSpan.FromMinutes(1))
            {
                _ = _sent.Dequeue();
            }

            if (_sent.Count >= _options.MaxRequestsPerMinute)
            {
                return false;
            }

            _sent.Enqueue(now);
            return true;
        }
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (KeyValuePair<string, DateTimeOffset> pair in _recent)
        {
            if (now - pair.Value >= _options.DedupeWindow)
            {
                _ = _recent.TryRemove(pair.Key, out _);
            }
        }
    }

    private static string CreateDedupeKey(LogEvent logEvent)
    {
        var exceptionType = logEvent.Exception?.GetType().FullName ?? string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{logEvent.Level}|{exceptionType}|{logEvent.RenderMessage(CultureInfo.InvariantCulture)}");
    }
}
