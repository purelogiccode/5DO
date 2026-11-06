using System;
using System.Collections.Generic;

namespace FiveDo.Storage;

/// <summary>
/// Metadata for a known 3DO BIOS or BIOS2 image.
/// </summary>
/// <param name="Name">Human-readable profile name.</param>
/// <param name="Kind">Primary or secondary role.</param>
/// <param name="Territory">Target territory.</param>
/// <param name="VideoStandard">Target video standard.</param>
/// <param name="IsKanji">Whether the image provides Kanji font data.</param>
/// <param name="IsAnvil">Whether the Anvil workaround is required.</param>
/// <param name="IsPatched">Whether the image carries an arcade/unsigned-disc patch.</param>
/// <param name="Notes">Compatibility notes.</param>
/// <param name="Preference">Higher values win when two profiles are otherwise equivalent.</param>
/// <param name="IsRsaDisabled">Whether the image has the media RSA signature check disabled.</param>
public sealed record BiosDescriptor(
    string Name,
    BiosImageKind Kind,
    BiosTerritory Territory,
    BiosVideoStandard VideoStandard,
    bool IsKanji,
    bool IsAnvil,
    bool IsPatched,
    string Notes,
    int Preference,
    bool IsRsaDisabled = false);

/// <summary>
/// A lookup table of known 3DO BIOS MD5 hashes and their metadata.
/// </summary>
/// <remarks>
/// The hashes are the widely published 3DO BIOS identifiers used by 4DO, Opera, and the
/// Redump/MAME communities. Unknown images are still accepted by <see cref="BiosLibrary"/>,
/// but their metadata is inferred from the file name and is marked as unverified.
/// </remarks>
public sealed class BiosCatalog
{
    private readonly Dictionary<string, BiosDescriptor> _byMd5 = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the built-in catalog of known 3DO BIOS images.</summary>
    public static BiosCatalog Default { get; } = CreateDefault();

    /// <summary>Initializes an empty catalog.</summary>
    public BiosCatalog()
    {
    }

    /// <summary>Initializes a catalog with the supplied descriptors.</summary>
    /// <param name="descriptors">Descriptors keyed by MD5.</param>
    public BiosCatalog(IEnumerable<KeyValuePair<string, BiosDescriptor>> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        foreach ((var md5, BiosDescriptor descriptor) in descriptors)
        {
            Add(md5, descriptor);
        }
    }

    /// <summary>Adds or replaces a known BIOS descriptor.</summary>
    /// <param name="md5">Lowercase or uppercase MD5 hash.</param>
    /// <param name="descriptor">Descriptor for the image.</param>
    /// <exception cref="ArgumentException">Thrown when the hash is empty.</exception>
    public void Add(string md5, BiosDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(md5);
        ArgumentNullException.ThrowIfNull(descriptor);
        _byMd5[md5.Trim()] = descriptor;
    }

    /// <summary>Looks up a descriptor by MD5.</summary>
    /// <param name="md5">Lowercase or uppercase MD5 hash.</param>
    /// <param name="descriptor">Receives the descriptor when found.</param>
    /// <returns><see langword="true"/> when the hash is known.</returns>
    public bool TryGet(string md5, out BiosDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(md5);
        return _byMd5.TryGetValue(md5.Trim(), out descriptor!);
    }

    /// <summary>Gets the number of known images.</summary>
    public int Count => _byMd5.Count;

    private static BiosCatalog CreateDefault()
    {
        var catalog = new BiosCatalog();
        catalog.Add(
            "f47264dd47fe30f73ab3c010015c155b",
            new BiosDescriptor(
                "Panasonic FZ-1",
                BiosImageKind.Primary,
                BiosTerritory.Usa,
                BiosVideoStandard.Ntsc,
                IsKanji: false,
                IsAnvil: false,
                IsPatched: false,
                Notes: "Retail Panasonic FZ-1 BIOS. The default 4DO/5DO compatibility baseline.",
                Preference: 30));
        catalog.Add(
            "a496cfdded3da562759be3561317b605",
            new BiosDescriptor(
                "Panasonic FZ-1J",
                BiosImageKind.Primary,
                BiosTerritory.Japan,
                BiosVideoStandard.Ntsc,
                IsKanji: false,
                IsAnvil: false,
                IsPatched: false,
                Notes: "Japanese FZ-1 retail BIOS. Pair with the FZ-1J Kanji font ROM when text needs Kanji.",
                Preference: 42));
        catalog.Add(
            "f6c71de7470d16abe4f71b1444883dc8",
            new BiosDescriptor(
                "Panasonic FZ-1J (No RSA)",
                BiosImageKind.Primary,
                BiosTerritory.Japan,
                BiosVideoStandard.Ntsc,
                IsKanji: false,
                IsAnvil: false,
                IsPatched: true,
                Notes: "Japanese FZ-1J image without the RSA signature check. Use only when unsigned-disc support is explicitly needed.",
                Preference: 41,
                IsRsaDisabled: true));
        catalog.Add(
            "51f2f43ae2f3508a14d9f56597e2d3ce",
            new BiosDescriptor(
                "Panasonic FZ-10",
                BiosImageKind.Primary,
                BiosTerritory.Usa,
                BiosVideoStandard.Ntsc,
                IsKanji: false,
                IsAnvil: false,
                IsPatched: false,
                Notes: "Retail Panasonic FZ-10 BIOS. Preferred for some US titles that need the newer ROM.",
                Preference: 40));
        catalog.Add(
            "20706f726c22d79d3c385e215d7428e2",
            new BiosDescriptor(
                "Panasonic FZ-10J",
                BiosImageKind.Primary,
                BiosTerritory.Japan,
                BiosVideoStandard.Ntsc,
                IsKanji: false,
                IsAnvil: false,
                IsPatched: false,
                Notes: "Japanese FZ-10 retail BIOS. Pair with the FZ-10J Kanji font ROM when text needs Kanji.",
                Preference: 44));
        catalog.Add(
            "a48e6746bd7edec0f40cff078f0bb19f",
            new BiosDescriptor(
                "Panasonic FZ-10E (Anvil)",
                BiosImageKind.Primary,
                BiosTerritory.Europe,
                BiosVideoStandard.Pal,
                IsKanji: false,
                IsAnvil: true,
                IsPatched: false,
                Notes: "European FZ-10E BIOS with the Anvil workaround.",
                Preference: 40));
        catalog.Add(
            "428577250f43edc902ea239c50d2240d",
            new BiosDescriptor(
                "Panasonic FZ-10JA Kanji font (Anvil)",
                BiosImageKind.Secondary,
                BiosTerritory.Japan,
                BiosVideoStandard.Ntsc,
                IsKanji: true,
                IsAnvil: true,
                IsPatched: false,
                Notes: "Japanese FZ-10JA Kanji font ROM (BIOS2) for the Anvil/Japanese BIOS. Not a bootable primary image.",
                Preference: 35));
        catalog.Add(
            "8639fd5e549bd6238cfee79e3e749114",
            new BiosDescriptor(
                "GoldStar 3DO",
                BiosImageKind.Primary,
                BiosTerritory.Usa,
                BiosVideoStandard.Ntsc,
                IsKanji: false,
                IsAnvil: false,
                IsPatched: false,
                Notes: "GoldStar 3DO retail BIOS.",
                Preference: 20));
        catalog.Add(
            "35fa1a1ebaaeea286dc5cd15487c13ea",
            new BiosDescriptor(
                "Sanyo TRY",
                BiosImageKind.Primary,
                BiosTerritory.Japan,
                BiosVideoStandard.Ntsc,
                IsKanji: false,
                IsAnvil: false,
                IsPatched: false,
                Notes: "Sanyo TRY retail BIOS.",
                Preference: 20));
        catalog.Add(
            "1477bda80dc33731a65468c1f5bcbee9",
            new BiosDescriptor(
                "Panasonic FZ-10 (No RSA)",
                BiosImageKind.Primary,
                BiosTerritory.Usa,
                BiosVideoStandard.Ntsc,
                IsKanji: false,
                IsAnvil: false,
                IsPatched: true,
                Notes: "FZ-10 image with the RSA signature check disabled. Used for unsigned, homebrew, and Arcade CD-ROM titles. Not a general-purpose default.",
                Preference: 50,
                IsRsaDisabled: true));
        catalog.Add(
            "cf11bbb5a16d7af9875cca9de9a15e09",
            new BiosDescriptor(
                "Panasonic FZ-10E (Anvil, No RSA)",
                BiosImageKind.Primary,
                BiosTerritory.Europe,
                BiosVideoStandard.Pal,
                IsKanji: false,
                IsAnvil: true,
                IsPatched: true,
                Notes: "European FZ-10E image with the Anvil workaround and the RSA signature check disabled. Used for unsigned, homebrew, and Arcade CD-ROM titles. Not a general-purpose default.",
                Preference: 50,
                IsRsaDisabled: true));
        catalog.Add(
            "c23fb5d5e6bb1c240d02cf968972be37",
            new BiosDescriptor(
                "Panasonic FZ-1J Kanji font",
                BiosImageKind.Secondary,
                BiosTerritory.Japan,
                BiosVideoStandard.Ntsc,
                IsKanji: true,
                IsAnvil: false,
                IsPatched: false,
                Notes: "Japanese FZ-1J Kanji font ROM (BIOS2). Pair with the FZ-1J BIOS.",
                Preference: 36));
        catalog.Add(
            "b8dc97f778a6245c58e064b0312e8281",
            new BiosDescriptor(
                "Panasonic FZ-1 Kanji font",
                BiosImageKind.Secondary,
                BiosTerritory.Japan,
                BiosVideoStandard.Ntsc,
                IsKanji: true,
                IsAnvil: false,
                IsPatched: false,
                Notes: "Optional Kanji font ROM (BIOS2) for Japanese text with the FZ-1 BIOS.",
                Preference: 30));
        catalog.Add(
            "8970fc987ab89a7f64da9f8a8c4333ff",
            new BiosDescriptor(
                "3DO Arcade (Shootout at Old Tucson)",
                BiosImageKind.Secondary,
                BiosTerritory.Arcade,
                BiosVideoStandard.Ntsc,
                IsKanji: false,
                IsAnvil: false,
                IsPatched: true,
                Notes: "512 KB arcade support ROM. Pair with the patched FZ-10 image for arcade discs.",
                Preference: 10));
        return catalog;
    }
}
