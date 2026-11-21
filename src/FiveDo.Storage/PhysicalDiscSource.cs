using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>
/// A 3DO disc source backed by a host optical drive.
/// </summary>
/// <remarks>
/// <para>
/// Data tracks are read through the host's cooked block interface, so every read returns
/// the 2048-byte user-data sector that <see cref="IDiscSource"/> requires. Data discs boot
/// exactly like the same disc in an image file.
/// </para>
/// <para>
/// The drive's table of contents is exposed through <see cref="ITrackedDiscSource"/>.
/// Audio tracks carry raw 2352-byte Red Book frames, read with
/// <c>IOCTL_CDROM_RAW_READ</c> in CDDA mode on Windows and <c>CDROMREADAUDIO</c> on Linux,
/// so a physical mixed-mode disc can play CD-DA through the 3DO CD player or a game.
/// </para>
/// <para>
/// Windows drives are written as <c>D:</c>, <c>D:\</c>, <c>\\.\D:</c>, or
/// <c>\\.\CdRom0</c>; Linux and macOS devices are written as <c>/dev/sr0</c> or the
/// platform's block-device path. The <c>CdRomN</c> form is used when Windows cannot
/// mount the disc's filesystem: a 3DO disc does not use ISO 9660, so it commonly has no
/// drive letter even though the drive and media are ready. Media is detected through
/// the device itself, not through the mounted volume. The source is read-only and never
/// writes to the disc.
/// </para>
/// </remarks>
public sealed class PhysicalDiscSource : ITrackedDiscSource
{
    /// <summary>Gets the size of one 3DO user-data sector.</summary>
    public const int SectorBytes = 2048;

    private const int MaxOpticalDevices = 32;
    private const uint DriveTypeCdRom = 5;
    private const uint IoctlStorageCheckVerify2 = 0x002D0800;
    private const uint IoctlCdromGetDriveGeometry = 0x0002404C;
    private const uint IoctlCdromReadToc = 0x00024000;
    private const uint IoctlCdromRawRead = 0x0002403E;
    private const int TrackModeCdda = 2;
    private const int TocBufferBytes = 4 + (100 * 8);
    private const int ErrorNotReady = 21;
    private const int GeometryBufferBytes = 24;

    private const nuint LinuxCdromReadTocHeader = 0x5305;
    private const nuint LinuxCdromReadTocEntry = 0x5306;
    private const nuint LinuxCdromReadAudio = 0x530E;
    private const byte LinuxCdromLba = 0x01;
    private const byte LinuxLeadOutTrack = 0xAA;
    private const byte LinuxControlDataTrack = 0x40;

    private readonly FileStream _stream;
    private readonly Lock _gate = new();
    private readonly byte[] _audioSector = new byte[PhysicalDiscToc.AudioSectorBytes];
    private readonly IReadOnlyList<DiscTrack> _tracks;
    private bool _disposed;

    private PhysicalDiscSource(
        string devicePath,
        FileStream stream,
        int cookedSectorCount,
        IReadOnlyList<DiscTrack> tracks)
    {
        DevicePath = devicePath;
        _stream = stream;
        CookedSectorCount = cookedSectorCount;
        _tracks = tracks.Count > 0
            ? PhysicalDiscToc.EnsureCoverage(tracks, cookedSectorCount)
            : [new DiscTrack(1, DiscTrackKind.Data, 0, cookedSectorCount, SectorBytes)];
        SectorCount = Math.Max(cookedSectorCount, _tracks[^1].EndLba);
    }

    /// <summary>Gets the normalized host device path.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "UnusedAutoPropertyAccessor.Global",
        Justification = "Public diagnostic identity used by host tooling and tests.")]
    public string DevicePath { get; }

    /// <inheritdoc />
    public int SectorCount { get; }

    /// <inheritdoc />
    public int SectorSize => SectorBytes;

    /// <summary>Gets the number of 2048-byte sectors addressable through the cooked interface.</summary>
    /// <remarks>
    /// A mixed-mode disc's cooked view covers its data area only; the remaining sectors
    /// are Red Book audio and are reachable through <see cref="ReadTrackSector"/>.
    /// </remarks>
    public int CookedSectorCount { get; }

    /// <inheritdoc />
    public IReadOnlyList<DiscTrack> Tracks => _tracks;

    /// <inheritdoc />
    public int TotalSectorCount => SectorCount;

    /// <summary>
    /// Determines whether a path names an optical drive root rather than an image file.
    /// </summary>
    /// <param name="path">Candidate path.</param>
    /// <returns><see langword="true"/> for a drive root or a Unix block device.</returns>
    public static bool IsOpticalDrivePath(string? path)
    {
        return TryNormalizeDrivePath(path, out _);
    }

    /// <summary>Lists attached optical drives that currently contain media.</summary>
    /// <returns>Device paths suitable for <see cref="Open(string)"/>.</returns>
    /// <remarks>
    /// Windows media detection is performed through <c>IOCTL_STORAGE_CHECK_VERIFY2</c>
    /// rather than <see cref="DriveInfo.IsReady"/>. The latter asks whether a filesystem
    /// volume is mounted, and a 3DO disc has a custom filesystem that Windows cannot
    /// mount. Drives without a drive letter are enumerated through their <c>CdRomN</c>
    /// device names.
    /// </remarks>
    public static IReadOnlyList<string> GetOpticalDriveNames()
    {
        if (!OperatingSystem.IsWindows())
        {
            var linuxDrives = new List<string>();
            for (var index = 0; index < 8; index++)
            {
                var device = $"/dev/sr{index}";
                if (File.Exists(device))
                {
                    linuxDrives.Add(device);
                }
            }

            return linuxDrives;
        }

        var lettersByDeviceIndex = new Dictionary<int, string>();
        var unmappedLetters = new List<string>();
        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            var root = string.Concat(letter, ":\\");
            if (GetDriveTypeW(root) != DriveTypeCdRom)
            {
                continue;
            }

            if (TryGetCdRomIndex(letter, out var deviceIndex))
            {
                lettersByDeviceIndex[deviceIndex] = string.Concat(letter, ":");
            }
            else
            {
                unmappedLetters.Add(string.Concat(letter, ":"));
            }
        }

        var drives = new List<string>();
        for (var index = 0; index < MaxOpticalDevices; index++)
        {
            var devicePath = $@"\\.\CdRom{index}";
            if (!HasOpticalMedia(devicePath))
            {
                continue;
            }

            drives.Add(lettersByDeviceIndex.TryGetValue(index, out var letterPath)
                ? letterPath
                : devicePath);
        }

        // A virtual or filter-driver optical device may not use the CdRomN names.
        // Keep lettered drives that could not be matched to a CdRomN device.
        foreach (var letterPath in unmappedLetters)
        {
            if (HasOpticalMedia(letterPath))
            {
                drives.Add(letterPath);
            }
        }

        return drives;
    }

    /// <summary>Opens a host optical drive.</summary>
    /// <param name="path">Drive root or block-device path.</param>
    /// <returns>An open disc source. The caller owns and must dispose it.</returns>
    /// <exception cref="DiscImageException">
    /// Thrown when the path is not an optical drive, contains no readable media, or the
    /// drive cannot be opened.
    /// </exception>
    public static PhysicalDiscSource Open(string path)
    {
        if (!TryNormalizeDrivePath(path, out var devicePath))
        {
            throw new DiscImageException($"'{path}' is not an optical drive path.");
        }

        if (OperatingSystem.IsWindows() && !IsCdRomDrive(devicePath))
        {
            throw new DiscImageException($"'{path}' is not a CD/DVD drive.");
        }

        FileStream? stream = null;
        try
        {
            stream = new FileStream(
                devicePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                SectorBytes,
                FileOptions.None);
            long length = ResolveLength(stream);
            if (length < 0 || length > (long)int.MaxValue * SectorBytes)
            {
                throw new DiscImageException($"The optical drive '{path}' reports an implausible media size.");
            }

            int cookedSectorCount = (int)(length / SectorBytes);
            IReadOnlyList<DiscTrack> tracks = TryReadTracks(stream, cookedSectorCount);
            int trackSectorCount = tracks.Count > 0 ? tracks[^1].EndLba : 0;
            if (Math.Max(cookedSectorCount, trackSectorCount) < 1)
            {
                throw new DiscImageException($"The optical drive '{path}' contains no readable media.");
            }

            return new PhysicalDiscSource(devicePath, stream, cookedSectorCount, tracks);
        }
        catch (DiscImageException)
        {
            // Empty media and implausible-size probing already produce a domain
            // exception; dispose the handle before letting it reach the caller.
            stream?.Dispose();
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            stream?.Dispose();
            throw new DiscImageException(
                $"The optical drive '{path}' could not be read: {exception.Message}",
                exception);
        }
    }

    /// <inheritdoc />
    public void ReadSector(int lba, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(lba);
        if (lba >= SectorCount)
        {
            throw new DiscImageException($"Sector {lba} is outside the optical disc ({SectorCount} sectors).");
        }

        if (FindTrack(lba) is { IsAudio: true })
        {
            // Red Book audio has no 2048-byte user data. A guest that reads an audio
            // LBA as data receives silence instead of a fault; CD-DA playback uses
            // ReadTrackSector with the native 2352-byte frame.
            destination[..SectorBytes].Clear();
            return;
        }

        if (CookedSectorCount > 0 && lba >= CookedSectorCount)
        {
            throw new DiscImageException(
                $"Sector {lba} is outside the drive's {CookedSectorCount}-sector data area.");
        }

        lock (_gate)
        {
            try
            {
                _stream.Position = (long)lba * SectorBytes;
                var read = _stream.ReadAtLeast(destination[..SectorBytes], SectorBytes, throwOnEndOfStream: false);
                if (read < SectorBytes)
                {
                    throw new DiscImageException(
                        $"The optical drive returned {read} of {SectorBytes} bytes for sector {lba}.");
                }
            }
            catch (IOException exception)
            {
                throw new DiscImageException(
                    $"The optical drive read failed at sector {lba}: {exception.Message}",
                    exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new DiscImageException(
                    $"The optical drive denied access at sector {lba}: {exception.Message}",
                    exception);
            }
        }
    }

    /// <inheritdoc />
    public int ReadTrackSector(int lba, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(lba);
        DiscTrack? track = FindTrack(lba);
        if (track is null)
        {
            throw new DiscImageException($"Sector {lba} is outside the optical disc ({SectorCount} sectors).");
        }

        if (destination.Length < track.SectorSize)
        {
            throw new ArgumentException(
                $"The destination must hold {track.SectorSize} bytes for track {track.Number}.",
                nameof(destination));
        }

        if (!track.IsAudio)
        {
            ReadSector(lba, destination[..SectorBytes]);
            return SectorBytes;
        }

        ReadAudioSector(lba, destination[..PhysicalDiscToc.AudioSectorBytes]);
        return PhysicalDiscToc.AudioSectorBytes;
    }

    /// <inheritdoc />
    public DiscTrack? FindTrack(int lba)
    {
        foreach (DiscTrack track in _tracks)
        {
            if (track.Contains(lba))
            {
                return track;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream.Dispose();
    }

    /// <summary>
    /// Reads one native 2352-byte Red Book frame with the platform's raw-audio request.
    /// </summary>
    /// <param name="lba">Audio-sector logical block address.</param>
    /// <param name="destination">Destination span of at least 2352 bytes.</param>
    private void ReadAudioSector(int lba, Span<byte> destination)
    {
        lock (_gate)
        {
            if (OperatingSystem.IsWindows())
            {
                ReadWindowsAudioSector(lba);
            }
            else if (OperatingSystem.IsLinux())
            {
                ReadLinuxAudioSector(lba);
            }
            else
            {
                throw new DiscImageException(
                    "CD-DA playback from a physical drive is not supported on this platform.");
            }

            _audioSector.AsSpan(0, PhysicalDiscToc.AudioSectorBytes).CopyTo(destination);
        }
    }

    private void ReadWindowsAudioSector(int lba)
    {
        var info = new RawReadInfo
        {
            // IOCTL_CDROM_RAW_READ offsets are expressed in 2048-byte units even when
            // the requested track mode returns 2352-byte CDDA frames.
            DiskOffset = (long)lba * SectorBytes,
            SectorCount = 1,
            TrackMode = TrackModeCdda,
        };
        IntPtr handle = _stream.SafeFileHandle.DangerousGetHandle();
        if (!DeviceIoControl(
                handle,
                IoctlCdromRawRead,
                ref info,
                Marshal.SizeOf<RawReadInfo>(),
                _audioSector,
                PhysicalDiscToc.AudioSectorBytes,
                out int returned,
                IntPtr.Zero)
            || returned < PhysicalDiscToc.AudioSectorBytes)
        {
            int error = Marshal.GetLastPInvokeError();
            var inner = new System.ComponentModel.Win32Exception(error);
            throw new DiscImageException(
                $"The optical drive could not read audio sector {lba}: {inner.Message}",
                inner);
        }
    }

    private void ReadLinuxAudioSector(int lba)
    {
        IntPtr fd = _stream.SafeFileHandle.DangerousGetHandle();
        var request = new LinuxReadAudioRequest
        {
            Lba = lba,
            AddressFormat = LinuxCdromLba,
            FrameCount = 1,
            Buffer = IntPtr.Zero,
        };
        GCHandle pin = GCHandle.Alloc(_audioSector, GCHandleType.Pinned);
        try
        {
            request.Buffer = pin.AddrOfPinnedObject();
            if (LinuxIoctlReadAudio(fd, LinuxCdromReadAudio, ref request) != 0)
            {
                int error = Marshal.GetLastPInvokeError();
                throw new DiscImageException(
                    $"The optical drive could not read audio sector {lba} (CDROMREADAUDIO errno {error}).");
            }
        }
        finally
        {
            pin.Free();
        }
    }

    private static IReadOnlyList<DiscTrack> TryReadTracks(FileStream stream, int fallbackLeadOutLba)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var buffer = new byte[TocBufferBytes];
                IntPtr handle = stream.SafeFileHandle.DangerousGetHandle();
                if (!DeviceIoControlBuffer(handle, IoctlCdromReadToc, buffer, out int returned)
                    || returned <= 0)
                {
                    return [];
                }

                // The CDROM_TOC Length field is not trustworthy on every driver (Windows'
                // ISO mounter reports 4608 for a 20-byte response), so parse exactly the
                // bytes the device wrote.
                return PhysicalDiscToc.ParseWindowsToc(
                    buffer.AsSpan(0, Math.Min(returned, buffer.Length)),
                    fallbackLeadOutLba);
            }

            if (OperatingSystem.IsLinux())
            {
                return ReadLinuxTracks(stream.SafeFileHandle.DangerousGetHandle());
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            Serilog.Log.Debug(exception, "The optical drive's table of contents could not be read.");
        }

        return [];
    }

    private static IReadOnlyList<DiscTrack> ReadLinuxTracks(IntPtr fd)
    {
        var header = default(LinuxTocHeader);
        if (LinuxIoctlTocHeader(fd, LinuxCdromReadTocHeader, ref header) != 0
            || header.FirstTrack == 0
            || header.LastTrack < header.FirstTrack)
        {
            return [];
        }

        var entries = new List<DiscTocEntry>(header.LastTrack - header.FirstTrack + 1);
        for (int number = header.FirstTrack; number <= header.LastTrack; number++)
        {
            var entry = new LinuxTocEntry { Track = (byte)number, Format = LinuxCdromLba };
            if (LinuxIoctlTocEntry(fd, LinuxCdromReadTocEntry, ref entry) != 0)
            {
                continue;
            }

            // The Linux control nibble is the high half of the ADR/control byte;
            // CDROM_DATA_TRACK (0x04) therefore appears as 0x40.
            entries.Add(new DiscTocEntry(number, (entry.AdrControl & LinuxControlDataTrack) == 0, entry.Lba));
        }

        var leadOut = new LinuxTocEntry { Track = LinuxLeadOutTrack, Format = LinuxCdromLba };
        int leadOutLba = LinuxIoctlTocEntry(fd, LinuxCdromReadTocEntry, ref leadOut) == 0
            ? leadOut.Lba
            : -1;
        return PhysicalDiscToc.BuildTracks(entries, leadOutLba);
    }

    private static long ResolveLength(FileStream stream)
    {
        try
        {
            if (stream.Length > 0)
            {
                return stream.Length;
            }
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException)
        {
            // Device handles commonly report no length through fstat; probe below.
            Serilog.Log.Debug(exception, "The device length could not be read directly; probing instead.");
        }

        if (OperatingSystem.IsWindows())
        {
            long windowsLength = WindowsDiskLength(stream);
            if (windowsLength > 0)
            {
                return windowsLength;
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            long linuxLength = LinuxBlockDeviceLength(stream);
            if (linuxLength > 0)
            {
                return linuxLength;
            }
        }

        return ProbeLength(stream);
    }

    private static long ProbeLength(FileStream stream)
    {
        var buffer = new byte[SectorBytes];
        long low = 0;
        long high = 1;
        while (TryReadSector(stream, high, buffer))
        {
            low = high + 1;
            high *= 2;
            if (high > 4L * 1024 * 1024 * 1024 / SectorBytes)
            {
                throw new DiscImageException("The optical drive reports an implausible media size.");
            }
        }

        while (low < high)
        {
            long middle = low + ((high - low) / 2);
            if (TryReadSector(stream, middle, buffer))
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        stream.Position = 0;
        return low * SectorBytes;
    }

    private static bool TryReadSector(FileStream stream, long lba, byte[] buffer)
    {
        try
        {
            stream.Position = lba * SectorBytes;
            return stream.ReadAtLeast(buffer, SectorBytes, throwOnEndOfStream: false) == SectorBytes;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Debug(exception, "Sector {Lba} could not be read from the physical disc.", lba);
            return false;
        }
    }

    private static bool IsCdRomDrive(string devicePath)
    {
        // A CdRomN device name identifies an optical device directly; the drive-type
        // query only applies to lettered roots.
        if (TryParseCdRomDeviceName(devicePath, out _))
        {
            return true;
        }

        // Normalization always produces \\.\X:, so the drive root is X:\.
        var root = devicePath.Length >= 6
            ? devicePath[4..] + "\\"
            : devicePath;
        return GetDriveTypeW(root) == DriveTypeCdRom;
    }

    private static bool HasOpticalMedia(string devicePath)
    {
        try
        {
            using var stream = new FileStream(
                devicePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                SectorBytes,
                FileOptions.None);
            IntPtr handle = stream.SafeFileHandle.DangerousGetHandle();

            // CHECK_VERIFY2 works without a mounted filesystem, which is required for a
            // 3DO disc: Windows does not recognize its custom filesystem and therefore
            // does not create a ready volume or a drive letter.
            if (DeviceIoControlNoOutput(handle, IoctlStorageCheckVerify2))
            {
                return true;
            }

            if (Marshal.GetLastWin32Error() == ErrorNotReady)
            {
                return false;
            }

            if (WindowsDiskLength(stream) > 0)
            {
                return true;
            }

            // Some filter drivers do not implement CHECK_VERIFY2. The CD-ROM geometry
            // request is the older, widely supported media-presence probe.
            var geometry = new byte[GeometryBufferBytes];
            return DeviceIoControlBuffer(handle, IoctlCdromGetDriveGeometry, geometry, out _);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            Serilog.Log.Debug(
                exception,
                "Optical device {Device} could not be probed for media.",
                devicePath);
            return false;
        }
    }

    private static bool TryGetCdRomIndex(char letter, out int index)
    {
        index = -1;
        var target = new char[64];
        var length = QueryDosDeviceW(
            string.Concat(letter, ":"),
            target,
            (uint)target.Length);
        if (length == 0)
        {
            return false;
        }

        var targetText = new string(target).TrimEnd('\0');
        return TryParseCdRomTarget(targetText, out index);
    }

    private static bool TryParseCdRomTarget(string target, out int index)
    {
        const string prefix = @"\Device\CdRom";
        index = -1;
        return target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(
                target.AsSpan(prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out index);
    }

    private static bool TryParseCdRomDeviceName(string value, out int index)
    {
        const string prefix = @"\\.\CdRom";
        index = -1;
        return value.Length > prefix.Length
            && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(
                value.AsSpan(prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out index)
            && index < MaxOpticalDevices;
    }

    private static long WindowsDiskLength(FileStream stream)
    {
        const uint IoctlDiskGetLengthInfo = 0x0007405C;
        IntPtr handle = stream.SafeFileHandle.DangerousGetHandle();
        if (!DeviceIoControl(handle, IoctlDiskGetLengthInfo, IntPtr.Zero, 0, out long length, sizeof(long), out _, IntPtr.Zero))
        {
            return 0;
        }

        return length;
    }

    private static bool DeviceIoControlNoOutput(IntPtr handle, uint controlCode)
    {
        return DeviceIoControl(
            handle,
            controlCode,
            IntPtr.Zero,
            0,
            IntPtr.Zero,
            0,
            out _,
            IntPtr.Zero);
    }

    private static bool DeviceIoControlBuffer(
        IntPtr handle,
        uint controlCode,
        byte[] output,
        out int returned)
    {
        return DeviceIoControl(
            handle,
            controlCode,
            IntPtr.Zero,
            0,
            output,
            output.Length,
            out returned,
            IntPtr.Zero);
    }

    private static long LinuxBlockDeviceLength(FileStream stream)
    {
        const ulong IoctlBlockGetSize64 = 0x80081272;
        IntPtr handle = stream.SafeFileHandle.DangerousGetHandle();
        return Ioctl(handle, IoctlBlockGetSize64, out ulong size) == 0 && size > 0
            ? (long)Math.Min(size, long.MaxValue)
            : 0;
    }

    private static bool TryNormalizeDrivePath(string? path, out string devicePath)
    {
        devicePath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var value = path.Trim();
        if (TryParseCdRomDeviceName(value, out var cdRomIndex))
        {
            devicePath = $@"\\.\CdRom{cdRomIndex}";
            return true;
        }

        if (value.Length is >= 6 and <= 7
            && (value.StartsWith(@"\\.\", StringComparison.Ordinal)
                || value.StartsWith(@"\\?\", StringComparison.Ordinal))
            && char.IsAsciiLetter(value[4])
            && value[5] == ':'
            && (value.Length == 6 || value[6] is '\\' or '/'))
        {
            devicePath = $@"\\.\{char.ToUpperInvariant(value[4])}:";
            return true;
        }

        if (value.Length is 2 or 3
            && char.IsAsciiLetter(value[0])
            && value[1] == ':'
            && (value.Length == 2 || value[2] is '\\' or '/'))
        {
            devicePath = $@"\\.\{char.ToUpperInvariant(value[0])}:";
            return true;
        }

        if (value.StartsWith("/dev/", StringComparison.Ordinal)
            && !value.Contains('*', StringComparison.Ordinal))
        {
            devicePath = value;
            return true;
        }

        return false;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetDriveTypeW", CharSet = CharSet.Unicode)]
    private static extern uint GetDriveTypeW(string rootPathName);

    [DllImport("kernel32.dll", EntryPoint = "QueryDosDeviceW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint QueryDosDeviceW(
        string deviceName,
        [Out] char[] targetPath,
        uint maximumLength);

    [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        IntPtr device,
        uint controlCode,
        IntPtr input,
        int inputSize,
        out long output,
        int outputSize,
        out int bytesReturned,
        IntPtr overlapped);

    [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        IntPtr device,
        uint controlCode,
        IntPtr input,
        int inputSize,
        IntPtr output,
        int outputSize,
        out int bytesReturned,
        IntPtr overlapped);

    [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        IntPtr device,
        uint controlCode,
        IntPtr input,
        int inputSize,
        [Out] byte[] output,
        int outputSize,
        out int bytesReturned,
        IntPtr overlapped);

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int Ioctl(IntPtr fileDescriptor, ulong request, out ulong value);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawReadInfo
    {
        internal long DiskOffset;
        internal uint SectorCount;
        internal int TrackMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LinuxTocHeader
    {
        internal byte FirstTrack;
        internal byte LastTrack;
    }

    [StructLayout(LayoutKind.Explicit, Size = 12)]
    private struct LinuxTocEntry
    {
        [FieldOffset(0)]
        internal byte Track;

        [FieldOffset(1)]
        internal byte AdrControl;

        [FieldOffset(2)]
        internal byte Format;

        [FieldOffset(4)]
        internal int Lba;

        [FieldOffset(8)]
        internal byte DataMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LinuxReadAudioRequest
    {
        internal int Lba;
        internal byte AddressFormat;
        internal int FrameCount;
        internal IntPtr Buffer;
    }

    [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        IntPtr device,
        uint controlCode,
        ref RawReadInfo input,
        int inputSize,
        [Out] byte[] output,
        int outputSize,
        out int bytesReturned,
        IntPtr overlapped);

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int LinuxIoctlTocHeader(
        IntPtr fileDescriptor,
        nuint request,
        ref LinuxTocHeader header);

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int LinuxIoctlTocEntry(
        IntPtr fileDescriptor,
        nuint request,
        ref LinuxTocEntry entry);

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int LinuxIoctlReadAudio(
        IntPtr fileDescriptor,
        nuint controlCode,
        ref LinuxReadAudioRequest audioRequest);
}
