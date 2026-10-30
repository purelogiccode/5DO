using System;
using System.Globalization;
using System.IO;
using Avalonia.Media.Imaging;
using FiveDo.Host;

namespace FiveDo.Frontend.Avalonia.ViewModels;

/// <summary>
/// Presentation wrapper for one disc in the game library: title, metadata, and an
/// optional cover thumbnail decoded at a bounded size.
/// </summary>
public sealed class LibraryItem : IDisposable
{
    private const int CoverDecodeWidth = 480;

    /// <summary>Creates the item and loads its cover art when one is available.</summary>
    /// <param name="entry">Scanner result for the disc.</param>
    public LibraryItem(GameLibraryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entry = entry;

        if (!entry.Pending && !string.IsNullOrWhiteSpace(entry.CoverPath))
        {
            try
            {
                using FileStream stream = File.OpenRead(entry.CoverPath);
                Cover = Bitmap.DecodeToWidth(stream, CoverDecodeWidth);
            }
            catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
            {
                Serilog.Log.Debug(exception, "Cover art could not be decoded for {Path}.", entry.CoverPath);
                Cover = null;
            }
        }
    }

    /// <summary>Gets the underlying scanner result.</summary>
    public GameLibraryEntry Entry { get; }

    /// <summary>Gets the display title.</summary>
    public string Name => Entry.Name;

    /// <summary>Gets the disc path.</summary>
    public string Path => Entry.Path;

    /// <summary>Gets the container format label (ISO, BIN, CUE, CHD).</summary>
    public string Format => Entry.Format;

    /// <summary>Gets a value indicating whether the 4DO database recognized the disc.</summary>
    public bool Known => Entry.Known;

    /// <summary>Gets a value indicating whether the disc is missing from the 4DO database.</summary>
    public bool IsUnknown => !Known;

    /// <summary>Gets the database badge text.</summary>
    public string DatabaseBadge => Entry.Pending ? "Scanning" : Known ? "4DO DB" : "Unknown";

    /// <summary>Gets the short game identifier shown under the title.</summary>
    public string GameIdText => Entry.GameId.Length >= 8
        ? Entry.GameId[..8].ToUpperInvariant()
        : Entry.GameId.ToUpperInvariant();

    /// <summary>Gets the formatted sector count.</summary>
    public string SectorText => Entry.Pending
        ? "Scanning metadata..."
        : Entry.SectorCount > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{Entry.SectorCount:N0} sectors")
            : "Unreadable image";

    /// <summary>Gets the decoded cover art, or <see langword="null"/> when none exists.</summary>
    public Bitmap? Cover { get; }

    /// <summary>Gets a value indicating whether cover art was loaded.</summary>
    public bool HasCover => Cover is not null;

    /// <summary>Gets the hover tooltip with full path and image details.</summary>
    public string Tip
    {
        get
        {
            var tip = $"{Path}{Environment.NewLine}{Format} · {SectorText}";
            return string.IsNullOrWhiteSpace(Entry.Error)
                ? tip
                : $"{tip}{Environment.NewLine}{Entry.Error}";
        }
    }

    /// <inheritdoc />
    public void Dispose() => Cover?.Dispose();
}
