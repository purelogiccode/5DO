
using FiveDo.Core;

namespace FiveDo.Cli;


internal sealed record CliBenchResult(
    bool Ok,
    int Frames,
    double Seconds,
    double FramesPerSecond,
    int CyclesPerFrame,
    long Instructions,
    uint FrameHash,
    string Profile = "Compatible",
    string ArmTiming = "FreeDoCompatible",
    string ClioTimers = "FreeDoCompatible",
    string CelTiming = "FreeDoCompatible",
    string CdromTiming = "FreeDoCompatible",
    int CpuClock = EmulatorConstants.ArmClock,
    string Memory = "21",
    bool SwiHle = false,
    long SwiHleInstructions = 0,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    double? SnapshotCaptureMilliseconds = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    double? SnapshotRestoreMilliseconds = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    int? SnapshotBytes = null);
