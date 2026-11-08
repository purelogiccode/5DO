#version 450

layout(location = 0) out vec4 fragColor;
layout(set = 0, binding = 0) uniform usampler2D uPixels;
layout(set = 0, binding = 1) uniform sampler2D uClut;
layout(set = 0, binding = 2) uniform usampler2D uParams;

layout(push_constant) uniform PushConstants
{
    vec4 rect;
    vec2 outputSize;
    ivec2 sourceSize;
    int filterMode;
    float scanline;
    float bloom;
    float bloomThreshold;
    float sharpness;
} pc;

uint expand5(uint value)
{
    return (value << 3u) | (value >> 2u);
}

vec3 fixedClut(uint pixel)
{
    uint r = (pixel >> 10u) & 0x1fu;
    uint g = (pixel >> 5u) & 0x1fu;
    uint b = pixel & 0x1fu;
    return vec3(float(expand5(r)), float(expand5(g)), float(expand5(b))) / 255.0;
}

vec3 backgroundColor(uint background)
{
    float r = float((background >> 16u) & 0xffu) / 255.0;
    float g = float((background >> 8u) & 0xffu) / 255.0;
    float b = float(background & 0xffu) / 255.0;
    return vec3(r, g, b);
}

void main()
{
    ivec2 coordinate = clamp(ivec2(gl_FragCoord.xy), ivec2(0), pc.sourceSize - 1);
    uint pixel = texelFetch(uPixels, coordinate, 0).r;
    uvec2 parameters = texelFetch(uParams, ivec2(0, coordinate.y), 0).rg;
    uint background = parameters.x;
    uint outcontrol = parameters.y;

    vec3 color;
    if (pixel == 0u)
    {
        color = backgroundColor(background);
    }
    else if ((outcontrol & 0x02000000u) != 0u && (pixel & 0x8000u) != 0u)
    {
        color = fixedClut(pixel);
    }
    else
    {
        uint indexR = (pixel >> 10u) & 0x1fu;
        uint indexG = (pixel >> 5u) & 0x1fu;
        uint indexB = pixel & 0x1fu;
        color = vec3(
            texelFetch(uClut, ivec2(int(indexR), coordinate.y), 0).r,
            texelFetch(uClut, ivec2(int(indexG), coordinate.y), 0).g,
            texelFetch(uClut, ivec2(int(indexB), coordinate.y), 0).b);
    }

    fragColor = vec4(color, 1.0);
}
