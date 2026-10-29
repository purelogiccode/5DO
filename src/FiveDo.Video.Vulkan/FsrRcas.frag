#version 450

layout(location = 0) out vec4 fragColor;
layout(set = 0, binding = 0) uniform sampler2D uFsr;

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

vec3 load(ivec2 p)
{
    return texelFetch(uFsr, clamp(p, ivec2(0), pc.sourceSize - 1), 0).rgb;
}

void main()
{
    ivec2 ip = ivec2(gl_FragCoord.xy) - ivec2(pc.rect.xy);
    vec3 b = load(ip + ivec2( 0, -1));
    vec3 d = load(ip + ivec2(-1,  0));
    vec3 e = load(ip);
    vec3 f = load(ip + ivec2( 1,  0));
    vec3 h = load(ip + ivec2( 0,  1));

    float bL = (b.r * 0.5) + b.g + (b.b * 0.5);
    float dL = (d.r * 0.5) + d.g + (d.b * 0.5);
    float eL = (e.r * 0.5) + e.g + (e.b * 0.5);
    float fL = (f.r * 0.5) + f.g + (f.b * 0.5);
    float hL = (h.r * 0.5) + h.g + (h.b * 0.5);

    float mn4R = min(min(b.r, d.r), min(f.r, h.r));
    float mn4G = min(min(b.g, d.g), min(f.g, h.g));
    float mn4B = min(min(b.b, d.b), min(f.b, h.b));
    float mx4R = max(max(b.r, d.r), max(f.r, h.r));
    float mx4G = max(max(b.g, d.g), max(f.g, h.g));
    float mx4B = max(max(b.b, d.b), max(f.b, h.b));

    float hitMinR = min(mn4R, e.r) / max(4.0 * mx4R, 1e-6);
    float hitMinG = min(mn4G, e.g) / max(4.0 * mx4G, 1e-6);
    float hitMinB = min(mn4B, e.b) / max(4.0 * mx4B, 1e-6);
    float hitMaxR = (1.0 - max(mx4R, e.r)) / max((4.0 * mn4R) - 4.0, 1e-6);
    float hitMaxG = (1.0 - max(mx4G, e.g)) / max((4.0 * mn4G) - 4.0, 1e-6);
    float hitMaxB = (1.0 - max(mx4B, e.b)) / max((4.0 * mn4B) - 4.0, 1e-6);

    float lobeR = max(-hitMinR, hitMaxR);
    float lobeG = max(-hitMinG, hitMaxG);
    float lobeB = max(-hitMinB, hitMaxB);
    float sharpness = exp2(-pc.sharpness);
    float lobe = max(-0.1875, min(max(max(lobeR, lobeG), lobeB), 0.0)) * sharpness;

    float rcpL = 1.0 / max((4.0 * lobe) + 1.0, 1e-6);
    vec3 color = ((b + d + f + h) * lobe + e) * rcpL;
    fragColor = vec4(clamp(color, vec3(0.0), vec3(1.0)), 1.0);
}
