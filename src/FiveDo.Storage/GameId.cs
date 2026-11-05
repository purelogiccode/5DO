using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>
/// Computes the 3DO game identifier used by the 4DO game database.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security",
    "CA5351:Do Not Use Broken Cryptographic Algorithms",
    Justification = "MD5 of the first two sectors is the 3DO game identifier used by 4DO.")]
public static class GameId
{
    /// <summary>Computes the lowercase MD5 of the first two disc sectors.</summary>
    /// <param name="disc">Disc source.</param>
    /// <returns>32 hexadecimal characters.</returns>
    public static string Compute(IDiscSource disc)
    {
        ArgumentNullException.ThrowIfNull(disc);

        // Audio-only discs have no data sectors to hash. Use a stable TOC-derived ID
        // so the disc still gets save-state, cheat, and NVRAM keys.
        if (disc is ITrackedDiscSource { Tracks.Count: > 0 } tracked
            && tracked.Tracks.All(static track => track.IsAudio))
        {
            var toc = new StringBuilder("audio-cd");
            foreach (DiscTrack track in tracked.Tracks)
            {
                toc.Append('|').Append(track.Number).Append(':').Append(track.SectorCount);
            }

            return Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(toc.ToString())));
        }

        if (disc.SectorCount < 2)
        {
            throw new DiscImageException("A 3DO disc needs at least two sectors for a game ID.");
        }

        var data = new byte[4096];
        disc.ReadSector(0, data.AsSpan(0, 2048));
        disc.ReadSector(1, data.AsSpan(2048, 2048));
        return Convert.ToHexStringLower(MD5.HashData(data));
    }
}
