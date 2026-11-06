using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>
/// Scans a folder for 3DO BIOS images and selects a region-appropriate set for a game.
/// </summary>
/// <remarks>
/// The library never copies or modifies ROMs. It records the MD5, size, role, territory,
/// video standard, Kanji support, Anvil requirement, and patch state for each file. Known
/// images come from <see cref="BiosCatalog.Default"/>; unknown images are accepted but their
/// metadata is inferred from the file name and marked as unverified in <see cref="BiosProfile.Notes"/>.
/// </remarks>
[SuppressMessage(
    "Security",
    "CA5351:Do Not Use Broken Cryptographic Algorithms",
    Justification = "MD5 is the 3DO BIOS identifier used by 4DO, Opera, and the community.")]
public sealed class BiosLibrary
{
    private static readonly string[] Extensions = [".bin", ".rom"];

    private readonly List<BiosProfile> _profiles = [];
    private readonly List<string> _errors = [];
    private readonly BiosCatalog _catalog;

    private BiosLibrary(BiosCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Gets the recognized primary and secondary images.</summary>
    public IReadOnlyList<BiosProfile> Profiles => _profiles;

    /// <summary>Gets primary images only.</summary>
    public IReadOnlyList<BiosProfile> Primaries => _profiles.Where(profile => profile.IsPrimary).ToArray();

    /// <summary>Gets secondary images only.</summary>
    public IReadOnlyList<BiosProfile> Secondaries => _profiles.Where(profile => profile.IsSecondary).ToArray();

    /// <summary>Gets files that could not be classified.</summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>Scans a folder for BIOS images.</summary>
    /// <param name="folder">Folder to scan.</param>
    /// <param name="catalog">Optional known-image catalog. Defaults to <see cref="BiosCatalog.Default"/>.</param>
    /// <param name="recursive">Whether to scan subfolders.</param>
    /// <returns>The scanned library. Individual failures are reported through <see cref="Errors"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="folder"/> is empty.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the folder does not exist.</exception>
    public static BiosLibrary Scan(string folder, BiosCatalog? catalog = null, bool recursive = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var resolved = Path.GetFullPath(folder);
        if (!Directory.Exists(resolved))
        {
            throw new DirectoryNotFoundException($"BIOS folder '{folder}' does not exist.");
        }

        var library = new BiosLibrary(catalog ?? BiosCatalog.Default);
        var options = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var path in Directory.EnumerateFiles(resolved, "*", options).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (!Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                library._profiles.Add(Describe(path, library._catalog));
            }
            catch (Exception exception) when (exception is IOException
                or InvalidDataException
                or UnauthorizedAccessException
                or ArgumentException)
            {
                library._errors.Add($"{path}: {exception.Message}");
            }
        }

        library._profiles.Sort(static (left, right) =>
        {
            var kind = left.Kind.CompareTo(right.Kind);
            if (kind != 0)
            {
                return kind;
            }

            var territory = left.Territory.CompareTo(right.Territory);
            if (territory != 0)
            {
                return territory;
            }

            var name = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            return name != 0 ? name : string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
        });

        return library;
    }

    /// <summary>Reads and classifies one BIOS file.</summary>
    /// <param name="path">Path to a 1 MB BIOS or a smaller BIOS2/font ROM.</param>
    /// <param name="catalog">Optional known-image catalog. Defaults to <see cref="BiosCatalog.Default"/>.</param>
    /// <returns>The classified profile.</returns>
    /// <exception cref="InvalidDataException">Thrown when the file size is not a supported BIOS size.</exception>
    public static BiosProfile Describe(string path, BiosCatalog? catalog = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var resolved = Path.GetFullPath(path);
        var bytes = File.ReadAllBytes(resolved);
        var fileName = Path.GetFileName(resolved);
        var md5 = Convert.ToHexStringLower(MD5.HashData(bytes));
        var knownCatalog = catalog ?? BiosCatalog.Default;
        var isKnown = knownCatalog.TryGet(md5, out BiosDescriptor descriptor);

        if (bytes.Length == EmulatorConstants.RomBankSize)
        {
            if (IsKanjiFontImage(bytes))
            {
                if (isKnown)
                {
                    return CreateProfile(
                        resolved,
                        fileName,
                        md5,
                        bytes.Length,
                        descriptor,
                        descriptor.IsAnvil);
                }

                var fontHeuristic = ClassifyFileName(fileName, isAnvil: false);
                return CreateProfile(
                    resolved,
                    fileName,
                    md5,
                    bytes.Length,
                    new BiosDescriptor(
                        Path.GetFileNameWithoutExtension(fileName),
                        BiosImageKind.Secondary,
                        BiosTerritory.Japan,
                        BiosVideoStandard.Ntsc,
                        IsKanji: true,
                        IsAnvil: fontHeuristic.IsAnvil,
                        IsPatched: false,
                        "Unrecognized 1 MB Kanji font ROM (volume-header signature). Treat as BIOS2, not a primary BIOS.",
                        Preference: 0),
                    isAnvil: false);
            }

            BiosImage image = BiosImage.FromBytes(bytes);
            var heuristic = ClassifyFileName(fileName, image.IsAnvil);
            if (isKnown)
            {
                return CreateProfile(
                    resolved,
                    fileName,
                    md5,
                    bytes.Length,
                    descriptor,
                    image.IsAnvil || descriptor.IsAnvil);
            }

            return CreateProfile(
                resolved,
                fileName,
                md5,
                bytes.Length,
                new BiosDescriptor(
                    Path.GetFileNameWithoutExtension(fileName),
                    BiosImageKind.Primary,
                    heuristic.Territory,
                    heuristic.VideoStandard,
                    heuristic.IsKanji,
                    heuristic.IsAnvil,
                    heuristic.IsPatched,
                    "Unrecognized 1 MB BIOS. Metadata was inferred from the file name; verify the image before use.",
                    Preference: 0,
                    IsRsaDisabled: heuristic.IsRsaDisabled),
                image.IsAnvil);
        }

        if (bytes.Length > 0 && bytes.Length < EmulatorConstants.RomBankSize)
        {
            var heuristic = ClassifyFileName(fileName, isAnvil: false);
            if (isKnown)
            {
                return CreateProfile(
                    resolved,
                    fileName,
                    md5,
                    bytes.Length,
                    descriptor,
                    descriptor.IsAnvil);
            }

            return CreateProfile(
                resolved,
                fileName,
                md5,
                bytes.Length,
                new BiosDescriptor(
                    Path.GetFileNameWithoutExtension(fileName),
                    BiosImageKind.Secondary,
                    heuristic.Territory,
                    heuristic.VideoStandard,
                    heuristic.IsKanji,
                    heuristic.IsAnvil,
                    heuristic.IsPatched,
                    "Unrecognized secondary ROM. Verify that it is a 3DO BIOS2, Kanji font, or arcade support image.",
                    Preference: 0,
                    IsRsaDisabled: heuristic.IsRsaDisabled),
                isAnvil: false);
        }

        throw new InvalidDataException(
            $"BIOS file '{fileName}' is {bytes.Length} bytes; expected 1 MB or a smaller BIOS2/font ROM.");
    }

    /// <summary>Finds a primary image by MD5, preferring the highest-ranked duplicate.</summary>
    /// <param name="md5">Lowercase or uppercase MD5.</param>
    /// <returns>The matching profile, or <see langword="null"/>.</returns>
    public BiosProfile? FindPrimary(string md5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(md5);
        return Primaries
            .Where(profile => string.Equals(profile.Md5, md5, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(profile => profile.Preference)
            .ThenBy(profile => profile.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>Finds a secondary image by MD5, preferring the highest-ranked duplicate.</summary>
    /// <param name="md5">Lowercase or uppercase MD5.</param>
    /// <returns>The matching profile, or <see langword="null"/>.</returns>
    public BiosProfile? FindSecondary(string md5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(md5);
        return Secondaries
            .Where(profile => string.Equals(profile.Md5, md5, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(profile => profile.Preference)
            .ThenBy(profile => profile.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>Finds a primary image by MD5 or file name, preferring the highest-ranked duplicate.</summary>
    /// <param name="value">MD5 or basename.</param>
    /// <returns>The matching profile, or <see langword="null"/>.</returns>
    public BiosProfile? FindPrimaryByMd5OrName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        BiosProfile? byMd5 = FindPrimary(value);
        if (byMd5 is not null)
        {
            return byMd5;
        }

        var fileName = Path.GetFileName(value);
        return Primaries
            .Where(profile => string.Equals(Path.GetFileName(profile.Path), fileName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(profile => profile.Preference)
            .ThenBy(profile => profile.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>Finds a secondary image by MD5 or file name, preferring the highest-ranked duplicate.</summary>
    /// <param name="value">MD5 or basename.</param>
    /// <returns>The matching profile, or <see langword="null"/>.</returns>
    public BiosProfile? FindSecondaryByMd5OrName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        BiosProfile? byMd5 = FindSecondary(value);
        if (byMd5 is not null)
        {
            return byMd5;
        }

        var fileName = Path.GetFileName(value);
        return Secondaries
            .Where(profile => string.Equals(Path.GetFileName(profile.Path), fileName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(profile => profile.Preference)
            .ThenBy(profile => profile.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>Selects a primary BIOS and optional secondary ROM for a game.</summary>
    /// <param name="game">Game metadata, or <see langword="null"/> for an unknown disc.</param>
    /// <param name="options">Optional selection overrides.</param>
    /// <returns>The selected BIOS set and the reason for the choice.</returns>
    public BiosSelection SelectForGame(GameRecord? game, BiosSelectionOptions? options = null)
    {
        options ??= new BiosSelectionOptions();
        IReadOnlyList<BiosTerritory> desired = DesiredTerritoriesForGame(game, options.DiscName);
        var discFileName = string.IsNullOrWhiteSpace(options.DiscName)
            ? null
            : Path.GetFileName(options.DiscName);
        var isArcade = game?.Name.Contains("Arcade", StringComparison.OrdinalIgnoreCase) == true
            || discFileName?.Contains("Arcade", StringComparison.OrdinalIgnoreCase) == true;
        var allowPatched = options.AllowPatched || isArcade;
        var allowUnsignedDiscs = options.AllowUnsignedDiscs;

        if (!string.IsNullOrWhiteSpace(options.PreferredMd5))
        {
            BiosProfile? overrideProfile = FindPrimary(options.PreferredMd5);
            if (overrideProfile is not null)
            {
                BiosProfile? overrideSecondary = ChooseSecondary(overrideProfile, desired, options);
                return new BiosSelection(
                    overrideProfile,
                    overrideSecondary,
                    $"Using the per-game BIOS override {overrideProfile.Name} ({overrideProfile.Md5[..8]}).");
            }
        }

        if (!string.IsNullOrWhiteSpace(game?.Bios))
        {
            BiosProfile? databaseProfile = FindPrimaryByMd5OrName(game.Bios);
            if (databaseProfile is not null)
            {
                BiosProfile? databaseSecondary = string.IsNullOrWhiteSpace(game.Bios2)
                    ? ChooseSecondary(databaseProfile, desired, options)
                    : FindSecondaryByMd5OrName(game.Bios2) ?? ChooseSecondary(databaseProfile, desired, options);
                return new BiosSelection(
                    databaseProfile,
                    databaseSecondary,
                    $"Using the game database BIOS requirement for {game.Name}: {databaseProfile.Name}. {databaseProfile.Notes}");
            }
        }

        var knownMd5 = BiosGameRequirements.PreferredMd5(options.DiscName);
        if (!string.IsNullOrWhiteSpace(knownMd5))
        {
            BiosProfile? knownProfile = FindPrimary(knownMd5);
            if (knownProfile is not null)
            {
                BiosProfile? knownSecondary = ChooseSecondary(knownProfile, desired, options);
                return new BiosSelection(
                    knownProfile,
                    knownSecondary,
                    $"Using the known BIOS requirement for {game?.Name ?? Path.GetFileNameWithoutExtension(options.DiscName)}: {knownProfile.Name}. {knownProfile.Notes}");
            }
        }

        var candidates = Primaries
            .Where(profile => (allowPatched
                    || (allowUnsignedDiscs && profile.IsRsaDisabled)
                    || !profile.IsPatched)
                && (!options.RequireKanji || profile.IsKanji))
            .ToList();
        var fallback = false;
        if (candidates.Count == 0)
        {
            if (options.RequireKanji)
            {
                return BiosSelection.None("No Kanji-capable primary BIOS was found.");
            }

            candidates = Primaries.ToList();
            fallback = true;
        }

        if (candidates.Count == 0)
        {
            return BiosSelection.None("No primary BIOS images were found in the BIOS folder.");
        }

        BiosProfile best = candidates
            .OrderByDescending(profile => Score(profile, desired, isArcade, options))
            .ThenByDescending(profile => profile.Preference)
            .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(profile => profile.Path, StringComparer.OrdinalIgnoreCase)
            .First();
        BiosProfile? secondary = ChooseSecondary(best, desired, options);
        var reason = BuildReason(best, secondary, desired, fallback, isArcade);
        return new BiosSelection(best, secondary, reason);
    }

    /// <summary>Maps 4DO region codes to BIOS territories.</summary>
    /// <param name="regions">Region string such as <c>US</c>, <c>JP</c>, <c>EU</c>, or <c>EU-US</c>.</param>
    /// <returns>The distinct territories in preference order.</returns>
    public static IReadOnlyList<BiosTerritory> DesiredTerritories(string? regions)
    {
        if (string.IsNullOrWhiteSpace(regions))
        {
            return [BiosTerritory.Unknown];
        }

        var result = new List<BiosTerritory>();
        foreach (var token in regions.Split(
                     ['-', ',', '/', ';', ' '],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            BiosTerritory territory = token.ToUpperInvariant() switch
            {
                "US" or "USA" or "CA" or "CAN" or "NTSC" => BiosTerritory.Usa,
                "JP" or "JPN" or "JAPAN" => BiosTerritory.Japan,
                "EU" or "EUR" or "FR" or "GB" or "DE" or "ES" or "IT" or "NL" or "PAL" => BiosTerritory.Europe,
                "KR" or "KOR" or "KOREA" => BiosTerritory.Korea,
                "AS" or "ASIA" => BiosTerritory.World,
                _ => BiosTerritory.Unknown,
            };

            if (!result.Contains(territory))
            {
                result.Add(territory);
            }
        }

        return result.Count == 0 ? [BiosTerritory.Unknown] : result;
    }

    private static IReadOnlyList<BiosTerritory> DesiredTerritoriesForGame(GameRecord? game, string? discName)
    {
        IReadOnlyList<BiosTerritory> desired = DesiredTerritories(game?.Regions);
        if (desired.Count != 1 || desired[0] != BiosTerritory.Unknown)
        {
            return desired;
        }

        List<BiosTerritory> fromName = DesiredTerritoriesFromText(game?.Name);
        if (fromName.Count > 0)
        {
            return fromName;
        }

        fromName = DesiredTerritoriesFromText(
            string.IsNullOrWhiteSpace(discName) ? null : Path.GetFileNameWithoutExtension(discName));
        return fromName.Count > 0 ? fromName : desired;
    }

    private static List<BiosTerritory> DesiredTerritoriesFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var result = new List<BiosTerritory>();
        if (text.Contains("USA", StringComparison.OrdinalIgnoreCase)
            || text.Contains("(US", StringComparison.OrdinalIgnoreCase))
        {
            result.Add(BiosTerritory.Usa);
        }

        if (text.Contains("Japan", StringComparison.OrdinalIgnoreCase)
            || text.Contains("(JP", StringComparison.OrdinalIgnoreCase))
        {
            result.Add(BiosTerritory.Japan);
        }

        if (text.Contains("Europe", StringComparison.OrdinalIgnoreCase)
            || text.Contains("(EU", StringComparison.OrdinalIgnoreCase))
        {
            result.Add(BiosTerritory.Europe);
        }

        if (text.Contains("Arcade", StringComparison.OrdinalIgnoreCase))
        {
            result.Add(BiosTerritory.Arcade);
        }

        return result;
    }

    private static BiosProfile CreateProfile(
        string path,
        string fileName,
        string md5,
        long size,
        BiosDescriptor descriptor,
        bool isAnvil)
    {
        return new BiosProfile(
            path,
            fileName,
            md5,
            size,
            descriptor.Kind,
            descriptor.Territory,
            descriptor.VideoStandard,
            descriptor.IsKanji,
            isAnvil,
            descriptor.IsPatched,
            descriptor.Name,
            descriptor.Notes,
            descriptor.Preference,
            descriptor.IsRsaDisabled);
    }

    private BiosProfile? ChooseSecondary(BiosProfile primary, IReadOnlyList<BiosTerritory> desired, BiosSelectionOptions options)
    {
        if (primary.IsKanji)
        {
            return null;
        }

        if (!options.RequireKanji && !desired.Contains(BiosTerritory.Japan))
        {
            return null;
        }

        var primaryFamily = FamilyToken(primary.Name);
        return Secondaries
            .Where(profile => profile.IsKanji)
            .OrderByDescending(profile => string.Equals(FamilyToken(profile.Name), primaryFamily, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(profile => profile.Preference)
            .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(profile => profile.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static int Score(
        BiosProfile profile,
        IReadOnlyList<BiosTerritory> desired,
        bool isArcade,
        BiosSelectionOptions options)
    {
        var score = profile.Preference;
        if (desired.Contains(profile.Territory))
        {
            score += 1000;
        }
        else if (profile.Territory == BiosTerritory.World)
        {
            score += 300;
        }
        else if (profile.Territory == BiosTerritory.Unknown)
        {
            score += 100;
        }

        if (desired.Contains(BiosTerritory.Europe))
        {
            score += profile.VideoStandard switch
            {
                BiosVideoStandard.Pal => 300,
                BiosVideoStandard.Ntsc => -100,
                _ => 0,
            };
        }

        if (desired.Contains(BiosTerritory.Usa) || desired.Contains(BiosTerritory.Japan))
        {
            score += profile.VideoStandard switch
            {
                BiosVideoStandard.Ntsc => 300,
                BiosVideoStandard.Pal => -100,
                _ => 0,
            };
        }

        if (isArcade)
        {
            if (profile.Territory == BiosTerritory.Arcade)
            {
                score += 200;
            }

            if (profile.IsPatched)
            {
                score += 400;
            }
        }
        else if (profile.IsPatched && !(options.AllowUnsignedDiscs && profile.IsRsaDisabled))
        {
            score -= 2000;
        }

        if (options.AllowUnsignedDiscs && profile.IsRsaDisabled)
        {
            score += 600;
        }

        if (profile.IsKanji && desired.Contains(BiosTerritory.Japan))
        {
            score += 200;
        }

        if (options.PreferAnvil && profile.IsAnvil)
        {
            score += 150;
        }

        return score;
    }

    private static string BuildReason(
        BiosProfile primary,
        BiosProfile? secondary,
        IReadOnlyList<BiosTerritory> desired,
        bool fallback,
        bool isArcade)
    {
        var regions = string.Join("/", desired.Select(DescribeTerritory));
        var secondaryText = secondary is null ? string.Empty : $" + {secondary.Name}";
        var prefix = fallback ? "Fallback selection" : "Auto-selected";
        var arcadeText = isArcade ? " arcade" : string.Empty;
        return $"{prefix}{arcadeText} {primary.Name}{secondaryText} for {regions} ({primary.RegionLabel}). {primary.Notes}";
    }

    private static string DescribeTerritory(BiosTerritory territory)
    {
        return territory switch
        {
            BiosTerritory.Usa => "US",
            BiosTerritory.Japan => "JP",
            BiosTerritory.Europe => "EU",
            BiosTerritory.Korea => "KR",
            BiosTerritory.Arcade => "arcade",
            BiosTerritory.World => "world",
            _ => "unknown region",
        };
    }

    private static string FamilyToken(string name)
    {
        // FZ-10 contains the substring FZ-1, so check the longer model first.
        if (name.Contains("fz-10", StringComparison.OrdinalIgnoreCase)
            || name.Contains("fz10", StringComparison.OrdinalIgnoreCase))
        {
            return "fz10";
        }

        if (name.Contains("fz-1", StringComparison.OrdinalIgnoreCase)
            || name.Contains("fz1", StringComparison.OrdinalIgnoreCase))
        {
            return "fz1";
        }

        return string.Empty;
    }

    private static bool IsKanjiFontImage(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 7
            && bytes[0] == 0x01
            && bytes[1] == 0x5A
            && bytes[2] == 0x5A
            && bytes[3] == 0x5A
            && bytes[4] == 0x5A
            && bytes[5] == 0x5A
            && bytes[6] == 0x01;
    }

    private static FileNameClassification ClassifyFileName(string fileName, bool isAnvil)
    {
        var lower = fileName.ToLowerInvariant();
        var kanji = lower.Contains("kanji", StringComparison.Ordinal)
            || lower.Contains("font", StringComparison.Ordinal);
        var patched = lower.Contains("patch", StringComparison.Ordinal)
            || lower.Contains("arcade", StringComparison.Ordinal);
        var rsaDisabled = lower.Contains("norsa", StringComparison.Ordinal)
            || lower.Contains("no-rsa", StringComparison.Ordinal)
            || lower.Contains("no_rsa", StringComparison.Ordinal)
            || lower.Contains("no rsa", StringComparison.Ordinal)
            || lower.Contains("rsa disabled", StringComparison.Ordinal)
            || lower.Contains("rsa-disabled", StringComparison.Ordinal)
            || lower.Contains("rsa_disabled", StringComparison.Ordinal)
            || lower.Contains("rsa patched", StringComparison.Ordinal)
            || lower.Contains("rsa-patched", StringComparison.Ordinal)
            || lower.Contains("rsa_patched", StringComparison.Ordinal)
            || lower.Contains("unsigned", StringComparison.Ordinal);
        patched |= rsaDisabled;
        isAnvil |= lower.Contains("anvil", StringComparison.Ordinal);

        if (lower.Contains("fz10e", StringComparison.Ordinal)
            || lower.Contains("e-anvil", StringComparison.Ordinal)
            || lower.Contains("europe", StringComparison.Ordinal)
            || lower.Contains("pal", StringComparison.Ordinal))
        {
            return new FileNameClassification(BiosTerritory.Europe, BiosVideoStandard.Pal, kanji, isAnvil, patched, rsaDisabled);
        }

        if (lower.Contains("fz10ja", StringComparison.Ordinal)
            || lower.Contains("japan", StringComparison.Ordinal)
            || lower.Contains("sanyo", StringComparison.Ordinal)
            || kanji)
        {
            return new FileNameClassification(BiosTerritory.Japan, BiosVideoStandard.Ntsc, kanji, isAnvil, patched, rsaDisabled);
        }

        if (lower.Contains("fz1", StringComparison.Ordinal)
            || lower.Contains("fz10", StringComparison.Ordinal)
            || lower.Contains("goldstar", StringComparison.Ordinal)
            || lower.Contains("usa", StringComparison.Ordinal)
            || lower.Contains("ntsc", StringComparison.Ordinal))
        {
            return new FileNameClassification(BiosTerritory.Usa, BiosVideoStandard.Ntsc, kanji, isAnvil, patched, rsaDisabled);
        }

        if (lower.Contains("arcade", StringComparison.Ordinal)
            || lower.Contains("saot", StringComparison.Ordinal))
        {
            return new FileNameClassification(BiosTerritory.Arcade, BiosVideoStandard.Ntsc, kanji, isAnvil, patched, rsaDisabled);
        }

        return new FileNameClassification(BiosTerritory.Unknown, BiosVideoStandard.Unknown, kanji, isAnvil, patched, rsaDisabled);
    }

    private readonly record struct FileNameClassification(
        BiosTerritory Territory,
        BiosVideoStandard VideoStandard,
        bool IsKanji,
        bool IsAnvil,
        bool IsPatched,
        bool IsRsaDisabled);
}
