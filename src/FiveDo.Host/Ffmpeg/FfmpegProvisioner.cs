using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Core;

namespace FiveDo.Host;

/// <summary>One pinned FFmpeg download for a supported platform.</summary>
/// <param name="Platform">Platform label shown to the user.</param>
/// <param name="Url">HTTPS download URL.</param>
/// <param name="Sha256">Expected SHA-256 of the downloaded executable.</param>
/// <param name="SizeBytes">Expected executable size.</param>
/// <param name="Version">FFmpeg version string.</param>
public sealed record FfmpegDownloadSource(
    string Platform,
    string Url,
    string Sha256,
    long SizeBytes,
    string Version)
{
    /// <summary>Gets the download size in mebibytes for display.</summary>
    public double SizeMiB => SizeBytes / (1024.0 * 1024.0);
}

/// <summary>Progress for an FFmpeg download and verification.</summary>
/// <param name="Stage">Current stage.</param>
/// <param name="BytesReceived">Bytes written so far.</param>
/// <param name="TotalBytes">Expected total bytes, or zero when unknown.</param>
public readonly record struct FfmpegInstallProgress(
    string Stage,
    long BytesReceived,
    long TotalBytes)
{
    /// <summary>Gets the percentage complete, or zero when the total is unknown.</summary>
    public double Percent => TotalBytes > 0
        ? Math.Clamp(BytesReceived * 100.0 / TotalBytes, 0, 100)
        : 0;
}

/// <summary>The installed FFmpeg executable.</summary>
/// <param name="Path">Absolute executable path.</param>
/// <param name="Version">Detected version string.</param>
/// <param name="SourceUrl">Pinned source URL.</param>
/// <param name="Sha256">Verified SHA-256.</param>
/// <param name="Bytes">Downloaded bytes.</param>
public sealed record FfmpegInstallResult(
    string Path,
    string Version,
    string SourceUrl,
    string Sha256,
    long Bytes);

/// <summary>
/// Locates, downloads, verifies, and installs a pinned FFmpeg executable.
/// </summary>
/// <remarks>
/// <para>
/// FFmpeg remains an optional external program. The downloader is explicit: the desktop
/// asks before downloading, and the CLI requires <c>5do ffmpeg install</c>. The executable
/// is verified against a pinned SHA-256 before it is moved into place. On Windows and
/// portable Linux folders the file is written beside 5DO; read-only installs and macOS
/// app bundles fall back to the user's data directory.
/// </para>
/// <para>
/// The pinned static builds are FFmpeg 6.1.1 on Windows, 7.0.2 on Linux, and 6.0 on
/// macOS, each with libx264 (or the platform encoder) and AAC. The Windows arm64 build
/// uses the x64 FFmpeg under Windows' x64 emulation. FFmpeg is not distributed inside
/// 5DO; see <see href="https://ffmpeg.org/legal.html"/>.
/// </para>
/// </remarks>
public static class FfmpegProvisioner
{
    /// <summary>Maximum download size accepted from the server.</summary>
    public const long MaximumDownloadBytes = 160L * 1024 * 1024;

    private const string BaseUrl =
        "https://github.com/eugeneware/ffmpeg-static/releases/download/b6.1.1/";
    private const int BufferSize = 128 * 1024;

    private static readonly HttpClient SharedHttp = CreateHttpClient();

    /// <summary>Gets the pinned source for this process, or <see langword="null"/> when unsupported.</summary>
    public static FfmpegDownloadSource? CurrentSource => GetSource(
        RuntimeInformation.ProcessArchitecture,
        OperatingSystem.IsWindows(),
        OperatingSystem.IsLinux(),
        OperatingSystem.IsMacOS());

    /// <summary>Gets the pinned source for explicit platform inputs.</summary>
    /// <param name="architecture">Process architecture.</param>
    /// <param name="windows">Whether the target is Windows.</param>
    /// <param name="linux">Whether the target is Linux.</param>
    /// <param name="macOS">Whether the target is macOS.</param>
    /// <returns>The source, or <see langword="null"/> for an unsupported platform.</returns>
    internal static FfmpegDownloadSource? GetSource(
        Architecture architecture,
        bool windows,
        bool linux,
        bool macOS)
    {
        if (windows && architecture is Architecture.X64 or Architecture.Arm64)
        {
            return new FfmpegDownloadSource(
                architecture == Architecture.Arm64 ? "Windows arm64 (x64 emulation)" : "Windows x64",
                BaseUrl + "ffmpeg-win32-x64",
                "04e1307997530f9cf2fe35cba2ca7e8875ca91da02f89d6c7243df819c94ad00",
                82_797_568,
                "6.1.1");
        }

        if (linux && architecture == Architecture.X64)
        {
            return new FfmpegDownloadSource(
                "Linux x64",
                BaseUrl + "ffmpeg-linux-x64",
                "e7e7fb30477f717e6f55f9180a70386c62677ef8a4d4d1a5d948f4098aa3eb99",
                79_826_272,
                "7.0.2");
        }

        if (linux && architecture == Architecture.Arm64)
        {
            return new FfmpegDownloadSource(
                "Linux arm64",
                BaseUrl + "ffmpeg-linux-arm64",
                "6bb182d0d75d23028db82e9e4f723ca69b853d055698486e6984ddb2c06fb8ce",
                51_134_160,
                "7.0.2");
        }

        if (macOS && architecture == Architecture.Arm64)
        {
            return new FfmpegDownloadSource(
                "macOS arm64",
                BaseUrl + "ffmpeg-darwin-arm64",
                "a90e3db6a3fd35f6074b013f948b1aa45b31c6375489d39e572bea3f18336584",
                45_568_216,
                "6.0");
        }

        return null;
    }

    /// <summary>Finds an installed FFmpeg without starting a process.</summary>
    /// <returns>The absolute path, or <see langword="null"/> when FFmpeg is unavailable.</returns>
    public static string? FindInstalled()
    {
        var explicitPath = Environment.GetEnvironmentVariable("FIVEDO_FFMPEG");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        string executable = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        string local = Path.Combine(AppContext.BaseDirectory, executable);
        if (File.Exists(local))
        {
            return Path.GetFullPath(local);
        }

        string tools = Path.Combine(PortablePaths.DataDirectory, "tools", executable);
        if (File.Exists(tools))
        {
            return Path.GetFullPath(tools);
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim('"'), executable);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                // A malformed PATH entry is ignored; the remaining entries are still useful.
            }
        }

        return null;
    }

    /// <summary>Finds an installed FFmpeg that reports H.264 and AAC support.</summary>
    /// <returns>The absolute path, or <see langword="null"/> when no usable FFmpeg is available.</returns>
    public static string? FindUsable()
    {
        string? path = FindInstalled();
        return path is not null && VerifyExecutable(path, expectedVersion: null)
            ? path
            : null;
    }

    /// <summary>Gets the directory where a new executable will be written.</summary>
    /// <returns>The application directory when writable, otherwise the user data tools folder.</returns>
    public static string GetInstallDirectory()
    {
        if (!OperatingSystem.IsMacOS() && IsWritable(AppContext.BaseDirectory))
        {
            return Path.GetFullPath(AppContext.BaseDirectory);
        }

        var directory = Path.Combine(PortablePaths.DataDirectory, "tools");
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>Downloads and installs FFmpeg if it is not already available.</summary>
    /// <param name="progress">Optional progress receiver.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="force">Whether an existing installed copy is replaced.</param>
    /// <returns>The installed executable.</returns>
    /// <exception cref="InvalidOperationException">The platform is unsupported.</exception>
    /// <exception cref="InvalidDataException">The download failed verification.</exception>
    /// <exception cref="IOException">The download or install failed.</exception>
    public static Task<FfmpegInstallResult> InstallAsync(
        IProgress<FfmpegInstallProgress>? progress = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var source = CurrentSource
            ?? throw new InvalidOperationException(
                "Automatic FFmpeg installation is not available for this platform. "
                + "Install FFmpeg manually or set FIVEDO_FFMPEG.");
        return InstallAsync(source, progress, force, httpClient: null, targetDirectory: null, cancellationToken);
    }

    /// <summary>Downloads and installs one explicit source.</summary>
    /// <param name="source">Pinned source.</param>
    /// <param name="progress">Optional progress receiver.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="force">Whether an existing installed copy is replaced.</param>
    /// <param name="httpClient">Optional HTTP client for tests.</param>
    /// <param name="targetDirectory">Optional target directory for tests.</param>
    /// <returns>The installed executable.</returns>
    internal static async Task<FfmpegInstallResult> InstallAsync(
        FfmpegDownloadSource source,
        IProgress<FfmpegInstallProgress>? progress,
        bool force,
        HttpClient? httpClient,
        string? targetDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The FFmpeg download URL must be HTTPS.");
        }

        if (!IsSha256(source.Sha256))
        {
            throw new InvalidDataException("The FFmpeg download is missing a valid SHA-256.");
        }

        if (source.SizeBytes is <= 0 or > MaximumDownloadBytes)
        {
            throw new InvalidDataException("The FFmpeg download size is invalid.");
        }

        string directory = targetDirectory ?? GetInstallDirectory();
        Directory.CreateDirectory(directory);
        string executableName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        string target = Path.Combine(directory, executableName);
        if (!force
            && File.Exists(target)
            && VerifyExecutable(target, source.Version))
        {
            return new FfmpegInstallResult(
                target,
                source.Version,
                source.Url,
                source.Sha256,
                new FileInfo(target).Length);
        }

        string temporary = Path.Combine(
            directory,
            "." + executableName + "." + Guid.NewGuid().ToString("N") + ".download");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("5DO-FFmpegInstaller/1.0");
            HttpClient client = httpClient ?? SharedHttp;
            using HttpResponseMessage response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            long? contentLength = response.Content.Headers.ContentLength;
            if (contentLength > MaximumDownloadBytes)
            {
                throw new InvalidDataException(
                    $"The FFmpeg download is {contentLength} bytes, larger than the {MaximumDownloadBytes}-byte limit.");
            }

            long total = contentLength ?? source.SizeBytes;
            using (Stream input = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false))
            using (var output = new FileStream(
                temporary,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[BufferSize];
                long received = 0;
                progress?.Report(new FfmpegInstallProgress("Downloading", 0, total));
                while (true)
                {
                    int read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    received += read;
                    if (received > MaximumDownloadBytes)
                    {
                        throw new InvalidDataException("The FFmpeg download exceeded the size limit.");
                    }

                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    progress?.Report(new FfmpegInstallProgress("Downloading", received, total));
                }

                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                byte[] actual = hash.GetHashAndReset();
                string actualHex = Convert.ToHexString(actual).ToLowerInvariant();
                if (!string.Equals(actualHex, source.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "The FFmpeg download failed its SHA-256 check. The file was not installed.");
                }

                if (received != source.SizeBytes)
                {
                    throw new InvalidDataException(
                        $"The FFmpeg download is {received} bytes; expected {source.SizeBytes}.");
                }
            }

            progress?.Report(new FfmpegInstallProgress("Verifying", source.SizeBytes, source.SizeBytes));
            File.Move(temporary, target, overwrite: true);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    target,
                    UnixFileMode.UserRead
                    | UnixFileMode.UserWrite
                    | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead
                    | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead
                    | UnixFileMode.OtherExecute);
            }

            if (!VerifyExecutable(target, source.Version))
            {
                TryDelete(target);
                throw new InvalidDataException(
                    "The downloaded FFmpeg did not start or did not report the expected version.");
            }

            progress?.Report(new FfmpegInstallProgress("Installed", source.SizeBytes, source.SizeBytes));
            return new FfmpegInstallResult(
                target,
                source.Version,
                source.Url,
                source.Sha256,
                source.SizeBytes);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>Gets the FFmpeg version reported by an executable.</summary>
    /// <param name="path">Executable path.</param>
    /// <returns>The version token, or <see langword="null"/> when it cannot be read.</returns>
    public static string? GetVersion(string path)
    {
        string? output = RunCapture(path, ["-hide_banner", "-version"]);
        if (output is null)
        {
            return null;
        }

        const string marker = "ffmpeg version ";
        int index = output.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        int start = index + marker.Length;
        int end = output.IndexOfAny([' ', '\r', '\n'], start);
        return end > start ? output[start..end] : output[start..].Trim();
    }

    /// <summary>Determines whether a file is a usable FFmpeg with H.264 and AAC.</summary>
    /// <param name="path">Executable path.</param>
    /// <param name="expectedVersion">Expected version prefix, or <see langword="null"/> to skip the version check.</param>
    /// <returns><see langword="true"/> when the executable reports the expected version and encoders.</returns>
    public static bool VerifyExecutable(string path, string? expectedVersion)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        string? version = GetVersion(path);
        if (version is null
            || (expectedVersion is not null
                && !version.StartsWith(expectedVersion, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        string? encoders = RunCapture(path, ["-hide_banner", "-encoders"]);
        return encoders is not null
            && encoders.Contains(" aac ", StringComparison.Ordinal)
            && (encoders.Contains("libx264", StringComparison.Ordinal)
                || encoders.Contains("h264_nvenc", StringComparison.Ordinal)
                || encoders.Contains("h264_qsv", StringComparison.Ordinal)
                || encoders.Contains("h264_amf", StringComparison.Ordinal)
                || encoders.Contains("h264_videotoolbox", StringComparison.Ordinal));
    }

    private static string? RunCapture(string path, string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = new Process();
            process.StartInfo = startInfo;
            if (!process.Start())
            {
                return null;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5_000);
                }
                catch (Exception exception) when (exception is InvalidOperationException
                    or System.ComponentModel.Win32Exception)
                {
                    // The process already exited or could not be killed; the timeout result stands.
                }

                return null;
            }

            Task.WaitAll(outputTask, errorTask);
            return outputTask.Result + errorTask.Result;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception
            or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsWritable(string directory)
    {
        try
        {
            string probe = Path.Combine(
                directory,
                ".5do-write-test-" + Guid.NewGuid().ToString("N"));
            using (File.Create(probe))
            {
            }

            File.Delete(probe);
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            // The best-effort write test only chooses an install directory.
            return false;
        }
    }

    private static bool IsSha256(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10),
        };
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            // Best-effort cleanup; the caller reports the original failure.
        }
    }
}
