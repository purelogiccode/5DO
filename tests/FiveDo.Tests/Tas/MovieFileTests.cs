using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Tas;

namespace FiveDo.Tests.Tas;

public sealed class MovieFileTests
{
    [Fact]
    public void Roundtrip_PreservesHeaderFramesAndState()
    {
        var header = new MovieHeader
        {
            GameName = "Test Game",
            GameId = "ABCD1234",
            DiscHash = "disc-hash",
            DiscFile = "test.chd",
            BiosMd5 = "0123456789abcdef0123456789abcdef",
            BiosName = "Test BIOS",
            StartFrame = 120,
            Notes = "roundtrip",
            Options = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["armTiming"] = "Arm60Datasheet",
                ["cdSpeed"] = "X8",
            },
        };
        var frames = new List<byte[]>
        {
            new byte[] { 0x00, 0x48 },
            new byte[] { 0x80, 0x80, 0x01, 0x02 },
            Array.Empty<byte>(),
        };
        var state = new byte[] { 1, 2, 3, 4, 5 };
        var movie = new Movie(header, frames, state);
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-movie-{Guid.NewGuid():N}.5domovie");
        try
        {
            MovieFile.Save(path, movie);
            Movie loaded = MovieFile.Load(path);

            Assert.Equal(3, loaded.FrameCount);
            Assert.Equal("Test Game", loaded.Header.GameName);
            Assert.Equal("ABCD1234", loaded.Header.GameId);
            Assert.Equal("disc-hash", loaded.Header.DiscHash);
            Assert.Equal("test.chd", loaded.Header.DiscFile);
            Assert.Equal(120, loaded.Header.StartFrame);
            Assert.Equal("roundtrip", loaded.Header.Notes);
            Assert.Equal("Arm60Datasheet", loaded.Header.Options!["armTiming"]);
            Assert.Equal("X8", loaded.Header.Options["cdSpeed"]);
            Assert.True(loaded.Header.HasState);
            Assert.Equal(state, loaded.State);
            for (var i = 0; i < frames.Count; i++)
            {
                Assert.Equal(frames[i], loaded.Frames[i]);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void EmptyMovie_Roundtrips()
    {
        var movie = new Movie(new MovieHeader { GameName = "Empty" }, []);
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-movie-{Guid.NewGuid():N}.5domovie");
        try
        {
            MovieFile.Save(path, movie);
            Movie loaded = MovieFile.Load(path);

            Assert.Empty(loaded.Frames);
            Assert.False(loaded.HasState);
            Assert.Equal(0, loaded.Header.FrameCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CorruptPayload_IsRejected()
    {
        Movie movie = CreateSimpleMovie();
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-movie-{Guid.NewGuid():N}.5domovie");
        try
        {
            MovieFile.Save(path, movie);
            byte[] bytes = File.ReadAllBytes(path);
            bytes[^1] ^= 0xFF;
            File.WriteAllBytes(path, bytes);

            Assert.Throws<InvalidDataException>(() => MovieFile.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnsupportedVersion_IsRejected()
    {
        Movie movie = CreateSimpleMovie();
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-movie-{Guid.NewGuid():N}.5domovie");
        try
        {
            MovieFile.Save(path, movie);
            byte[] bytes = File.ReadAllBytes(path);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, 4), 99);
            File.WriteAllBytes(path, bytes);

            Assert.Throws<NotSupportedException>(() => MovieFile.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TrailingData_IsRejected()
    {
        Movie movie = CreateSimpleMovie();
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-movie-{Guid.NewGuid():N}.5domovie");
        try
        {
            MovieFile.Save(path, movie);
            using (var stream = new FileStream(path, FileMode.Append))
            {
                stream.WriteByte(0x55);
            }

            Assert.Throws<InvalidDataException>(() => MovieFile.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void NotAMovie_IsRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-movie-{Guid.NewGuid():N}.5domovie");
        try
        {
            File.WriteAllBytes(path, new byte[64]);
            Assert.Throws<InvalidDataException>(() => MovieFile.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_ReplacesAnExistingFileAtomically()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fivedo-movie-{Guid.NewGuid():N}.5domovie");
        try
        {
            MovieFile.Save(path, CreateSimpleMovie());
            MovieFile.Save(path, new Movie(new MovieHeader { GameName = "Second" }, [[0x01]]));

            Movie loaded = MovieFile.Load(path);
            Assert.Equal("Second", loaded.Header.GameName);
            Assert.Single(loaded.Frames);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static Movie CreateSimpleMovie()
    {
        return new Movie(
            new MovieHeader { GameName = "Simple", StartFrame = 7 },
            [[0x00, 0x48], [0x80, 0x80]]);
    }
}
