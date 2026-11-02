using System.Runtime.InteropServices;

namespace FiveDo.Core;

/// <summary>
/// A fast-interrupt request observed by the CLIO interrupt controller.
/// </summary>
/// <param name="Cycle">ARM clock cycle at which the request became pending.</param>
/// <param name="ActiveReason1">CLIO active-low reason register (<c>0x40</c>).</param>
/// <param name="ActiveReason2">CLIO active-high reason register (<c>0x60</c>).</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct FiqRequest(long Cycle, uint ActiveReason1, uint ActiveReason2);

/// <summary>
/// A fast-interrupt vector entry observed by the CPU.
/// </summary>
/// <param name="Cycle">ARM clock cycle at which the vector was entered.</param>
/// <param name="ReturnAddress">Value written to <c>LR_fiq</c> (<c>PC + 4</c>).</param>
/// <param name="ActiveReason1">CLIO active-low reason register at entry.</param>
/// <param name="ActiveReason2">CLIO active-high reason register at entry.</param>
/// <param name="LatencyCycles">
/// Cycles between the matched request and this entry, or zero when the entry had no
/// tracked request (a test-hook or level-triggered re-entry).
/// </param>
/// <param name="MatchedRequest">Whether this entry consumed a tracked request.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct FiqEntry(
    long Cycle,
    uint ReturnAddress,
    uint ActiveReason1,
    uint ActiveReason2,
    long LatencyCycles,
    bool MatchedRequest);

/// <summary>
/// Measures the interval between a CLIO FIQ request and ARM60 vector entry in ARM clock
/// cycles.
/// </summary>
/// <remarks>
/// <para>
/// The 3DO wires every device interrupt to CLIO's FIQ controller; there is no separate IRQ
/// line. A request becomes pending when the active and enable masks intersect. The CPU checks
/// the level after each instruction, so the minimum observed latency is the cycle count of the
/// instruction that was executing when the request became visible. Vector entry itself costs
/// no extra cycles, matching FreeDO and Opera.
/// </para>
/// <para>
/// The tracker is single-threaded and is owned by one <c>FiveDoMachine</c>. Requests that
/// arrive while one is already pending are coalesced because the CLIO interrupt model is
/// level-triggered; the earliest cycle is retained. Entries without a tracked request still
/// count as entries and are reported as unmatched so nested or level-triggered re-entries stay
/// visible.
/// </para>
/// </remarks>
public sealed class FiqLatencyTracker
{
    private FiqRequest? _pending;
    private long _coalescedRequests;

    /// <summary>Gets the number of pending transitions observed since the last reset.</summary>
    public long Requests { get; private set; }

    /// <summary>Gets the number of FIQ vector entries observed since the last reset.</summary>
    public long Entries { get; private set; }

    /// <summary>Gets the number of entries that consumed a tracked request.</summary>
    public long MatchedEntries { get; private set; }

    /// <summary>Gets the number of entries with no tracked request.</summary>
    public long UnmatchedEntries { get; private set; }

    /// <summary>Gets the number of requests coalesced into an already-pending request.</summary>
    public long CoalescedRequests => _coalescedRequests;

    /// <summary>Gets the latency of the most recent matched entry, or -1 when none has occurred.</summary>
    public long LastLatencyCycles { get; private set; } = -1;

    /// <summary>Gets the largest matched latency observed since the last reset.</summary>
    public long MaxLatencyCycles { get; private set; }

    /// <summary>Gets the sum of all matched latencies, used for the average.</summary>
    public long TotalLatencyCycles { get; private set; }

    /// <summary>Gets the mean matched latency in ARM clock cycles.</summary>
    public double AverageLatencyCycles =>
        MatchedEntries == 0 ? 0 : (double)TotalLatencyCycles / MatchedEntries;

    /// <summary>Gets the cycle at which the oldest unmatched request became pending.</summary>
    public long? PendingSinceCycle => _pending?.Cycle;

    /// <summary>Gets the number of requests currently waiting for a vector entry (zero or one).</summary>
    public int PendingRequests => _pending is null ? 0 : 1;

    /// <summary>Records a FIQ request transition.</summary>
    /// <param name="request">Request timestamp and active reason masks.</param>
    public void OnRequested(in FiqRequest request)
    {
        Requests++;
        if (_pending is { } pending)
        {
            _coalescedRequests++;
            if (request.Cycle < pending.Cycle)
            {
                _pending = request;
            }

            return;
        }

        _pending = request;
    }

    /// <summary>Records a FIQ vector entry and matches it to the oldest request.</summary>
    /// <param name="cycle">ARM clock cycle at which the vector was entered.</param>
    /// <param name="returnAddress">Value written to <c>LR_fiq</c>.</param>
    /// <param name="activeReason1">CLIO active-low reason register at entry.</param>
    /// <param name="activeReason2">CLIO active-high reason register at entry.</param>
    /// <returns>The entry record, including the matched latency.</returns>
    public FiqEntry OnEntered(
        long cycle,
        uint returnAddress,
        uint activeReason1,
        uint activeReason2)
    {
        Entries++;
        if (_pending is not { } request)
        {
            UnmatchedEntries++;
            return new FiqEntry(cycle, returnAddress, activeReason1, activeReason2, 0, false);
        }

        var latency = cycle - request.Cycle;
        if (latency < 0)
        {
            latency = 0;
        }

        _pending = null;
        MatchedEntries++;
        LastLatencyCycles = latency;
        TotalLatencyCycles += latency;
        if (latency > MaxLatencyCycles)
        {
            MaxLatencyCycles = latency;
        }

        return new FiqEntry(cycle, returnAddress, activeReason1, activeReason2, latency, true);
    }

    /// <summary>Clears all counters and any pending request.</summary>
    public void Reset()
    {
        _pending = null;
        _coalescedRequests = 0;
        Requests = 0;
        Entries = 0;
        MatchedEntries = 0;
        UnmatchedEntries = 0;
        LastLatencyCycles = -1;
        MaxLatencyCycles = 0;
        TotalLatencyCycles = 0;
    }
}
