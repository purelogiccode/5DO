using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FiveDo.Video.Vulkan;

/// <summary>
/// Sets the bundled MoltenVK ICD before the Vulkan loader initializes on macOS.
/// </summary>
/// <remarks>
/// The Vulkan loader reads the ICD search path early. The
/// <see cref="VulkanOffscreenRenderer"/> static constructor installs the path before
/// Silk.NET creates any Vulkan API instance.
/// </remarks>
internal static class MacVulkanBootstrap
{
    internal static void Initialize()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        string icd = Path.Combine(AppContext.BaseDirectory, "MoltenVK_icd.json");
        if (!File.Exists(icd))
        {
            return;
        }

        // Use the native setenv so the Vulkan loader sees the value, not only the
        // managed environment cache. Keep the first value when a user already set it.
        _ = SetUnixEnvironment("VK_ICD_FILENAMES", icd, 1);
        _ = SetUnixEnvironment("VK_DRIVER_FILES", icd, 1);
    }

    [DllImport("libc", EntryPoint = "setenv", SetLastError = true)]
    private static extern int SetUnixEnvironment(string name, string value, int overwrite);
}
