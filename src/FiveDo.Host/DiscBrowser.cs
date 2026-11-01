using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Host;

/// <summary>
/// One file or directory in a mounted 3DO Opera file system.
/// </summary>
/// <remarks>
/// The browser model is read-only. It keeps the parsed entry alongside the display
/// metadata so a caller can extract the entry without re-parsing the directory tree.
/// </remarks>
public sealed class DiscBrowserNode
{
    private readonly List<DiscBrowserNode> _children = [];

    internal DiscBrowserNode(
        OperaFileEntry? entry,
        string name,
        string path,
        bool isDirectory,
        ulong size,
        uint lba,
        int depth)
    {
        Entry = entry;
        Name = name;
        Path = path;
        IsDirectory = isDirectory;
        Size = size;
        Lba = lba;
        Depth = depth;
    }

    /// <summary>Gets the parsed Opera entry, or <see langword="null"/> for the synthetic root.</summary>
    public OperaFileEntry? Entry { get; }

    /// <summary>Gets the file or directory name.</summary>
    public string Name { get; }

    /// <summary>Gets the Opera path, including the leading backslash.</summary>
    public string Path { get; }

    /// <summary>Gets a value indicating whether this node is a directory.</summary>
    public bool IsDirectory { get; }

    /// <summary>Gets the entry size in bytes. Directory sizes are the on-disc directory length.</summary>
    public ulong Size { get; }

    /// <summary>Gets the first data block (LBA) of the entry.</summary>
    public uint Lba { get; }

    /// <summary>Gets the zero-based tree depth. The synthetic root is zero.</summary>
    public int Depth { get; }

    /// <summary>Gets the child nodes, sorted directories-first then by name.</summary>
    public IReadOnlyList<DiscBrowserNode> Children => _children;

    /// <summary>Gets a display name with a trailing slash for directories.</summary>
    public string DisplayName => IsDirectory
        ? string.Equals(Name, "\\", StringComparison.Ordinal) ? "\\" : Name + "/"
        : Name;

    /// <summary>Gets a depth-indented display name for a flat list.</summary>
    public string IndentedName => new string(' ', Depth * 2) + DisplayName;

    /// <summary>Gets a short type label.</summary>
    public string Kind => IsDirectory ? "Directory" : "File";

    /// <summary>Gets a formatted size or child count.</summary>
    public string SizeText => IsDirectory
        ? _children.Count == 1 ? "1 item" : $"{_children.Count} items"
        : FormatBytes(Size);

    /// <summary>Gets a one-line detail string for the flat list.</summary>
    public string ListDetail => IsDirectory ? SizeText : $"{SizeText}, LBA {Lba}";

    internal void AddChild(DiscBrowserNode child)
    {
        _children.Add(child);
    }

    internal void SortRecursively()
    {
        _children.Sort(static (left, right) =>
        {
            if (left.IsDirectory != right.IsDirectory)
            {
                return left.IsDirectory ? -1 : 1;
            }

            var name = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            return name != 0 ? name : string.Compare(left.Path, right.Path, StringComparison.Ordinal);
        });

        foreach (DiscBrowserNode child in _children)
        {
            child.SortRecursively();
        }
    }

    /// <summary>Formats a byte count with binary units.</summary>
    /// <param name="bytes">Byte count.</param>
    /// <returns>A short display string such as <c>1.5 MB</c>.</returns>
    public static string FormatBytes(ulong bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var value = (double)bytes;
        string[] units = ["KB", "MB", "GB", "TB"];
        foreach (var unit in units)
        {
            value /= 1024;
            if (value < 1024 || string.Equals(unit, units[^1], StringComparison.Ordinal))
            {
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{value:0.##} {unit}");
            }
        }

        return $"{bytes} B";
    }
}

/// <summary>
/// A mounted 3DO disc that can be browsed and extracted without starting the emulator.
/// </summary>
/// <remarks>
/// The model is intentionally independent of Avalonia so it can be tested and reused by
/// a future headless API. <see cref="Open"/> owns the underlying disc source and disposes
/// it; <see cref="Create"/> leaves ownership with the caller.
/// </remarks>
public sealed class DiscBrowser : IDisposable
{
    private const int ExtractionBufferSize = 64 * 1024;

    private static readonly char[] InvalidFileNameCharacters =
    [
        '<', '>', ':', '"', '/', '\\', '|', '?', '*',
    ];

    private readonly IDiscSource _disc;
    private readonly OperaFileSystem _fileSystem;
    private readonly bool _ownsDisc;
    private bool _disposed;

    private DiscBrowser(IDiscSource disc, string sourcePath, bool ownsDisc)
    {
        ArgumentNullException.ThrowIfNull(disc);
        _disc = disc;
        _ownsDisc = ownsDisc;
        SourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? string.Empty
            : Path.GetFullPath(sourcePath);
        _fileSystem = OperaFileSystem.Mount(disc);

        GameId = TryComputeGameId(disc);
        Root = BuildTree(_fileSystem);
        Nodes = Flatten(Root);
        Info = BuildInfo();
    }

    /// <summary>Gets the source image path, or an empty string for an in-memory source.</summary>
    public string SourcePath { get; }

    /// <summary>Gets the detected image format label.</summary>
    public string Format => Path.GetExtension(SourcePath).ToUpperInvariant() switch
    {
        ".ISO" => "ISO",
        ".BIN" => "BIN",
        ".CUE" => "CUE",
        ".CHD" => "CHD",
        _ => "Disc",
    };

    /// <summary>Gets the Opera volume label.</summary>
    public string Label => _fileSystem.Label;

    /// <summary>Gets the Opera volume comment.</summary>
    public string Comment => _fileSystem.Comment;

    /// <summary>Gets the lowercase MD5 of the first two sectors, or an empty string for a tiny image.</summary>
    public string GameId { get; }

    /// <summary>Gets the number of 2048-byte user-data sectors.</summary>
    public int SectorCount => _disc.SectorCount;

    /// <summary>Gets the synthetic root node.</summary>
    public DiscBrowserNode Root { get; }

    /// <summary>Gets every node in depth-first order, including the root.</summary>
    public IReadOnlyList<DiscBrowserNode> Nodes { get; }

    /// <summary>Gets the disc and game metadata shown by the info dialog.</summary>
    public DiscInfo Info { get; }

    /// <summary>Opens a disc image and owns the resulting source.</summary>
    /// <param name="path">ISO, BIN, CUE, or CHD path.</param>
    /// <returns>A mounted browser. The caller must dispose it.</returns>
    /// <exception cref="DiscImageException">Thrown when the image cannot be opened.</exception>
    /// <exception cref="InvalidDataException">Thrown when the Opera file system is malformed.</exception>
    public static DiscBrowser Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        IDiscSource disc = DiscImageFactory.Open(path, chdReadAheadHunks: 0);
        try
        {
            return new DiscBrowser(disc, path, ownsDisc: true);
        }
        catch
        {
            disc.Dispose();
            throw;
        }
    }

    /// <summary>Mounts an already-open disc source without taking ownership.</summary>
    /// <param name="disc">Open disc source.</param>
    /// <param name="sourcePath">Optional display path.</param>
    /// <returns>A mounted browser. The caller owns <paramref name="disc"/>.</returns>
    public static DiscBrowser Create(IDiscSource disc, string sourcePath = "")
    {
        return new DiscBrowser(disc, sourcePath, ownsDisc: false);
    }

    /// <summary>Extracts one file or directory below a destination folder.</summary>
    /// <param name="node">Node to extract. The root extracts the whole disc.</param>
    /// <param name="destinationDirectory">Destination root folder.</param>
    /// <param name="overwrite">Whether existing files are replaced.</param>
    /// <returns>Extraction statistics.</returns>
    /// <exception cref="ArgumentException">Thrown when the destination is empty.</exception>
    /// <exception cref="InvalidDataException">Thrown when the source entry is truncated or unsafe.</exception>
    /// <exception cref="IOException">Thrown when a destination file cannot be written.</exception>
    public DiscExtractionResult Extract(
        DiscBrowserNode node,
        string destinationDirectory,
        bool overwrite = true)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var root = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(root);

        var files = 0;
        var directories = 0;
        long bytes = 0;
        ExtractNode(
            node,
            root,
            relativePath: RelativePathFor(node),
            overwrite,
            ref files,
            ref directories,
            ref bytes);
        return new DiscExtractionResult(files, directories, bytes, root);
    }

    /// <summary>Extracts every file on the disc below a destination folder.</summary>
    /// <param name="destinationDirectory">Destination root folder.</param>
    /// <param name="overwrite">Whether existing files are replaced.</param>
    /// <returns>Extraction statistics.</returns>
    public DiscExtractionResult ExtractAll(string destinationDirectory, bool overwrite = true)
    {
        return Extract(Root, destinationDirectory, overwrite);
    }

    /// <summary>Finds a node by its Opera path.</summary>
    /// <param name="path">Path such as <c>\DIR\FILE.BIN</c>.</param>
    /// <returns>The matching node, or <see langword="null"/>.</returns>
    public DiscBrowserNode? Find(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = path.StartsWith('\\') ? path : "\\" + path;
        return Nodes.FirstOrDefault(node =>
            string.Equals(node.Path, normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Makes an Opera entry name safe for a host file system.
    /// </summary>
    /// <remarks>
    /// The replacement set is fixed rather than platform-dependent so an extraction
    /// performed on Linux cannot create a file that Windows later rejects. Reserved
    /// Windows device names are prefixed with an underscore.
    /// </remarks>
    /// <param name="name">Entry name.</param>
    /// <returns>A non-empty, relative, safe file name.</returns>
    public static string SanitizeFileName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            builder.Append(
                character < 32
                || Array.IndexOf(InvalidFileNameCharacters, character) >= 0
                    ? '_'
                    : character);
        }

        var sanitized = builder.ToString().Trim().TrimEnd('.', ' ');
        if (sanitized.Length == 0 || sanitized is "." or "..")
        {
            sanitized = "_";
        }

        var stem = Path.GetFileNameWithoutExtension(sanitized);
        if (IsReservedDeviceName(stem))
        {
            sanitized = "_" + sanitized;
        }

        return sanitized;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsDisc)
        {
            _disc.Dispose();
        }
    }

    private static string TryComputeGameId(IDiscSource disc)
    {
        if (disc.SectorCount < 2)
        {
            return string.Empty;
        }

        try
        {
            return FiveDo.Storage.GameId.Compute(disc);
        }
        catch (DiscImageException)
        {
            return string.Empty;
        }
    }

    private static DiscBrowserNode BuildTree(OperaFileSystem fileSystem)
    {
        var root = new DiscBrowserNode(
            entry: null,
            name: "\\",
            path: "\\",
            isDirectory: true,
            size: 0,
            lba: 0,
            depth: 0);
        var nodes = new Dictionary<string, DiscBrowserNode>(StringComparer.OrdinalIgnoreCase)
        {
            ["\\"] = root,
        };

        foreach (OperaFileEntry entry in fileSystem.Entries.Skip(1))
        {
            var parentPath = GetParentPath(entry.Path);
            if (!nodes.TryGetValue(parentPath, out DiscBrowserNode? parent))
            {
                throw new InvalidDataException(
                    $"The Opera entry '{entry.Path}' has no parsed parent directory.");
            }

            var node = new DiscBrowserNode(
                entry,
                entry.Name,
                entry.Path,
                entry.IsDirectory,
                entry.Size,
                entry.Lba,
                parent.Depth + 1);
            if (!nodes.TryAdd(entry.Path, node))
            {
                throw new InvalidDataException($"The Opera entry '{entry.Path}' appears more than once.");
            }

            parent.AddChild(node);
        }

        root.SortRecursively();
        return root;
    }

    private static string GetParentPath(string path)
    {
        var separator = path.LastIndexOf('\\');
        return separator <= 0 ? "\\" : path[..separator];
    }

    private static List<DiscBrowserNode> Flatten(DiscBrowserNode root)
    {
        var nodes = new List<DiscBrowserNode>();
        AddDepthFirst(root, nodes);
        return nodes;
    }

    private static void AddDepthFirst(DiscBrowserNode node, List<DiscBrowserNode> nodes)
    {
        nodes.Add(node);
        foreach (DiscBrowserNode child in node.Children)
        {
            AddDepthFirst(child, nodes);
        }
    }

    private DiscInfo BuildInfo()
    {
        var files = 0;
        var directories = 0;
        long totalBytes = 0;
        foreach (DiscBrowserNode node in Nodes)
        {
            if (node == Root)
            {
                continue;
            }

            if (node.IsDirectory)
            {
                directories++;
                continue;
            }

            files++;
            if (node.Size > long.MaxValue)
            {
                totalBytes = long.MaxValue;
            }
            else
            {
                var size = (long)node.Size;
                totalBytes = size > long.MaxValue - totalBytes
                    ? long.MaxValue
                    : totalBytes + size;
            }
        }

        GameRecord? record = string.IsNullOrWhiteSpace(GameId)
            ? null
            : GameDatabase.Default.FindByChecksum(GameId);
        var fixes = string.IsNullOrWhiteSpace(GameId)
            ? FixMode.None
            : GameCompatibility.ForGameId(GameId).FixMode;
        return new DiscInfo(
            SourcePath,
            Format,
            Label,
            Comment,
            GameId,
            SectorCount,
            files,
            directories,
            totalBytes,
            record?.Name,
            record?.Regions,
            record?.Publisher,
            record?.ReleaseYear,
            fixes);
    }

    private static string RelativePathFor(DiscBrowserNode node)
    {
        if (string.Equals(node.Path, "\\", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var parts = node.Path.TrimStart('\\').Split('\\');
        return string.Join(
            Path.DirectorySeparatorChar,
            parts.Select(SanitizeFileName));
    }

    private void ExtractNode(
        DiscBrowserNode node,
        string root,
        string relativePath,
        bool overwrite,
        ref int files,
        ref int directories,
        ref long bytes)
    {
        var target = relativePath.Length == 0
            ? root
            : Path.Combine(root, relativePath);
        EnsureContained(root, target);

        if (node.IsDirectory)
        {
            Directory.CreateDirectory(target);
            directories++;
            foreach (DiscBrowserNode child in node.Children)
            {
                var childRelative = relativePath.Length == 0
                    ? SanitizeFileName(child.Name)
                    : Path.Combine(relativePath, SanitizeFileName(child.Name));
                ExtractNode(child, root, childRelative, overwrite, ref files, ref directories, ref bytes);
            }

            return;
        }

        OperaFileEntry entry = node.Entry
            ?? throw new InvalidDataException($"The Opera node '{node.Path}' has no file entry.");
        var parent = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        using var output = new FileStream(
            target,
            overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            ExtractionBufferSize);
        var buffer = new byte[ExtractionBufferSize];
        long offset = 0;
        while (offset < (long)node.Size)
        {
            var read = _fileSystem.ReadFile(entry, offset, buffer);
            if (read <= 0)
            {
                break;
            }

            output.Write(buffer, 0, read);
            offset += read;
            bytes += read;
        }

        if (offset != (long)node.Size)
        {
            throw new InvalidDataException(
                $"The Opera entry '{node.Path}' ended after {offset} of {node.Size} bytes.");
        }

        files++;
    }

    private static void EnsureContained(string root, string target)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullTarget = Path.GetFullPath(target);
        var prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(fullTarget, fullRoot, comparison)
            && !fullTarget.StartsWith(prefix, comparison))
        {
            throw new InvalidDataException(
                $"The extraction target '{target}' escapes the destination folder.");
        }
    }

    private static bool IsReservedDeviceName(string stem)
    {
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || IsReservedNumberedName(stem, "COM")
            || IsReservedNumberedName(stem, "LPT");
    }

    private static bool IsReservedNumberedName(string stem, string prefix)
    {
        if (stem.Length != 4
            || !stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || stem[3] is < '1' or > '9')
        {
            return false;
        }

        return true;
    }
}

/// <summary>Metadata shown by the disc information dialog.</summary>
/// <param name="SourcePath">Disc image path.</param>
/// <param name="Format">Detected container format.</param>
/// <param name="Label">Opera volume label.</param>
/// <param name="Comment">Opera volume comment.</param>
/// <param name="GameId">Lowercase MD5 of the first two sectors, or empty.</param>
/// <param name="SectorCount">Number of 2048-byte sectors.</param>
/// <param name="FileCount">Number of files on the disc.</param>
/// <param name="DirectoryCount">Number of directories below the root.</param>
/// <param name="TotalFileBytes">Sum of file sizes in bytes.</param>
/// <param name="GameName">Database title, when the game ID is recognized.</param>
/// <param name="Regions">Database region text, when recognized.</param>
/// <param name="Publisher">Database publisher, when recognized.</param>
/// <param name="ReleaseYear">Database release year, when recognized.</param>
/// <param name="Fixes">Automatic 4DO compatibility flags for the game ID.</param>
public sealed record DiscInfo(
    string SourcePath,
    string Format,
    string Label,
    string Comment,
    string GameId,
    int SectorCount,
    int FileCount,
    int DirectoryCount,
    long TotalFileBytes,
    string? GameName,
    string? Regions,
    string? Publisher,
    string? ReleaseYear,
    FixMode Fixes);

/// <summary>Statistics from a disc extraction.</summary>
/// <param name="Files">Number of files written.</param>
/// <param name="Directories">Number of directories created.</param>
/// <param name="Bytes">Number of file bytes written.</param>
/// <param name="DestinationPath">Destination root folder.</param>
public sealed record DiscExtractionResult(
    int Files,
    int Directories,
    long Bytes,
    string DestinationPath);
