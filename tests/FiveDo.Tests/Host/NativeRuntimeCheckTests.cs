using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class NativeRuntimeCheckTests
{
    [Theory]
    [InlineData(NativeRuntimePlatform.Windows, "libSkiaSharp.dll", "libHarfBuzzSharp.dll")]
    [InlineData(NativeRuntimePlatform.Linux, "libSkiaSharp.so", "libHarfBuzzSharp.so")]
    [InlineData(NativeRuntimePlatform.MacOS, "libSkiaSharp.dylib", "libHarfBuzzSharp.dylib")]
    public void FindMissing_ReportsBothLibraries_WhenTheFolderIsEmpty(
        NativeRuntimePlatform platform,
        string skia,
        string harfBuzz)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-native-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            IReadOnlyList<string> missing = NativeRuntimeCheck.FindMissing(directory, platform);

            Assert.Equal([skia, harfBuzz], missing);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FindMissing_IsEmpty_WhenEveryRequiredFileExists()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-native-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var name in NativeRuntimeCheck.RequiredLibraries(NativeRuntimePlatform.Windows))
            {
                File.WriteAllBytes(Path.Combine(directory, name), []);
            }

            Assert.Empty(NativeRuntimeCheck.FindMissing(directory, NativeRuntimePlatform.Windows));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void IsSelfContainedApphost_UsesTheManagedAssemblyPresence()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-native-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Assert.True(NativeRuntimeCheck.IsSelfContainedApphost(directory, "FiveDo.dll"));

            File.WriteAllBytes(Path.Combine(directory, "FiveDo.dll"), []);
            Assert.False(NativeRuntimeCheck.IsSelfContainedApphost(directory, "FiveDo.dll"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void BuildStartupMessage_NamesTheMissingFilesAndTheFolder()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-native-{Guid.NewGuid():N}");

        var message = NativeRuntimeCheck.BuildStartupMessage(
            directory,
            ["libSkiaSharp.dll"],
            NativeRuntimePlatform.Windows);

        Assert.Contains("libSkiaSharp.dll", message, StringComparison.Ordinal);
        Assert.Contains(directory, message, StringComparison.Ordinal);
        Assert.Contains("Extract All", message, StringComparison.Ordinal);
        Assert.DoesNotContain("libHarfBuzzSharp.dll", message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildStartupMessage_GuidesLinuxAndMacUsers()
    {
        var linux = NativeRuntimeCheck.BuildStartupMessage(
            "/opt/5do",
            ["libSkiaSharp.so"],
            NativeRuntimePlatform.Linux);
        var mac = NativeRuntimeCheck.BuildStartupMessage(
            "/Applications/5DO.app/Contents/MacOS",
            ["libSkiaSharp.dylib"],
            NativeRuntimePlatform.MacOS);

        Assert.Contains("tar.gz", linux, StringComparison.Ordinal);
        Assert.Contains(".dmg", mac, StringComparison.Ordinal);
    }
}
