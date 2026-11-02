using System;
using System.IO;
using FiveDo.Core;

namespace FiveDo.Tests.Core;

public sealed class InstanceLockTests
{
    [Fact]
    public void TryAcquire_CreatesTheLockFileAndRejectsASecondOwner()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fivedo-lock-{Guid.NewGuid():N}");
        InstanceLock? first = null;
        InstanceLock? second = null;
        try
        {
            Assert.True(InstanceLock.TryAcquire(directory, out first, out var conflict));
            Assert.NotNull(first);
            Assert.Null(conflict);
            Assert.True(File.Exists(first.Path));
            Assert.Contains(
                Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                first.Owner,
                StringComparison.Ordinal);
            Assert.Contains(FiveDoVersion.Version, first.Owner, StringComparison.Ordinal);

            Assert.False(InstanceLock.TryAcquire(directory, out second, out conflict));
            Assert.Null(second);
            Assert.NotNull(conflict);
            Assert.Contains("Another 5DO instance", conflict, StringComparison.Ordinal);

            first.Dispose();
            first = null;
            Assert.False(File.Exists(Path.Combine(directory, InstanceLock.FileName)));

            Assert.True(InstanceLock.TryAcquire(directory, out second, out conflict));
            Assert.NotNull(second);
            Assert.Null(conflict);
        }
        finally
        {
            first?.Dispose();
            second?.Dispose();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void TryAcquire_CreatesAMissingDataDirectory()
    {
        var parent = Path.Combine(Path.GetTempPath(), $"fivedo-lock-{Guid.NewGuid():N}");
        var directory = Path.Combine(parent, "nested", "data");
        try
        {
            Assert.True(InstanceLock.TryAcquire(directory, out InstanceLock? instanceLock, out var conflict));
            using (instanceLock)
            {
                Assert.Null(conflict);
                Assert.True(Directory.Exists(directory));
            }
        }
        finally
        {
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
    }

    [Fact]
    public void ReadOwner_ReturnsNullWhenTheFileIsMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-lock-{Guid.NewGuid():N}", InstanceLock.FileName);

        Assert.Null(InstanceLock.ReadOwner(path));
    }

    [Fact]
    public void TryAcquire_ReportsAnUnwritableDestination()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-lock-{Guid.NewGuid():N}");
        File.WriteAllText(path, "not a directory");
        try
        {
            Assert.False(InstanceLock.TryAcquire(path, out InstanceLock? instanceLock, out var conflict));
            Assert.Null(instanceLock);
            Assert.NotNull(conflict);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
