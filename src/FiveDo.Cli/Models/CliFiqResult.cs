namespace FiveDo.Cli;

/// <summary>
/// FIQ request-to-vector latency counters reported by <c>run --json</c>.
/// </summary>
internal sealed record CliFiqResult(
    long Requests,
    long Entries,
    long MatchedEntries,
    long UnmatchedEntries,
    long CoalescedRequests,
    long LastLatencyCycles,
    long MaxLatencyCycles,
    double AverageLatencyCycles);
