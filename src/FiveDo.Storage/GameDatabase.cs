using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace FiveDo.Storage;

/// <summary>
/// The 4DO game database, keyed by game ID and by the two-sector MD5 checksum.
/// </summary>
/// <remarks>
/// The XML resource is ported from 4DO's <c>GameDatabase.xml</c> with attribution; it contains
/// game metadata facts only. Lookups accept either case for the checksum.
/// </remarks>
public sealed class GameDatabase
{
    private const string ResourceName = "FiveDo.Storage.Data.GameDatabase.xml";

    private readonly Dictionary<string, GameRecord> _byChecksum;
    private readonly Dictionary<string, GameRecord> _byId;

    private GameDatabase(Dictionary<string, GameRecord> byChecksum, Dictionary<string, GameRecord> byId)
    {
        _byChecksum = byChecksum;
        _byId = byId;
    }

    /// <summary>Gets the embedded database instance.</summary>
    public static GameDatabase Default { get; } = LoadEmbedded();

    /// <summary>Gets the number of records.</summary>
    public int Count => _byChecksum.Count;

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
        catch (Exception exception) when (exception is System.Xml.XmlException or IOException)
        {
            throw new InvalidDataException("The game database XML is malformed.", exception);
        }

        var byChecksum = new Dictionary<string, GameRecord>(StringComparer.OrdinalIgnoreCase);
        var byId = new Dictionary<string, GameRecord>(StringComparer.OrdinalIgnoreCase);
        XElement root = document.Root ?? throw new InvalidDataException("The game database has no root element.");

        foreach (XElement node in root.Elements("game"))
        {
            var record = new GameRecord(
                (string?)node.Element("id") ?? string.Empty,
                (string?)node.Element("checkSum") ?? string.Empty,
                (string?)node.Element("name") ?? string.Empty,
                (string?)node.Element("releaseYear") ?? string.Empty,
                (string?)node.Element("publisher") ?? string.Empty,
                (string?)node.Element("regions") ?? string.Empty);

            if (record.Id.Length == 0 || record.CheckSum.Length == 0)
            {
                continue;
            }

            byChecksum[record.CheckSum] = record;
            byId[record.Id] = record;
        }

        return new GameDatabase(byChecksum, byId);
    }

    private static GameDatabase LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
                              ?? throw new InvalidOperationException($"Embedded game database '{ResourceName}' is missing.");
        return Load(stream);
    }
}
