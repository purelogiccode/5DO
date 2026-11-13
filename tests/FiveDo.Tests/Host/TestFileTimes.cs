using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace FiveDo.Tests.Host;

/// <summary>
/// Sets a file's last-write time to a Win32 FILETIME that cannot be represented by
/// <see cref="DateTime"/>. Some removable and network file systems report such values,
/// and <see cref="FileSystemInfo.LastWriteTimeUtc"/> throws
/// <see cref="ArgumentOutOfRangeException"/> when it reads them.
/// </summary>
internal static class TestFileTimes
{
    // SYSLIB1054 suggests LibraryImport, which requires unsafe code in the test project.
#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileTime(
        IntPtr file,
        IntPtr creationTime,
        IntPtr lastAccessTime,
        ref FILETIME lastWriteTime);
#pragma warning restore SYSLIB1054

    /// <summary>Marks a file as having an out-of-range Win32 last-write time.</summary>
    /// <param name="path">File to modify.</param>
    /// <returns><see langword="true"/> when the timestamp was applied.</returns>
    internal static bool TrySetInvalidLastWriteTime(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

        // 0x7FFF_FFFF_FFFF_FFFF is a valid Win32 FILETIME (year 30828) but is outside
        // the DateTime range, so .NET throws when it converts the value.
        var time = new FILETIME { dwLowDateTime = -1, dwHighDateTime = int.MaxValue };
        return SetFileTime(
            stream.SafeFileHandle.DangerousGetHandle(),
            IntPtr.Zero,
            IntPtr.Zero,
            ref time);
    }
}
