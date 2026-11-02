using System;
using FiveDo.Video.Direct3D;

namespace FiveDo.Tests.Video;

public sealed class Direct3D11ShaderTests
{
    [Fact]
    public void AllShaders_CompileForShaderModel5()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string[] names = Direct3D11Renderer.CompileAllShaders();
        Assert.Equal(
            ["Frame.vert", "Palette.frag", "Scale.frag", "Xbr.frag", "FsrEasu.frag", "FsrRcas.frag"],
            names);
    }
}
