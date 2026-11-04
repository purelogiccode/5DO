using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FiveDo.Tests.Cli;

[Collection("Console")]
public sealed class CliLibraryVerifyTests
{
    [Fact]
    public void Verify_ReportsKnownDumpAndWritesTheCache()
    {
        using var folder = new TempFolder();
        string iso = CreateIso(folder.Path, "game.iso", sectors: 2);
        string dat = CreateDat(folder.Path, "game.iso", File.ReadAllBytes(iso));
        string cache = Path.Combine(folder.Path, "health.json");

        CliResult result = Invoke(
            "library", "verify", "--folder", folder.Path, "--dat", dat, "--cache", cache, "--json");

        Assert.Equal(0, result.Code);
        Assert.Equal(string.Empty, result.Error);
        using JsonDocument json = JsonDocument.Parse(result.Output);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(1, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("verified").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("readErrors").GetInt32());
        JsonElement entry = Assert.Single(json.RootElement.GetProperty("entries").EnumerateArray());
        Assert.Equal("Verified", entry.GetProperty("status").GetString());
        Assert.Equal("Test Game", entry.GetProperty("gameName").GetString());
        Assert.True(File.Exists(cache));
        Assert.Contains("Test Game", File.ReadAllText(cache), StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_ReportsReadErrorAndReturnsOne()
    {
        using var folder = new TempFolder();
        File.WriteAllBytes(Path.Combine(folder.Path, "broken.iso"), new byte[1000]);
        string cache = Path.Combine(folder.Path, "health.json");

        CliResult result = Invoke(
            "library", "verify", "--folder", folder.Path, "--cache", cache, "--json");

        Assert.Equal(1, result.Code);
        using JsonDocument json = JsonDocument.Parse(result.Output);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(1, json.RootElement.GetProperty("readErrors").GetInt32());
        JsonElement entry = Assert.Single(json.RootElement.GetProperty("entries").EnumerateArray());
        Assert.Equal("ReadError", entry.GetProperty("status").GetString());
    }

    [Fact]
    public void Verify_EmptyFolderReturnsAnError()
    {
        using var folder = new TempFolder();

        CliResult result = Invoke("library", "verify", "--folder", folder.Path, "--cache", Path.Combine(folder.Path, "health.json"), "--json");

        Assert.NotEqual(0, result.Code);
        Assert.Contains("No supported disc images", result.Output, StringComparison.Ordinal);
    }

    private static string CreateIso(string folder, string name, int sectors)
    {
        var path = Path.Combine(folder, name);
        var data = new byte[2048 * sectors];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 251);
        }

        File.WriteAllBytes(path, data);
        return path;
    }

    private static string CreateDat(string folder, string romName, byte[] data)
    {
        var path = Path.Combine(folder, "library.dat");
        var md5 = Hash(data, HashAlgorithmName.MD5);
        var sha1 = Hash(data, HashAlgorithmName.SHA1);
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"""
            <?xml version="1.0"?>
            <datafile>
              <game name="Test Game">
                <description>CLI library verification test</description>
                <rom name="{romName}" size="{data.Length}" md5="{md5}" sha1="{sha1}"/>
              </game>
            </datafile>
            """);
        File.WriteAllText(path, builder.ToString());
        return path;
    }

    private static string Hash(byte[] data, HashAlgorithmName algorithm)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(algorithm);
        hash.AppendData(data);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static CliResult Invoke(params string[] args)
    {
        TextWriter previousOut = Console.Out;
        TextWriter previousError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var code = global::FiveDo.Cli.Program.Main(args);
            return new CliResult(code, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }

    private readonly record struct CliResult(int Code, string Output, string Error);

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "5do-library-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A failed cleanup must not hide the test result.
            }
        }
    }
}
