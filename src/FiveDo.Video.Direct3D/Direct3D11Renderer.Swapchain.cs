using System;
using SharpGen.Runtime;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace FiveDo.Video.Direct3D;

/// <summary>
/// DXGI flip-model swapchain presentation for <see cref="Direct3D11Renderer"/>.
/// </summary>
/// <remarks>
/// The renderer always produces the final output image in its output texture. When a
/// native window handle is supplied, each present copies that texture into the current
/// back buffer and presents it. Readback and parity captures stay available.
/// </remarks>
public sealed partial class Direct3D11Renderer
{
    private nint _windowHandle;
    private IDXGISwapChain1? _swapChain;
    private ID3D11Texture2D? _backBuffer;
    private ID3D11RenderTargetView? _backBufferView;
    private bool _swapchainVsync = true;
    private bool _swapchainValid;
    private bool _tearingSupported;
    private int _swapchainWidth;
    private int _swapchainHeight;

    /// <summary>Gets a value indicating whether a native swapchain is active.</summary>
    public bool SwapchainActive => _swapchainValid;

    /// <summary>Gets the active present mode, or <see langword="null"/> when offscreen.</summary>
    public string? PresentMode => _swapchainValid
        ? _swapchainVsync ? "fifo" : "immediate"
        : null;

    /// <summary>Gets the swapchain width in pixels.</summary>
    public int SwapchainWidth => _swapchainWidth;

    /// <summary>Gets the swapchain height in pixels.</summary>
    public int SwapchainHeight => _swapchainHeight;

    /// <summary>Recreates the swapchain with the requested vsync mode.</summary>
    /// <param name="vsync">Whether FIFO vsync should be used.</param>
    public void SetVsync(bool vsync)
    {
        if (_swapchainVsync == vsync)
        {
            return;
        }

        _swapchainVsync = vsync;
        if (_swapchainValid)
        {
            DestroySwapchainResources();
            CreateSwapchainResources();
        }
    }

    /// <summary>
    /// Initializes the renderer and a DXGI swapchain for the supplied Win32 window.
    /// </summary>
    /// <param name="windowHandle">Native Win32 HWND.</param>
    /// <param name="width">Desired output width.</param>
    /// <param name="height">Desired output height.</param>
    /// <param name="vsync">Request vsync presentation.</param>
    public void InitializeSurface(nint windowHandle, int width, int height, bool vsync = true)
    {
        ArgumentOutOfRangeException.ThrowIfZero(windowHandle);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The embedded Direct3D 11 swapchain currently supports Win32 windows only.");
        }

        _windowHandle = windowHandle;
        _swapchainVsync = vsync;
        Initialize(width, height);
    }

    private void CreateSwapchainResources()
    {
        if (_windowHandle == 0 || _factory is null || _device is null || _context is null)
        {
            return;
        }

        DestroySwapchainResources();
        var description = new SwapChainDescription1(
            (uint)_outputWidth,
            (uint)_outputHeight,
            stereo: false,
            bufferUsage: Usage.RenderTargetOutput,
            bufferCount: 2,
            scaling: Scaling.Stretch,
            swapEffect: SwapEffect.FlipDiscard,
            alphaMode: AlphaMode.Ignore,
            flags: _swapchainVsync ? SwapChainFlags.None : SwapChainFlags.AllowTearing);
        try
        {
            _swapChain = _factory.CreateSwapChainForHwnd(
                _device,
                _windowHandle,
                description);
            _tearingSupported = !_swapchainVsync;
        }
        catch (SharpGenException exception)
        {
            Serilog.Log.Debug(exception, "AllowTearing swapchain creation failed; retrying without tearing.");
            description.Flags = SwapChainFlags.None;
            _swapChain = _factory.CreateSwapChainForHwnd(
                _device,
                _windowHandle,
                description);
            _tearingSupported = false;
        }

        _backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _backBufferView = _device.CreateRenderTargetView(_backBuffer);
        _swapchainWidth = _outputWidth;
        _swapchainHeight = _outputHeight;
        _swapchainValid = true;
    }

    private void ResizeSwapchain(int width, int height)
    {
        if (!_swapchainValid || _swapChain is null)
        {
            return;
        }

        if (width == _swapchainWidth && height == _swapchainHeight)
        {
            return;
        }

        _context!.UnsetRenderTargets();
        _backBufferView?.Dispose();
        _backBufferView = null;
        _backBuffer?.Dispose();
        _backBuffer = null;
        SwapChainFlags flags = _tearingSupported ? SwapChainFlags.AllowTearing : SwapChainFlags.None;
        Result result = _swapChain.ResizeBuffers(
            0,
            (uint)width,
            (uint)height,
            Format.B8G8R8A8_UNorm,
            flags);
        result.CheckError();
        _backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _backBufferView = _device!.CreateRenderTargetView(_backBuffer);
        _swapchainWidth = width;
        _swapchainHeight = height;
    }

    private void PresentSwapchain()
    {
        if (!_swapchainValid || _swapChain is null || _context is null || _output is null)
        {
            return;
        }

        using (ID3D11Texture2D currentBackBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0))
        {
            _context.CopyResource(currentBackBuffer, _output.Texture);
        }

        Result result = _swapChain.Present(
            _swapchainVsync ? 1u : 0u,
            _swapchainVsync || !_tearingSupported ? PresentFlags.None : PresentFlags.AllowTearing);
        result.CheckError();
    }

    private void DestroySwapchainResources()
    {
        _swapchainValid = false;
        _swapchainWidth = 0;
        _swapchainHeight = 0;
        _backBufferView?.Dispose();
        _backBufferView = null;
        _backBuffer?.Dispose();
        _backBuffer = null;
        _swapChain?.Dispose();
        _swapChain = null;
    }
}
