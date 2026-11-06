using System;

namespace FiveDo.Storage;

/// <summary>
/// Applies the documented in-memory RSA check bypass to a 3DO BIOS image.
/// </summary>
/// <remarks>
/// <para>
/// Retail 3DO BIOS images test the media RSA signature at two sites. The community
/// documented patch changes the second word of each site from <c>000111A0</c> to
/// <c>000011A0</c>; the similar <c>000213A0000111A0</c> sequence must be left alone.
/// </para>
/// <para>
/// 5DO applies this patch only when the user explicitly enables unsigned-disc support
/// and never writes the BIOS file. Applying it to a retail FZ-10 or FZ-10E image
/// reproduces the known No-RSA image MD5 exactly.
/// </para>
/// </remarks>
public static class BiosRsaPatch
{
    private static readonly byte[] SearchPattern = [0x00, 0x03, 0x13, 0xA0, 0x00, 0x01, 0x11, 0xA0];
    private static readonly byte[] PatchedPattern = [0x00, 0x03, 0x13, 0xA0, 0x00, 0x00, 0x11, 0xA0];

    /// <summary>Gets the length in bytes of one RSA patch site.</summary>
    public const int SiteLength = 8;

    /// <summary>Counts the RSA patch sites in a BIOS image.</summary>
    /// <param name="bios">1 MB BIOS image bytes.</param>
    /// <returns>The number of unpatched sites; zero for an already No-RSA image.</returns>
    public static int CountSites(ReadOnlySpan<byte> bios)
    {
        var count = 0;
        var offset = 0;
        while (offset <= bios.Length - SiteLength)
        {
            var index = bios[offset..].IndexOf(SearchPattern);
            if (index < 0)
            {
                break;
            }

            count++;
            offset += index + SiteLength;
        }

        return count;
    }

    /// <summary>Patches every RSA check site in a BIOS image in place.</summary>
    /// <param name="bios">Writable 1 MB BIOS image bytes.</param>
    /// <returns>The number of sites patched; zero when the image is already patched.</returns>
    public static int Apply(Span<byte> bios)
    {
        var count = 0;
        var offset = 0;
        while (offset <= bios.Length - SiteLength)
        {
            var index = bios[offset..].IndexOf(SearchPattern);
            if (index < 0)
            {
                break;
            }

            index += offset;
            PatchedPattern.CopyTo(bios[index..]);
            count++;
            offset = index + SiteLength;
        }

        return count;
    }
}
