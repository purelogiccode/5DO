using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace FiveDo.Storage;

/// <summary>
/// The 4DO game database, keyed by game ID and by the two-sector MD5 checksum.
/// </summary>
/// <remarks>
/// The XML resource is ported from 4DO's <c>GameDatabase.xml</c> with attribution; it contains
/// game metadata facts only. Lookups accept either case for the checksum. The optional
/// <c>revision</c>, <c>discNumber</c>, <c>discTotal</c>, <c>bios</c>, <c>bios2</c>,
/// <c>fixMode</c>, <c>inputDevices</c>, <c>nvram</c>, and <c>cheats</c> elements extend the
/// original 4DO schema; older files remain readable.
/// </remarks>
public sealed class GameDatabase
{
    private const string ResourceName = "FiveDo.Storage.Data.GameDatabase.xml";
    private const string HeaderComment =
        "Ported from 4DO (FourDO) GameDatabase.xml. Game metadata facts only; see THIRD-PARTY-NOTICES.md.";

    private readonly Dictionary<string, GameRecord> _byChecksum;
    private readonly Dictionary<string, GameRecord> _byId;
    private readonly List<GameRecord> _records;

    private GameDatabase(
        Dictionary<string, GameRecord> byChecksum,
        Dictionary<string, GameRecord> byId,
        List<GameRecord> records)
    {
        _byChecksum = byChecksum;
        _byId = byId;
        _records = records;
    }

    /// <summary>Gets the embedded database instance.</summary>
    public static GameDatabase Default { get; } = LoadEmbedded();

    /// <summary>Gets the number of records.</summary>
    public int Count => _byChecksum.Count;

    /// <summary>Gets every record in stable checksum order.</summary>
    public IReadOnlyList<GameRecord> Records => _records;

    /// <summary>Looks up a game by the MD5 of its first two sectors.</summary>
    /// <param name="checksum">Hexadecimal checksum; case-insensitive.</param>
    /// <returns>The record, or <see langword="null"/> for an unknown disc.</returns>
    public GameRecord? FindByChecksum(string checksum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checksum);
        return _byChecksum.GetValueOrDefault(checksum.ToUpperInvariant());
    }

    /// <summary>Looks up a game by its 4DO ID.</summary>
    /// <param name="id">Game ID.</param>
    /// <returns>The record, or <see langword="null"/> for an unknown ID.</returns>
    public GameRecord? FindById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _byId.GetValueOrDefault(id.ToUpperInvariant());
    }

    /// <summary>Loads a database from XML.</summary>
    /// <param name="stream">XML stream.</param>
    /// <returns>The loaded database.</returns>
    /// <exception cref="InvalidDataException">Thrown when the XML is malformed.</exception>
    public static GameDatabase Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        XDocument document;
        try
        {
            document = XDocument.Load(stream);
        }
        catch (Exception exception) when (exception is XmlException or IOException)
        {
            throw new InvalidDataException("The game database XML is malformed.", exception);
        }

        XElement root = document.Root ?? throw new InvalidDataException("The game database has no root element.");
        return FromRecords(root.Elements("game").Select(ParseRecord).OfType<GameRecord>());
    }

    /// <summary>Builds a database from records. Later records replace earlier records with the same checksum.</summary>
    /// <param name="records">Game records.</param>
    /// <returns>A database containing every valid record.</returns>
    public static GameDatabase FromRecords(IEnumerable<GameRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var byChecksum = new Dictionary<string, GameRecord>(StringComparer.OrdinalIgnoreCase);
        var byId = new Dictionary<string, GameRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (GameRecord record in records)
        {
            if (record.Id.Length == 0 || record.CheckSum.Length == 0)
            {
                continue;
            }

            byChecksum[record.CheckSum] = record;
            byId[record.Id] = record;
        }

        var ordered = byChecksum.Values
            .OrderBy(record => record.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new GameDatabase(byChecksum, byId, ordered);
    }

    /// <summary>Merges another set of records into this database.</summary>
    /// <param name="records">Records to add.</param>
    /// <param name="overwrite">When true, incoming records replace existing records with the same checksum.</param>
    /// <returns>A new merged database; this instance is unchanged.</returns>
    public GameDatabase Merge(IEnumerable<GameRecord> records, bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(records);

        var merged = new Dictionary<string, GameRecord>(_byChecksum, StringComparer.OrdinalIgnoreCase);
        foreach (GameRecord record in records)
        {
            if (record.Id.Length == 0 || record.CheckSum.Length == 0)
            {
                continue;
            }

            if (overwrite || !merged.ContainsKey(record.CheckSum))
            {
                merged[record.CheckSum] = record;
            }
        }

        return FromRecords(merged.Values);
    }

    /// <summary>Writes the database as XML.</summary>
    /// <param name="stream">Writable stream.</param>
    public void Save(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false,
        };

        using var writer = XmlWriter.Create(stream, settings);
        writer.WriteStartDocument();
        writer.WriteComment(HeaderComment);
        writer.WriteStartElement("database");
        foreach (GameRecord record in _records)
        {
            writer.WriteStartElement("game");
            WriteElement(writer, "id", record.Id);
            WriteElement(writer, "checkSum", record.CheckSum.ToUpperInvariant());
            WriteElement(writer, "name", record.Name);
            WriteElement(writer, "releaseYear", record.ReleaseYear);
            WriteElement(writer, "publisher", record.Publisher);
            WriteElement(writer, "regions", record.Regions);
            WriteElement(writer, "revision", record.Revision);
            if (record.DiscNumber is { } discNumber)
            {
                WriteElement(writer, "discNumber", discNumber.ToString(CultureInfo.InvariantCulture));
            }

            if (record.DiscTotal is { } discTotal)
            {
                WriteElement(writer, "discTotal", discTotal.ToString(CultureInfo.InvariantCulture));
            }

            WriteElement(writer, "bios", record.Bios);
            WriteElement(writer, "bios2", record.Bios2);
            if (record.FixMode is { } fixMode)
            {
                WriteElement(writer, "fixMode", $"0x{fixMode:X8}");
            }

            WriteElement(writer, "inputDevices", record.InputDevices);
            WriteElement(writer, "nvram", record.Nvram);
            WriteElement(writer, "cheats", record.Cheats);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    /// <summary>Saves the database to a file.</summary>
    /// <param name="path">Destination path.</param>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = File.Create(path);
        Save(stream);
    }

    private static void WriteElement(XmlWriter writer, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            writer.WriteElementString(name, value);
        }
    }

    private static GameRecord? ParseRecord(XElement node)
    {
        var id = (string?)node.Element("id") ?? string.Empty;
        var checkSum = (string?)node.Element("checkSum") ?? string.Empty;
        if (id.Length == 0 || checkSum.Length == 0)
        {
            return null;
        }

        return new GameRecord(
            id.ToUpperInvariant(),
            checkSum.ToUpperInvariant(),
            (string?)node.Element("name") ?? string.Empty,
            (string?)node.Element("releaseYear") ?? string.Empty,
            (string?)node.Element("publisher") ?? string.Empty,
            (string?)node.Element("regions") ?? string.Empty)
        {
            Revision = (string?)node.Element("revision") ?? string.Empty,
            DiscNumber = ParseNullableInt((string?)node.Element("discNumber")),
            DiscTotal = ParseNullableInt((string?)node.Element("discTotal")),
            Bios = (string?)node.Element("bios") ?? string.Empty,
            Bios2 = (string?)node.Element("bios2") ?? string.Empty,
            FixMode = ParseNullableHex((string?)node.Element("fixMode")),
            InputDevices = (string?)node.Element("inputDevices") ?? string.Empty,
            Nvram = (string?)node.Element("nvram") ?? string.Empty,
            Cheats = (string?)node.Element("cheats") ?? string.Empty,
        };
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : null;
    }

    private static uint? ParseNullableHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        return uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsed)
            ? parsed
            : null;
    }

    private static GameDatabase LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
                              ?? throw new InvalidOperationException($"Embedded game database '{ResourceName}' is missing.");
        return Load(stream);
    }
}
