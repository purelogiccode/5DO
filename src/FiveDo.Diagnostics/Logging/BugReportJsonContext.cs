using System.Text.Json.Serialization;

namespace FiveDo.Diagnostics.Logging;

/// <summary>Source-generated JSON metadata for the trim-safe CLI and frontend.</summary>
[JsonSerializable(typeof(BugReportPayload))]
internal sealed partial class BugReportJsonContext : JsonSerializerContext;
