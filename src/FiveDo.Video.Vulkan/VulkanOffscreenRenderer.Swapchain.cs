using System;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using VkApi = Silk.NET.Vulkan.Vk;

namespace FiveDo.Video.Vulkan;

/// <summary>
/// Optional Win32 swapchain presentation for <see cref="VulkanOffscreenRenderer"/>.
/// </summary>
/// <remarks>
/// The offscreen pipeline remains unchanged: it renders into a sampled output image at
/// the requested output size. When a window handle is supplied, each present blits that
/// image into the current swapchain image and queues it for presentation. The blit keeps
/// the swapchain path independent of the existing render passes, and readback remains
/// available for parity captures.
/// </remarks>
public sealed unsafe partial class VulkanOffscreenRenderer
{
    private nint _windowHandle;
    private SurfaceKHR _surface;
    private KhrSurface? _khrSurface;
    private KhrSwapchain? _khrSwapchain;
    private SwapchainKHR _swapchain;
    private Image[] _swapchainImages = [];
    private ImageView[] _swapchainImageViews = [];
    private Semaphore _imageAvailableSemaphore;
    private Semaphore _renderFinishedSemaphore;
    private Format _swapchainFormat;
    private int _swapchainWidth;
    private int _swapchainHeight;
    private bool _swapchainVsync = true;
    private bool _swapchainFifo;
    private bool _swapchainValid;

    /// <summary>Gets a value indicating whether a native swapchain is active.</summary>
    public bool SwapchainActive => _swapchainValid;

    /// <summary>Gets the active swapchain present mode, or <see langword="null"/> when offscreen.</summary>
    public string? PresentMode => _swapchainValid ? (_swapchainFifo ? "fifo" : "immediate") : null;

    /// <summary>Gets the swapchain width in pixels.</summary>
    public int SwapchainWidth => _swapchainWidth;

    /// <summary>Gets the swapchain height in pixels.</summary>
    public int SwapchainHeight => _swapchainHeight;

    /// <summary>Recreates the swapchain with the requested vsync mode.</summary>
    /// <param name="vsync">Whether FIFO vsync should be used.</param>
    public void SetVsync(bool vsync)
    {
        if (_swapchainVsync == vsync || !_swapchainValid || _vk is null)
        {
            return;
        }

        _swapchainVsync = vsync;
        _vk.DeviceWaitIdle(_device);
        DestroySwapchainResources();
        CreateSwapchainResources();
    }

    /// <summary>
    /// Initializes the renderer and a Win32 swapchain for the supplied window handle.
    /// </summary>
    /// <param name="windowHandle">Native Win32 HWND.</param>
    /// <param name="width">Desired output width.</param>
    /// <param name="height">Desired output height.</param>
    /// <param name="vsync">Request FIFO (vsync) presentation instead of immediate.</param>
    public void InitializeSurface(nint windowHandle, int width, int height, bool vsync = true)
    {
        ArgumentOutOfRangeException.ThrowIfZero(windowHandle);
        _windowHandle = windowHandle;
        _swapchainVsync = vsync;
        Initialize(width, height);
    }

    private string[] GetInstanceExtensions()
    {
        if (_windowHandle == 0)
        {
            return [];
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The native Vulkan swapchain currently supports Win32 windows only.");
        }

        return ["VK_KHR_surface", "VK_KHR_win32_surface"];
    }

    private string[] GetDeviceExtensions()
        => _windowHandle == 0 ? [] : ["VK_KHR_swapchain"];

    private void CreateSurface()
    {
        if (_windowHandle == 0)
        {
            return;
        }

        if (!_vk!.TryGetInstanceExtension(_instance, out KhrWin32Surface win32Surface))
        {
            throw new NotSupportedException("VK_KHR_win32_surface is unavailable.");
        }

        Win32SurfaceCreateInfoKHR create = new()
        {
            SType = StructureType.Win32SurfaceCreateInfoKhr,
            Hinstance = GetModuleHandle(null),
            Hwnd = _windowHandle,
        };
        Check(win32Surface.CreateWin32Surface(_instance, &create, null, out _surface), "vkCreateWin32SurfaceKHR");
        if (!_vk.TryGetInstanceExtension(_instance, out KhrSurface surface))
        {
            throw new NotSupportedException("VK_KHR_surface is unavailable.");
        }

        _khrSurface = surface;
    }

    private PhysicalDevice SelectSurfacePhysicalDevice(VkApi vk, PhysicalDevice* devices, uint count)
    {
        var fallback = devices[0];
        for (var i = 0; i < count; i++)
        {
            PhysicalDevice device = devices[i];
            if (!HasSurfaceQueue(vk, device))
            {
                continue;
            }

            PhysicalDeviceProperties properties = vk.GetPhysicalDeviceProperties(device);
            if (properties.DeviceType == PhysicalDeviceType.DiscreteGpu)
            {
                return device;
            }

            fallback = device;
        }

        return fallback;
    }

    private bool HasSurfaceQueue(VkApi vk, PhysicalDevice device)
    {
        uint count = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, null);
        if (count == 0)
        {
            return false;
        }

        var families = stackalloc QueueFamilyProperties[(int)count];
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, families);
        for (uint i = 0; i < count; i++)
        {
            if ((families[i].QueueFlags & QueueFlags.GraphicsBit) == QueueFlags.None)
            {
                continue;
            }

            Bool32 supported = false;
            _khrSurface!.GetPhysicalDeviceSurfaceSupport(device, i, _surface, &supported);
            if (supported.Value != 0)
            {
                return true;
            }
        }

        return false;
    }

    private uint FindSurfaceQueueFamily(VkApi vk, PhysicalDevice device)
    {
        uint count = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, null);
        var families = stackalloc QueueFamilyProperties[(int)count];
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, families);
        for (uint i = 0; i < count; i++)
        {
            if ((families[i].QueueFlags & QueueFlags.GraphicsBit) == QueueFlags.None)
            {
                continue;
            }

            Bool32 supported = false;
            _khrSurface!.GetPhysicalDeviceSurfaceSupport(device, i, _surface, &supported);
            if (supported.Value != 0)
            {
                return i;
            }
        }

        throw new InvalidOperationException("The Vulkan device has no graphics queue that can present to the window.");
    }

    private void CreateSwapchainResources()
    {
        if (_windowHandle == 0 || _khrSwapchain is null || _khrSurface is null)
        {
            return;
        }

        SurfaceCapabilitiesKHR capabilities = default;
        Check(
            _khrSurface.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, &capabilities),
            "vkGetPhysicalDeviceSurfaceCapabilitiesKHR");

        uint formatCount = 0;
        Check(
            _khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, null),
            "vkGetPhysicalDeviceSurfaceFormatsKHR");
        if (formatCount == 0)
        {
            throw new NotSupportedException("The Vulkan surface exposes no formats.");
        }

        var formats = stackalloc SurfaceFormatKHR[(int)formatCount];
        Check(
            _khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, formats),
            "vkGetPhysicalDeviceSurfaceFormatsKHR");
        SurfaceFormatKHR selectedFormat = formats[0];
        for (var i = 0; i < formatCount; i++)
        {
            if (formats[i].Format == Format.B8G8R8A8Unorm
                && formats[i].ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
            {
                selectedFormat = formats[i];
                break;
            }
        }

        _swapchainFormat = selectedFormat.Format;

        uint modeCount = 0;
        Check(
            _khrSurface.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &modeCount, null),
            "vkGetPhysicalDeviceSurfacePresentModesKHR");
        var modes = stackalloc PresentModeKHR[(int)Math.Max(1u, modeCount)];
        if (modeCount > 0)
        {
            Check(
                _khrSurface.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &modeCount, modes),
                "vkGetPhysicalDeviceSurfacePresentModesKHR");
        }

        PresentModeKHR presentMode = PresentModeKHR.FifoKhr;
        _swapchainFifo = true;
        if (!_swapchainVsync)
        {
            _swapchainFifo = false;
            presentMode = PresentModeKHR.ImmediateKhr;
            for (var i = 0; i < modeCount; i++)
            {
                if (modes[i] == PresentModeKHR.MailboxKhr)
                {
                    presentMode = modes[i];
                    break;
                }
            }

            var hasImmediate = false;
            for (var i = 0; i < modeCount; i++)
            {
                if (modes[i] == PresentModeKHR.ImmediateKhr)
                {
                    hasImmediate = true;
                    break;
                }
            }

            if (!hasImmediate && presentMode == PresentModeKHR.ImmediateKhr)
            {
                presentMode = PresentModeKHR.FifoKhr;
                _swapchainFifo = true;
            }
        }

        Extent2D extent = capabilities.CurrentExtent;
        if (extent.Width == uint.MaxValue || extent.Height == uint.MaxValue)
        {
            extent = new Extent2D
            {
                Width = (uint)Math.Clamp(_outputWidth, (int)capabilities.MinImageExtent.Width, (int)capabilities.MaxImageExtent.Width),
                Height = (uint)Math.Clamp(_outputHeight, (int)capabilities.MinImageExtent.Height, (int)capabilities.MaxImageExtent.Height),
            };
        }

        var minImageCount = Math.Max(capabilities.MinImageCount, 3u);
        if (capabilities.MaxImageCount > 0 && minImageCount > capabilities.MaxImageCount)
        {
            minImageCount = capabilities.MaxImageCount;
        }

        SwapchainCreateInfoKHR create = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            MinImageCount = minImageCount,
            ImageFormat = selectedFormat.Format,
            ImageColorSpace = selectedFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.TransferDstBit,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true,
            OldSwapchain = default,
        };
        Check(
            _khrSwapchain.CreateSwapchain(_device, &create, null, out _swapchain),
            "vkCreateSwapchainKHR");

        SemaphoreCreateInfo semaphoreCreate = new()
        {
            SType = StructureType.SemaphoreCreateInfo,
        };
        Check(
            _vk!.CreateSemaphore(_device, &semaphoreCreate, null, out _imageAvailableSemaphore),
            "vkCreateSemaphore");
        Check(
            _vk.CreateSemaphore(_device, &semaphoreCreate, null, out _renderFinishedSemaphore),
            "vkCreateSemaphore");

        uint imageCount = 0;
        Check(_khrSwapchain.GetSwapchainImages(_device, _swapchain, &imageCount, null), "vkGetSwapchainImagesKHR");
        if (imageCount == 0)
        {
            throw new InvalidOperationException("The Vulkan swapchain has no images.");
        }

        _swapchainImages = new Image[imageCount];
        _swapchainImageViews = new ImageView[imageCount];
        fixed (Image* images = _swapchainImages)
        {
            Check(
                _khrSwapchain.GetSwapchainImages(_device, _swapchain, &imageCount, images),
                "vkGetSwapchainImagesKHR");
        }

        for (var i = 0; i < _swapchainImages.Length; i++)
        {
            ImageViewCreateInfo viewCreate = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _swapchainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = _swapchainFormat,
                Components = new ComponentMapping
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity,
                },
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
            };
            Check(_vk!.CreateImageView(_device, &viewCreate, null, out _swapchainImageViews[i]), "vkCreateImageView");
        }

        _swapchainWidth = (int)extent.Width;
        _swapchainHeight = (int)extent.Height;
        _swapchainValid = true;
    }

    private void ResizeSwapchain(int width, int height)
    {
        if (!_swapchainValid)
        {
            return;
        }

        if (width == _swapchainWidth && height == _swapchainHeight)
        {
            return;
        }

        _vk!.DeviceWaitIdle(_device);
        DestroySwapchainResources();
        _swapchainWidth = width;
        _swapchainHeight = height;
        CreateSwapchainResources();
    }

    private void PresentSwapchain()
    {
        if (!_swapchainValid || _khrSwapchain is null)
        {
            return;
        }

        uint imageIndex = 0;
        Result acquire = _khrSwapchain.AcquireNextImage(
            _device,
            _swapchain,
            ulong.MaxValue,
            _imageAvailableSemaphore,
            default,
            &imageIndex);
        if (acquire == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapchain();
            return;
        }

        Check(acquire, "vkAcquireNextImageKHR");
        Check(_vk!.ResetCommandBuffer(_commandBuffer, CommandBufferResetFlags.None), "vkResetCommandBuffer");
        CommandBufferBeginInfo begin = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };
        Check(_vk.BeginCommandBuffer(_commandBuffer, &begin), "vkBeginCommandBuffer");

        Image swapchainImage = _swapchainImages[imageIndex];
        TransitionSwapchainImage(
            swapchainImage,
            ImageLayout.Undefined,
            ImageLayout.TransferDstOptimal,
            AccessFlags.None,
            AccessFlags.TransferWriteBit,
            PipelineStageFlags.TopOfPipeBit,
            PipelineStageFlags.TransferBit);

        ImageBlit region = new()
        {
            SrcSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
            DstSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };
        region.SrcOffsets[0] = new Offset3D { X = 0, Y = 0, Z = 0 };
        region.SrcOffsets[1] = new Offset3D { X = _outputWidth, Y = _outputHeight, Z = 1 };
        region.DstOffsets[0] = new Offset3D { X = 0, Y = 0, Z = 0 };
        region.DstOffsets[1] = new Offset3D { X = _swapchainWidth, Y = _swapchainHeight, Z = 1 };
        _vk.CmdBlitImage(
            _commandBuffer,
            _outputImage,
            ImageLayout.General,
            swapchainImage,
            ImageLayout.TransferDstOptimal,
            1,
            &region,
            Filter.Linear);

        TransitionSwapchainImage(
            swapchainImage,
            ImageLayout.TransferDstOptimal,
            ImageLayout.PresentSrcKhr,
            AccessFlags.TransferWriteBit,
            AccessFlags.None,
            PipelineStageFlags.TransferBit,
            PipelineStageFlags.BottomOfPipeBit);
        Check(_vk.EndCommandBuffer(_commandBuffer), "vkEndCommandBuffer");

        CommandBuffer commandBuffer = _commandBuffer;
        PipelineStageFlags waitStage = PipelineStageFlags.TransferBit;
        Semaphore imageAvailable = _imageAvailableSemaphore;
        Semaphore renderFinished = _renderFinishedSemaphore;
        Fence fence = _frameFence;
        SubmitInfo submit = new()
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &imageAvailable,
            PWaitDstStageMask = &waitStage,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &renderFinished,
        };
        Check(_vk.QueueSubmit(_queue, 1, &submit, fence), "vkQueueSubmit");
        _frameFencePending = true;

        SwapchainKHR swapchain = _swapchain;
        PresentInfoKHR present = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &renderFinished,
            SwapchainCount = 1,
            PSwapchains = &swapchain,
            PImageIndices = &imageIndex,
        };
        Result result = _khrSwapchain.QueuePresent(_queue, &present);
        if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr)
        {
            RecreateSwapchain();
            return;
        }

        Check(result, "vkQueuePresentKHR");
    }

    private void TransitionSwapchainImage(
        Image image,
        ImageLayout oldLayout,
        ImageLayout newLayout,
        AccessFlags sourceAccess,
        AccessFlags destinationAccess,
        PipelineStageFlags sourceStage,
        PipelineStageFlags destinationStage)
    {
        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = VkApi.QueueFamilyIgnored,
            DstQueueFamilyIndex = VkApi.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
            SrcAccessMask = sourceAccess,
            DstAccessMask = destinationAccess,
        };
        _vk!.CmdPipelineBarrier(
            _commandBuffer,
            sourceStage,
            destinationStage,
            DependencyFlags.None,
            0,
            null,
            0,
            null,
            1,
            &barrier);
    }

    private void RecreateSwapchain()
    {
        if (!_swapchainValid)
        {
            return;
        }

        _vk!.DeviceWaitIdle(_device);
        DestroySwapchainResources();
        CreateSwapchainResources();
    }

    private void DestroySwapchainResources()
    {
        _swapchainValid = false;
        if (_vk is null || _device.Handle == 0)
        {
            _swapchainImages = [];
            _swapchainImageViews = [];
            _imageAvailableSemaphore = default;
            _renderFinishedSemaphore = default;
            return;
        }

        _vk.DeviceWaitIdle(_device);
        _frameFencePending = false;
        if (_frameFence.Handle != 0)
        {
            Fence fence = _frameFence;
            _vk.ResetFences(_device, 1, &fence);
        }

        foreach (ImageView view in _swapchainImageViews)
        {
            if (view.Handle != 0)
            {
                _vk.DestroyImageView(_device, view, null);
            }
        }

        _swapchainImageViews = [];
        _swapchainImages = [];
        if (_swapchain.Handle != 0 && _khrSwapchain is not null)
        {
            _khrSwapchain.DestroySwapchain(_device, _swapchain, null);
            _swapchain = default;
        }

        if (_imageAvailableSemaphore.Handle != 0)
        {
            _vk.DestroySemaphore(_device, _imageAvailableSemaphore, null);
            _imageAvailableSemaphore = default;
        }

        if (_renderFinishedSemaphore.Handle != 0)
        {
            _vk.DestroySemaphore(_device, _renderFinishedSemaphore, null);
            _renderFinishedSemaphore = default;
        }
    }

    private void DestroySurfaceResources()
    {
        if (_surface.Handle != 0 && _khrSurface is not null && _instance.Handle != 0)
        {
            _khrSurface.DestroySurface(_instance, _surface, null);
            _surface = default;
        }

        _khrSurface = null;
        _khrSwapchain = null;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle(string? moduleName);
}
