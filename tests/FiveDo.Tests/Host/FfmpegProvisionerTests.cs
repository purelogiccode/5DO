using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Host;

namespace FiveDo.Tests.Host;

public sealed class FfmpegProvisionerTests
{
    [Theory]
    [InlineData("X64", true, false, false, "Windows x64", "04e1307997530f9cf2fe35cba2ca7e8875ca91da02f89d6c7243df819c94ad00", "6.1.1")]
    [InlineData("Arm64", true, false, false, "Windows arm64 (x64 emulation)", "04e1307997530f9cf2fe35cba2ca7e8875ca91da02f89d6c7243df819c94ad00", "6.1.1")]
    [InlineData("X64", false, true, false, "Linux x64", "e7e7fb30477f717e6f55f9180a70386c62677ef8a4d4d1a5d948f4098aa3eb99", "7.0.2")]
    [InlineData("Arm64", false, true, false, "Linux arm64", "6bb182d0d75d23028db82e9e4f723ca69b853d055698486e6984ddb2c06fb8ce", "7.0.2")]
    [InlineData("Arm64", false, false, true, "macOS arm64", "a90e3db6a3fd35f6074b013f948b1aa45b31c6375489d39e572bea3f18336584", "6.0")]
    public void GetSource_ReturnsPinnedSha256(
        string architecture,
        bool windows,
        bool linux,
        bool macOS,
        string platform,
        string sha256,
        string version)
    {
        var parsed = Enum.Parse<System.Runtime.InteropServices.Architecture>(architecture);
        FfmpegDownloadSource? source = FfmpegProvisioner.GetSource(parsed, windows, linux, macOS);

        Assert.NotNull(source);
        Assert.Equal(platform, source.Platform);
        Assert.Equal(sha256, source.Sha256);
        Assert.Equal(version, source.Version);
        Assert.StartsWith("https://", source.Url, StringComparison.Ordinal);
        Assert.InRange(source.SizeBytes, 1, FfmpegProvisioner.MaximumDownloadBytes);
    }

    [Fact]
    public void GetSource_RejectsUnsupportedPlatforms()
    {
        Assert.Null(FfmpegProvisioner.GetSource(
            System.Runtime.InteropServices.Architecture.X86,
            windows: true,
            linux: false,
            macOS: false));
        Assert.Null(FfmpegProvisioner.GetSource(
            System.Runtime.InteropServices.Architecture.X64,
            windows: false,
            linux: false,
            macOS: true));
    }

    [Fact]
    public async Task InstallAsync_ChecksumMismatchDoesNotInstall()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"fivedo-ffmpeg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var payload = new byte[] { 1, 2, 3, 4, 5 };
            var source = new FfmpegDownloadSource(
                "test",
                "https://example.invalid/ffmpeg",
                new string('0', 64),
                payload.Length,
                "6.1.1");
            using var client = new HttpClient(new FixedBytesHandler(payload));

            await Assert.ThrowsAsync<InvalidDataException>(() => FfmpegProvisioner.InstallAsync(
                source,
                progress: null,
                force: false,
                client,
                directory,
                CancellationToken.None));

            Assert.Empty(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FixedBytesHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            });
        }
    }
}
