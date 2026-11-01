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
    public void ResolveDataDirectory_PlacesNamedProfilesUnderProfiles()
    {
        var applicationDirectory = Path.Combine(Path.GetTempPath(), "fivedo-app");

        var resolved = PortablePaths.ResolveDataDirectory(
            applicationDirectory,
            portable: true,
            overrideDirectory: null,
            profileName: "work profile");

        Assert.Equal(
            Path.Combine(Path.GetFullPath(applicationDirectory), "profiles", "work_profile"),
            resolved);
    }

    [Fact]
    public void NormalizeProfileName_RejectsTraversalAndEmptyNames()
    {
        Assert.Equal(".._.._escape", PortablePaths.NormalizeProfileName("../../escape"));
        Assert.Equal("desk-top.1", PortablePaths.NormalizeProfileName("desk-top.1"));
        Assert.Equal("...", PortablePaths.NormalizeProfileName("..."));
        Assert.Throws<ArgumentException>(() => PortablePaths.NormalizeProfileName(" "));
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
