using System;
using System.Collections.Generic;
using System.Linq;

namespace FiveDo.Cheats;

/// <summary>The JSON document stored for one game's cheats.</summary>
public sealed class CheatDocument
{
    /// <summary>Gets or sets the document format version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the 4DO game ID, or <c>global</c> for a shared file.</summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary>Gets or sets the cheat entries in display order.</summary>
    public IList<CheatEntry> Cheats { get; set; } = new List<CheatEntry>();

    /// <summary>Creates a document from the current entries of a cheat set.</summary>
    /// <param name="gameId">Game ID or an empty string for the global file.</param>
    /// <param name="set">Cheat set to copy.</param>
    /// <returns>A serializable document.</returns>
    public static CheatDocument FromSet(string gameId, CheatSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return new CheatDocument
        {
            GameId = gameId,
            Cheats = set.Entries.Select(entry => entry.Clone()).ToList(),
        };
    }
}
