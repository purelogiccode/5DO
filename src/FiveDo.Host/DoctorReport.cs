using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using FiveDo.Core;

namespace FiveDo.Host;

/// <summary>Severity of one diagnostic row.</summary>
public enum DoctorStatus
{
    /// <summary>The check passed.</summary>
    Ok,

    /// <summary>Informational only; nothing to fix.</summary>
    Info,

    /// <summary>5DO can run, but something will degrade or needs user action.</summary>
    Warn,

    /// <summary>5DO cannot run correctly on this host.</summary>
    Fail,
}

/// <summary>One named diagnostic row.</summary>
/// <param name="Name">Short check name.</param>
/// <param name="Status">Check severity.</param>
/// <param name="Detail">Human-readable result, including the fix when known.</param>
public sealed record DoctorCheck(string Name, DoctorStatus Status, string Detail);

/// <summary>The complete host report produced by <see cref="DoctorReport"/>.</summary>
/// <param name="Product">Product name.</param>
/// <param name="Version">5DO version.</param>
/// <param name="Runtime">CLR description.</param>
/// <param name="OperatingSystem">OS description.</param>
/// <param name="Architecture">Process architecture.</param>
/// <param name="SessionType">Display session type.</param>
/// <param name="DataDirectory">Effective data folder.</param>
/// <param name="DataDirectoryWritable">Whether the data folder accepted a write probe.</param>
/// <param name="Summary">Worst status across all checks.</param>
/// <param name="Checks">All diagnostic rows.</param>
/// <param name="Bios">Default-folder BIOS discovery result.</param>
/// <param name="Notes">Extra environment notes (overrides that are set).</param>
public sealed record DoctorResult(
    string Product,
    string Version,
    string Runtime,
    string OperatingSystem,
    string Architecture,
    string SessionType,
    string DataDirectory,
    bool DataDirectoryWritable,
    DoctorStatus Summary,
    IReadOnlyList<DoctorCheck> Checks,
    BiosDiscoveryResult Bios,
    IReadOnlyList<string> Notes);

/// <summary>
/// Collects the host facts a Linux support request needs: session type, data-folder
/// access, native libraries, input devices, optical drives, and BIOS discovery.
/// </summary>
/// <remarks>
/// The report is read-only apart from a single write probe in the data folder. It never
/// starts the emulator, opens a window, or contacts the network, so it is safe to run
/// over SSH or paste into an issue.
/// </remarks>
public static class DoctorReport
{
    /// <summary>Collects a report for the current host.</summary>
    /// <param name="bios">Optional pre-collected BIOS discovery result.</param>
    /// <param name="dataDirectory">Optional data-folder override, used by tests.</param>
    /// <returns>The report.</returns>
    public static DoctorResult Collect(BiosDiscoveryResult? bios = null, string? dataDirectory = null)
    {
        var checks = new List<DoctorCheck>();
        string resolvedDataDirectory = ProbeDataDirectory(checks, dataDirectory, out bool writable);
        var session = ProbeSession(checks);
        ProbeDiskSpace(checks, resolvedDataDirectory);
        ProbeNativeLibraries(checks);
        if (OperatingSystem.IsLinux())
        {
            ProbeLinuxInput(checks);
            ProbeOpticalDrives(checks);
        }

        ProbeSettings(checks);

        BiosDiscoveryResult biosResult;
        try
        {
            biosResult = bios ?? BiosDiscovery.DiscoverDefault();
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException)
        {
            biosResult = new BiosDiscoveryResult([], null);
            checks.Add(new DoctorCheck("BIOS", DoctorStatus.Warn, $"Discovery failed: {exception.Message}"));
        }

        if (biosResult.HasBios)
        {
            var preferred = biosResult.PreferredPrimary!;
            checks.Add(new DoctorCheck(
                "BIOS",
                DoctorStatus.Ok,
                $"{preferred.Name} ({preferred.RegionLabel}) at {preferred.Path}"));
        }
        else
        {
            var searched = biosResult.ExistingFolders.Count == 0
                ? "none of the default folders exist"
                : string.Join(", ", biosResult.ExistingFolders.Select(folder => folder.Path));
            checks.Add(new DoctorCheck(
                "BIOS",
                DoctorStatus.Warn,
                $"No BIOS image found ({searched}). Open File > BIOS, or set FIVEDO_BIOS_DIR."));
        }

        return new DoctorResult(
            FiveDoVersion.ProductName,
            FiveDoVersion.Version,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            session,
            resolvedDataDirectory,
            writable,
            Worst(checks),
            checks,
            biosResult,
            EnvironmentNotes());
    }

    /// <summary>Renders a report as plain text for a terminal.</summary>
    /// <param name="result">Report to render.</param>
    /// <returns>Multi-line text.</returns>
    public static string FormatText(DoctorResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var builder = new StringBuilder();
        builder.AppendLine($"{result.Product} {result.Version} doctor");
        builder.AppendLine($"  runtime:    {result.Runtime}");
        builder.AppendLine($"  os:         {result.OperatingSystem} ({result.Architecture})");
        builder.AppendLine($"  session:    {result.SessionType}");
        builder.AppendLine($"  data:       {result.DataDirectory}");
        builder.AppendLine($"  summary:    {result.Summary.ToString().ToLowerInvariant()}");
        if (result.Notes.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Environment");
            foreach (var note in result.Notes)
            {
                builder.AppendLine($"  {note}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Checks");
        foreach (DoctorCheck check in result.Checks)
        {
            builder.AppendLine($"  [{Tag(check.Status)}] {check.Name}: {check.Detail}");
        }

        if (result.Bios.HasBios || result.Bios.Folders.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("BIOS folders");
            foreach (BiosDiscoveryFolder folder in result.Bios.Folders)
            {
                var state = folder.Exists
                    ? $"{folder.Primaries.Count} primary, {folder.Secondaries.Count} secondary"
                    : "missing";
                builder.AppendLine($"  {folder.Path} ({folder.Source}): {state}");
                foreach (var error in folder.Errors)
                {
                    builder.AppendLine($"    error: {error}");
                }
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string ProbeDataDirectory(
        List<DoctorCheck> checks,
        string? dataDirectory,
        out bool writable)
    {
        writable = false;
        string directory;
        try
        {
            directory = string.IsNullOrWhiteSpace(dataDirectory)
                ? PortablePaths.DataDirectory
                : Path.GetFullPath(dataDirectory);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException)
        {
            checks.Add(new DoctorCheck(
                "Data folder",
                DoctorStatus.Fail,
                $"Cannot resolve the data folder: {exception.Message}"));
            return "(unresolved)";
        }

        var probe = Path.Combine(directory, $".doctor-{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(probe, "5do");
            writable = true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            checks.Add(new DoctorCheck(
                "Data folder",
                DoctorStatus.Fail,
                $"{directory} is not writable: {exception.Message}. Set FIVEDO_DATA_DIR to a writable folder."));
            return directory;
        }
        finally
        {
            TryDelete(probe);
        }

        checks.Add(new DoctorCheck("Data folder", DoctorStatus.Ok, $"{directory} is writable"));
        return directory;
    }

    private static string ProbeSession(List<DoctorCheck> checks)
    {
        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        var waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        var display = Environment.GetEnvironmentVariable("DISPLAY");
        var hasWayland = !string.IsNullOrWhiteSpace(waylandDisplay);
        var hasX11 = !string.IsNullOrWhiteSpace(display);

        if (!OperatingSystem.IsLinux())
        {
            checks.Add(new DoctorCheck("Display session", DoctorStatus.Info, "Not a Linux session"));
            return string.IsNullOrWhiteSpace(sessionType) ? "unknown" : sessionType;
        }

        var effective = string.IsNullOrWhiteSpace(sessionType)
            ? (hasWayland ? "wayland" : hasX11 ? "x11" : "headless")
            : sessionType;

        if (!hasWayland && !hasX11)
        {
            checks.Add(new DoctorCheck(
                "Display session",
                DoctorStatus.Warn,
                "No DISPLAY or WAYLAND_DISPLAY. Run 5DO from a desktop session; use xvfb-run only for smoke tests."));
        }
        else if (hasWayland && !hasX11)
        {
            checks.Add(new DoctorCheck(
                "Display session",
                DoctorStatus.Warn,
                "Wayland-only session: the Vulkan child window needs X11, so 5DO uses the slower offscreen presenter. Install or enable XWayland for GPU presentation."));
        }
        else if (hasWayland)
        {
            checks.Add(new DoctorCheck(
                "Display session",
                DoctorStatus.Info,
                "Wayland session with XWayland (DISPLAY is set)"));
        }
        else
        {
            checks.Add(new DoctorCheck("Display session", DoctorStatus.Ok, "X11 session"));
        }

        return effective;
    }

    private static void ProbeDiskSpace(List<DoctorCheck> checks, string directory)
    {
        try
        {
            var drive = new DriveInfo(directory);
            if (!drive.IsReady)
            {
                checks.Add(new DoctorCheck("Disk space", DoctorStatus.Info, "Volume is not ready"));
                return;
            }

            var freeGb = drive.AvailableFreeSpace / (1024d * 1024d * 1024d);
            var detail = string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.0} GB free on {1}",
                freeGb,
                drive.Name);
            var status = freeGb < 2 ? DoctorStatus.Warn : DoctorStatus.Ok;
            checks.Add(new DoctorCheck("Disk space", status, detail));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            checks.Add(new DoctorCheck("Disk space", DoctorStatus.Info, exception.Message));
        }
    }

    private static void ProbeNativeLibraries(List<DoctorCheck> checks)
    {
        if (OperatingSystem.IsWindows())
        {
            checks.Add(ProbeLibrary("OpenGL", "opengl32.dll"));
            checks.Add(ProbeLibrary("Vulkan", "vulkan-1.dll"));
            checks.Add(ProbeLibrary("OpenAL", "OpenAL32.dll"));
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            checks.Add(new DoctorCheck(
                "OpenGL",
                DoctorStatus.Info,
                "Not used on macOS; the shell presents through Vulkan/MoltenVK"));
            checks.Add(ProbeLibrary("Vulkan", "libvulkan.1.dylib", "libvulkan.dylib", "libMoltenVK.dylib"));
            checks.Add(ProbeLibrary("OpenAL", "libopenal.dylib"));
            return;
        }

        checks.Add(ProbeLibrary("OpenGL", "libGL.so.1", "libGL.so"));
        checks.Add(ProbeLibrary("Vulkan", "libvulkan.so.1", "libvulkan.so"));
        checks.Add(ProbeLibrary("OpenAL", "libopenal.so.1", "libopenal.so"));
    }

    private static DoctorCheck ProbeLibrary(string name, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            IntPtr handle = IntPtr.Zero;
            try
            {
                if (NativeLibrary.TryLoad(candidate, out handle))
                {
                    return new DoctorCheck(name, DoctorStatus.Ok, $"loaded {candidate}");
                }
            }
            catch (Exception exception) when (exception is DllNotFoundException
                or BadImageFormatException
                or InvalidOperationException)
            {
                // Try the next candidate.
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    try
                    {
                        NativeLibrary.Free(handle);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }
        }

        return new DoctorCheck(
            name,
            DoctorStatus.Warn,
            $"not found ({string.Join(", ", candidates)}); the CPU renderer still works");
    }

    private static void ProbeLinuxInput(List<DoctorCheck> checks)
    {
        var events = SafeEnumerate("/dev/input", "event*");
        if (events.Length == 0)
        {
            checks.Add(new DoctorCheck(
                "Input devices",
                DoctorStatus.Warn,
                "No /dev/input/event* devices. Gamepads need evdev; add the user to the 'input' group if the nodes exist but are unreadable."));
        }
        else
        {
            var readable = events.Count(CanRead);
            var status = readable == 0 ? DoctorStatus.Warn : DoctorStatus.Ok;
            var detail = readable == 0
                ? $"{events.Length} event device(s), none readable. Run: sudo usermod -aG input $USER (then log out and back in)"
                : $"{readable}/{events.Length} event device(s) readable";
            checks.Add(new DoctorCheck("Input devices", status, detail));
        }

        checks.Add(new DoctorCheck(
            "Virtual input",
            File.Exists("/dev/uinput") ? DoctorStatus.Ok : DoctorStatus.Info,
            File.Exists("/dev/uinput")
                ? "/dev/uinput is present (virtual pads and input tests)"
                : "/dev/uinput is missing (only needed for virtual pads and input tests)"));
    }

    private static void ProbeOpticalDrives(List<DoctorCheck> checks)
    {
        var drives = SafeEnumerate("/dev", "sr*");
        checks.Add(drives.Length == 0
            ? new DoctorCheck(
                "Optical drive",
                DoctorStatus.Info,
                "No /dev/sr* device. File > Boot Optical Drive needs an attached drive.")
            : new DoctorCheck(
                "Optical drive",
                DoctorStatus.Ok,
                string.Join(", ", drives)));
    }

    private static void ProbeSettings(List<DoctorCheck> checks)
    {
        string path;
        try
        {
            path = AppSettingsStore.DefaultPath;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidOperationException)
        {
            checks.Add(new DoctorCheck("Settings", DoctorStatus.Warn, exception.Message));
            return;
        }

        if (!File.Exists(path))
        {
            checks.Add(new DoctorCheck("Settings", DoctorStatus.Info, $"No settings file yet ({path})"));
            return;
        }

        try
        {
            _ = File.ReadAllText(path);
            checks.Add(new DoctorCheck("Settings", DoctorStatus.Ok, path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            checks.Add(new DoctorCheck("Settings", DoctorStatus.Warn, $"{path} is unreadable: {exception.Message}"));
        }
    }

    private static List<string> EnvironmentNotes()
    {
        var notes = new List<string>();
        AddNote(notes, "FIVEDO_DATA_DIR");
        AddNote(notes, "FIVEDO_PROFILE");
        AddNote(notes, "FIVEDO_PORTABLE");
        AddNote(notes, "FIVEDO_BIOS_DIR");
        if (string.Equals(Environment.GetEnvironmentVariable("FIVEDO_PORTABLE"), "1", StringComparison.Ordinal))
        {
            notes.Add("portable mode is active; settings and BIOS state live next to the executable");
        }

        return notes;
    }

    private static void AddNote(List<string> notes, string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(value))
        {
            notes.Add($"{variable}={value}");
        }
    }

    private static DoctorStatus Worst(IReadOnlyList<DoctorCheck> checks)
    {
        var status = DoctorStatus.Ok;
        foreach (DoctorCheck check in checks)
        {
            if (Rank(check.Status) > Rank(status))
            {
                status = check.Status;
            }
        }

        return status;
    }

    private static int Rank(DoctorStatus status) => status switch
    {
        DoctorStatus.Ok => 0,
        DoctorStatus.Info => 1,
        DoctorStatus.Warn => 2,
        _ => 3,
    };

    private static string Tag(DoctorStatus status) => status switch
    {
        DoctorStatus.Ok => "ok",
        DoctorStatus.Info => "info",
        DoctorStatus.Warn => "warn",
        _ => "fail",
    };

    private static bool CanRead(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            return false;
        }
    }

    private static string[] SafeEnumerate(string folder, string pattern)
    {
        try
        {
            return Directory.Exists(folder)
                ? Directory.GetFiles(folder, pattern).OrderBy(path => path, StringComparer.Ordinal).ToArray()
                : [];
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            return [];
        }
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
