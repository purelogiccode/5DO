using Silk.NET.Vulkan;

namespace FiveDo.Video.Vulkan;

/// <summary>
/// Orders Vulkan physical devices so hardware devices win over software rasterizers.
/// </summary>
/// <remarks>
/// Device enumeration order is driver-defined. On Linux Mesa lists the integrated GPU
/// first and <c>llvmpipe</c> second, so taking the first device with a graphics queue
/// silently selects a CPU renderer. The ranking makes the preference explicit and keeps
/// a software device as the last resort instead of the default.
/// </remarks>
internal static class VulkanDeviceRanking
{
    /// <summary>Gets a preference score; higher is better.</summary>
    /// <param name="type">Physical device type reported by Vulkan.</param>
    /// <returns>The preference score.</returns>
    internal static int Score(PhysicalDeviceType type)
    {
        return type switch
        {
            PhysicalDeviceType.DiscreteGpu => 5,
            PhysicalDeviceType.IntegratedGpu => 4,
            PhysicalDeviceType.VirtualGpu => 3,
            PhysicalDeviceType.Other => 2,
            PhysicalDeviceType.Cpu => 0,
            _ => 1,
        };
    }

    /// <summary>Determines whether a candidate device is preferred over the current best.</summary>
    /// <param name="candidate">Candidate device type.</param>
    /// <param name="current">Current best device type.</param>
    /// <returns><see langword="true"/> when the candidate should replace the current best.</returns>
    internal static bool IsBetter(PhysicalDeviceType candidate, PhysicalDeviceType current)
    {
        return Score(candidate) > Score(current);
    }
}
