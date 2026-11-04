using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using FiveDo.Host;

namespace FiveDo.Frontend.Avalonia.ViewModels;

/// <summary>
/// Presentation wrapper for one disc in the game library: title, metadata, and an
/// optional cover thumbnail decoded at a bounded size.
/// </summary>
public sealed class LibraryItem : IDisposable, INotifyPropertyChanged
{
    /// <summary>Creates the item and loads its cover art when one is available.</summary>
    /// <param name="entry">Scanner result for the disc.</param>
    public LibraryItem(GameLibraryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entry = entry;

        if (!entry.Pending && !string.IsNullOrWhiteSpace(entry.CoverPath))
        {
            Cover = CoverCache.GetOrLoad(entry.CoverPath);
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the underlying scanner result.</summary>
    public GameLibraryEntry Entry { get; private set; }

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

    /// <summary>Gets the favorite indicator shown on the card.</summary>
    public string FavoriteGlyph => Entry.FavoriteGlyph;

    /// <summary>Gets a value indicating whether the game is a favorite.</summary>
    public bool IsFavorite => Entry.Favorite;

    /// <summary>Gets the compact metadata line.</summary>
    public string MetadataText => Entry.MetadataText;

    /// <summary>Gets the formatted play time.</summary>
    public string PlaytimeText => Entry.PlaytimeText;

    /// <summary>Gets the formatted last-played timestamp.</summary>
    public string LastPlayedText => Entry.LastPlayedText;

    /// <summary>Gets the detailed tooltip text.</summary>
    public string DetailText => Entry.DetailText;

    /// <summary>Gets a value indicating whether the disc has a current health result.</summary>
    public bool HasDumpBadge => Entry.HasDumpBadge;

    /// <summary>Gets the compact disc-health badge text.</summary>
    public string DumpBadge => Entry.DumpBadge;

    /// <summary>Gets the disc-health tooltip text.</summary>
    public string DumpTip => Entry.DumpTip;

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
            if (MetadataText.Length > 0)
            {
                tip += $"{Environment.NewLine}{MetadataText}";
            }

            if (PlaytimeText.Length > 0)
            {
                tip += $"{Environment.NewLine}Played {PlaytimeText}";
            }

            if (LastPlayedText.Length > 0)
            {
                tip += $"{Environment.NewLine}Last played {LastPlayedText}";
            }

            if (DumpTip.Length > 0)
            {
                tip += $"{Environment.NewLine}{DumpTip}";
            }

            return string.IsNullOrWhiteSpace(Entry.Error)
                ? tip
                : $"{tip}{Environment.NewLine}{Entry.Error}";
        }
    }

    /// <summary>Replaces the entry with a decorated version and notifies bound views.</summary>
    /// <param name="entry">Updated entry.</param>
    public void Update(GameLibraryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entry = entry;
        OnPropertyChanged(nameof(Entry));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(FavoriteGlyph));
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(MetadataText));
        OnPropertyChanged(nameof(PlaytimeText));
        OnPropertyChanged(nameof(LastPlayedText));
        OnPropertyChanged(nameof(DetailText));
        OnPropertyChanged(nameof(HasDumpBadge));
        OnPropertyChanged(nameof(DumpBadge));
        OnPropertyChanged(nameof(DumpTip));
        OnPropertyChanged(nameof(Tip));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Cover bitmaps are shared by path through <see cref="CoverCache"/>, so they are
    /// released when the library is cleared rather than with an individual item.
    /// </remarks>
    public void Dispose()
    {
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Decodes cover art once per file. Duplicate discs in different library folders share
/// the same bitmap instead of decoding a separate copy for each entry.
/// </summary>
internal static class CoverCache
{
    // Matches the largest card the grid library can show so big covers stay sharp.
    private const int DecodeHeight = (int)AppSettings.MaxLibraryCardSize;
    private static readonly Dictionary<string, Bitmap> Covers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Loads and caches a cover image.</summary>
    /// <param name="path">Cover image path.</param>
    /// <returns>The shared bitmap, or <see langword="null"/> when the image cannot be decoded.</returns>
    internal static Bitmap? GetOrLoad(string path)
    {
        if (Covers.TryGetValue(path, out Bitmap? cached))
        {
            return cached;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            Bitmap bitmap = Bitmap.DecodeToHeight(stream, DecodeHeight);
            Covers[path] = bitmap;
            return bitmap;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
        {
            Serilog.Log.Debug(exception, "Cover art could not be decoded for {Path}.", path);
            return null;
        }
    }

    /// <summary>Disposes every cached cover. Call when the library items are replaced.</summary>
    internal static void Clear()
    {
        foreach (Bitmap cover in Covers.Values)
        {
            cover.Dispose();
        }

        Covers.Clear();
    }
}
