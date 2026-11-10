using System;
using System.IO;
using FiveDo.Core;
using FiveDo.Storage;

namespace FiveDo.Tests.Storage;

public sealed class DiscImageWriterTests
{
    [Theory]
    [InlineData(DiscImageFormat.Iso, ".iso")]
    [InlineData(DiscImageFormat.Bin, ".bin")]
    [InlineData(DiscImageFormat.Cue, ".cue")]
    [InlineData(DiscImageFormat.Chd, ".chd")]
    public void ConvertRoundTripsEveryDataSector(DiscImageFormat format, string extension)
    {
        string directory = Path.Combine(Path.GetTempPath(), "5do-convert-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string sourcePath = Path.Combine(directory, "source.iso");
            var expected = new byte[4 * IsoImage.SectorBytes];
            for (var i = 0; i < expected.Length; i++)
            {
                expected[i] = (byte)(i * 7);
            }

            File.WriteAllBytes(sourcePath, expected);
            string outputPath = Path.Combine(directory, "converted" + extension);

            using (IDiscSource source = DiscImageFactory.Open(sourcePath))
            {
                int sectors = DiscImageWriter.Write(source, outputPath, format);
                Assert.Equal(4, sectors);
            }

            using IDiscSource converted = DiscImageFactory.Open(outputPath);
            Assert.Equal(4, converted.SectorCount);
            var actual = new byte[IsoImage.SectorBytes];
            for (var lba = 0; lba < converted.SectorCount; lba++)
            {
                converted.ReadSector(lba, actual);
                Assert.Equal(
                    expected.AsSpan(lba * IsoImage.SectorBytes, IsoImage.SectorBytes).ToArray(),
                    actual);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
