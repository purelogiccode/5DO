using System;

namespace FiveDo.Host;

/// <summary>
/// Measures a running play session and adds the elapsed time to <see cref="LibraryState"/>.
/// </summary>
/// <remarks>
/// The tracker is host-side and UI-thread friendly. It never touches the emulation thread.
/// Call <see cref="Start"/> when emulation starts or resumes, <see cref="Stop"/> when it pauses
/// or stops, and <see cref="Flush"/> periodically so a crash loses at most one flush interval.
/// </remarks>
public sealed class LibraryPlaytimeTracker
{
    private readonly LibraryState _state;
    private readonly Action? _persist;
    private readonly Func<DateTime> _utcNow;
    private DateTime? _startedUtc;
    private string _gameId = string.Empty;
    private string _path = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="LibraryPlaytimeTracker"/> class.</summary>
    /// <param name="state">Library state that receives play time.</param>
    /// <param name="persist">Optional callback invoked after a session is flushed.</param>
    /// <param name="utcNow">Optional UTC clock used by tests.</param>
    public LibraryPlaytimeTracker(LibraryState state, Action? persist = null, Func<DateTime>? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
        _persist = persist;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>Gets a value indicating whether a session is being timed.</summary>
    public bool IsTracking => _startedUtc is not null;

    /// <summary>Gets the path of the game being timed, or an empty string.</summary>
    public string CurrentPath => _path;

    /// <summary>Starts timing a game. A different active game is stopped first.</summary>
    /// <param name="gameId">4DO game ID, or empty for an unknown disc.</param>
    /// <param name="path">Full disc path.</param>
    public void Start(string? gameId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (IsTracking)
        {
            if (string.Equals(_path, path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Stop();
        }

        _gameId = gameId?.Trim().ToUpperInvariant() ?? string.Empty;
        _path = path;
        _startedUtc = _utcNow();
    }

    /// <summary>Stops timing and adds the elapsed time to the library state.</summary>
    /// <returns>The elapsed time added.</returns>
    public TimeSpan Stop()
    {
        if (_startedUtc is null)
        {
            return TimeSpan.Zero;
        }

        var elapsed = _utcNow() - _startedUtc.Value;
        _startedUtc = null;
        if (elapsed > TimeSpan.Zero)
        {
            _state.AddPlaytime(_gameId, _path, elapsed);
        }

        _persist?.Invoke();
        return elapsed;
    }

    /// <summary>
    /// Adds the time elapsed so far and keeps the session running. This bounds the amount of
    /// play time lost if the process exits unexpectedly.
    /// </summary>
    /// <returns>The time added by this flush.</returns>
    public TimeSpan Flush()
    {
        if (_startedUtc is null)
        {
            return TimeSpan.Zero;
        }

        DateTime now = _utcNow();
        TimeSpan elapsed = now - _startedUtc.Value;
        _startedUtc = now;
        if (elapsed > TimeSpan.Zero)
        {
            _state.AddPlaytime(_gameId, _path, elapsed);
            _persist?.Invoke();
        }

        return elapsed;
    }
}
