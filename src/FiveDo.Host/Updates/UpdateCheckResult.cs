namespace FiveDo.Host;

/// <summary>
/// Outcome of one GitHub release check.
/// </summary>
/// <param name="Info">Release information, or <see langword="null"/> when the check failed.</param>
/// <param name="Error">Failure message, or <see langword="null"/> on success.</param>
/// <param name="FromCache">Whether the result came from the local update-check cache.</param>
public sealed record UpdateCheckResult(UpdateInfo? Info, string? Error, bool FromCache)
{
    /// <summary>Gets a value indicating whether the check produced release information.</summary>
    public bool Ok => Info is not null;
}
