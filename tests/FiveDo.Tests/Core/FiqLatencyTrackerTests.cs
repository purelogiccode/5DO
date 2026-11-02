using FiveDo.Core;

namespace FiveDo.Tests.Core;

public sealed class FiqLatencyTrackerTests
{
    [Fact]
    public void MatchedEntry_RecordsLatencyAndCounters()
    {
        var tracker = new FiqLatencyTracker();
        tracker.OnRequested(new FiqRequest(100, 1u << 9, 0));

        var entry = tracker.OnEntered(106, 0x1008, 1u << 9, 0);

        Assert.True(entry.MatchedRequest);
        Assert.Equal(6, entry.LatencyCycles);
        Assert.Equal(1, tracker.Requests);
        Assert.Equal(1, tracker.Entries);
        Assert.Equal(1, tracker.MatchedEntries);
        Assert.Equal(0, tracker.UnmatchedEntries);
        Assert.Equal(6, tracker.LastLatencyCycles);
        Assert.Equal(6, tracker.MaxLatencyCycles);
        Assert.Equal(6.0, tracker.AverageLatencyCycles);
        Assert.Equal(0, tracker.PendingRequests);
    }

    [Fact]
    public void RequestsWhilePending_CoalesceButKeepTheEarliestCycle()
    {
        var tracker = new FiqLatencyTracker();
        tracker.OnRequested(new FiqRequest(100, 1u << 9, 0));
        tracker.OnRequested(new FiqRequest(300, 0, 1u << 3));

        Assert.Equal(2, tracker.Requests);
        Assert.Equal(1, tracker.CoalescedRequests);
        Assert.Equal(100, tracker.PendingSinceCycle);

        var entry = tracker.OnEntered(400, 0x1008, 1u << 9, 1u << 3);

        Assert.True(entry.MatchedRequest);
        Assert.Equal(300, entry.LatencyCycles);
        Assert.Equal(1u << 9, entry.ActiveReason1);
        Assert.Equal(1u << 3, entry.ActiveReason2);
    }

    [Fact]
    public void EntryWithoutRequest_CountsAsUnmatchedAndKeepsLatencyZero()
    {
        var tracker = new FiqLatencyTracker();

        var entry = tracker.OnEntered(50, 0x1008, 1u << 0, 0);

        Assert.False(entry.MatchedRequest);
        Assert.Equal(0, entry.LatencyCycles);
        Assert.Equal(1, tracker.Entries);
        Assert.Equal(0, tracker.MatchedEntries);
        Assert.Equal(1, tracker.UnmatchedEntries);
        Assert.Equal(-1, tracker.LastLatencyCycles);
        Assert.Equal(0, tracker.AverageLatencyCycles);
    }

    [Fact]
    public void Reset_ClearsCountersAndPendingRequest()
    {
        var tracker = new FiqLatencyTracker();
        tracker.OnRequested(new FiqRequest(10, 1, 0));
        tracker.OnEntered(12, 0x1008, 1, 0);
        tracker.OnRequested(new FiqRequest(20, 1, 0));

        tracker.Reset();

        Assert.Equal(0, tracker.Requests);
        Assert.Equal(0, tracker.Entries);
        Assert.Equal(0, tracker.MatchedEntries);
        Assert.Equal(0, tracker.UnmatchedEntries);
        Assert.Equal(0, tracker.CoalescedRequests);
        Assert.Equal(-1, tracker.LastLatencyCycles);
        Assert.Equal(0, tracker.MaxLatencyCycles);
        Assert.Equal(0, tracker.TotalLatencyCycles);
        Assert.Null(tracker.PendingSinceCycle);
    }

    [Fact]
    public void NonMonotonicClock_ClampsLatencyAtZero()
    {
        var tracker = new FiqLatencyTracker();
        tracker.OnRequested(new FiqRequest(100, 1, 0));

        var entry = tracker.OnEntered(90, 0x1008, 1, 0);

        Assert.True(entry.MatchedRequest);
        Assert.Equal(0, entry.LatencyCycles);
        Assert.Equal(0, tracker.MaxLatencyCycles);
    }
}
