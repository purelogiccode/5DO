#version 450

layout(location = 0) out vec4 fragColor;
layout(set = 0, binding = 0) uniform sampler2D uExpanded;
layout(set = 0, binding = 1) uniform usampler2D uRules;

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

uvec3 fetchColor(ivec2 p)
{
    vec3 color = texelFetch(uExpanded, clamp(p, ivec2(0), pc.sourceSize - 1), 0).rgb;
    return uvec3(trunc((color * 255.0) + 0.5));
}

ivec3 toYuv(uvec3 color)
{
    int r = int(color.r);
    int g = int(color.g);
    int b = int(color.b);
    int y = int(trunc((0.299 * float(r)) + (0.587 * float(g)) + (0.114 * float(b))));
    int u = ((int(trunc((-0.169 * float(r)) - (0.331 * float(g)) + (0.5 * float(b)))) + 128) & 255);
    int v = ((int(trunc((0.5 * float(r)) - (0.419 * float(g)) - (0.081 * float(b)))) + 128) & 255);
    return ivec3(y, u, v);
}

bool yuvDiff(ivec3 a, ivec3 b)
{
    return abs(a.x - b.x) > 48
        || abs(a.y - b.y) > 7
        || abs(a.z - b.z) > 6;
}

uvec3 pickColor(
    uvec3 c0, uvec3 c1, uvec3 c2,
    uvec3 c3, uvec3 c4, uvec3 c5,
    uvec3 c6, uvec3 c7, uvec3 c8,
    int index)
{
    if (index == 0) { return c0; }
    if (index == 1) { return c1; }
    if (index == 2) { return c2; }
    if (index == 3) { return c3; }
    if (index == 4) { return c4; }
    if (index == 5) { return c5; }
    if (index == 6) { return c6; }
    if (index == 7) { return c7; }
    return c8;
}

void main()
{
    ivec2 outCoord = ivec2(gl_FragCoord.xy);
    int scale = pc.filterMode;
    ivec2 sourceCoord = outCoord / scale;
    int subpixel = ((outCoord.y % scale) * scale) + (outCoord.x % scale);

    uvec3 c0 = fetchColor(sourceCoord + ivec2(-1, -1));
    uvec3 c1 = fetchColor(sourceCoord + ivec2( 0, -1));
    uvec3 c2 = fetchColor(sourceCoord + ivec2( 1, -1));
    uvec3 c3 = fetchColor(sourceCoord + ivec2(-1,  0));
    uvec3 c4 = fetchColor(sourceCoord);
    uvec3 c5 = fetchColor(sourceCoord + ivec2( 1,  0));
    uvec3 c6 = fetchColor(sourceCoord + ivec2(-1,  1));
    uvec3 c7 = fetchColor(sourceCoord + ivec2( 0,  1));
    uvec3 c8 = fetchColor(sourceCoord + ivec2( 1,  1));

    ivec3 y0 = toYuv(c0);
    ivec3 y1 = toYuv(c1);
    ivec3 y2 = toYuv(c2);
    ivec3 y3 = toYuv(c3);
    ivec3 y4 = toYuv(c4);
    ivec3 y5 = toYuv(c5);
    ivec3 y6 = toYuv(c6);
    ivec3 y7 = toYuv(c7);
    ivec3 y8 = toYuv(c8);

    uint pattern = 0u;
    if (any(notEqual(c0, c4)) && yuvDiff(y0, y4)) { pattern |= 1u; }
    if (any(notEqual(c1, c4)) && yuvDiff(y1, y4)) { pattern |= 2u; }
    if (any(notEqual(c2, c4)) && yuvDiff(y2, y4)) { pattern |= 4u; }
    if (any(notEqual(c3, c4)) && yuvDiff(y3, y4)) { pattern |= 8u; }
    if (any(notEqual(c5, c4)) && yuvDiff(y5, y4)) { pattern |= 16u; }
    if (any(notEqual(c6, c4)) && yuvDiff(y6, y4)) { pattern |= 32u; }
    if (any(notEqual(c7, c4)) && yuvDiff(y7, y4)) { pattern |= 64u; }
    if (any(notEqual(c8, c4)) && yuvDiff(y8, y4)) { pattern |= 128u; }

    bool condition0 = yuvDiff(y1, y3);  // w2, w4
    bool condition1 = yuvDiff(y1, y5);  // w2, w6
    bool condition2 = yuvDiff(y3, y7);  // w4, w8
    bool condition3 = yuvDiff(y5, y7);  // w6, w8

    int rowOffset = scale == 2 ? 0 : (scale == 3 ? 4 : 13);
    uvec2 pair = texelFetch(uRules, ivec2(int(pattern), rowOffset + subpixel), 0).xy;
    uint rule = pair.x;
    uint condition = (rule >> 27u) & 7u;
    if (condition != 7u)
    {
        bool selected = condition == 0u
            ? condition0
            : (condition == 1u ? condition1 : (condition == 2u ? condition2 : condition3));
        if (selected)
        {
            rule = pair.y;
        }
    }

    uint weight0 = rule & 15u;
    uint index0 = (rule >> 4u) & 15u;
    uint weight1 = (rule >> 8u) & 15u;
    uint index1 = (rule >> 12u) & 15u;
    uint weight2 = (rule >> 16u) & 15u;
    uint index2 = (rule >> 20u) & 15u;
    uint shift = (rule >> 24u) & 7u;

    uvec3 sum = pickColor(c0, c1, c2, c3, c4, c5, c6, c7, c8, int(index0)) * weight0
        + pickColor(c0, c1, c2, c3, c4, c5, c6, c7, c8, int(index1)) * weight1
        + pickColor(c0, c1, c2, c3, c4, c5, c6, c7, c8, int(index2)) * weight2;
    uvec3 rgb = sum >> shift;
    fragColor = vec4(vec3(rgb) / 255.0, 1.0);
}
