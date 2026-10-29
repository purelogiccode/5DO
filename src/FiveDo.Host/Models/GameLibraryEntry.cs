using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Host;


/// <summary>One disc found by the library scanner.</summary>
/// <param name="Path">Disc file path.</param>
/// <param name="Format">Container format label.</param>
/// <param name="SectorCount">Number of 2048-byte sectors.</param>
/// <param name="GameId">MD5 game identifier, or empty when the image is unreadable.</param>
/// <param name="Name">Database title, or the file name for unknown discs.</param>
/// <param name="Known">Whether the 4DO game database recognized the disc.</param>
/// <param name="Error">Error message when the image could not be opened.</param>
/// <param name="CoverPath">Cover-art image path, or empty when none was found.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "NotAccessedPositionalProperty.Global",
    Justification = "The game identifier is part of the scanner's public result for host tooling.")]
public sealed record GameLibraryEntry(
    string Path,
    string Format,
    int SectorCount,
    string GameId,
    string Name,
    bool Known,
    string Error,
    string CoverPath = "");
