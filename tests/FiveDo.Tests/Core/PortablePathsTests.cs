using System;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Tests.Core;

public sealed class PortablePathsTests
{
    [Fact]
    public void ResolveDataDirectory_UsesTheApplicationDirectoryWhenPortable()
    {
        var applicationDirectory = Path.Combine(Path.GetTempPath(), "fivedo-app");

        var resolved = PortablePaths.ResolveDataDirectory(applicationDirectory, portable: true, overrideDirectory: null);

        Assert.Equal(Path.GetFullPath(applicationDirectory), resolved);
    }

    [Fact]
    public void ResolveDataDirectory_UsesPerUserDataWhenNotPortable()
    {
        var applicationDirectory = Path.Combine(Path.GetTempPath(), "fivedo-app");

        var resolved = PortablePaths.ResolveDataDirectory(applicationDirectory, portable: false, overrideDirectory: null);

        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "5DO"),
            resolved);
    }

    [Fact]
    public void ResolveDataDirectory_PrefersAnExplicitOverride()
    {
        var applicationDirectory = Path.Combine(Path.GetTempPath(), "fivedo-app");
        var overrideDirectory = Path.Combine(Path.GetTempPath(), "fivedo-instance");

        var resolved = PortablePaths.ResolveDataDirectory(applicationDirectory, portable: true, overrideDirectory);

        Assert.Equal(Path.GetFullPath(overrideDirectory), resolved);
    }
}
