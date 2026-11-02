using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace FiveDo.Storage;

/// <summary>One ROM entry in a Redump/No-Intro DAT file.</summary>
/// <param name="Name">ROM file name.</param>
/// <param name="Size">File size in bytes.</param>
/// <param name="Crc32">Uppercase CRC-32, or empty when absent.</param>
/// <param name="Md5">Uppercase MD5, or empty when absent.</param>
/// <param name="Sha1">Uppercase SHA-1, or empty when absent.</param>
public sealed record DatRom(
    string Name,
    long Size,
    string Crc32,
    string Md5,
    string Sha1);

/// <summary>One game entry in a Redump/No-Intro DAT file.</summary>
/// <param name="Name">Game name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Roms">ROM entries for this game.</param>
public sealed record DatGame(
    string Name,
    string Description,
    IReadOnlyList<DatRom> Roms);

/// <summary>
/// Parses the XML DAT format used by Redump and No-Intro.
/// </summary>
/// <remarks>
/// The parser accepts the common <c>&lt;datafile&gt;&lt;game name="..."&gt;&lt;rom .../&gt;</c>
/// layout. Hash values are normalized to uppercase and validated; entries without a
/// name, size, or at least one hash are ignored. The DAT is a user-supplied reference and
/// is never written.
/// </remarks>
public sealed class RedumpDat
{
    private RedumpDat(IReadOnlyList<DatGame> games)
    {
        Games = games;
    }

    /// <summary>Gets the parsed games.</summary>
    public IReadOnlyList<DatGame> Games { get; }

    /// <summary>Gets the number of ROM entries across every game.</summary>
    public int RomCount
    {
        get
        {
            var count = 0;
            foreach (DatGame game in Games)
            {
                count += game.Roms.Count;
            }

            return count;
        }
    }

    /// <summary>Loads and parses a DAT file.</summary>
    /// <param name="path">DAT path.</param>
    /// <returns>The parsed DAT.</returns>
    /// <exception cref="ArgumentException">Thrown when the path is empty.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    /// <exception cref="InvalidDataException">Thrown when the XML is invalid.</exception>
    public static RedumpDat Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllText(path));
    }

    /// <summary>Parses DAT XML text.</summary>
    /// <param name="xml">XML text.</param>
    /// <returns>The parsed DAT.</returns>
    /// <exception cref="ArgumentException">Thrown when the text is empty.</exception>
    /// <exception cref="InvalidDataException">Thrown when the XML is invalid.</exception>
    public static RedumpDat Parse(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (System.Xml.XmlException exception)
        {
            throw new InvalidDataException($"The DAT is not valid XML: {exception.Message}", exception);
        }

        XElement? root = document.Root;
        if (root is null || !string.Equals(root.Name.LocalName, "datafile", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The DAT root must be a <datafile> element.");
        }

        var games = new List<DatGame>();
        foreach (XElement gameElement in root.Elements())
        {
            if (!string.Equals(gameElement.Name.LocalName, "game", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = ((string?)gameElement.Attribute("name"))?.Trim() ?? string.Empty;
            var description = string.Empty;
            var roms = new List<DatRom>();
            foreach (XElement child in gameElement.Elements())
            {
                if (string.Equals(child.Name.LocalName, "description", StringComparison.OrdinalIgnoreCase))
                {
                    description = child.Value.Trim();
                    continue;
                }

                if (!string.Equals(child.Name.LocalName, "rom", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var romName = ((string?)child.Attribute("name"))?.Trim() ?? string.Empty;
                var sizeText = ((string?)child.Attribute("size"))?.Trim();
                if (romName.Length == 0
                    || !long.TryParse(sizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)
                    || size <= 0)
                {
                    continue;
                }

                var crc = NormalizeHash((string?)child.Attribute("crc"), 8);
                var md5 = NormalizeHash((string?)child.Attribute("md5"), 32);
                var sha1 = NormalizeHash((string?)child.Attribute("sha1"), 40);
                if (crc.Length == 0 && md5.Length == 0 && sha1.Length == 0)
                {
                    continue;
                }

                roms.Add(new DatRom(romName, size, crc, md5, sha1));
            }

            if (name.Length == 0 || roms.Count == 0)
            {
                continue;
            }

            games.Add(new DatGame(name, description, roms));
        }

        if (games.Count == 0)
        {
            throw new InvalidDataException("The DAT contains no usable game entries.");
        }

        return new RedumpDat(games);
    }

    private static string NormalizeHash(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (normalized.Length != length)
        {
            return string.Empty;
        }

        foreach (var character in normalized)
        {
            if (!char.IsAsciiHexDigit(character))
            {
                return string.Empty;
            }
        }

        return normalized;
    }
}
