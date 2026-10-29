using System.Collections.Generic;
using System.Text.Json.Serialization;
using FiveDo.Host;

namespace FiveDo.Cli;


internal sealed record CliBenchResult(
    bool Ok,
    int Frames,
    double Seconds,
    double FramesPerSecond,
    int CyclesPerFrame,
    long Instructions,
    uint FrameHash);
