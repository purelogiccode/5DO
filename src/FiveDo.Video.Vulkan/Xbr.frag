#version 450

layout(location = 0) out vec4 fragColor;
layout(set = 0, binding = 0) uniform sampler2D uPalette;

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

const vec3 rgbw = vec3(14.352, 28.176, 5.472);
const float eqThreshold = 15.0;
const float lv2Coefficient = 2.0;
const vec4 Ao = vec4( 1.0, -1.0, -1.0, 1.0);
const vec4 Bo = vec4( 1.0,  1.0, -1.0,-1.0);
const vec4 Co = vec4( 1.5,  0.5, -0.5, 0.5);
const vec4 Ax = vec4( 1.0, -1.0, -1.0, 1.0);
const vec4 Bx = vec4( 0.5,  2.0, -0.5,-2.0);
const vec4 Cx = vec4( 1.0,  1.0, -0.5, 0.0);
const vec4 Ay = vec4( 1.0, -1.0, -1.0, 1.0);
const vec4 By = vec4( 2.0,  0.5, -2.0,-0.5);
const vec4 Cy = vec4( 2.0,  0.0, -1.0, 0.5);
const vec4 Ci = vec4(0.25, 0.25, 0.25, 0.25);
const vec4 delta = vec4(0.25, 0.25, 0.25, 0.25);
const vec4 deltaL = vec4(0.125, 0.25, 0.125, 0.25);
const vec4 deltaU = vec4(0.25, 0.125, 0.25, 0.125);

vec4 df(vec4 a, vec4 b) { return abs(a - b); }
vec4 diff(vec4 a, vec4 b) { return vec4(notEqual(a, b)); }
vec4 eq(vec4 a, vec4 b) { return step(df(a, b), vec4(eqThreshold)); }
vec4 neq(vec4 a, vec4 b) { return vec4(1.0) - eq(a, b); }

vec4 weightedDistance(vec4 a, vec4 b, vec4 c, vec4 d, vec4 e, vec4 f, vec4 g, vec4 h)
{
    return df(a, b) + df(a, c) + df(d, e) + df(d, f) + 4.0 * df(g, h);
}

float colorDistance(vec3 a, vec3 b)
{
    vec3 d = abs(a - b);
    return d.r + d.g + d.b;
}

vec3 sampleFrame(vec2 uv)
{
    ivec2 coordinate = ivec2(floor(uv * vec2(pc.sourceSize)));
    return texelFetch(uPalette, clamp(coordinate, ivec2(0), pc.sourceSize - 1), 0).rgb;
}

void main()
{
    vec2 texCoord = gl_FragCoord.xy / vec2(pc.outputSize);
    vec2 ps = vec2(1.0) / vec2(pc.sourceSize);
    vec2 fp = fract(texCoord * vec2(pc.sourceSize));

    vec3 A1 = sampleFrame(texCoord + vec2(-ps.x, -2.0 * ps.y));
    vec3 B1 = sampleFrame(texCoord + vec2( 0.0,  -2.0 * ps.y));
    vec3 C1 = sampleFrame(texCoord + vec2( ps.x, -2.0 * ps.y));
    vec3 A  = sampleFrame(texCoord + vec2(-ps.x, -ps.y));
    vec3 B  = sampleFrame(texCoord + vec2( 0.0,  -ps.y));
    vec3 C  = sampleFrame(texCoord + vec2( ps.x, -ps.y));
    vec3 D  = sampleFrame(texCoord + vec2(-ps.x,  0.0));
    vec3 E  = sampleFrame(texCoord);
    vec3 F  = sampleFrame(texCoord + vec2( ps.x,  0.0));
    vec3 G  = sampleFrame(texCoord + vec2(-ps.x,  ps.y));
    vec3 H  = sampleFrame(texCoord + vec2( 0.0,   ps.y));
    vec3 I  = sampleFrame(texCoord + vec2( ps.x,  ps.y));
    vec3 G5 = sampleFrame(texCoord + vec2(-ps.x,  2.0 * ps.y));
    vec3 H5 = sampleFrame(texCoord + vec2( 0.0,   2.0 * ps.y));
    vec3 I5 = sampleFrame(texCoord + vec2( ps.x,  2.0 * ps.y));
    vec3 A0 = sampleFrame(texCoord + vec2(-2.0 * ps.x, -ps.y));
    vec3 D0 = sampleFrame(texCoord + vec2(-2.0 * ps.x,  0.0));
    vec3 G0 = sampleFrame(texCoord + vec2(-2.0 * ps.x,  ps.y));
    vec3 C4 = sampleFrame(texCoord + vec2( 2.0 * ps.x, -ps.y));
    vec3 F4 = sampleFrame(texCoord + vec2( 2.0 * ps.x,  0.0));
    vec3 I4 = sampleFrame(texCoord + vec2( 2.0 * ps.x,  ps.y));

    vec4 b = vec4(dot(B, rgbw), dot(D, rgbw), dot(H, rgbw), dot(F, rgbw));
    vec4 c = vec4(dot(C, rgbw), dot(A, rgbw), dot(G, rgbw), dot(I, rgbw));
    vec4 d = b.yzwx;
    vec4 e = vec4(dot(E, rgbw));
    vec4 f = b.wxyz;
    vec4 g = c.zwxy;
    vec4 h = b.zwxy;
    vec4 i = c.wxyz;
    vec4 i4 = vec4(dot(I4, rgbw), dot(C1, rgbw), dot(A0, rgbw), dot(G5, rgbw));
    vec4 i5 = vec4(dot(I5, rgbw), dot(C4, rgbw), dot(A1, rgbw), dot(G0, rgbw));
    vec4 h5 = vec4(dot(H5, rgbw), dot(F4, rgbw), dot(B1, rgbw), dot(D0, rgbw));
    vec4 f4 = h5.yzwx;

    vec4 fx = (Ao * fp.y) + (Bo * fp.x);
    vec4 fxL = (Ax * fp.y) + (Bx * fp.x);
    vec4 fxU = (Ay * fp.y) + (By * fp.x);

    vec4 irlv0 = diff(e, f) * diff(e, h);
    vec4 irlv1 = irlv0 * (
        neq(f, b) * neq(f, c)
        + neq(h, d) * neq(h, g)
        + eq(e, i) * (neq(f, f4) * neq(f, i4) + neq(h, h5) * neq(h, i5))
        + eq(e, g)
        + eq(e, c));
    vec4 irlv2l = diff(e, g) * diff(d, g);
    vec4 irlv2u = diff(e, c) * diff(b, c);

    vec4 fx45i = clamp((fx + delta - Co - Ci) / (2.0 * delta), 0.0, 1.0);
    vec4 fx45 = clamp((fx + delta - Co) / (2.0 * delta), 0.0, 1.0);
    vec4 fx30 = clamp((fxL + deltaL - Cx) / (2.0 * deltaL), 0.0, 1.0);
    vec4 fx60 = clamp((fxU + deltaU - Cy) / (2.0 * deltaU), 0.0, 1.0);

    vec4 wd1 = weightedDistance(e, c, g, i, h5, f4, h, f);
    vec4 wd2 = weightedDistance(h, d, i5, f, i4, b, e, i);

    vec4 edri = step(wd1, wd2) * irlv0;
    vec4 edr = step(wd1 + vec4(0.1), wd2) * step(vec4(0.5), irlv1);
    vec4 edrL = step(lv2Coefficient * df(f, g), df(h, c)) * irlv2l * edr;
    vec4 edrU = step(lv2Coefficient * df(h, c), df(f, g)) * irlv2u * edr;

    fx45 = edr * fx45;
    fx30 = edrL * fx30;
    fx60 = edrU * fx60;
    fx45i = edri * fx45i;

    vec4 px = step(df(e, f), df(e, h));
    vec4 maximos = max(max(fx30, fx60), max(fx45, fx45i));

    vec3 res1 = E;
    res1 = mix(res1, mix(H, F, px.x), maximos.x);
    res1 = mix(res1, mix(B, D, px.z), maximos.z);

    vec3 res2 = E;
    res2 = mix(res2, mix(F, B, px.y), maximos.y);
    res2 = mix(res2, mix(D, H, px.w), maximos.w);

    vec3 res = mix(res1, res2, step(colorDistance(E, res1), colorDistance(E, res2)));
    fragColor = vec4(res, 1.0);
}
