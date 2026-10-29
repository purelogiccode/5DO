using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Host;


/// <summary>One section in a save-state file.</summary>
/// <param name="Id">Section identifier.</param>
/// <param name="Version">Section format version.</param>
/// <param name="PayloadLength">Uncompressed payload length in bytes.</param>
/// <param name="Compressed">Whether the payload is Brotli-compressed in the file.</param>
/// <param name="StoredLength">Payload length on disk in bytes.</param>
public sealed record StateSectionInfo(
    uint Id,
    ushort Version,
    int PayloadLength,
    bool Compressed = false,
    int StoredLength = 0);
