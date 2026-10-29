using System.Collections.Generic;
using System.Text.Json.Serialization;
using FiveDo.Host;

namespace FiveDo.Cli;


internal sealed record CliInfoResult(
    bool Ok,
    string Path,
    int SectorCount,
    string VolumeLabel,
    uint VolumeBlockCount,
    uint BlockSize,
    string GameId,
    uint FixMode,
    bool Known,
    string Name,
    string Publisher,
    string ReleaseYear,
    string Regions);
