using System;
using FiveDo.Video.OpenGL;

namespace FiveDo.Tests.Video;

public sealed class ShaderSourceTests
{
    [Theory]
    [InlineData(false, "#version 330 core")]
    [InlineData(true, "#version 300 es")]
    public void XbrShader_UsesTheContextVersion(bool es, string header)
    {
        var source = Shaders.FragmentXbr(es);

        Assert.StartsWith(header, source, StringComparison.Ordinal);
        Assert.Contains("uniform sampler2D uPalette", source, StringComparison.Ordinal);
        Assert.Contains("uniform ivec2 uSource", source, StringComparison.Ordinal);
        Assert.Contains("uniform ivec2 uXbrOutput", source, StringComparison.Ordinal);
        Assert.Contains("vec4 irlv0", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mat4x3", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void XbrShader_HasBalancedBracesAndParentheses(bool es)
    {
        var source = Shaders.FragmentXbr(es);

        Assert.Equal(Count(source, '{'), Count(source, '}'));
        Assert.Equal(Count(source, '('), Count(source, ')'));
    }

    [Fact]
    public void ScaleShader_HandlesEveryFilterBranch()
    {
        var source = Shaders.FragmentScale(es: false);

        Assert.Contains("uFilter == 1", source, StringComparison.Ordinal);
        Assert.Contains("uFilter == 2", source, StringComparison.Ordinal);
        Assert.Contains("uScanline", source, StringComparison.Ordinal);
        Assert.Contains("uBloom", source, StringComparison.Ordinal);
        Assert.Contains("uBloomThreshold", source, StringComparison.Ordinal);
        Assert.Contains("max(sampleColor - vec3(uBloomThreshold)", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FsrShaders_UseTheContextVersionAndBothPasses(bool es)
    {
        var easu = Shaders.FragmentFsrEasu(es);
        var rcas = Shaders.FragmentFsrRcas(es);

        Assert.StartsWith(es ? "#version 300 es" : "#version 330 core", easu, StringComparison.Ordinal);
        Assert.StartsWith(es ? "#version 300 es" : "#version 330 core", rcas, StringComparison.Ordinal);
        Assert.Contains("uniform sampler2D uExpanded", easu, StringComparison.Ordinal);
        Assert.Contains("accumulateDirection", easu, StringComparison.Ordinal);
        Assert.Contains("accumulateTap", easu, StringComparison.Ordinal);
        Assert.Contains("uniform sampler2D uFsr", rcas, StringComparison.Ordinal);
        Assert.Contains("uSharpness", rcas, StringComparison.Ordinal);
        Assert.Contains("0.1875", rcas, StringComparison.Ordinal);
        Assert.Equal(Count(easu, '{'), Count(easu, '}'));
        Assert.Equal(Count(easu, '('), Count(easu, ')'));
        Assert.Equal(Count(rcas, '{'), Count(rcas, '}'));
        Assert.Equal(Count(rcas, '('), Count(rcas, ')'));
    }

    [Fact]
    public void PaletteShader_KeepsBackgroundFixedClutAndPerLineClutPaths()
    {
        var source = Shaders.FragmentPalette(es: false);

        Assert.Contains("pixel == 0u", source, StringComparison.Ordinal);
        Assert.Contains("0x02000000u", source, StringComparison.Ordinal);
        Assert.Contains("texelFetch(uClut", source, StringComparison.Ordinal);
        Assert.Contains("indexR", source, StringComparison.Ordinal);
        Assert.Contains("indexG", source, StringComparison.Ordinal);
        Assert.Contains("indexB", source, StringComparison.Ordinal);
    }

    private static int Count(string text, char value)
    {
        var count = 0;
        foreach (var character in text)
        {
            if (character == value)
            {
                count++;
            }
        }

        return count;
    }
}
