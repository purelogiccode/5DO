#version 450

layout(location = 0) out vec4 fragColor;

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
    vec4 voidColor;
    vec4 voidAccent;
    int pattern;
    float patternScale;
    float patternTile;
} pc;

vec3 piecewise3(float t, vec3 a, vec3 b, vec3 c, float split)
{
    return t < split ? mix(a, b, t / split) : mix(b, c, (t - split) / (1.0 - split));
}

vec3 patternColor(vec2 pixel)
{
    vec2 logical = pixel / max(pc.patternScale, 0.001);
    vec2 p = mod(logical, max(pc.patternTile, 1.0));
    vec3 color = pc.voidColor.rgb;

    if (pc.pattern == 1)
    {
        float cell = mod(floor(p.x / 8.0) + floor(p.y / 8.0), 2.0);
        if (cell >= 1.0)
        {
            color = pc.voidAccent.rgb;
        }
    }
    else if (pc.pattern == 2)
    {
        if (p.x < 1.0 || p.y < 1.0)
        {
            color = pc.voidAccent.rgb;
        }
    }
    else if (pc.pattern == 3)
    {
        if (mod(floor(p.y), 4.0) < 1.0)
        {
            color = pc.voidAccent.rgb;
        }
    }
    else if (pc.pattern == 4)
    {
        float t = clamp((p.x + p.y) / (2.0 * pc.patternTile), 0.0, 1.0);
        vec3 gradient = piecewise3(
            t,
            vec3(0.0, 0.0, 0.0),
            vec3(0.227, 0.431, 0.647),
            vec3(0.106, 0.227, 0.361),
            0.55);
        float alpha = t < 0.55
            ? mix(0.0, 96.0 / 255.0, t / 0.55)
            : mix(96.0 / 255.0, 160.0 / 255.0, (t - 0.55) / 0.45);
        color = mix(color, gradient, alpha);
    }
    else if (pc.pattern == 5)
    {
        bool inside = false;
        if (distance(p, vec2(4.0, 4.0)) <= 3.0) { inside = true; }
        if (distance(p, vec2(12.0, 4.0)) <= 3.0) { inside = true; }
        if (distance(p, vec2(4.0, 12.0)) <= 3.0) { inside = true; }
        if (distance(p, vec2(12.0, 12.0)) <= 3.0) { inside = true; }
        if (inside)
        {
            float t = clamp((p.x + p.y) / (2.0 * pc.patternTile), 0.0, 1.0);
            float alpha = t < 0.5
                ? mix(90.0 / 255.0, 24.0 / 255.0, t * 2.0)
                : mix(24.0 / 255.0, 0.0, (t - 0.5) * 2.0);
            color = mix(color, vec3(1.0), alpha);
        }
    }
    else if (pc.pattern == 6)
    {
        if (mod(floor(p.y), 2.0) < 1.0)
        {
            float t = clamp(p.y / pc.patternTile, 0.0, 1.0);
            vec3 streak;
            float alpha;
            if (t < 0.35)
            {
                streak = vec3(1.0);
                alpha = mix(70.0 / 255.0, 12.0 / 255.0, t / 0.35);
            }
            else if (t < 0.7)
            {
                float k = (t - 0.35) / 0.35;
                streak = mix(vec3(1.0), vec3(0.0), k);
                alpha = mix(12.0 / 255.0, 40.0 / 255.0, k);
            }
            else
            {
                streak = vec3(0.0);
                alpha = mix(40.0 / 255.0, 0.0, (t - 0.7) / 0.3);
            }

            color = mix(color, streak, alpha);
        }
    }

    return color;
}

void main()
{
    vec2 pixel = gl_FragCoord.xy;
    fragColor = vec4(patternColor(pixel), 1.0);
}
