using FiveDo.Video.Vulkan;
using Silk.NET.Vulkan;

namespace FiveDo.Tests.Video;

public sealed class VulkanDeviceRankingTests
{
    [Fact]
    public void Score_PrefersHardwareDevicesOverSoftware()
    {
        Assert.True(VulkanDeviceRanking.Score(PhysicalDeviceType.DiscreteGpu)
            > VulkanDeviceRanking.Score(PhysicalDeviceType.IntegratedGpu));
        Assert.True(VulkanDeviceRanking.Score(PhysicalDeviceType.IntegratedGpu)
            > VulkanDeviceRanking.Score(PhysicalDeviceType.VirtualGpu));
        Assert.True(VulkanDeviceRanking.Score(PhysicalDeviceType.VirtualGpu)
            > VulkanDeviceRanking.Score(PhysicalDeviceType.Cpu));
    }

    [Fact]
    public void IsBetter_IntegratedGpuBeatsLlvmpipeRegardlessOfEnumerationOrder()
    {
        // Regression: Mesa lists the Intel GPU first and llvmpipe second on Linux.
        // The old selector kept overwriting its fallback and returned llvmpipe.
        Assert.True(VulkanDeviceRanking.IsBetter(
            PhysicalDeviceType.IntegratedGpu,
            PhysicalDeviceType.Cpu));
        Assert.False(VulkanDeviceRanking.IsBetter(
            PhysicalDeviceType.Cpu,
            PhysicalDeviceType.IntegratedGpu));
    }

    [Fact]
    public void IsBetter_KeepsTheFirstDeviceWhenTypesMatch()
    {
        Assert.False(VulkanDeviceRanking.IsBetter(
            PhysicalDeviceType.IntegratedGpu,
            PhysicalDeviceType.IntegratedGpu));
        Assert.False(VulkanDeviceRanking.IsBetter(
            PhysicalDeviceType.Cpu,
            PhysicalDeviceType.Cpu));
    }

    [Fact]
    public void SurfaceFactories_SetTheExpectedKindAndHandles()
    {
        VulkanSurface win32 = VulkanSurface.ForWin32(0x1234);
        Assert.Equal(VulkanSurfaceKind.Win32, win32.Kind);
        Assert.Equal(0x1234, win32.Window);
        Assert.Equal(0, win32.Display);

        VulkanSurface xlib = VulkanSurface.ForXlib(0x5678, 0x9ABC);
        Assert.Equal(VulkanSurfaceKind.Xlib, xlib.Kind);
        Assert.Equal(0x9ABC, xlib.Window);
        Assert.Equal(0x5678, xlib.Display);
    }
}
