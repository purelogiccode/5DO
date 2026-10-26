using System;
using System.IO;
using FiveDo.Storage.Chd;

namespace FiveDo.Tests.Storage;

public sealed class ChdDiscImageTests
{
    [Fact]
    public void Open_NonChdFile_ThrowsInvalidDataException()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, new byte[4096]);
            Assert.Throws<InvalidDataException>(() => ChdDiscImage.Open(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Open_MissingFile_ThrowsInvalidDataException()
    {
        string path = Path.Combine(Path.GetTempPath(), $"five-do-missing-{Guid.NewGuid():N}.chd");
        Assert.Throws<InvalidDataException>(() => ChdDiscImage.Open(path));
    }
}
