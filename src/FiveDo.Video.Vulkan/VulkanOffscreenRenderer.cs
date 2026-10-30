using System;
using System.Runtime.InteropServices;
using FiveDo.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using VkApi = Silk.NET.Vulkan.Vk;

namespace FiveDo.Video.Vulkan;

/// <summary>
/// An optional headless Vulkan presentation backend.
/// </summary>
/// <remarks>
/// <para>
/// This renderer mirrors the two-stage OpenGL presenter: the indexed frame and per-line
/// palette are expanded into a native-resolution image, then a second pass scales and
/// filters that image into the output. It renders offscreen and exposes the result through
/// <see cref="ReadPixels"/>, so it can be used by validation probes and by hosts that copy
/// the frame into their own surface. It does not create a window or swapchain.
/// </para>
/// <para>
/// The renderer is deliberately optional. The OpenGL path remains the required desktop
/// presenter; this backend is for Vulkan validation, parity checks, and future window-system
/// integration. Unsupported filters fall back to sharp-bilinear and are reported by
/// <see cref="FilterFallback"/>.
/// </para>
/// </remarks>
public sealed unsafe partial class VulkanOffscreenRenderer : ICpuFrameRenderer, IRendererTiming
{
    private const int MaxSourceWidth = VdlFrameUploader.MaxWidth;
    private const int MaxSourceHeight = VdlFrameUploader.MaxHeight;
    private const int ClutEntries = VdlFrameUploader.ClutEntries;

    private readonly VdlFrameUploader _uploader = new();
    private readonly HqxFilter _hqxProcessor = new();
    private readonly uint[] _clutRgba = new uint[ClutEntries * MaxSourceHeight];
    private readonly ulong[] _timestamps = new ulong[2];
    private FrameBuffer _frame = new(1, 1);
    private FrameBuffer? _hqxSourceFrame;
    private FrameBuffer? _hqxCpuFrame;
    private int _hqxSourceWidth;
    private int _hqxSourceHeight;
    private int _hqxScale;

    private VkApi? _vk;
    private Instance _instance;
    private PhysicalDevice _physicalDevice;
    private Device _device;
    private Queue _queue;
    private uint _queueFamily;
    private CommandPool _commandPool;
    private CommandBuffer _commandBuffer;
    private QueryPool _queryPool;
    private Sampler _nearestSampler;
    private Sampler _linearSampler;
    private DescriptorSetLayout _paletteLayout;
    private DescriptorSetLayout _scaleLayout;
    private PipelineLayout _palettePipelineLayout;
    private PipelineLayout _scalePipelineLayout;
    private Pipeline _palettePipeline;
    private Pipeline _scalePipeline;
    private Pipeline _xbrPipeline;
    private Pipeline _fsrEasuPipeline;
    private Pipeline _fsrRcasPipeline;
    private RenderPass _renderPass;
    private DescriptorPool _descriptorPool;
    private DescriptorSet _paletteSet;
    private DescriptorSet _scaleSet;
    private DescriptorSet _scaleXbrSet;
    private DescriptorSet _xbrSet;
    private DescriptorSet _fsrEasuSet;
    private DescriptorSet _fsrRcasSet;
    private DescriptorSet _hqxScaleSet;

    private Image _pixelImage;
    private DeviceMemory _pixelMemory;
    private ImageView _pixelView;
    private Image _clutImage;
    private DeviceMemory _clutMemory;
    private ImageView _clutView;
    private Image _paramImage;
    private DeviceMemory _paramMemory;
    private ImageView _paramView;
    private Image _expandedImage;
    private DeviceMemory _expandedMemory;
    private ImageView _expandedView;
    private Image _outputImage;
    private DeviceMemory _outputMemory;
    private ImageView _outputView;
    private Framebuffer _expandedFramebuffer;
    private Framebuffer _outputFramebuffer;
    private Image _xbrImage;
    private DeviceMemory _xbrMemory;
    private ImageView _xbrView;
    private Framebuffer _xbrFramebuffer;
    private int _xbrWidth;
    private int _xbrHeight;
    private Image _fsrImage;
    private DeviceMemory _fsrMemory;
    private ImageView _fsrView;
    private Framebuffer _fsrFramebuffer;
    private int _fsrWidth;
    private int _fsrHeight;
    private Image _hqxImage;
    private DeviceMemory _hqxMemory;
    private ImageView _hqxView;
    private int _hqxWidth;
    private int _hqxHeight;
    private Format _hqxFormat = Format.B8G8R8A8Unorm;
    private bool _hqxSwizzle;
    private Silk.NET.Vulkan.Buffer _hqxStaging;
    private DeviceMemory _hqxStagingMemory;
    private void* _hqxStagingMapped;

    private Silk.NET.Vulkan.Buffer _pixelStaging;
    private DeviceMemory _pixelStagingMemory;
    private void* _pixelStagingMapped;
    private Silk.NET.Vulkan.Buffer _clutStaging;
    private DeviceMemory _clutStagingMemory;
    private void* _clutStagingMapped;
    private Silk.NET.Vulkan.Buffer _paramStaging;
    private DeviceMemory _paramStagingMemory;
    private void* _paramStagingMapped;
    private Silk.NET.Vulkan.Buffer _readbackBuffer;
    private DeviceMemory _readbackMemory;
    private void* _readbackMapped;
    private ulong _readbackSize;

    private Format _outputFormat = Format.B8G8R8A8Unorm;
    private bool _swizzleReadback;
    private RendererSettings _settings = new();
    private RendererCapabilities _capabilities;
    private string _deviceName = "Vulkan";
    private int _outputWidth;
    private int _outputHeight;
    private int _sourceWidth;
    private int _sourceHeight;
    private float _timestampPeriod;
    private bool _initialized;
    private bool _disposed;
    private bool _filterFallback;
    private double? _gpuMilliseconds;

    /// <summary>Gets a value indicating whether the last present fell back to sharp-bilinear.</summary>
    public bool FilterFallback => _filterFallback;

    /// <summary>Gets the Vulkan device name.</summary>
    public string DeviceName => _deviceName;

    /// <summary>
    /// Gets or sets a value indicating whether each present copies the rendered output into
    /// <see cref="Frame"/>. The desktop Vulkan fallback enables this; the timing probe leaves
    /// it disabled so readback is not part of the measured present path.
    /// </summary>
    public bool CaptureToFrame { get; set; }

    /// <inheritdoc />
    public FrameBuffer Frame => _frame;

    /// <inheritdoc />
    public string Name => "Vulkan";

    /// <inheritdoc />
    public RendererSettings Settings
    {
        get => _settings;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            value.Validate();
            _settings = value;
        }
    }

    /// <inheritdoc />
    public RendererCapabilities Capabilities => _capabilities;

    /// <inheritdoc />
    public double? GpuMilliseconds => _gpuMilliseconds;

    /// <summary>Gets the output format used by the renderer.</summary>
    public string OutputFormat => _outputFormat.ToString();

    /// <inheritdoc />
    public void Initialize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ObjectDisposedException.ThrowIf(_disposed, this);
        DestroyResources();
        _outputWidth = width;
        _outputHeight = height;
        _sourceWidth = 0;
        _sourceHeight = 0;
        _frame = new FrameBuffer(width, height);
        _filterFallback = false;
        _gpuMilliseconds = null;
        CreateDevice();
        CreateCommandResources();
        CreateSamplers();
        CreateDescriptorLayouts();
        CreateRenderPass();
        CreatePipelines();
        CreateStagingResources();
        CreateSourceImages();
        CreateExpandedTarget();
        CreateOutputTarget();
        UpdateDescriptors();
        _initialized = true;
        CreateSwapchainResources();
    }

    /// <inheritdoc />
    public void Resize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_outputWidth == width && _outputHeight == height)
        {
            return;
        }

        _outputWidth = width;
        _outputHeight = height;
        _frame = new FrameBuffer(width, height);
        if (_initialized)
        {
            DestroyOutputTarget();
            CreateOutputTarget();
            ResizeSwapchain(width, height);
        }
    }

    /// <inheritdoc />
    public void Present(VdlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
        {
            Initialize(EmulatorConstants.VideoWidth, EmulatorConstants.VideoHeight);
        }

        _uploader.Update(frame);
        var width = _uploader.Width;
        var height = _uploader.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (width != _sourceWidth || height != _sourceHeight)
        {
            _sourceWidth = width;
            _sourceHeight = height;
            RecreateExpandedTarget();
        }

        if (IsHqxFilter)
        {
            PrepareHqx(frame);
        }

        UploadSourceData();
        RecordFrame();
        WaitForFrame();
        ReadGpuTiming();
        if (CaptureToFrame)
        {
            ReadPixels(_frame.PixelSpan, out _, out _);
        }

        PresentSwapchain();
    }

    private bool IsHqxFilter
        => _settings.Filter is VideoFilter.Hqx or VideoFilter.Hqx3x or VideoFilter.Hqx4x;

    private void PrepareHqx(VdlFrame frame)
    {
        var scale = HqxFilter.ScaleFor(_settings.Filter);
        var width = _sourceWidth * scale;
        var height = _sourceHeight * scale;
        if (width > _capabilities.MaxTextureSize || height > _capabilities.MaxTextureSize)
        {
            DestroyHqxTarget();
            return;
        }

        EnsureHqxTarget(width, height);
        EnsureHqxFrames(_sourceWidth, _sourceHeight, scale);
        FrameAssembler.Assemble(frame, _hqxSourceFrame!, _sourceWidth, _sourceHeight);
        _hqxProcessor.Expand(_hqxSourceFrame!, _hqxCpuFrame!);
        UploadHqxPixels(_hqxCpuFrame!);
    }

    private void EnsureHqxFrames(int width, int height, int scale)
    {
        if (_hqxCpuFrame is not null
            && _hqxSourceWidth == width
            && _hqxSourceHeight == height
            && _hqxScale == scale)
        {
            return;
        }

        _hqxSourceFrame = new FrameBuffer(width, height);
        _hqxCpuFrame = new FrameBuffer(width * scale, height * scale);
        _hqxSourceWidth = width;
        _hqxSourceHeight = height;
        _hqxScale = scale;
    }

    private void UploadHqxPixels(FrameBuffer frame)
    {
        var source = frame.PixelSpan;
        var destination = new Span<byte>(_hqxStagingMapped, checked(frame.Width * frame.Height * 4));
        if (_hqxSwizzle)
        {
            for (var i = 0; i < destination.Length; i += 4)
            {
                destination[i] = source[i + 2];
                destination[i + 1] = source[i + 1];
                destination[i + 2] = source[i];
                destination[i + 3] = source[i + 3];
            }
        }
        else
        {
            source.CopyTo(destination);
        }
    }

    /// <inheritdoc />
    public void ReadPixels(Span<byte> destination, out int width, out int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        width = _outputWidth;
        height = _outputHeight;
        var required = checked(width * height * 4);
        if (destination.Length < required)
        {
            throw new ArgumentException($"The destination needs {required} bytes.", nameof(destination));
        }

        if (!_initialized || _readbackMapped is null)
        {
            destination[..required].Clear();
            return;
        }

        var source = new ReadOnlySpan<byte>(_readbackMapped, required);
        if (_swizzleReadback)
        {
            for (var i = 0; i < required; i += 4)
            {
                destination[i] = source[i + 2];
                destination[i + 1] = source[i + 1];
                destination[i + 2] = source[i];
                destination[i + 3] = 0xff;
            }
        }
        else
        {
            source.CopyTo(destination);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DestroySwapchainResources();
        DestroySurfaceResources();
        DestroyResources();
        if (_instance.Handle != 0 && _vk is not null)
        {
            _vk.DestroyInstance(_instance, null);
            _instance = default;
        }

        if (_vk is not null)
        {
            _vk.Dispose();
            _vk = null;
        }
    }

    private void CreateDevice()
    {
        _vk?.Dispose();
        _vk = VkApi.GetApi();
        var applicationName = SilkMarshal.StringToPtr("5DO Vulkan");
        var engineName = SilkMarshal.StringToPtr("5DO");
        string[] instanceExtensions = GetInstanceExtensions();
        var extensionPointers = new nint[instanceExtensions.Length];
        try
        {
            for (var i = 0; i < instanceExtensions.Length; i++)
            {
                extensionPointers[i] = SilkMarshal.StringToPtr(instanceExtensions[i]);
            }

            fixed (nint* extensionPointer = extensionPointers)
            {
                ApplicationInfo application = new()
                {
                    SType = StructureType.ApplicationInfo,
                    PApplicationName = (byte*)applicationName,
                    ApplicationVersion = new Silk.NET.Core.Version32(1, 0, 0),
                    PEngineName = (byte*)engineName,
                    EngineVersion = new Silk.NET.Core.Version32(1, 0, 0),
                    ApiVersion = VkApi.Version10,
                };
                InstanceCreateInfo instanceCreate = new()
                {
                    SType = StructureType.InstanceCreateInfo,
                    PApplicationInfo = &application,
                    EnabledExtensionCount = (uint)instanceExtensions.Length,
                    PpEnabledExtensionNames = (byte**)extensionPointer,
                };
                Check(_vk.CreateInstance(&instanceCreate, null, out _instance), "vkCreateInstance");
            }
        }
        finally
        {
            foreach (var pointer in extensionPointers)
            {
                if (pointer != 0)
                {
                    SilkMarshal.Free(pointer);
                }
            }

            SilkMarshal.Free(applicationName);
            SilkMarshal.Free(engineName);
        }

        if (_windowHandle != 0)
        {
            CreateSurface();
        }

        uint deviceCount = 0;
        Check(_vk.EnumeratePhysicalDevices(_instance, &deviceCount, null), "vkEnumeratePhysicalDevices");
        if (deviceCount == 0)
        {
            throw new InvalidOperationException("Vulkan is available but no physical device was found.");
        }

        var devices = stackalloc PhysicalDevice[(int)deviceCount];
        Check(_vk.EnumeratePhysicalDevices(_instance, &deviceCount, devices), "vkEnumeratePhysicalDevices");
        _physicalDevice = _windowHandle != 0
            ? SelectSurfacePhysicalDevice(_vk, devices, deviceCount)
            : SelectPhysicalDevice(_vk, devices, deviceCount);
        PhysicalDeviceProperties properties = _vk.GetPhysicalDeviceProperties(_physicalDevice);
        _deviceName = SilkMarshal.PtrToString((nint)properties.DeviceName) ?? "Vulkan device";
        _timestampPeriod = properties.Limits.TimestampPeriod;
        _outputFormat = SelectOutputFormat();
        _swizzleReadback = _outputFormat == Format.R8G8B8A8Unorm;
        _hqxFormat = SelectHqxFormat();
        _hqxSwizzle = _hqxFormat == Format.R8G8B8A8Unorm;
        EnsureSourceFormat(Format.R16Uint);
        EnsureSourceFormat(Format.R8G8B8A8Unorm);
        EnsureSourceFormat(Format.R32G32Uint);
        _capabilities = new RendererCapabilities(
            _deviceName,
            (int)(properties.ApiVersion >> 22),
            (int)((properties.ApiVersion >> 12) & 0x3ff),
            (int)properties.Limits.MaxImageDimension2D,
            SupportsIntegerTextures: true,
            SupportsVsync: _windowHandle != 0,
            RendererApi.Vulkan);

        _queueFamily = _windowHandle != 0
            ? FindSurfaceQueueFamily(_vk, _physicalDevice)
            : FindGraphicsQueueFamily(_vk, _physicalDevice);
        float priority = 1f;
        DeviceQueueCreateInfo queueCreate = new()
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = _queueFamily,
            QueueCount = 1,
            PQueuePriorities = &priority,
        };
        string[] deviceExtensions = GetDeviceExtensions();
        var deviceExtensionPointers = new nint[deviceExtensions.Length];
        try
        {
            for (var i = 0; i < deviceExtensions.Length; i++)
            {
                deviceExtensionPointers[i] = SilkMarshal.StringToPtr(deviceExtensions[i]);
            }

            fixed (nint* deviceExtensionPointer = deviceExtensionPointers)
            {
                DeviceCreateInfo deviceCreate = new()
                {
                    SType = StructureType.DeviceCreateInfo,
                    QueueCreateInfoCount = 1,
                    PQueueCreateInfos = &queueCreate,
                    EnabledExtensionCount = (uint)deviceExtensions.Length,
                    PpEnabledExtensionNames = (byte**)deviceExtensionPointer,
                };
                Check(_vk.CreateDevice(_physicalDevice, &deviceCreate, null, out _device), "vkCreateDevice");
            }
        }
        finally
        {
            foreach (var pointer in deviceExtensionPointers)
            {
                if (pointer != 0)
                {
                    SilkMarshal.Free(pointer);
                }
            }
        }

        _vk.GetDeviceQueue(_device, _queueFamily, 0, out _queue);
        if (_windowHandle != 0)
        {
            if (!_vk.TryGetDeviceExtension(_instance, _device, out _khrSwapchain))
            {
                throw new NotSupportedException("VK_KHR_swapchain is unavailable.");
            }
        }
    }

    private void EnsureSourceFormat(Format format)
    {
        const FormatFeatureFlags required = FormatFeatureFlags.SampledImageBit
            | FormatFeatureFlags.TransferDstBit;
        FormatProperties properties = _vk!.GetPhysicalDeviceFormatProperties(_physicalDevice, format);
        if ((properties.OptimalTilingFeatures & required) != required)
        {
            throw new NotSupportedException(
                $"The Vulkan device does not support sampled/transfer format {format}.");
        }
    }

    private Format SelectOutputFormat()
    {
        const FormatFeatureFlags required = FormatFeatureFlags.ColorAttachmentBit
            | FormatFeatureFlags.SampledImageBit
            | FormatFeatureFlags.TransferSrcBit;
        FormatProperties properties = _vk!.GetPhysicalDeviceFormatProperties(_physicalDevice, Format.B8G8R8A8Unorm);
        if ((properties.OptimalTilingFeatures & required) == required)
        {
            return Format.B8G8R8A8Unorm;
        }

        properties = _vk.GetPhysicalDeviceFormatProperties(_physicalDevice, Format.R8G8B8A8Unorm);
        if ((properties.OptimalTilingFeatures & required) == required)
        {
            return Format.R8G8B8A8Unorm;
        }

        throw new NotSupportedException("The Vulkan device does not support a sampled RGBA8 render target.");
    }

    private Format SelectHqxFormat()
    {
        const FormatFeatureFlags required = FormatFeatureFlags.SampledImageBit
            | FormatFeatureFlags.TransferDstBit;
        FormatProperties properties = _vk!.GetPhysicalDeviceFormatProperties(_physicalDevice, Format.B8G8R8A8Unorm);
        if ((properties.OptimalTilingFeatures & required) == required)
        {
            return Format.B8G8R8A8Unorm;
        }

        properties = _vk.GetPhysicalDeviceFormatProperties(_physicalDevice, Format.R8G8B8A8Unorm);
        if ((properties.OptimalTilingFeatures & required) == required)
        {
            return Format.R8G8B8A8Unorm;
        }

        throw new NotSupportedException("The Vulkan device does not support a sampled BGRA8/RGBA8 HQx source texture.");
    }

    private static PhysicalDevice SelectPhysicalDevice(VkApi vk, PhysicalDevice* devices, uint count)
    {
        var fallback = devices[0];
        for (var i = 0; i < count; i++)
        {
            PhysicalDevice device = devices[i];
            PhysicalDeviceProperties properties = vk.GetPhysicalDeviceProperties(device);
            if (properties.DeviceType == PhysicalDeviceType.DiscreteGpu
                && HasGraphicsQueue(vk, device))
            {
                return device;
            }

            if (HasGraphicsQueue(vk, device))
            {
                fallback = device;
            }
        }

        return fallback;
    }

    private static bool HasGraphicsQueue(VkApi vk, PhysicalDevice device)
    {
        uint count = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, null);
        if (count == 0)
        {
            return false;
        }

        var families = stackalloc QueueFamilyProperties[(int)count];
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, families);
        for (var i = 0; i < count; i++)
        {
            if ((families[i].QueueFlags & QueueFlags.GraphicsBit) == QueueFlags.GraphicsBit)
            {
                return true;
            }
        }

        return false;
    }

    private static uint FindGraphicsQueueFamily(VkApi vk, PhysicalDevice device)
    {
        uint count = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, null);
        var families = stackalloc QueueFamilyProperties[(int)count];
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &count, families);
        for (uint i = 0; i < count; i++)
        {
            if ((families[i].QueueFlags & QueueFlags.GraphicsBit) == QueueFlags.GraphicsBit)
            {
                return i;
            }
        }

        throw new InvalidOperationException("The Vulkan device has no graphics queue.");
    }

    private void CreateCommandResources()
    {
        CommandPoolCreateInfo poolCreate = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _queueFamily,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
        };
        Check(_vk!.CreateCommandPool(_device, &poolCreate, null, out _commandPool), "vkCreateCommandPool");
        CommandBufferAllocateInfo allocate = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };
        CommandBuffer commandBuffer;
        Check(_vk.AllocateCommandBuffers(_device, &allocate, &commandBuffer), "vkAllocateCommandBuffers");
        _commandBuffer = commandBuffer;
        QueryPoolCreateInfo queryCreate = new()
        {
            SType = StructureType.QueryPoolCreateInfo,
            QueryType = QueryType.Timestamp,
            QueryCount = 2,
        };
        Check(_vk.CreateQueryPool(_device, &queryCreate, null, out _queryPool), "vkCreateQueryPool");
    }

    private void CreateSamplers()
    {
        _nearestSampler = CreateSampler(Filter.Nearest);
        _linearSampler = CreateSampler(Filter.Linear);
    }

    private Sampler CreateSampler(Filter filter)
    {
        SamplerCreateInfo create = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = filter,
            MinFilter = filter,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge,
            AnisotropyEnable = false,
            MaxAnisotropy = 1f,
            BorderColor = BorderColor.FloatOpaqueBlack,
            UnnormalizedCoordinates = false,
            CompareEnable = false,
            MipmapMode = SamplerMipmapMode.Nearest,
            MipLodBias = 0f,
            MinLod = 0f,
            MaxLod = 0f,
        };
        Check(_vk!.CreateSampler(_device, &create, null, out Sampler sampler), "vkCreateSampler");
        return sampler;
    }

    private void CreateDescriptorLayouts()
    {
        DescriptorSetLayoutBinding* paletteBindings = stackalloc DescriptorSetLayoutBinding[3];
        paletteBindings[0] = new DescriptorSetLayoutBinding
        {
            Binding = 0,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit,
        };
        paletteBindings[1] = new DescriptorSetLayoutBinding
        {
            Binding = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit,
        };
        paletteBindings[2] = new DescriptorSetLayoutBinding
        {
            Binding = 2,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit,
        };
        DescriptorSetLayoutCreateInfo paletteCreate = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 3,
            PBindings = paletteBindings,
        };
        Check(
            _vk!.CreateDescriptorSetLayout(_device, &paletteCreate, null, out _paletteLayout),
            "vkCreateDescriptorSetLayout(palette)");

        DescriptorSetLayoutBinding scaleBinding = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit,
        };
        DescriptorSetLayoutCreateInfo scaleCreate = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &scaleBinding,
        };
        Check(
            _vk.CreateDescriptorSetLayout(_device, &scaleCreate, null, out _scaleLayout),
            "vkCreateDescriptorSetLayout(scale)");

        DescriptorPoolSize* poolSizes = stackalloc DescriptorPoolSize[1];
        poolSizes[0] = new DescriptorPoolSize
        {
            Type = DescriptorType.CombinedImageSampler,
            DescriptorCount = 16,
        };
        DescriptorPoolCreateInfo poolCreate = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            MaxSets = 8,
            PoolSizeCount = 1,
            PPoolSizes = poolSizes,
        };
        Check(_vk.CreateDescriptorPool(_device, &poolCreate, null, out _descriptorPool), "vkCreateDescriptorPool");
    }

    private void CreatePipelines()
    {
        PushConstantRange pushRange = new()
        {
            StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            Offset = 0,
            Size = (uint)Marshal.SizeOf<PushConstants>(),
        };
        _palettePipelineLayout = CreatePipelineLayout(_paletteLayout, &pushRange);
        _scalePipelineLayout = CreatePipelineLayout(_scaleLayout, &pushRange);
        _palettePipeline = CreatePipeline(_palettePipelineLayout, VulkanShaders.Palette_frag);
        _scalePipeline = CreatePipeline(_scalePipelineLayout, VulkanShaders.Scale_frag);
        _xbrPipeline = CreatePipeline(_scalePipelineLayout, VulkanShaders.Xbr_frag);
        _fsrEasuPipeline = CreatePipeline(_scalePipelineLayout, VulkanShaders.FsrEasu_frag);
        _fsrRcasPipeline = CreatePipeline(_scalePipelineLayout, VulkanShaders.FsrRcas_frag);
    }

    private PipelineLayout CreatePipelineLayout(DescriptorSetLayout setLayout, PushConstantRange* pushRange)
    {
        PipelineLayoutCreateInfo create = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = &setLayout,
            PushConstantRangeCount = 1,
            PPushConstantRanges = pushRange,
        };
        Check(
            _vk!.CreatePipelineLayout(_device, &create, null, out PipelineLayout layout),
            "vkCreatePipelineLayout");
        return layout;
    }

    private Pipeline CreatePipeline(PipelineLayout layout, ReadOnlySpan<byte> fragmentShader)
    {
        ShaderModule vertexModule = CreateShaderModule(VulkanShaders.Frame_vert);
        ShaderModule fragmentModule = CreateShaderModule(fragmentShader);
        try
        {
            PipelineShaderStageCreateInfo* stages = stackalloc PipelineShaderStageCreateInfo[2];
            stages[0] = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = vertexModule,
                PName = (byte*)SilkMarshal.StringToPtr("main"),
            };
            stages[1] = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = fragmentModule,
                PName = (byte*)SilkMarshal.StringToPtr("main"),
            };
            try
            {
                PipelineVertexInputStateCreateInfo vertexInput = new()
                {
                    SType = StructureType.PipelineVertexInputStateCreateInfo,
                };
                PipelineInputAssemblyStateCreateInfo inputAssembly = new()
                {
                    SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                    Topology = PrimitiveTopology.TriangleList,
                    PrimitiveRestartEnable = false,
                };
                Viewport viewport = new()
                {
                    X = 0,
                    Y = 0,
                    Width = _outputWidth,
                    Height = _outputHeight,
                    MinDepth = 0f,
                    MaxDepth = 1f,
                };
                Rect2D scissor = new()
                {
                    Offset = new Offset2D { X = 0, Y = 0 },
                    Extent = new Extent2D { Width = (uint)_outputWidth, Height = (uint)_outputHeight },
                };
                PipelineViewportStateCreateInfo viewportState = new()
                {
                    SType = StructureType.PipelineViewportStateCreateInfo,
                    ViewportCount = 1,
                    PViewports = &viewport,
                    ScissorCount = 1,
                    PScissors = &scissor,
                };
                PipelineRasterizationStateCreateInfo raster = new()
                {
                    SType = StructureType.PipelineRasterizationStateCreateInfo,
                    DepthClampEnable = false,
                    RasterizerDiscardEnable = false,
                    PolygonMode = PolygonMode.Fill,
                    CullMode = CullModeFlags.None,
                    FrontFace = FrontFace.CounterClockwise,
                    DepthBiasEnable = false,
                    LineWidth = 1f,
                };
                PipelineMultisampleStateCreateInfo multisample = new()
                {
                    SType = StructureType.PipelineMultisampleStateCreateInfo,
                    SampleShadingEnable = false,
                    RasterizationSamples = SampleCountFlags.Count1Bit,
                };
                PipelineColorBlendAttachmentState blendAttachment = new()
                {
                    BlendEnable = false,
                    ColorWriteMask = ColorComponentFlags.RBit
                        | ColorComponentFlags.GBit
                        | ColorComponentFlags.BBit
                        | ColorComponentFlags.ABit,
                };
                PipelineColorBlendStateCreateInfo colorBlend = new()
                {
                    SType = StructureType.PipelineColorBlendStateCreateInfo,
                    LogicOpEnable = false,
                    AttachmentCount = 1,
                    PAttachments = &blendAttachment,
                };
                DynamicState* dynamicStates = stackalloc DynamicState[2];
                dynamicStates[0] = DynamicState.Viewport;
                dynamicStates[1] = DynamicState.Scissor;
                PipelineDynamicStateCreateInfo dynamicState = new()
                {
                    SType = StructureType.PipelineDynamicStateCreateInfo,
                    DynamicStateCount = 2,
                    PDynamicStates = dynamicStates,
                };
                GraphicsPipelineCreateInfo pipelineCreate = new()
                {
                    SType = StructureType.GraphicsPipelineCreateInfo,
                    StageCount = 2,
                    PStages = stages,
                    PVertexInputState = &vertexInput,
                    PInputAssemblyState = &inputAssembly,
                    PViewportState = &viewportState,
                    PRasterizationState = &raster,
                    PMultisampleState = &multisample,
                    PColorBlendState = &colorBlend,
                    PDynamicState = &dynamicState,
                    Layout = layout,
                    RenderPass = _renderPass,
                    Subpass = 0,
                };
                Check(
                    _vk!.CreateGraphicsPipelines(_device, default, 1, &pipelineCreate, null, out Pipeline pipeline),
                    "vkCreateGraphicsPipelines");
                return pipeline;
            }
            finally
            {
                SilkMarshal.Free((nint)stages[0].PName);
                SilkMarshal.Free((nint)stages[1].PName);
            }
        }
        finally
        {
            _vk!.DestroyShaderModule(_device, vertexModule, null);
            _vk.DestroyShaderModule(_device, fragmentModule, null);
        }
    }

    private ShaderModule CreateShaderModule(ReadOnlySpan<byte> shader)
    {
        var words = new uint[(shader.Length + 3) / 4];
        shader.CopyTo(MemoryMarshal.AsBytes(words.AsSpan()));
        fixed (uint* code = words)
        {
            ShaderModuleCreateInfo create = new()
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)shader.Length,
                PCode = code,
            };
            Check(_vk!.CreateShaderModule(_device, &create, null, out ShaderModule module), "vkCreateShaderModule");
            return module;
        }
    }

    private void CreateRenderPass()
    {
        AttachmentDescription attachment = new()
        {
            Format = _outputFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.General,
            FinalLayout = ImageLayout.General,
        };
        AttachmentReference reference = new()
        {
            Attachment = 0,
            Layout = ImageLayout.General,
        };
        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &reference,
        };
        RenderPassCreateInfo create = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &attachment,
            SubpassCount = 1,
            PSubpasses = &subpass,
        };
        Check(_vk!.CreateRenderPass(_device, &create, null, out _renderPass), "vkCreateRenderPass");
    }

    private void CreateStagingResources()
    {
        _pixelStaging = CreateBuffer(
            MaxSourceWidth * MaxSourceHeight * sizeof(ushort),
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out _pixelStagingMemory);
        void* pixelMapped;
        _vk!.MapMemory(_device, _pixelStagingMemory, 0, VkApi.WholeSize, MemoryMapFlags.None, &pixelMapped);
        _pixelStagingMapped = pixelMapped;

        _clutStaging = CreateBuffer(
            ClutEntries * MaxSourceHeight * sizeof(uint),
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out _clutStagingMemory);
        void* clutMapped;
        _vk.MapMemory(_device, _clutStagingMemory, 0, VkApi.WholeSize, MemoryMapFlags.None, &clutMapped);
        _clutStagingMapped = clutMapped;

        _paramStaging = CreateBuffer(
            MaxSourceHeight * 2 * sizeof(uint),
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out _paramStagingMemory);
        void* paramMapped;
        _vk.MapMemory(_device, _paramStagingMemory, 0, VkApi.WholeSize, MemoryMapFlags.None, &paramMapped);
        _paramStagingMapped = paramMapped;
    }

    private void CreateSourceImages()
    {
        CreateImage(
            MaxSourceWidth,
            MaxSourceHeight,
            Format.R16Uint,
            ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit,
            out _pixelImage,
            out _pixelMemory,
            out _pixelView);
        CreateImage(
            ClutEntries,
            MaxSourceHeight,
            Format.R8G8B8A8Unorm,
            ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit,
            out _clutImage,
            out _clutMemory,
            out _clutView);
        CreateImage(
            1,
            MaxSourceHeight,
            Format.R32G32Uint,
            ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit,
            out _paramImage,
            out _paramMemory,
            out _paramView);
    }

    private void CreateExpandedTarget()
    {
        var width = Math.Max(1, _sourceWidth);
        var height = Math.Max(1, _sourceHeight);
        CreateImage(
            (uint)width,
            (uint)height,
            _outputFormat,
            ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
            out _expandedImage,
            out _expandedMemory,
            out _expandedView);
        _expandedFramebuffer = CreateFramebuffer(_expandedView, (uint)width, (uint)height);
    }

    private void RecreateExpandedTarget()
    {
        DestroyExpandedTarget();
        DestroyFsrTarget();
        CreateExpandedTarget();
        UpdateScaleDescriptor(_expandedView);
        UpdateImageDescriptor(_xbrSet, _expandedView, _nearestSampler);
        UpdateImageDescriptor(_fsrEasuSet, _expandedView, _nearestSampler);
    }

    private void CreateOutputTarget()
    {
        CreateImage(
            (uint)_outputWidth,
            (uint)_outputHeight,
            _outputFormat,
            ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferSrcBit,
            out _outputImage,
            out _outputMemory,
            out _outputView);
        _outputFramebuffer = CreateFramebuffer(_outputView, (uint)_outputWidth, (uint)_outputHeight);

        _readbackSize = (ulong)(_outputWidth * _outputHeight * 4);
        _readbackBuffer = CreateBuffer(
            _readbackSize,
            BufferUsageFlags.TransferDstBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out _readbackMemory);
        void* mapped;
        _vk!.MapMemory(_device, _readbackMemory, 0, VkApi.WholeSize, MemoryMapFlags.None, &mapped);
        _readbackMapped = mapped;
    }

    private Framebuffer CreateFramebuffer(ImageView view, uint width, uint height)
    {
        ImageView* attachments = stackalloc ImageView[1];
        attachments[0] = view;
        FramebufferCreateInfo create = new()
        {
            SType = StructureType.FramebufferCreateInfo,
            RenderPass = _renderPass,
            AttachmentCount = 1,
            PAttachments = attachments,
            Width = width,
            Height = height,
            Layers = 1,
        };
        Check(_vk!.CreateFramebuffer(_device, &create, null, out Framebuffer framebuffer), "vkCreateFramebuffer");
        return framebuffer;
    }

    private void CreateImage(
        uint width,
        uint height,
        Format format,
        ImageUsageFlags usage,
        out Image image,
        out DeviceMemory memory,
        out ImageView view)
    {
        ImageCreateInfo create = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = format,
            Extent = new Extent3D { Width = width, Height = height, Depth = 1 },
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
        };
        Check(_vk!.CreateImage(_device, &create, null, out image), "vkCreateImage");
        _vk.GetImageMemoryRequirements(_device, image, out MemoryRequirements requirements);
        MemoryAllocateInfo allocate = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = FindMemoryType(requirements.MemoryTypeBits, MemoryPropertyFlags.DeviceLocalBit),
        };
        Check(_vk.AllocateMemory(_device, &allocate, null, out memory), "vkAllocateMemory");
        Check(_vk.BindImageMemory(_device, image, memory, 0), "vkBindImageMemory");

        ImageViewCreateInfo viewCreate = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };
        Check(_vk.CreateImageView(_device, &viewCreate, null, out view), "vkCreateImageView");
        TransitionImageToGeneral(image);
    }

    private void TransitionImageToGeneral(Image image)
    {
        CommandBufferAllocateInfo allocate = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };
        CommandBuffer commandBuffer;
        Check(
            _vk!.AllocateCommandBuffers(_device, &allocate, &commandBuffer),
            "vkAllocateCommandBuffers(transition)");
        CommandBufferBeginInfo begin = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };
        Check(_vk.BeginCommandBuffer(commandBuffer, &begin), "vkBeginCommandBuffer(transition)");
        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = ImageLayout.Undefined,
            NewLayout = ImageLayout.General,
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
            SrcAccessMask = AccessFlags.None,
            DstAccessMask = AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit,
        };
        _vk.CmdPipelineBarrier(
            commandBuffer,
            PipelineStageFlags.TopOfPipeBit,
            PipelineStageFlags.AllCommandsBit,
            DependencyFlags.None,
            0,
            null,
            0,
            null,
            1,
            &barrier);
        Check(_vk.EndCommandBuffer(commandBuffer), "vkEndCommandBuffer(transition)");
        SubmitInfo submit = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
        };
        Check(_vk.QueueSubmit(_queue, 1, &submit, default), "vkQueueSubmit(transition)");
        Check(_vk.QueueWaitIdle(_queue), "vkQueueWaitIdle(transition)");
        _vk.FreeCommandBuffers(_device, _commandPool, 1, &commandBuffer);
    }

    private Silk.NET.Vulkan.Buffer CreateBuffer(
        ulong size,
        BufferUsageFlags usage,
        MemoryPropertyFlags memoryProperties,
        out DeviceMemory memory)
    {
        BufferCreateInfo create = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
        };
        Check(_vk!.CreateBuffer(_device, &create, null, out Silk.NET.Vulkan.Buffer buffer), "vkCreateBuffer");
        _vk.GetBufferMemoryRequirements(_device, buffer, out MemoryRequirements requirements);
        MemoryAllocateInfo allocate = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = FindMemoryType(requirements.MemoryTypeBits, memoryProperties),
        };
        Check(_vk.AllocateMemory(_device, &allocate, null, out memory), "vkAllocateMemory");
        Check(_vk.BindBufferMemory(_device, buffer, memory, 0), "vkBindBufferMemory");
        return buffer;
    }

    private uint FindMemoryType(uint typeBits, MemoryPropertyFlags properties)
    {
        _vk!.GetPhysicalDeviceMemoryProperties(_physicalDevice, out PhysicalDeviceMemoryProperties memoryProperties);
        for (uint i = 0; i < memoryProperties.MemoryTypeCount; i++)
        {
            if ((typeBits & (1u << (int)i)) != 0
                && (memoryProperties.MemoryTypes[(int)i].PropertyFlags & properties) == properties)
            {
                return i;
            }
        }

        throw new InvalidOperationException("No Vulkan memory type matches the requested properties.");
    }

    private void UpdateDescriptors()
    {
        _paletteSet = AllocateDescriptorSet(_paletteLayout);
        _scaleSet = AllocateDescriptorSet(_scaleLayout);
        _scaleXbrSet = AllocateDescriptorSet(_scaleLayout);
        _xbrSet = AllocateDescriptorSet(_scaleLayout);
        _fsrEasuSet = AllocateDescriptorSet(_scaleLayout);
        _fsrRcasSet = AllocateDescriptorSet(_scaleLayout);
        _hqxScaleSet = AllocateDescriptorSet(_scaleLayout);

        DescriptorImageInfo* paletteImages = stackalloc DescriptorImageInfo[3];
        paletteImages[0] = new DescriptorImageInfo { ImageView = _pixelView, ImageLayout = ImageLayout.General, Sampler = _nearestSampler };
        paletteImages[1] = new DescriptorImageInfo { ImageView = _clutView, ImageLayout = ImageLayout.General, Sampler = _nearestSampler };
        paletteImages[2] = new DescriptorImageInfo { ImageView = _paramView, ImageLayout = ImageLayout.General, Sampler = _nearestSampler };
        WriteDescriptorSet* paletteWrites = stackalloc WriteDescriptorSet[3];
        for (uint i = 0; i < 3; i++)
        {
            paletteWrites[i] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = _paletteSet,
                DstBinding = i,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &paletteImages[i],
            };
        }

        _vk!.UpdateDescriptorSets(_device, 3, paletteWrites, 0, null);
        UpdateScaleDescriptor(_expandedView);
        UpdateImageDescriptor(_xbrSet, _expandedView, _nearestSampler);
        UpdateImageDescriptor(_fsrEasuSet, _expandedView, _nearestSampler);
    }

    private DescriptorSet AllocateDescriptorSet(DescriptorSetLayout layout)
    {
        DescriptorSetAllocateInfo allocate = new()
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &layout,
        };
        Check(_vk!.AllocateDescriptorSets(_device, &allocate, out DescriptorSet set), "vkAllocateDescriptorSets");
        return set;
    }

    private void UpdateScaleDescriptor(ImageView view)
    {
        UpdateImageDescriptor(_scaleSet, view, _linearSampler);
    }

    private void UpdateImageDescriptor(DescriptorSet set, ImageView view, Sampler sampler)
    {
        if (_vk is null || set.Handle == 0 || view.Handle == 0)
        {
            return;
        }

        DescriptorImageInfo image = new()
        {
            ImageView = view,
            ImageLayout = ImageLayout.General,
            Sampler = sampler,
        };
        WriteDescriptorSet write = new()
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = set,
            DstBinding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            PImageInfo = &image,
        };
        _vk.UpdateDescriptorSets(_device, 1, &write, 0, null);
    }

    private void UploadSourceData()
    {
        var pixelCount = _sourceWidth * _sourceHeight;
        var pixelDestination = new Span<ushort>(_pixelStagingMapped, pixelCount);
        _uploader.Pixels.CopyTo(pixelDestination);

        var clutSource = _uploader.Clut;
        for (var y = 0; y < _sourceHeight; y++)
        {
            var sourceRow = y * ClutEntries * 3;
            var destinationRow = y * ClutEntries;
            for (var i = 0; i < ClutEntries; i++)
            {
                var source = sourceRow + (i * 3);
                _clutRgba[destinationRow + i] = 0xff000000u
                    | ((uint)clutSource[source + 2] << 16)
                    | ((uint)clutSource[source + 1] << 8)
                    | clutSource[source];
            }
        }

        var clutDestination = new Span<uint>(_clutStagingMapped, _sourceHeight * ClutEntries);
        _clutRgba.AsSpan(0, clutDestination.Length).CopyTo(clutDestination);
        var paramDestination = new Span<uint>(_paramStagingMapped, _sourceHeight * 2);
        _uploader.LineParams.CopyTo(paramDestination);
    }

    private void RecordFrame()
    {
        Check(_vk!.ResetCommandBuffer(_commandBuffer, CommandBufferResetFlags.None), "vkResetCommandBuffer");
        CommandBufferBeginInfo begin = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };
        Check(_vk.BeginCommandBuffer(_commandBuffer, &begin), "vkBeginCommandBuffer");
        _vk.CmdWriteTimestamp(
            _commandBuffer,
            PipelineStageFlags.TopOfPipeBit,
            _queryPool,
            0);
        CopyStagingToImages();
        RecordPalettePass();
        RecordFilterPass();
        CopyOutputToReadback();
        _vk.CmdWriteTimestamp(
            _commandBuffer,
            PipelineStageFlags.BottomOfPipeBit,
            _queryPool,
            1);
        Check(_vk.EndCommandBuffer(_commandBuffer), "vkEndCommandBuffer");

        CommandBuffer commandBuffer = _commandBuffer;
        SubmitInfo submit = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
        };
        Check(_vk.QueueSubmit(_queue, 1, &submit, default), "vkQueueSubmit");
    }

    private void WaitForFrame()
    {
        Check(_vk!.QueueWaitIdle(_queue), "vkQueueWaitIdle");
    }

    private void CopyStagingToImages()
    {
        var hqxUpload = IsHqxFilter && _hqxImage.Handle != 0;
        var barrierCount = hqxUpload ? 4u : 3u;
        ImageMemoryBarrier* barriers = stackalloc ImageMemoryBarrier[4];
        const AccessFlags uploadSource = AccessFlags.ShaderReadBit
            | AccessFlags.MemoryReadBit
            | AccessFlags.MemoryWriteBit;
        barriers[0] = CreateUploadBarrier(_pixelImage, uploadSource, AccessFlags.TransferWriteBit);
        barriers[1] = CreateUploadBarrier(_clutImage, uploadSource, AccessFlags.TransferWriteBit);
        barriers[2] = CreateUploadBarrier(_paramImage, uploadSource, AccessFlags.TransferWriteBit);
        if (hqxUpload)
        {
            barriers[3] = CreateUploadBarrier(_hqxImage, uploadSource, AccessFlags.TransferWriteBit);
        }

        _vk!.CmdPipelineBarrier(
            _commandBuffer,
            PipelineStageFlags.AllCommandsBit,
            PipelineStageFlags.TransferBit,
            DependencyFlags.None,
            0,
            null,
            0,
            null,
            barrierCount,
            barriers);
        CopyBufferToImage(_pixelStaging, _pixelImage, (uint)_sourceWidth, (uint)_sourceHeight);
        CopyBufferToImage(_clutStaging, _clutImage, ClutEntries, (uint)_sourceHeight);
        CopyBufferToImage(_paramStaging, _paramImage, 1, (uint)_sourceHeight);
        if (hqxUpload)
        {
            CopyBufferToImage(_hqxStaging, _hqxImage, (uint)_hqxWidth, (uint)_hqxHeight);
        }

        barriers[0] = CreateUploadBarrier(_pixelImage, AccessFlags.TransferWriteBit, AccessFlags.ShaderReadBit);
        barriers[1] = CreateUploadBarrier(_clutImage, AccessFlags.TransferWriteBit, AccessFlags.ShaderReadBit);
        barriers[2] = CreateUploadBarrier(_paramImage, AccessFlags.TransferWriteBit, AccessFlags.ShaderReadBit);
        if (hqxUpload)
        {
            barriers[3] = CreateUploadBarrier(_hqxImage, AccessFlags.TransferWriteBit, AccessFlags.ShaderReadBit);
        }

        _vk.CmdPipelineBarrier(
            _commandBuffer,
            PipelineStageFlags.TransferBit,
            PipelineStageFlags.FragmentShaderBit,
            DependencyFlags.None,
            0,
            null,
            0,
            null,
            barrierCount,
            barriers);
    }

    private void Transition(
        Image image,
        AccessFlags source,
        AccessFlags destination,
        PipelineStageFlags sourceStage,
        PipelineStageFlags destinationStage)
    {
        ImageMemoryBarrier barrier = CreateUploadBarrier(image, source, destination);
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

    private static ImageMemoryBarrier CreateUploadBarrier(Image image, AccessFlags source, AccessFlags destination)
    {
        return new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = ImageLayout.General,
            NewLayout = ImageLayout.General,
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
            SrcAccessMask = source,
            DstAccessMask = destination,
        };
    }

    private void CopyBufferToImage(Silk.NET.Vulkan.Buffer buffer, Image image, uint width, uint height)
    {
        BufferImageCopy region = new()
        {
            BufferOffset = 0,
            BufferRowLength = width,
            BufferImageHeight = height,
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
            ImageOffset = new Offset3D { X = 0, Y = 0, Z = 0 },
            ImageExtent = new Extent3D { Width = width, Height = height, Depth = 1 },
        };
        _vk!.CmdCopyBufferToImage(
            _commandBuffer,
            buffer,
            image,
            ImageLayout.General,
            1,
            &region);
    }

    private void RecordPalettePass()
    {
        Transition(
            _expandedImage,
            AccessFlags.ShaderReadBit | AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit,
            AccessFlags.ColorAttachmentWriteBit,
            PipelineStageFlags.AllCommandsBit,
            PipelineStageFlags.ColorAttachmentOutputBit);
        ClearValue clear = new() { Color = new ClearColorValue(0f, 0f, 0f, 1f) };
        RenderPassBeginInfo begin = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _expandedFramebuffer,
            RenderArea = new Rect2D
            {
                Offset = new Offset2D { X = 0, Y = 0 },
                Extent = new Extent2D { Width = (uint)_sourceWidth, Height = (uint)_sourceHeight },
            },
            ClearValueCount = 1,
            PClearValues = &clear,
        };
        _vk!.CmdBeginRenderPass(_commandBuffer, &begin, SubpassContents.Inline);
        Viewport viewport = new()
        {
            X = 0,
            Y = 0,
            Width = _sourceWidth,
            Height = _sourceHeight,
            MinDepth = 0f,
            MaxDepth = 1f,
        };
        Rect2D scissor = new()
        {
            Offset = new Offset2D { X = 0, Y = 0 },
            Extent = new Extent2D { Width = (uint)_sourceWidth, Height = (uint)_sourceHeight },
        };
        _vk.CmdSetViewport(_commandBuffer, 0, 1, &viewport);
        _vk.CmdSetScissor(_commandBuffer, 0, 1, &scissor);
        _vk.CmdBindPipeline(_commandBuffer, PipelineBindPoint.Graphics, _palettePipeline);
        DescriptorSet paletteSet = _paletteSet;
        _vk.CmdBindDescriptorSets(
            _commandBuffer,
            PipelineBindPoint.Graphics,
            _palettePipelineLayout,
            0,
            1,
            &paletteSet,
            0,
            null);
        PushConstants constants = new()
        {
            Rect = new System.Numerics.Vector4(0f, 0f, _sourceWidth, _sourceHeight),
            OutputSize = new System.Numerics.Vector2(_sourceWidth, _sourceHeight),
            SourceWidth = _sourceWidth,
            SourceHeight = _sourceHeight,
        };
        _vk.CmdPushConstants(
            _commandBuffer,
            _palettePipelineLayout,
            ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            0,
            (uint)sizeof(PushConstants),
            &constants);
        _vk.CmdDraw(_commandBuffer, 3, 1, 0, 0);
        _vk.CmdEndRenderPass(_commandBuffer);
        Transition(
            _expandedImage,
            AccessFlags.ColorAttachmentWriteBit,
            AccessFlags.ShaderReadBit,
            PipelineStageFlags.ColorAttachmentOutputBit,
            PipelineStageFlags.FragmentShaderBit);
    }

    private void RecordFilterPass()
    {
        _filterFallback = false;
        var filter = _settings.Filter;
        if (filter == VideoFilter.Xbr)
        {
            var xbrWidth = _sourceWidth * 4;
            var xbrHeight = _sourceHeight * 4;
            if (xbrWidth <= _capabilities.MaxTextureSize && xbrHeight <= _capabilities.MaxTextureSize)
            {
                EnsureXbrTarget(xbrWidth, xbrHeight);
                RecordXbrPass(xbrWidth, xbrHeight);
                RecordScalePass(xbrWidth, xbrHeight, 1, filter, _scaleXbrSet);
                return;
            }

            _filterFallback = true;
        }
        else if (filter == VideoFilter.Fsr)
        {
            ViewportRect viewport = ViewportCalculator.Compute(
                _sourceWidth,
                _sourceHeight,
                _outputWidth,
                _outputHeight,
                _settings);
            if (viewport.Width <= _capabilities.MaxTextureSize && viewport.Height <= _capabilities.MaxTextureSize)
            {
                EnsureFsrTarget(Math.Max(1, viewport.Width), Math.Max(1, viewport.Height));
                RecordFsrEasuPass(viewport);
                RecordFsrRcasPass(viewport);
                return;
            }

            _filterFallback = true;
        }
        else if (filter is VideoFilter.Hqx or VideoFilter.Hqx3x or VideoFilter.Hqx4x)
        {
            var scale = HqxFilter.ScaleFor(filter);
            var hqxWidth = _sourceWidth * scale;
            var hqxHeight = _sourceHeight * scale;
            if (hqxWidth <= _capabilities.MaxTextureSize
                && hqxHeight <= _capabilities.MaxTextureSize
                && _hqxImage.Handle != 0
                && _hqxWidth == hqxWidth
                && _hqxHeight == hqxHeight)
            {
                RecordScalePass(hqxWidth, hqxHeight, 1, filter, _hqxScaleSet);
                return;
            }

            _filterFallback = true;
        }

        UpdateScaleDescriptor(_expandedView);
        RecordScalePass(_sourceWidth, _sourceHeight, FilterValue(filter), filter, _scaleSet);
    }

    private void RecordXbrPass(int width, int height)
    {
        Transition(
            _xbrImage,
            AccessFlags.ShaderReadBit | AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit,
            AccessFlags.ColorAttachmentWriteBit,
            PipelineStageFlags.AllCommandsBit,
            PipelineStageFlags.ColorAttachmentOutputBit);
        ClearValue clear = new() { Color = new ClearColorValue(0f, 0f, 0f, 1f) };
        RenderPassBeginInfo begin = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _xbrFramebuffer,
            RenderArea = new Rect2D
            {
                Offset = new Offset2D { X = 0, Y = 0 },
                Extent = new Extent2D { Width = (uint)width, Height = (uint)height },
            },
            ClearValueCount = 1,
            PClearValues = &clear,
        };
        _vk!.CmdBeginRenderPass(_commandBuffer, &begin, SubpassContents.Inline);
        Viewport viewport = new()
        {
            X = 0,
            Y = 0,
            Width = width,
            Height = height,
            MinDepth = 0f,
            MaxDepth = 1f,
        };
        Rect2D scissor = new()
        {
            Offset = new Offset2D { X = 0, Y = 0 },
            Extent = new Extent2D { Width = (uint)width, Height = (uint)height },
        };
        _vk.CmdSetViewport(_commandBuffer, 0, 1, &viewport);
        _vk.CmdSetScissor(_commandBuffer, 0, 1, &scissor);
        _vk.CmdBindPipeline(_commandBuffer, PipelineBindPoint.Graphics, _xbrPipeline);
        DescriptorSet xbrSet = _xbrSet;
        _vk.CmdBindDescriptorSets(
            _commandBuffer,
            PipelineBindPoint.Graphics,
            _scalePipelineLayout,
            0,
            1,
            &xbrSet,
            0,
            null);
        PushConstants constants = new()
        {
            Rect = new System.Numerics.Vector4(0f, 0f, width, height),
            OutputSize = new System.Numerics.Vector2(width, height),
            SourceWidth = _sourceWidth,
            SourceHeight = _sourceHeight,
            Filter = 1,
        };
        _vk.CmdPushConstants(
            _commandBuffer,
            _scalePipelineLayout,
            ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            0,
            (uint)sizeof(PushConstants),
            &constants);
        _vk.CmdDraw(_commandBuffer, 3, 1, 0, 0);
        _vk.CmdEndRenderPass(_commandBuffer);
        Transition(
            _xbrImage,
            AccessFlags.ColorAttachmentWriteBit,
            AccessFlags.ShaderReadBit,
            PipelineStageFlags.ColorAttachmentOutputBit,
            PipelineStageFlags.FragmentShaderBit);
    }

    private void RecordFsrEasuPass(ViewportRect viewport)
    {
        var width = Math.Max(1, viewport.Width);
        var height = Math.Max(1, viewport.Height);
        Transition(
            _fsrImage,
            AccessFlags.ShaderReadBit | AccessFlags.MemoryReadBit | AccessFlags.MemoryWriteBit,
            AccessFlags.ColorAttachmentWriteBit,
            PipelineStageFlags.AllCommandsBit,
            PipelineStageFlags.ColorAttachmentOutputBit);
        ClearValue clear = new() { Color = new ClearColorValue(0f, 0f, 0f, 1f) };
        RenderPassBeginInfo begin = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _fsrFramebuffer,
            RenderArea = new Rect2D
            {
                Offset = new Offset2D { X = 0, Y = 0 },
                Extent = new Extent2D { Width = (uint)width, Height = (uint)height },
            },
            ClearValueCount = 1,
            PClearValues = &clear,
        };
        _vk!.CmdBeginRenderPass(_commandBuffer, &begin, SubpassContents.Inline);
        Viewport targetViewport = new()
        {
            X = 0,
            Y = 0,
            Width = width,
            Height = height,
            MinDepth = 0f,
            MaxDepth = 1f,
        };
        Rect2D scissor = new()
        {
            Offset = new Offset2D { X = 0, Y = 0 },
            Extent = new Extent2D { Width = (uint)width, Height = (uint)height },
        };
        _vk.CmdSetViewport(_commandBuffer, 0, 1, &targetViewport);
        _vk.CmdSetScissor(_commandBuffer, 0, 1, &scissor);
        _vk.CmdBindPipeline(_commandBuffer, PipelineBindPoint.Graphics, _fsrEasuPipeline);
        DescriptorSet easuSet = _fsrEasuSet;
        _vk.CmdBindDescriptorSets(
            _commandBuffer,
            PipelineBindPoint.Graphics,
            _scalePipelineLayout,
            0,
            1,
            &easuSet,
            0,
            null);
        PushConstants constants = new()
        {
            Rect = new System.Numerics.Vector4(0f, 0f, width, height),
            OutputSize = new System.Numerics.Vector2(width, height),
            SourceWidth = _sourceWidth,
            SourceHeight = _sourceHeight,
            Filter = 1,
        };
        _vk.CmdPushConstants(
            _commandBuffer,
            _scalePipelineLayout,
            ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            0,
            (uint)sizeof(PushConstants),
            &constants);
        _vk.CmdDraw(_commandBuffer, 3, 1, 0, 0);
        _vk.CmdEndRenderPass(_commandBuffer);
        Transition(
            _fsrImage,
            AccessFlags.ColorAttachmentWriteBit,
            AccessFlags.ShaderReadBit,
            PipelineStageFlags.ColorAttachmentOutputBit,
            PipelineStageFlags.FragmentShaderBit);
    }

    private void RecordFsrRcasPass(ViewportRect viewport)
    {
        ClearValue clear = new() { Color = new ClearColorValue(0f, 0f, 0f, 1f) };
        RenderPassBeginInfo begin = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _outputFramebuffer,
            RenderArea = new Rect2D
            {
                Offset = new Offset2D { X = 0, Y = 0 },
                Extent = new Extent2D { Width = (uint)_outputWidth, Height = (uint)_outputHeight },
            },
            ClearValueCount = 1,
            PClearValues = &clear,
        };
        _vk!.CmdBeginRenderPass(_commandBuffer, &begin, SubpassContents.Inline);
        Viewport outputViewport = new()
        {
            X = 0,
            Y = 0,
            Width = _outputWidth,
            Height = _outputHeight,
            MinDepth = 0f,
            MaxDepth = 1f,
        };
        Rect2D outputScissor = new()
        {
            Offset = new Offset2D { X = 0, Y = 0 },
            Extent = new Extent2D { Width = (uint)_outputWidth, Height = (uint)_outputHeight },
        };
        _vk.CmdSetViewport(_commandBuffer, 0, 1, &outputViewport);
        _vk.CmdSetScissor(_commandBuffer, 0, 1, &outputScissor);
        _vk.CmdBindPipeline(_commandBuffer, PipelineBindPoint.Graphics, _fsrRcasPipeline);
        DescriptorSet rcasSet = _fsrRcasSet;
        _vk.CmdBindDescriptorSets(
            _commandBuffer,
            PipelineBindPoint.Graphics,
            _scalePipelineLayout,
            0,
            1,
            &rcasSet,
            0,
            null);
        PushConstants constants = new()
        {
            Rect = new System.Numerics.Vector4(
                viewport.X,
                viewport.Y,
                Math.Max(1, viewport.Width),
                Math.Max(1, viewport.Height)),
            OutputSize = new System.Numerics.Vector2(_outputWidth, _outputHeight),
            SourceWidth = Math.Max(1, viewport.Width),
            SourceHeight = Math.Max(1, viewport.Height),
            Filter = 1,
            Sharpness = (1f - _settings.FsrSharpness) * 2f,
        };
        _vk.CmdPushConstants(
            _commandBuffer,
            _scalePipelineLayout,
            ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            0,
            (uint)sizeof(PushConstants),
            &constants);
        _vk.CmdDraw(_commandBuffer, 3, 1, 0, 0);
        _vk.CmdEndRenderPass(_commandBuffer);
    }

    private void RecordScalePass(
        int sourceWidth,
        int sourceHeight,
        int filterMode,
        VideoFilter filter,
        DescriptorSet scaleSet)
    {
        ViewportRect viewport = ViewportCalculator.Compute(
            _sourceWidth,
            _sourceHeight,
            _outputWidth,
            _outputHeight,
            _settings);
        ClearValue clear = new() { Color = new ClearColorValue(0f, 0f, 0f, 1f) };
        RenderPassBeginInfo begin = new()
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _outputFramebuffer,
            RenderArea = new Rect2D
            {
                Offset = new Offset2D { X = 0, Y = 0 },
                Extent = new Extent2D { Width = (uint)_outputWidth, Height = (uint)_outputHeight },
            },
            ClearValueCount = 1,
            PClearValues = &clear,
        };
        _vk!.CmdBeginRenderPass(_commandBuffer, &begin, SubpassContents.Inline);
        Viewport outputViewport = new()
        {
            X = 0,
            Y = 0,
            Width = _outputWidth,
            Height = _outputHeight,
            MinDepth = 0f,
            MaxDepth = 1f,
        };
        Rect2D outputScissor = new()
        {
            Offset = new Offset2D { X = 0, Y = 0 },
            Extent = new Extent2D { Width = (uint)_outputWidth, Height = (uint)_outputHeight },
        };
        _vk.CmdSetViewport(_commandBuffer, 0, 1, &outputViewport);
        _vk.CmdSetScissor(_commandBuffer, 0, 1, &outputScissor);
        _vk.CmdBindPipeline(_commandBuffer, PipelineBindPoint.Graphics, _scalePipeline);
        _vk.CmdBindDescriptorSets(
            _commandBuffer,
            PipelineBindPoint.Graphics,
            _scalePipelineLayout,
            0,
            1,
            &scaleSet,
            0,
            null);
        PushConstants constants = new()
        {
            Rect = new System.Numerics.Vector4(
                viewport.X,
                viewport.Y,
                Math.Max(1, viewport.Width),
                Math.Max(1, viewport.Height)),
            OutputSize = new System.Numerics.Vector2(_outputWidth, _outputHeight),
            SourceWidth = sourceWidth,
            SourceHeight = sourceHeight,
            Filter = filterMode,
            Scanline = _settings.ScanlineStrength,
            Bloom = filter == VideoFilter.Crt ? _settings.Bloom : 0f,
            BloomThreshold = _settings.BloomThreshold,
        };
        _vk.CmdPushConstants(
            _commandBuffer,
            _scalePipelineLayout,
            ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            0,
            (uint)sizeof(PushConstants),
            &constants);
        _vk.CmdDraw(_commandBuffer, 3, 1, 0, 0);
        _vk.CmdEndRenderPass(_commandBuffer);
    }

    private void CopyOutputToReadback()
    {
        Transition(
            _outputImage,
            AccessFlags.ColorAttachmentWriteBit,
            AccessFlags.TransferReadBit,
            PipelineStageFlags.ColorAttachmentOutputBit,
            PipelineStageFlags.TransferBit);
        BufferImageCopy region = new()
        {
            BufferOffset = 0,
            BufferRowLength = (uint)_outputWidth,
            BufferImageHeight = (uint)_outputHeight,
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
            ImageOffset = new Offset3D { X = 0, Y = 0, Z = 0 },
            ImageExtent = new Extent3D { Width = (uint)_outputWidth, Height = (uint)_outputHeight, Depth = 1 },
        };
        _vk!.CmdCopyImageToBuffer(
            _commandBuffer,
            _outputImage,
            ImageLayout.General,
            _readbackBuffer,
            1,
            &region);
        Transition(
            _outputImage,
            AccessFlags.TransferReadBit,
            AccessFlags.ColorAttachmentWriteBit,
            PipelineStageFlags.TransferBit,
            PipelineStageFlags.ColorAttachmentOutputBit);
    }

    private void ReadGpuTiming()
    {
        if (_timestampPeriod <= 0f)
        {
            return;
        }

        var result = _vk!.GetQueryPoolResults(
            _device,
            _queryPool,
            0,
            2,
            new Span<ulong>(_timestamps),
            sizeof(ulong),
            QueryResultFlags.Result64Bit);
        if (result != Result.Success || _timestamps[1] <= _timestamps[0])
        {
            _gpuMilliseconds = null;
            return;
        }

        _gpuMilliseconds = (_timestamps[1] - _timestamps[0]) * _timestampPeriod / 1_000_000.0;
    }

    private static int FilterValue(VideoFilter filter) => filter switch
    {
        VideoFilter.Accurate or VideoFilter.Nearest => 0,
        VideoFilter.Crt => 2,
        _ => 1,
    };

    private void DestroyResources()
    {
        if (_vk is null || _device.Handle == 0)
        {
            _initialized = false;
            return;
        }

        _vk.DeviceWaitIdle(_device);
        DestroySwapchainResources();
        DestroyOutputTarget();
        DestroyExpandedTarget();
        DestroyImage(_pixelImage, _pixelMemory, _pixelView);
        DestroyImage(_clutImage, _clutMemory, _clutView);
        DestroyImage(_paramImage, _paramMemory, _paramView);
        DestroyBuffer(_pixelStaging, _pixelStagingMemory, _pixelStagingMapped);
        DestroyBuffer(_clutStaging, _clutStagingMemory, _clutStagingMapped);
        DestroyBuffer(_paramStaging, _paramStagingMemory, _paramStagingMapped);
        DestroyBuffer(_readbackBuffer, _readbackMemory, _readbackMapped);
        _pixelStagingMapped = null;
        _clutStagingMapped = null;
        _paramStagingMapped = null;
        _readbackMapped = null;
        _readbackSize = 0;

        if (_palettePipeline.Handle != 0)
        {
            _vk.DestroyPipeline(_device, _palettePipeline, null);
            _palettePipeline = default;
        }

        if (_scalePipeline.Handle != 0)
        {
            _vk.DestroyPipeline(_device, _scalePipeline, null);
            _scalePipeline = default;
        }

        if (_xbrPipeline.Handle != 0)
        {
            _vk.DestroyPipeline(_device, _xbrPipeline, null);
            _xbrPipeline = default;
        }

        if (_fsrEasuPipeline.Handle != 0)
        {
            _vk.DestroyPipeline(_device, _fsrEasuPipeline, null);
            _fsrEasuPipeline = default;
        }

        if (_fsrRcasPipeline.Handle != 0)
        {
            _vk.DestroyPipeline(_device, _fsrRcasPipeline, null);
            _fsrRcasPipeline = default;
        }

        if (_palettePipelineLayout.Handle != 0)
        {
            _vk.DestroyPipelineLayout(_device, _palettePipelineLayout, null);
            _palettePipelineLayout = default;
        }

        if (_scalePipelineLayout.Handle != 0)
        {
            _vk.DestroyPipelineLayout(_device, _scalePipelineLayout, null);
            _scalePipelineLayout = default;
        }

        if (_renderPass.Handle != 0)
        {
            _vk.DestroyRenderPass(_device, _renderPass, null);
            _renderPass = default;
        }

        if (_descriptorPool.Handle != 0)
        {
            _vk.DestroyDescriptorPool(_device, _descriptorPool, null);
            _descriptorPool = default;
        }

        _paletteSet = default;
        _scaleSet = default;
        _scaleXbrSet = default;
        _xbrSet = default;
        _fsrEasuSet = default;
        _fsrRcasSet = default;
        _hqxScaleSet = default;

        if (_paletteLayout.Handle != 0)
        {
            _vk.DestroyDescriptorSetLayout(_device, _paletteLayout, null);
            _paletteLayout = default;
        }

        if (_scaleLayout.Handle != 0)
        {
            _vk.DestroyDescriptorSetLayout(_device, _scaleLayout, null);
            _scaleLayout = default;
        }

        if (_nearestSampler.Handle != 0)
        {
            _vk.DestroySampler(_device, _nearestSampler, null);
            _nearestSampler = default;
        }

        if (_linearSampler.Handle != 0)
        {
            _vk.DestroySampler(_device, _linearSampler, null);
            _linearSampler = default;
        }

        if (_queryPool.Handle != 0)
        {
            _vk.DestroyQueryPool(_device, _queryPool, null);
            _queryPool = default;
        }

        if (_commandPool.Handle != 0)
        {
            _vk.DestroyCommandPool(_device, _commandPool, null);
            _commandPool = default;
        }

        if (_device.Handle != 0)
        {
            _vk.DestroyDevice(_device, null);
            _device = default;
        }

        DestroySurfaceResources();

        if (_instance.Handle != 0)
        {
            _vk.DestroyInstance(_instance, null);
            _instance = default;
        }

        _initialized = false;
    }

    private void DestroyExpandedTarget()
    {
        if (_vk is null)
        {
            return;
        }

        DestroyXbrTarget();
        DestroyHqxTarget();

        if (_expandedFramebuffer.Handle != 0)
        {
            _vk.DestroyFramebuffer(_device, _expandedFramebuffer, null);
            _expandedFramebuffer = default;
        }

        DestroyImage(_expandedImage, _expandedMemory, _expandedView);
        _expandedImage = default;
        _expandedMemory = default;
        _expandedView = default;
    }

    private void DestroyOutputTarget()
    {
        if (_vk is null)
        {
            return;
        }

        DestroyFsrTarget();

        if (_outputFramebuffer.Handle != 0)
        {
            _vk.DestroyFramebuffer(_device, _outputFramebuffer, null);
            _outputFramebuffer = default;
        }

        DestroyImage(_outputImage, _outputMemory, _outputView);
        _outputImage = default;
        _outputMemory = default;
        _outputView = default;
        DestroyBuffer(_readbackBuffer, _readbackMemory, _readbackMapped);
        _readbackBuffer = default;
        _readbackMemory = default;
        _readbackMapped = null;
    }

    private void EnsureXbrTarget(int width, int height)
    {
        if (_xbrFramebuffer.Handle != 0 && _xbrWidth == width && _xbrHeight == height)
        {
            return;
        }

        DestroyXbrTarget();
        CreateImage(
            (uint)width,
            (uint)height,
            _outputFormat,
            ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
            out _xbrImage,
            out _xbrMemory,
            out _xbrView);
        _xbrFramebuffer = CreateFramebuffer(_xbrView, (uint)width, (uint)height);
        _xbrWidth = width;
        _xbrHeight = height;
        UpdateImageDescriptor(_scaleXbrSet, _xbrView, _linearSampler);
    }

    private void DestroyXbrTarget()
    {
        if (_vk is null)
        {
            return;
        }

        if (_xbrFramebuffer.Handle != 0)
        {
            _vk.DestroyFramebuffer(_device, _xbrFramebuffer, null);
            _xbrFramebuffer = default;
        }

        DestroyImage(_xbrImage, _xbrMemory, _xbrView);
        _xbrImage = default;
        _xbrMemory = default;
        _xbrView = default;
        _xbrWidth = 0;
        _xbrHeight = 0;
    }

    private void EnsureFsrTarget(int width, int height)
    {
        if (_fsrFramebuffer.Handle != 0 && _fsrWidth == width && _fsrHeight == height)
        {
            return;
        }

        DestroyFsrTarget();
        CreateImage(
            (uint)width,
            (uint)height,
            _outputFormat,
            ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.SampledBit,
            out _fsrImage,
            out _fsrMemory,
            out _fsrView);
        _fsrFramebuffer = CreateFramebuffer(_fsrView, (uint)width, (uint)height);
        _fsrWidth = width;
        _fsrHeight = height;
        UpdateImageDescriptor(_fsrRcasSet, _fsrView, _nearestSampler);
    }

    private void DestroyFsrTarget()
    {
        if (_vk is null)
        {
            return;
        }

        if (_fsrFramebuffer.Handle != 0)
        {
            _vk.DestroyFramebuffer(_device, _fsrFramebuffer, null);
            _fsrFramebuffer = default;
        }

        DestroyImage(_fsrImage, _fsrMemory, _fsrView);
        _fsrImage = default;
        _fsrMemory = default;
        _fsrView = default;
        _fsrWidth = 0;
        _fsrHeight = 0;
    }

    private void EnsureHqxTarget(int width, int height)
    {
        if (_hqxImage.Handle != 0 && _hqxWidth == width && _hqxHeight == height)
        {
            return;
        }

        DestroyHqxTarget();
        CreateImage(
            (uint)width,
            (uint)height,
            _hqxFormat,
            ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit,
            out _hqxImage,
            out _hqxMemory,
            out _hqxView);
        _hqxWidth = width;
        _hqxHeight = height;
        _hqxStaging = CreateBuffer(
            (ulong)checked(width * height * 4),
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            out _hqxStagingMemory);
        void* mapped;
        Check(
            _vk!.MapMemory(_device, _hqxStagingMemory, 0, VkApi.WholeSize, MemoryMapFlags.None, &mapped),
            "vkMapMemory(HQx)");
        _hqxStagingMapped = mapped;
        UpdateImageDescriptor(_hqxScaleSet, _hqxView, _linearSampler);
    }

    private void DestroyHqxTarget()
    {
        if (_vk is null)
        {
            return;
        }

        DestroyBuffer(_hqxStaging, _hqxStagingMemory, _hqxStagingMapped);
        _hqxStaging = default;
        _hqxStagingMemory = default;
        _hqxStagingMapped = null;
        DestroyImage(_hqxImage, _hqxMemory, _hqxView);
        _hqxImage = default;
        _hqxMemory = default;
        _hqxView = default;
        _hqxWidth = 0;
        _hqxHeight = 0;
    }

    private void DestroyImage(Image image, DeviceMemory memory, ImageView view)
    {
        if (_vk is null)
        {
            return;
        }

        if (view.Handle != 0)
        {
            _vk.DestroyImageView(_device, view, null);
        }

        if (image.Handle != 0)
        {
            _vk.DestroyImage(_device, image, null);
        }

        if (memory.Handle != 0)
        {
            _vk.FreeMemory(_device, memory, null);
        }
    }

    private void DestroyBuffer(Silk.NET.Vulkan.Buffer buffer, DeviceMemory memory, void* mapped)
    {
        if (_vk is null)
        {
            return;
        }

        if (mapped is not null && memory.Handle != 0)
        {
            _vk.UnmapMemory(_device, memory);
        }

        if (buffer.Handle != 0)
        {
            _vk.DestroyBuffer(_device, buffer, null);
        }

        if (memory.Handle != 0)
        {
            _vk.FreeMemory(_device, memory, null);
        }
    }

    private static void Check(Result result, string operation)
    {
        if (result != Result.Success)
        {
            throw new InvalidOperationException($"{operation} failed with {result}.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PushConstants
    {
        public System.Numerics.Vector4 Rect;
        public System.Numerics.Vector2 OutputSize;
        public int SourceWidth;
        public int SourceHeight;
        public int Filter;
        public float Scanline;
        public float Bloom;
        public float BloomThreshold;
        public float Sharpness;
    }
}
