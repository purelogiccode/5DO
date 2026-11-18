using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using FiveDo.Video.Vulkan;

namespace FiveDo.Frontend.Avalonia;

/// <summary>
/// Native AppKit <c>NSView</c> plus <c>CAMetalLayer</c> used for macOS Vulkan presentation.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia has no Vulkan control base, and its macOS backend has no OpenGL control
/// either, so the Vulkan presenter owns a real <c>NSView</c> added above the Avalonia
/// view hierarchy. The view's backing layer is a <c>CAMetalLayer</c>, which is what
/// <c>VK_EXT_metal_surface</c> consumes through MoltenVK; presenting never copies a
/// frame back to the CPU.
/// </para>
/// <para>
/// Cocoa coordinates are points with the origin at the bottom-left. The surface takes
/// top-left point coordinates, converts internally, and sizes the drawable in pixels
/// from the window's backing scale.
/// </para>
/// </remarks>
internal sealed class MacMetalSurface : IDisposable
{
    private nint _contentView;
    private nint _view;
    private nint _layer;
    private nint _device;
    private bool _disposed;

    /// <summary>Creates a metal-backed view inside the supplied window or content view.</summary>
    /// <param name="windowOrView">NSWindow* or NSView* that will host the video view.</param>
    /// <param name="x">Left edge in points, top-left origin.</param>
    /// <param name="y">Top edge in points, top-left origin.</param>
    /// <param name="width">Width in points.</param>
    /// <param name="height">Height in points.</param>
    /// <param name="scale">Backing scale factor (1 for non-Retina, 2 for Retina).</param>
    /// <exception cref="InvalidOperationException">AppKit or Metal is unavailable.</exception>
    internal MacMetalSurface(nint windowOrView, double x, double y, double width, double height, double scale)
    {
        ArgumentOutOfRangeException.ThrowIfZero(windowOrView);
        if (double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "The surface needs a positive size.");
        }

        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("The Metal surface is macOS-only.");
        }

        _contentView = MacObjC.ResolveContentView(windowOrView);
        if (_contentView == 0)
        {
            throw new InvalidOperationException("The macOS window has no content view.");
        }

        try
        {
            _device = MacObjC.CreateSystemDefaultDevice();
            _layer = MacObjC.CreateMetalLayer(_device);
            _view = MacObjC.CreateMetalView(_layer);
            MacObjC.AddSubview(_contentView, _view);
            SetBounds(x, y, width, height, scale);
            MacObjC.SetHidden(_view, true);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Gets the <c>CAMetalLayer*</c> consumed by <c>VK_EXT_metal_surface</c>.</summary>
    internal nint Layer => _layer;

    /// <summary>Gets the Vulkan surface descriptor for this layer.</summary>
    internal VulkanSurface Surface => VulkanSurface.ForMetal(_layer);

    /// <summary>Positions and sizes the view and its drawable.</summary>
    /// <param name="x">Left edge in points, top-left origin.</param>
    /// <param name="y">Top edge in points, top-left origin.</param>
    /// <param name="width">Width in points.</param>
    /// <param name="height">Height in points.</param>
    /// <param name="scale">Backing scale factor.</param>
    internal void SetBounds(double x, double y, double width, double height, double scale)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        double contentHeight = MacObjC.GetBoundsHeight(_contentView);
        // A non-flipped NSView uses a bottom-left origin; Avalonia's own content view may
        // be flipped, in which case the top-left origin matches the caller directly.
        double frameY = MacObjC.GetIsFlipped(_contentView)
            ? y
            : Math.Max(0, contentHeight - y - height);
        MacObjC.SetFrame(_view, x, frameY, width, height);
        MacObjC.SetDrawable(_layer, width, height, scale <= 0 ? 1 : scale);
    }

    /// <summary>Shows or hides the video view.</summary>
    /// <param name="visible">Whether the view should be visible.</param>
    internal void SetVisible(bool visible)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        MacObjC.SetHidden(_view, !visible);
    }

    /// <summary>Moves the video view above the other subviews of the content view.</summary>
    internal void Raise()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        MacObjC.AddSubview(_contentView, _view);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_view != 0)
        {
            MacObjC.RemoveFromSuperview(_view);
            MacObjC.Release(_view);
            _view = 0;
        }

        if (_layer != 0)
        {
            MacObjC.Release(_layer);
            _layer = 0;
        }

        if (_device != 0)
        {
            MacObjC.Release(_device);
            _device = 0;
        }

        _contentView = 0;
    }
}

/// <summary>Minimal Objective-C/AppKit/Metal interop for the macOS Vulkan surface.</summary>
[SuppressMessage(
    "Design",
    "CA1060:Move pinvokes to native methods class",
    Justification = "A single interop helper keeps the macOS surface self-contained.")]
internal static class MacObjC
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private const string Metal = "/System/Library/Frameworks/Metal.framework/Metal";
    private const string QuartzCore = "/System/Library/Frameworks/QuartzCore.framework/QuartzCore";

    private static bool _frameworksLoaded;
    private const nint NsWindowAbove = 1;
    private const uint MtlPixelFormatBgra8Unorm = 80;

    private static readonly nint AllocSelector = Sel("alloc");
    private static readonly nint HitTestSelector = Sel("hitTest:");
    private static readonly nint NsViewClass = ObjCGetClass("NSView");
    private static readonly HitTestDelegate HitTestCallback = HitTest;
    private static readonly AcceptsFirstResponderDelegate AcceptsFirstResponderCallback = AcceptsFirstResponder;
    private static readonly nint MetalHostViewClass = CreateMetalHostViewClass();
    private static readonly nint NsApplicationSharedSelector = Sel("sharedApplication");
    private static readonly nint NsWindowInitSelector = Sel("initWithContentRect:styleMask:backing:defer:");
    private static readonly nint NsStringSelector = Sel("stringWithUTF8String:");
    private static readonly nint NsDateSelector = Sel("distantPast");
    private static readonly nint NsWindowSetTitleSelector = Sel("setTitle:");
    private static readonly nint NsWindowMakeKeySelector = Sel("makeKeyAndOrderFront:");
    private static readonly nint NsWindowCenterSelector = Sel("center");
    private static readonly nint NsWindowCloseSelector = Sel("close");
    private static readonly nint NsWindowSetReleasedSelector = Sel("setReleasedWhenClosed:");
    private static readonly nint NsApplicationSetActivationSelector = Sel("setActivationPolicy:");
    private static readonly nint NsApplicationActivateSelector = Sel("activateIgnoringOtherApps:");
    private static readonly nint FinishLaunchingSelector = Sel("finishLaunching");
    private static readonly nint ContentViewSelector = Sel("contentView");
    private static readonly nint RespondsToSelectorSelector = Sel("respondsToSelector:");
    private static readonly nint NsViewInitSelector = Sel("initWithFrame:");
    private static readonly nint SetWantsLayerSelector = Sel("setWantsLayer:");
    private static readonly nint SetLayerSelector = Sel("setLayer:");
    private static readonly nint AddSubviewSelector = Sel("addSubview:positioned:relativeTo:");
    private static readonly nint RemoveFromSuperviewSelector = Sel("removeFromSuperview");
    private static readonly nint SetHiddenSelector = Sel("setHidden:");
    private static readonly nint BoundsSelector = Sel("bounds");
    private static readonly nint IsFlippedSelector = Sel("isFlipped");
    private static readonly nint SetFrameSelector = Sel("setFrame:");
    private static readonly nint SetContentsScaleSelector = Sel("setContentsScale:");
    private static readonly nint SetDrawableSizeSelector = Sel("setDrawableSize:");
    private static readonly nint SetOpaqueSelector = Sel("setOpaque:");
    private static readonly nint SetFramebufferOnlySelector = Sel("setFramebufferOnly:");
    private static readonly nint SetPixelFormatSelector = Sel("setPixelFormat:");
    private static readonly nint SetDeviceSelector = Sel("setDevice:");
    private static readonly nint LayerSelector = Sel("layer");
    private static readonly nint RetainSelector = Sel("retain");
    private static readonly nint ReleaseSelector = Sel("release");
    private static readonly nint NextEventSelector = Sel("nextEventMatchingMask:untilDate:inMode:dequeue:");
    private static readonly nint SendEventSelector = Sel("sendEvent:");

    private static readonly nint NsApplicationClass = ObjCGetClass("NSApplication");
    private static readonly nint NsWindowClass = ObjCGetClass("NSWindow");
    private static readonly nint NsStringClass = ObjCGetClass("NSString");
    private static readonly nint NsDateClass = ObjCGetClass("NSDate");
    private static readonly nint CaMetalLayerClass = LoadQuartzCoreClass("CAMetalLayer");

    private static readonly nint MsgSendAddress = LoadMsgSendAddress();

    private static readonly MsgSendPtr SendPtr = GetMsgSend<MsgSendPtr>();
    private static readonly MsgSendVoid SendVoid = GetMsgSend<MsgSendVoid>();
    private static readonly MsgSendPtrPtr SendPtrPtr = GetMsgSend<MsgSendPtrPtr>();
    private static readonly MsgSendVoidBool SendVoidBool = GetMsgSend<MsgSendVoidBool>();
    private static readonly MsgSendVoidPtr SendVoidPtr = GetMsgSend<MsgSendVoidPtr>();
    private static readonly MsgSendVoidDouble SendVoidDouble = GetMsgSend<MsgSendVoidDouble>();
    private static readonly MsgSendVoidUInt SendVoidUInt = GetMsgSend<MsgSendVoidUInt>();
    private static readonly MsgSendVoidSize SendVoidSize = GetMsgSend<MsgSendVoidSize>();
    private static readonly MsgSendRect SendRect = GetMsgSend<MsgSendRect>();
    private static readonly MsgSendVoidRect SendVoidRect = GetMsgSend<MsgSendVoidRect>();
    private static readonly MsgSendBool SendBool = GetMsgSend<MsgSendBool>();
    private static readonly MsgSendPtrRect SendPtrRect = GetMsgSend<MsgSendPtrRect>();
    private static readonly MsgSendPtrRectUIntUIntBool SendPtrRectUIntUIntBool =
        GetMsgSend<MsgSendPtrRectUIntUIntBool>();
    private static readonly MsgSendVoidPtrIntPtr SendVoidPtrIntPtr = GetMsgSend<MsgSendVoidPtrIntPtr>();
    private static readonly MsgSendPtrNIntPtrPtrBool SendPtrNIntPtrPtrBool = GetMsgSend<MsgSendPtrNIntPtrPtrBool>();
    private static readonly MsgSendBoolPtr SendBoolPtr = GetMsgSend<MsgSendBoolPtr>();
    private static readonly MsgSendIntPtr SendIntPtr = GetMsgSend<MsgSendIntPtr>();

    // Cached once: an autoreleased string/date per frame would grow the probe's heap.
    private static readonly nint RunLoopMode = CreateAutoreleasedString("kCFRunLoopDefaultMode");
    private static readonly nint DistantPast = SendPtr(NsDateClass, NsDateSelector);

    /// <summary>Ensures the shared application exists.</summary>
    /// <param name="finishLaunching">Call <c>finishLaunching</c> (probe windows only).</param>
    /// <returns>The <c>NSApplication*</c>.</returns>
    internal static nint EnsureApplication(bool finishLaunching)
    {
        nint application = SendPtr(NsApplicationClass, NsApplicationSharedSelector);
        if (finishLaunching && application != 0)
        {
            // Regular activation policy lets an unbundled probe process show a window.
            _ = SendIntPtr(application, NsApplicationSetActivationSelector, 0);
            SendVoid(application, FinishLaunchingSelector);
        }

        return application;
    }

    /// <summary>Creates a titled window with a buffered backing store.</summary>
    /// <param name="title">Window title.</param>
    /// <param name="width">Content width in points.</param>
    /// <param name="height">Content height in points.</param>
    /// <returns>The <c>NSWindow*</c>.</returns>
    internal static nint CreateWindow(string title, double width, double height)
    {
        // Titled | Closable | Miniaturizable | Resizable, buffered backing store.
        const nuint styleMask = 1 | 2 | 4 | 8;
        const nuint backing = 2;
        nint window = SendPtrRectUIntUIntBool(
            SendPtr(NsWindowClass, AllocSelector),
            NsWindowInitSelector,
            new CGRect(0, 0, width, height),
            styleMask,
            backing,
            0);
        if (window == 0)
        {
            throw new InvalidOperationException("NSWindow initialization failed.");
        }

        nint nsTitle = CreateAutoreleasedString(title);
        SendVoidPtr(window, NsWindowSetTitleSelector, nsTitle);
        SendVoidBool(window, NsWindowSetReleasedSelector, 0);
        SendVoid(window, NsWindowCenterSelector);
        SendVoidPtr(window, NsWindowMakeKeySelector, 0);
        return window;
    }

    /// <summary>Closes and releases an <c>NSWindow</c> created by <see cref="CreateWindow"/>.</summary>
    /// <param name="window">The window.</param>
    internal static void CloseWindow(nint window)
    {
        if (window == 0)
        {
            return;
        }

        SendVoid(window, NsWindowCloseSelector);
        Release(window);
    }

    /// <summary>Drains pending AppKit events without blocking.</summary>
    /// <param name="application">The <c>NSApplication*</c>.</param>
    /// <returns>The number of dispatched events.</returns>
    internal static int PumpEvents(nint application)
    {
        var dispatched = 0;
        while (true)
        {
            nint ev = SendPtrNIntPtrPtrBool(
                application,
                NextEventSelector,
                -1, // NSEventMaskAny
                DistantPast,
                RunLoopMode,
                1);
            if (ev == 0)
            {
                return dispatched;
            }

            SendVoidPtr(application, SendEventSelector, ev);
            dispatched++;
        }
    }

    /// <summary>Brings the application to the front.</summary>
    /// <param name="application">The <c>NSApplication*</c>.</param>
    internal static void Activate(nint application)
    {
        if (application != 0)
        {
            SendVoidBool(application, NsApplicationActivateSelector, 1);
        }
    }

    /// <summary>Resolves an NSWindow or NSView handle to its content view.</summary>
    /// <param name="windowOrView">Native handle.</param>
    /// <returns>The <c>NSView*</c> that owns the video subviews.</returns>
    internal static nint ResolveContentView(nint windowOrView)
    {
        if (RespondsTo(windowOrView, ContentViewSelector))
        {
            return SendPtr(windowOrView, ContentViewSelector);
        }

        return windowOrView;
    }

    /// <summary>Creates a Metal device for the layer.</summary>
    /// <returns>The <c>MTLDevice*</c>.</returns>
    internal static nint CreateSystemDefaultDevice()
    {
        nint device = MTLCreateSystemDefaultDevice();
        if (device == 0)
        {
            throw new InvalidOperationException("MTLCreateSystemDefaultDevice returned no device.");
        }

        return device;
    }

    /// <summary>Creates a <c>CAMetalLayer</c>.</summary>
    /// <param name="device">The <c>MTLDevice*</c>.</param>
    /// <returns>The <c>CAMetalLayer*</c>, owned by the caller.</returns>
    internal static nint CreateMetalLayer(nint device)
    {
        if (CaMetalLayerClass == 0)
        {
            throw new InvalidOperationException("CAMetalLayer is unavailable.");
        }

        nint layer = SendPtr(CaMetalLayerClass, LayerSelector);
        if (layer == 0)
        {
            throw new InvalidOperationException("CAMetalLayer allocation failed.");
        }

        _ = SendPtr(layer, RetainSelector);
        SendVoidPtr(layer, SetDeviceSelector, device);
        SendVoidUInt(layer, SetPixelFormatSelector, MtlPixelFormatBgra8Unorm);
        SendVoidBool(layer, SetOpaqueSelector, 1);
        // The presenter blits the output image into the drawable, so the layer cannot be
        // framebuffer-only (Metal would reject a blit destination).
        SendVoidBool(layer, SetFramebufferOnlySelector, 0);
        return layer;
    }

    /// <summary>Creates a click-through view backed by the supplied layer.</summary>
    /// <param name="layer">The <c>CAMetalLayer*</c>.</param>
    /// <returns>The <c>NSView*</c>.</returns>
    internal static nint CreateMetalView(nint layer)
    {
        nint viewClass = MetalHostViewClass != 0 ? MetalHostViewClass : NsViewClass;
        nint view = SendPtrRect(
            SendPtr(viewClass, AllocSelector),
            NsViewInitSelector,
            new CGRect(0, 0, 1, 1));
        if (view == 0)
        {
            throw new InvalidOperationException("NSView initialization failed.");
        }

        SendVoidBool(view, SetWantsLayerSelector, 1);
        SendVoidPtr(view, SetLayerSelector, layer);
        return view;
    }

    /// <summary>Sets the view frame in points.</summary>
    internal static void SetFrame(nint view, double x, double y, double width, double height)
    {
        SendVoidRect(view, SetFrameSelector, new CGRect(x, y, width, height));
    }

    /// <summary>Reads the bounds height of a view in points.</summary>
    internal static double GetBoundsHeight(nint view)
    {
        CGRect bounds = SendRect(view, BoundsSelector);
        return bounds.Size.Height;
    }

    /// <summary>Gets whether the view uses a top-left coordinate origin.</summary>
    /// <param name="view">The <c>NSView*</c>.</param>
    /// <returns><see langword="true"/> for a flipped view.</returns>
    internal static bool GetIsFlipped(nint view)
    {
        return SendBool(view, IsFlippedSelector) != 0;
    }

    /// <summary>Sizes the drawable in pixels and sets the layer contents scale.</summary>
    internal static void SetDrawable(nint layer, double width, double height, double scale)
    {
        SendVoidDouble(layer, SetContentsScaleSelector, scale);
        SendVoidSize(layer, SetDrawableSizeSelector, new CGSize(width * scale, height * scale));
    }

    /// <summary>Adds (or raises) the view above its siblings.</summary>
    internal static void AddSubview(nint contentView, nint view)
    {
        SendVoidPtrIntPtr(contentView, AddSubviewSelector, view, NsWindowAbove, 0);
    }

    /// <summary>Removes the view from its superview.</summary>
    internal static void RemoveFromSuperview(nint view)
    {
        SendVoid(view, RemoveFromSuperviewSelector);
    }

    /// <summary>Shows or hides the view.</summary>
    internal static void SetHidden(nint view, bool hidden)
    {
        SendVoidBool(view, SetHiddenSelector, hidden ? (byte)1 : (byte)0);
    }

    /// <summary>Releases an Objective-C object.</summary>
    internal static void Release(nint obj)
    {
        if (obj != 0)
        {
            SendVoid(obj, ReleaseSelector);
        }
    }

    private static nint CreateAutoreleasedString(string value)
    {
        nint utf8 = Marshal.StringToHGlobalAnsi(value);
        try
        {
            return SendPtrPtr(NsStringClass, NsStringSelector, utf8);
        }
        finally
        {
            Marshal.FreeHGlobal(utf8);
        }
    }

    private static bool RespondsTo(nint obj, nint selector)
    {
        return obj != 0 && SendBoolPtr(obj, RespondsToSelectorSelector, selector) != 0;
    }

    private static nint CreateMetalHostViewClass()
    {
        if (NsViewClass == 0)
        {
            return 0;
        }

        nint cls = ObjCAllocateClassPair(NsViewClass, "FiveDoMetalHostView", 0);
        if (cls == 0)
        {
            // The class already exists (a second surface in the same process) or the
            // runtime rejected it; a plain NSView still presents, it only swallows clicks.
            return ObjCGetClass("FiveDoMetalHostView");
        }

        _ = ClassAddMethod(
            cls,
            HitTestSelector,
            Marshal.GetFunctionPointerForDelegate(HitTestCallback),
            "@@:{CGPoint=dd}");
        _ = ClassAddMethod(
            cls,
            Sel("acceptsFirstResponder"),
            Marshal.GetFunctionPointerForDelegate(AcceptsFirstResponderCallback),
            "c@:");
        ObjCRegisterClassPair(cls);
        return cls;
    }

    private static nint HitTest(nint self, nint selector, CGPoint point)
    {
        _ = self;
        _ = selector;
        _ = point;
        return 0;
    }

    private static byte AcceptsFirstResponder(nint self, nint selector)
    {
        _ = self;
        _ = selector;
        return 0;
    }

    private static T GetMsgSend<T>()
        where T : Delegate
    {
        return Marshal.GetDelegateForFunctionPointer<T>(MsgSendAddress);
    }

    private static nint LoadMsgSendAddress()
    {
        nint library = NativeLibrary.Load(ObjC);
        return NativeLibrary.GetExport(library, "objc_msgSend");
    }

    private static nint LoadQuartzCoreClass(string name)
    {
        _ = NativeLibrary.TryLoad(QuartzCore, out _);
        return ObjCGetClass(name);
    }

    private static nint LoadFramework(string name)
    {
        _ = NativeLibrary.TryLoad($"/System/Library/Frameworks/{name}.framework/{name}", out nint handle);
        return handle;
    }

    private static nint ObjCGetClass(string name)
    {
        if (!_frameworksLoaded)
        {
            _frameworksLoaded = true;
            // AppKit registers NSView/NSWindow/NSApplication; Foundation provides
            // NSString/NSDate. Class lookup fails until the images are loaded.
            _ = LoadFramework("AppKit");
            _ = LoadFramework("Foundation");
        }

        return ObjCGetClassNative(name);
    }

    [DllImport(ObjC, EntryPoint = "objc_getClass")]
    private static extern nint ObjCGetClassNative(string name);

    [DllImport(ObjC, EntryPoint = "sel_registerName")]
    private static extern nint Sel(string name);

    [DllImport(ObjC, EntryPoint = "objc_allocateClassPair")]
    private static extern nint ObjCAllocateClassPair(nint superclass, string name, nint extraBytes);

    [DllImport(ObjC, EntryPoint = "objc_registerClassPair")]
    private static extern void ObjCRegisterClassPair(nint cls);

    [DllImport(ObjC, EntryPoint = "class_addMethod")]
    private static extern bool ClassAddMethod(nint cls, nint name, nint implementation, string types);

    [DllImport(Metal)]
    private static extern nint MTLCreateSystemDefaultDevice();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint MsgSendPtr(nint receiver, nint selector);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MsgSendVoid(nint receiver, nint selector);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint MsgSendPtrPtr(nint receiver, nint selector, nint arg1);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MsgSendVoidBool(nint receiver, nint selector, byte arg1);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MsgSendVoidPtr(nint receiver, nint selector, nint arg1);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MsgSendVoidDouble(nint receiver, nint selector, double arg1);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MsgSendVoidUInt(nint receiver, nint selector, uint arg1);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MsgSendVoidSize(nint receiver, nint selector, CGSize arg1);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate CGRect MsgSendRect(nint receiver, nint selector);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte MsgSendBool(nint receiver, nint selector);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MsgSendVoidRect(nint receiver, nint selector, CGRect arg1);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint MsgSendPtrRect(nint receiver, nint selector, CGRect arg1);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint MsgSendPtrRectUIntUIntBool(
        nint receiver,
        nint selector,
        CGRect arg1,
        nuint arg2,
        nuint arg3,
        byte arg4);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MsgSendVoidPtrIntPtr(nint receiver, nint selector, nint arg1, nint arg2, nint arg3);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint MsgSendPtrNIntPtrPtrBool(
        nint receiver,
        nint selector,
        nint arg1,
        nint arg2,
        nint arg3,
        byte arg4);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte MsgSendBoolPtr(nint receiver, nint selector, nint arg1);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int MsgSendIntPtr(nint receiver, nint selector, nint arg1);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint HitTestDelegate(nint self, nint selector, CGPoint point);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte AcceptsFirstResponderDelegate(nint self, nint selector);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct CGPoint(double x, double y)
    {
        internal readonly double X = x;
        internal readonly double Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct CGSize(double width, double height)
    {
        internal readonly double Width = width;
        internal readonly double Height = height;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct CGRect(double x, double y, double width, double height)
    {
        internal readonly CGPoint Origin = new(x, y);
        internal readonly CGSize Size = new(width, height);
    }
}



