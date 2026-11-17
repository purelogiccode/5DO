using System.Collections.Generic;
using System.Linq;
using FiveDo.Host;

namespace FiveDo.Cli;


internal sealed record CliDoctorCheck(string Name, string Status, string Detail);

internal sealed record CliDoctorResult(
    bool Ok,
    string Product,
    string Version,
    string Runtime,
    string OperatingSystem,
    string Architecture,
    string SessionType,
    string DataDirectory,
    bool DataDirectoryWritable,
    string Summary,
    IReadOnlyList<CliDoctorCheck> Checks,
    CliBiosDiscoveryResult Bios,
    IReadOnlyList<string> Notes)
{
    internal static CliDoctorResult From(DoctorResult result)
    {
        return new CliDoctorResult(
            Ok: result.Summary is DoctorStatus.Ok or DoctorStatus.Info,
            Product: result.Product,
            Version: result.Version,
            Runtime: result.Runtime,
            OperatingSystem: result.OperatingSystem,
            Architecture: result.Architecture,
            SessionType: result.SessionType,
            DataDirectory: result.DataDirectory,
            DataDirectoryWritable: result.DataDirectoryWritable,
            Summary: result.Summary.ToString().ToLowerInvariant(),
            Checks: result.Checks.Select(check => new CliDoctorCheck(
                check.Name,
                check.Status.ToString().ToLowerInvariant(),
                check.Detail)).ToArray(),
            Bios: CliBiosDiscoveryResult.From(result.Bios),
            Notes: result.Notes);
    }
}
