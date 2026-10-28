using System.Text.Json.Serialization;

namespace FiveDo.Diagnostics.Logging;

/// <summary>
/// The JSON body accepted by the bug-report submit endpoint.
/// </summary>
/// <param name="Message">Environment details followed by the error message.</param>
/// <param name="ApplicationName">Product name.</param>
/// <param name="Version">Product version.</param>
/// <param name="UserInfo">Optional user information; not collected.</param>
/// <param name="Environment">Platform and application mode.</param>
/// <param name="StackTrace">Exception details block.</param>
public sealed record BugReportPayload(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("applicationName")] string ApplicationName,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("userInfo")] string? UserInfo,
    [property: JsonPropertyName("environment")] string Environment,
    [property: JsonPropertyName("stackTrace")] string? StackTrace);
