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

float pickWeight(vec4 weights, int index)
{
    if (index == 0) { return weights.x; }
    if (index == 1) { return weights.y; }
    if (index == 2) { return weights.z; }
    return weights.w;
}

void main()
{
    vec2 uv = (gl_FragCoord.xy - pc.rect.xy) / pc.rect.zw;
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
    {
        fragColor = vec4(0.0, 0.0, 0.0, 1.0);
        return;
    }

    if (pc.filterMode == 3)
    {
        vec2 coord = (uv * vec2(pc.sourceSize)) - vec2(0.5);
        vec2 base = floor(coord);
        vec2 fraction = coord - base;
        float f2x = fraction.x * fraction.x;
        float f3x = f2x * fraction.x;
        float f2y = fraction.y * fraction.y;
        float f3y = f2y * fraction.y;
        vec4 wx = vec4(
            (-0.5 * f3x) + f2x - (0.5 * fraction.x),
            (1.5 * f3x) - (2.5 * f2x) + 1.0,
            (-1.5 * f3x) + (2.0 * f2x) + (0.5 * fraction.x),
            (0.5 * f3x) - (0.5 * f2x));
        vec4 wy = vec4(
            (-0.5 * f3y) + f2y - (0.5 * fraction.y),
            (1.5 * f3y) - (2.5 * f2y) + 1.0,
            (-1.5 * f3y) + (2.0 * f2y) + (0.5 * fraction.y),
            (0.5 * f3y) - (0.5 * f2y));
        vec3 sum = vec3(0.0);
        for (int y = -1; y <= 2; y++)
        {
            for (int x = -1; x <= 2; x++)
            {
                ivec2 tap = clamp(
                    ivec2(base) + ivec2(x, y),
                    ivec2(0),
                    pc.sourceSize - 1);
                sum += texelFetch(uExpanded, tap, 0).rgb
                    * pickWeight(wx, x + 1) * pickWeight(wy, y + 1);
            }
        }

        fragColor = vec4(clamp(sum, 0.0, 1.0), 1.0);
        return;
    }

    if (pc.filterMode == 0)
    {
        ivec2 source = clamp(
            ivec2(floor(uv * vec2(pc.sourceSize))),
            ivec2(0),
            pc.sourceSize - 1);
        fragColor = vec4(texelFetch(uExpanded, source, 0).rgb, 1.0);
        return;
    }

    if (pc.filterMode == 1)
    {
        vec2 texel = uv * vec2(pc.sourceSize);
        vec2 texelFloored = floor(texel);
        vec2 texelFraction = fract(texel);
        vec2 scale = max(floor(pc.rect.zw / vec2(pc.sourceSize)), vec2(1.0));
        vec2 regionRange = vec2(0.5) - (vec2(0.5) / scale);
        vec2 centerDistance = texelFraction - vec2(0.5);
        vec2 blended = (centerDistance - clamp(centerDistance, -regionRange, regionRange))
            * scale + vec2(0.5);
        uv = (texelFloored + blended) / vec2(pc.sourceSize);
    }

    if (pc.filterMode == 2)
    {
        vec2 centered = uv * 2.0 - 1.0;
        centered *= 1.0 + (0.055 * dot(centered, centered));
        uv = centered * 0.5 + 0.5;
        if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
        {
            fragColor = vec4(0.0, 0.0, 0.0, 1.0);
            return;
        }
    }

    vec3 color = texture(uExpanded, clamp(uv, vec2(0.0), vec2(1.0))).rgb;
    if (pc.filterMode == 2)
    {
        float line = mod(floor(gl_FragCoord.y), 2.0);
        float aperture = mod(floor(gl_FragCoord.x), 3.0);
        vec3 mask = aperture < 1.0
            ? vec3(1.0, 0.82, 0.82)
            : aperture < 2.0
                ? vec3(0.82, 1.0, 0.82)
                : vec3(0.82, 0.82, 1.0);
        color *= mix(vec3(1.0), mask, pc.scanline * 0.5);
        color *= 1.0 - (pc.scanline * line);
        vec2 centered = uv * 2.0 - 1.0;
        color *= clamp(1.0 - (0.12 * dot(centered, centered)), 0.0, 1.0);

        if (pc.bloom > 0.0)
        {
            vec2 texel = 2.0 / vec2(pc.sourceSize);
            vec3 bloomColor = vec3(0.0);
            float weightSum = 0.0;
            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    float weight = (x == 0 ? 2.0 : 1.0) * (y == 0 ? 2.0 : 1.0);
                    vec2 offset = vec2(float(x), float(y)) * texel;
                    vec3 sampleColor = texture(
                        uExpanded,
                        clamp(uv + offset, vec2(0.0), vec2(1.0))).rgb;
                    bloomColor += max(sampleColor - vec3(pc.bloomThreshold), vec3(0.0)) * weight;
                    weightSum += weight;
                }
            }

            color += (bloomColor / weightSum) * pc.bloom;
        }
    }

    fragColor = vec4(color, 1.0);
}
