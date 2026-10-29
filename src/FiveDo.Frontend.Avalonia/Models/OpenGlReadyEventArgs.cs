using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using FiveDo.Core;
using FiveDo.Video;
using FiveDo.Video.OpenGL;
using Silk.NET.OpenGL;

namespace FiveDo.Frontend.Avalonia.Controls;


/// <summary>Event data for <see cref="VideoSurface.OpenGlReady"/>.</summary>
public sealed class OpenGlReadyEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="OpenGlReadyEventArgs"/> class.</summary>
    /// <param name="capabilities">Capabilities reported by the active renderer.</param>
    public OpenGlReadyEventArgs(RendererCapabilities capabilities)
    {
        Capabilities = capabilities;
    }

    /// <summary>Gets the capabilities reported by the active renderer.</summary>
    public RendererCapabilities Capabilities { get; }
}
