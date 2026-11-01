using System;
using System.IO;
using System.Text;
using FiveDo.Core;
using FiveDo.Machine;
using FiveDo.Storage;

namespace FiveDo.Tests.Storage;

public sealed class ParserFuzzTests
{
    [Fact]
    public void StateLoader_MutatedStatesFailWithTypedErrors()
    {
        FiveDoMachine machine = CreateMachine();
        machine.RunFrames(2);
        using var stream = new MemoryStream();
        machine.SaveState(stream);
        byte[] baseline = stream.ToArray();
        var random = new Random(12345);

        for (var iteration = 0; iteration < 80; iteration++)
        {
            byte[] mutated = (byte[])baseline.Clone();
            int changes = random.Next(1, 5);
            for (var i = 0; i < changes; i++)
            {
                mutated[random.Next(mutated.Length)] ^= (byte)(1 << random.Next(8));
            }

            try
            {
                machine.LoadState(new MemoryStream(mutated));
            }
            catch (InvalidStateException)
            {
            }
        }
    }

    [Fact]
    public void IsoAndChdOpeners_RejectRandomHeadersWithoutCrashing()
    {
        var random = new Random(9876);
        var isoPath = Path.Combine(Path.GetTempPath(), $"fivedo-fuzz-{Guid.NewGuid():N}.iso");
        var chdPath = Path.Combine(Path.GetTempPath(), $"fivedo-fuzz-{Guid.NewGuid():N}.chd");
        try
        {
            for (var iteration = 0; iteration < 20; iteration++)
            {
                File.WriteAllBytes(isoPath, RandomBytes(random, 8192));
                AssertTypedOpenFailure(isoPath);

                File.WriteAllBytes(chdPath, RandomBytes(random, 8192));
                AssertTypedOpenFailure(chdPath);
            }
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(chdPath);
        }
    }

    [Fact]
    public void CueParser_MutatedTextNeverEscapesTypedErrors()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fivedo-cue-fuzz-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var binPath = Path.Combine(root, "disc.bin");
            File.WriteAllBytes(binPath, new byte[2048 * 4]);
            var cuePath = Path.Combine(root, "disc.cue");
            const string original = """
                FILE "disc.bin" BINARY
                  TRACK 01 MODE1/2048
                    INDEX 01 00:00:00
                """;
            var random = new Random(2468);
            for (var iteration = 0; iteration < 80; iteration++)
            {
                var text = new StringBuilder(original);
                int changes = random.Next(1, 4);
                for (var i = 0; i < changes; i++)
                {
                    int index = random.Next(text.Length);
                    if (text[index] is '\r' or '\n')
                    {
                        continue;
                    }

                    text[index] = (char)random.Next(32, 127);
                }

                File.WriteAllText(cuePath, text.ToString());
                try
                {
                    using IDiscSource disc = DiscImageFactory.Open(cuePath);
                    Assert.True(disc.SectorCount >= 0);
                }
                catch (Exception exception) when (IsTypedFailure(exception))
                {
                }
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertTypedOpenFailure(string path)
    {
        try
        {
            using IDiscSource disc = DiscImageFactory.Open(path);
            Assert.True(disc.SectorCount >= 0);
        }
        catch (Exception exception) when (IsTypedFailure(exception))
        {
        }
    }

    private static bool IsTypedFailure(Exception exception) =>
        exception is DiscImageException
            or InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or InvalidStateException;

    private static byte[] RandomBytes(Random random, int count)
    {
        var bytes = new byte[count];
        random.NextBytes(bytes);
        return bytes;
    }

    private static FiveDoMachine CreateMachine()
    {
        var machine = new FiveDoMachine();
        machine.LoadBios(new byte[EmulatorConstants.RomBankSize]);
        machine.Reset();
        return machine;
    }
}
