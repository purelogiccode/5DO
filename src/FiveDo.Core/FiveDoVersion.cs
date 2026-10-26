namespace FiveDo.Core;

/// <summary>
/// Product identity and version constants shared by the CLI, frontend, and tests.
/// </summary>
public static class FiveDoVersion
{
    /// <summary>The product name.</summary>
    public const string ProductName = "5DO";

    /// <summary>The namespace prefix used by source projects; C# identifiers cannot begin with a digit.</summary>
    public const string NamespacePrefix = "FiveDo";

    /// <summary>The current release-candidate version.</summary>
    public const string Version = "1.0.0-rc.1";
}
