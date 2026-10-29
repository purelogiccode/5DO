#version 450

layout(location = 0) out vec4 fragColor;
layout(set = 0, binding = 0) uniform sampler2D uExpanded;

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

vec3 tap(ivec2 p)
{
    return texelFetch(uExpanded, clamp(p, ivec2(0), pc.sourceSize - 1), 0).rgb;
}

float luminance(vec3 c)
{
    return (c.r * 0.5) + c.g + (c.b * 0.5);
}

void accumulateTap(
    inout vec3 color,
    inout float weight,
    vec2 offset,
    vec2 direction,
    vec2 length,
    float lobe,
    float clip,
    vec3 tapColor)
{
    vec2 v = vec2(
        (offset.x * direction.x) + (offset.y * direction.y),
        (offset.x * (-direction.y)) + (offset.y * direction.x));
    v *= length;
    float d2 = min(dot(v, v), clip);
    float wB = (2.0 / 5.0) * d2 - 1.0;
    float wA = lobe * d2 - 1.0;
    wB *= wB;
    wA *= wA;
    wB = (25.0 / 16.0) * wB - ((25.0 / 16.0) - 1.0);
    float w = wB * wA;
    color += tapColor * w;
    weight += w;
}

void accumulateDirection(
    inout vec2 direction,
    inout float length,
    vec2 pp,
    float w,
    float lA,
    float lB,
    float lC,
    float lD,
    float lE)
{
    float dc = lD - lC;
    float cb = lC - lB;
    float lenX = max(abs(dc), abs(cb));
    lenX = 1.0 / max(lenX, 1e-6);
    float dirX = lD - lB;
    direction.x += dirX * w;
    lenX = clamp(abs(dirX) * lenX, 0.0, 1.0);
    lenX *= lenX;
    length += lenX * w;

    float ec = lE - lC;
    float ca = lC - lA;
    float lenY = max(abs(ec), abs(ca));
    lenY = 1.0 / max(lenY, 1e-6);
    float dirY = lE - lA;
    direction.y += dirY * w;
    lenY = clamp(abs(dirY) * lenY, 0.0, 1.0);
    lenY *= lenY;
    length += lenY * w;
}

void main()
{
    // Vulkan framebuffer coordinates are top-down, matching the top-down expanded
    // source texture; no vertical flip is required here.
    ivec2 ip = ivec2(int(gl_FragCoord.x), int(gl_FragCoord.y));
    vec2 pp = (vec2(ip) * vec2(pc.sourceSize) / vec2(pc.outputSize))
        + (vec2(0.5) * vec2(pc.sourceSize) / vec2(pc.outputSize) - vec2(0.5));
    vec2 fp = floor(pp);
    pp -= fp;
    ivec2 f = ivec2(fp);

    vec3 b = tap(f + ivec2( 0, -1));
    vec3 c = tap(f + ivec2( 1, -1));
    vec3 e = tap(f + ivec2(-1,  0));
    vec3 f0 = tap(f + ivec2( 0,  0));
    vec3 g = tap(f + ivec2( 1,  0));
    vec3 h = tap(f + ivec2( 2,  0));
    vec3 i = tap(f + ivec2(-1,  1));
    vec3 j = tap(f + ivec2( 0,  1));
    vec3 k = tap(f + ivec2( 1,  1));
    vec3 l = tap(f + ivec2( 2,  1));
    vec3 n = tap(f + ivec2( 0,  2));
    vec3 o = tap(f + ivec2( 1,  2));

    float bL = luminance(b);
    float cL = luminance(c);
    float eL = luminance(e);
    float fL = luminance(f0);
    float gL = luminance(g);
    float hL = luminance(h);
    float iL = luminance(i);
    float jL = luminance(j);
    float kL = luminance(k);
    float lL = luminance(l);
    float nL = luminance(n);
    float oL = luminance(o);

    vec2 direction = vec2(0.0);
    float length = 0.0;
    accumulateDirection(direction, length, pp, (1.0 - pp.x) * (1.0 - pp.y), bL, eL, fL, gL, jL);
    accumulateDirection(direction, length, pp, pp.x * (1.0 - pp.y), cL, fL, gL, hL, kL);
    accumulateDirection(direction, length, pp, (1.0 - pp.x) * pp.y, fL, iL, jL, kL, nL);
    accumulateDirection(direction, length, pp, pp.x * pp.y, gL, jL, kL, lL, oL);

    vec2 dir2 = direction * direction;
    float dirR = dir2.x + dir2.y;
    if (dirR < (1.0 / 32768.0))
    {
        direction = vec2(1.0, 0.0);
    }
    else
    {
        direction *= inversesqrt(dirR);
    }

    length *= 0.5;
    length *= length;
    float stretch = dot(direction, direction) / max(abs(direction.x), abs(direction.y));
    vec2 length2 = vec2(1.0 + (stretch - 1.0) * length, 1.0 - 0.5 * length);
    float lobe = 0.5 + (((1.0 / 4.0) - 0.04) - 0.5) * length;
    float clip = 1.0 / max(lobe, 1e-6);

    vec3 min4 = min(min(f0, k), min(j, g));
    vec3 max4 = max(max(f0, k), max(j, g));

    vec3 color = vec3(0.0);
    float weight = 0.0;
    accumulateTap(color, weight, vec2( 0.0, -1.0) - pp, direction, length2, lobe, clip, b);
    accumulateTap(color, weight, vec2( 1.0, -1.0) - pp, direction, length2, lobe, clip, c);
    accumulateTap(color, weight, vec2(-1.0,  1.0) - pp, direction, length2, lobe, clip, i);
    accumulateTap(color, weight, vec2( 0.0,  1.0) - pp, direction, length2, lobe, clip, j);
    accumulateTap(color, weight, vec2( 0.0,  0.0) - pp, direction, length2, lobe, clip, f0);
    accumulateTap(color, weight, vec2(-1.0,  0.0) - pp, direction, length2, lobe, clip, e);
    accumulateTap(color, weight, vec2( 1.0,  1.0) - pp, direction, length2, lobe, clip, k);
    accumulateTap(color, weight, vec2( 2.0,  1.0) - pp, direction, length2, lobe, clip, l);
    accumulateTap(color, weight, vec2( 2.0,  0.0) - pp, direction, length2, lobe, clip, h);
    accumulateTap(color, weight, vec2( 1.0,  0.0) - pp, direction, length2, lobe, clip, g);
    accumulateTap(color, weight, vec2( 1.0,  2.0) - pp, direction, length2, lobe, clip, o);
    accumulateTap(color, weight, vec2( 0.0,  2.0) - pp, direction, length2, lobe, clip, n);

    fragColor = vec4(min(max4, max(min4, color / max(weight, 1e-6))), 1.0);
}
