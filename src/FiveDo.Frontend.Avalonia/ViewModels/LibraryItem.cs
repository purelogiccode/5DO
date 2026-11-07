using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FiveDo.Host;

namespace FiveDo.Frontend.Avalonia.ViewModels;

/// <summary>
/// Presentation wrapper for one disc in the game library: title, metadata, and an
/// optional cover thumbnail decoded at a bounded size.
/// </summary>
public sealed class LibraryItem : IDisposable, INotifyPropertyChanged
{
    /// <summary>Creates the item. Cover art is decoded on first access in the background.</summary>
    /// <param name="entry">Scanner result for the disc.</param>
    public LibraryItem(GameLibraryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entry = entry;
    }

    private Bitmap? _cover;
    private bool _coverRequested;

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
    /// <remarks>
    /// The first read starts a bounded background decode and returns <see langword="null"/>.
    /// The binding is notified when the bitmap is ready, so the library list is never blocked
    /// while hundreds of covers are decoded.
    /// </remarks>
    public Bitmap? Cover
    {
        get
        {
            if (!_coverRequested)
            {
                _coverRequested = true;
                if (!Entry.Pending && !string.IsNullOrWhiteSpace(Entry.CoverPath))
                {
                    _ = LoadCoverAsync(Entry.CoverPath);
                }
            }

            return _cover;
        }
    }

    /// <summary>Gets a value indicating whether cover art was loaded.</summary>
    public bool HasCover => _cover is not null;

    private async Task LoadCoverAsync(string path)
    {
        Bitmap? cover = await CoverCache.LoadAsync(path).ConfigureAwait(false);
        if (cover is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                // A later cover resolution may have replaced this path while the
                // decode was in flight; never publish a stale bitmap.
                if (!string.Equals(Entry.CoverPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _cover = cover;
                OnPropertyChanged(nameof(Cover));
                OnPropertyChanged(nameof(HasCover));
            },
            DispatcherPriority.Background);
    }

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

            if (Entry.CoverSource.Length > 0
                && !string.Equals(Entry.CoverSource, "none", StringComparison.Ordinal))
            {
                tip += $"{Environment.NewLine}Cover: {Entry.CoverSource} ({Entry.CoverConfidence:P0})";
                if (Entry.CoverMatchName.Length > 0)
                {
                    tip += $"{Environment.NewLine}Matched: {Entry.CoverMatchName}";
                }
            }

            return string.IsNullOrWhiteSpace(Entry.Error)
                ? tip
                : $"{tip}{Environment.NewLine}{Entry.Error}";
        }
    }

    /// <summary>Replaces the local cover path and reloads the bitmap when it changes.</summary>
    /// <param name="path">Local cover path, or an empty string to clear it.</param>
    public void SetCoverPath(string? path)
    {
        path ??= string.Empty;
        if (string.Equals(Entry.CoverPath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Entry = Entry with { CoverPath = path };
        _cover = null;
        _coverRequested = false;
        OnPropertyChanged(nameof(Cover));
        OnPropertyChanged(nameof(HasCover));
        OnPropertyChanged(nameof(Tip));
        if (!string.IsNullOrWhiteSpace(path))
        {
            _coverRequested = true;
            _ = LoadCoverAsync(path);
        }
    }

    /// <summary>Replaces the entry with a decorated version and notifies bound views.</summary>
    /// <param name="entry">Updated entry.</param>
    public void Update(GameLibraryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var coverChanged = !string.Equals(Entry.CoverPath, entry.CoverPath, StringComparison.OrdinalIgnoreCase);
        Entry = entry;
        if (coverChanged)
        {
            _cover = null;
            _coverRequested = false;
            OnPropertyChanged(nameof(Cover));
            OnPropertyChanged(nameof(HasCover));
        }

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
/// Decodes cover art once per file on background threads. Duplicate discs in different
/// library folders share the same bitmap instead of decoding a separate copy for each entry.
/// </summary>
internal static class CoverCache
{
    // Covers are decoded at a bounded height so a large library does not keep hundreds of
    // near-full-resolution bitmaps alive. 256 px is still sharp for the default card size
    // and keeps background decoding and memory well below the 384 px maximum card size.
    private const int DecodeHeight = 256;
    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, Bitmap> Covers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Task<Bitmap?>> Pending = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim DecodeGate =
        new(Math.Clamp(Environment.ProcessorCount / 2, 1, 4));
    private static int _generation;

    /// <summary>Starts or joins a background cover decode.</summary>
    /// <param name="path">Cover image path.</param>
    /// <returns>The shared bitmap, or <see langword="null"/> when the image cannot be decoded.</returns>
    internal static Task<Bitmap?> LoadAsync(string path)
    {
        lock (Gate)
        {
            if (Covers.TryGetValue(path, out Bitmap? cached))
            {
                return Task.FromResult<Bitmap?>(cached);
            }

            if (Pending.TryGetValue(path, out Task<Bitmap?>? pending))
            {
                return pending;
            }

            var generation = _generation;
            Task<Bitmap?> task = Task.Run(() => LoadAsyncCore(path, generation));
            Pending[path] = task;
            return task;
        }
    }

    private static async Task<Bitmap?> LoadAsyncCore(string path, int generation)
    {
        await DecodeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            Bitmap bitmap;
            using (FileStream stream = File.OpenRead(path))
            {
                bitmap = Bitmap.DecodeToHeight(stream, DecodeHeight);
            }

            lock (Gate)
            {
                Pending.Remove(path);
                if (generation != _generation)
                {
                    bitmap.Dispose();
                    return null;
                }

                Covers[path] = bitmap;
                return bitmap;
            }
        }
        catch (Exception exception) when (exception is IOException
            or ArgumentException
            or UnauthorizedAccessException
            or NotSupportedException
            or InvalidOperationException)
        {
            Serilog.Log.Debug(exception, "Cover art could not be decoded for {Path}.", path);
            lock (Gate)
            {
                Pending.Remove(path);
            }

            return null;
        }
        finally
        {
            DecodeGate.Release();
        }
    }

    /// <summary>Disposes every cached cover. Call when the library items are replaced.</summary>
    internal static void Clear()
    {
        lock (Gate)
        {
            _generation++;
            foreach (Bitmap cover in Covers.Values)
            {
                cover.Dispose();
            }

            Covers.Clear();
            Pending.Clear();
        }
    }
}
