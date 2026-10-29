using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FiveDo.Storage;

namespace FiveDo.Tests.Storage;

public sealed class OperaFileSystemTests
{
    [Fact]
    public void Mount_ReadsVolumeAndDirectoryTree()
    {
        var image = SyntheticOperaDisc.Build(
            [
                new("HELLO.TXT", IsDirectory: false, Lba: 2, LengthBytes: 5),
                new("DIR", IsDirectory: true, Lba: 3, LengthBytes: 2048),
                new("Disc label", IsDirectory: false, Lba: 0, LengthBytes: 132, Type: 6),
            ],
            blockCount: 6,
            label: "TESTDISC",
            directories: new Dictionary<uint, IReadOnlyList<SyntheticOperaDisc.EntrySpec>>
            {
                [3] = [new("NESTED.BIN", IsDirectory: false, Lba: 4, LengthBytes: 4)],
            });
        SyntheticOperaDisc.WriteFile(image, 2, "hello"u8);
        SyntheticOperaDisc.WriteFile(image, 4, "nest"u8);

        using var disc = new MemoryDisc(image);
        var fileSystem = OperaFileSystem.Mount(disc);

        Assert.Equal("TESTDISC", fileSystem.Label);
        Assert.Equal(
            "\\|\\HELLO.TXT|\\DIR|\\DIR\\NESTED.BIN|\\Disc label",
            string.Join('|', fileSystem.Entries.Select(entry => entry.Path)));
        Assert.Equal(
            "True,False,True,False,False",
            string.Join(',', fileSystem.Entries.Select(entry => entry.IsDirectory)));
        Assert.Equal(
            "0,5,2048,4,132",
            string.Join(',', fileSystem.Entries.Select(entry => entry.Size)));
        Assert.Equal(
            "1,2,3,4,0",
            string.Join(',', fileSystem.Entries.Select(entry => entry.Lba)));

        Assert.Equal("hello", ReadText(fileSystem, fileSystem.Entries[1]));
        Assert.Equal("nest", ReadText(fileSystem, fileSystem.Entries[3]));
        Assert.Equal(132, fileSystem.ReadFile(fileSystem.Entries[4], 0, new byte[132]));
    }

    [Fact]
    public void ReadFile_HonorsOffsetsAndEndOfFile()
    {
        var image = SyntheticOperaDisc.Build(
            [new("DATA.BIN", IsDirectory: false, Lba: 2, LengthBytes: 6)],
            blockCount: 4);
        SyntheticOperaDisc.WriteFile(image, 2, "abcdef"u8);
        using var disc = new MemoryDisc(image);
        var fileSystem = OperaFileSystem.Mount(disc);
        var entry = fileSystem.Entries.Single(item => !item.IsDirectory);

        var buffer = new byte[8];
        Assert.Equal(4, fileSystem.ReadFile(entry, 2, buffer));
        Assert.Equal("cdef", Encoding.ASCII.GetString(buffer, 0, 4));
        Assert.Equal(0, fileSystem.ReadFile(entry, 6, buffer));
        Assert.Equal(0, fileSystem.ReadFile(entry, 100, buffer));
    }

    [Fact]
    public void Mount_FollowsMultiBlockDirectories()
    {
        const int fileCount = 60;
        var entries = Enumerable.Range(0, fileCount)
            .Select(index => new SyntheticOperaDisc.EntrySpec(
                $"FILE{index:D3}",
                IsDirectory: false,
                Lba: (uint)(20 + index),
                LengthBytes: 1))
            .ToArray();
        var image = SyntheticOperaDisc.Build(entries, blockCount: 100);
        for (var i = 0; i < fileCount; i++)
        {
            SyntheticOperaDisc.WriteFile(image, (uint)(20 + i), [(byte)i]);
        }

        using var disc = new MemoryDisc(image);
        var fileSystem = OperaFileSystem.Mount(disc);

        Assert.Equal(fileCount + 1, fileSystem.Entries.Count);
        Assert.Equal("\\FILE000", fileSystem.Entries[1].Path);
        Assert.Equal("\\FILE059", fileSystem.Entries[60].Path);
        Assert.Equal(60, fileSystem.Entries.Count(entry => !entry.IsDirectory));
        var buffer = new byte[1];
        Assert.Equal(1, fileSystem.ReadFile(fileSystem.Entries[60], 0, buffer));
        Assert.Equal(59, buffer[0]);
    }

    [Fact]
    public void ReadFile_SkipsGapsBetweenBursts()
    {
        var image = SyntheticOperaDisc.Build(
            [new("STREAM.BIN", IsDirectory: false, Lba: 10, LengthBytes: 4 * 2048, Burst: 2, Gap: 1)],
            blockCount: 30);
        for (var i = 0; i < 4; i++)
        {
            var block = new byte[2048];
            Array.Fill(block, (byte)(i + 1));
            var physicalLba = i < 2 ? 10 + i : 13 + (i - 2);
            SyntheticOperaDisc.WriteFile(image, (uint)physicalLba, block);
        }

        using var disc = new MemoryDisc(image);
        var fileSystem = OperaFileSystem.Mount(disc);
        var entry = fileSystem.Entries.Single(item => !item.IsDirectory);

        var buffer = new byte[2048];
        Assert.Equal(2048, fileSystem.ReadFile(entry, 0, buffer));
        Assert.All(buffer, value => Assert.Equal((byte)1, value));
        Assert.Equal(2048, fileSystem.ReadFile(entry, 2048, buffer));
        Assert.All(buffer, value => Assert.Equal((byte)2, value));
        Assert.Equal(2048, fileSystem.ReadFile(entry, 4096, buffer));
        Assert.All(buffer, value => Assert.Equal((byte)3, value));
        Assert.Equal(2048, fileSystem.ReadFile(entry, 6144, buffer));
        Assert.All(buffer, value => Assert.Equal((byte)4, value));
    }

    [Fact]
    public void Mount_RejectsInvalidVolumeHeaders()
    {
        using (var empty = new MemoryDisc([]))
        {
            Assert.Throws<InvalidDataException>(() => OperaFileSystem.Mount(empty));
        }

        var image = SyntheticOperaDisc.Build(
            [new("FILE", IsDirectory: false, Lba: 2, LengthBytes: 1)],
            blockCount: 3);
        image[0] = 0;
        using (var badRecord = new MemoryDisc(image))
        {
            Assert.Throws<InvalidDataException>(() => OperaFileSystem.Mount(badRecord));
        }

        var noRoot = SyntheticOperaDisc.Build(
            [new("FILE", IsDirectory: false, Lba: 2, LengthBytes: 1)],
            blockCount: 3);
        noRoot.AsSpan(100, 4).Clear();
        using (var missingRoot = new MemoryDisc(noRoot))
        {
            Assert.Throws<InvalidDataException>(() => OperaFileSystem.Mount(missingRoot));
        }
    }

    [Fact]
    public void Mount_RejectsDirectoryTraversalNames()
    {
        var image = SyntheticOperaDisc.Build(
            [new("..\\escape", IsDirectory: false, Lba: 2, LengthBytes: 1)],
            blockCount: 3);

        using var disc = new MemoryDisc(image);
        Assert.Throws<InvalidDataException>(() => OperaFileSystem.Mount(disc));
    }

    private static string ReadText(OperaFileSystem fileSystem, OperaFileEntry entry)
    {
        var bytes = new byte[(int)entry.Size];
        var read = fileSystem.ReadFile(entry, 0, bytes);
        return Encoding.ASCII.GetString(bytes, 0, read);
    }

    private sealed class MemoryDisc : FiveDo.Core.IDiscSource
    {
        private readonly byte[] _image;

        internal MemoryDisc(byte[] image)
        {
            _image = image;
        }

        public int SectorCount => _image.Length / 2048;

        public int SectorSize => 2048;

        public void ReadSector(int lba, Span<byte> destination)
        {
            _image.AsSpan(lba * 2048, 2048).CopyTo(destination);
        }

        public void Dispose()
        {
        }
    }
}
