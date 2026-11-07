namespace FiveDo.Video.Direct3D;

/// <summary>
/// HLSL sources for the Direct3D 11 presenter.
/// </summary>
/// <remarks>
/// The shaders are compiled at runtime with the platform D3DCompiler so the backend has
/// no build-time shader toolchain dependency. They mirror the OpenGL/Vulkan two-stage
/// pipeline: palette expansion at native resolution followed by an output filter pass.
/// </remarks>
internal static class Direct3D11Shaders
{
    internal const string Common = """
        cbuffer Constants : register(b0)
        {
            float4 Rect;
            float4 Params;
            float2 OutputSize;
            int2 SourceSize;
            int FilterMode;
            int Pad0;
            float2 Pad1;
        };

        struct PSInput
        {
            float4 position : SV_POSITION;
        };
        """;

    internal const string Vertex = Common + """

        PSInput VSMain(uint id : SV_VertexID)
        {
            float2 corner = float2((id << 1) & 2, id & 2);
            float2 pixel = Rect.xy + (corner * Rect.zw);
            float2 ndc = float2(
                ((pixel.x / OutputSize.x) * 2.0) - 1.0,
                1.0 - ((pixel.y / OutputSize.y) * 2.0));
            PSInput output;
            output.position = float4(ndc, 0.0, 1.0);
            return output;
        }
        """;

    internal const string Palette = Common + """

        Texture2D<uint> Pixels : register(t0);
        Texture2D<float4> Clut : register(t1);
        Texture2D<uint2> LineParams : register(t2);

        uint Expand5(uint value)
        {
            return (value << 3u) | (value >> 2u);
        }

        float3 FixedClut(uint pixel)
        {
            uint r = (pixel >> 10u) & 0x1fu;
            uint g = (pixel >> 5u) & 0x1fu;
            uint b = pixel & 0x1fu;
            return float3(float(Expand5(r)), float(Expand5(g)), float(Expand5(b))) / 255.0;
        }

        float3 BackgroundColor(uint background)
        {
            float r = float((background >> 16u) & 0xffu) / 255.0;
            float g = float((background >> 8u) & 0xffu) / 255.0;
            float b = float(background & 0xffu) / 255.0;
            return float3(r, g, b);
        }

        float4 PSMain(PSInput input) : SV_TARGET
        {
            int2 coordinate = clamp(int2(input.position.xy), int2(0, 0), SourceSize - 1);
            uint pixel = Pixels.Load(int3(coordinate, 0)).r;
            uint2 parameters = LineParams.Load(int3(0, coordinate.y, 0));
            uint background = parameters.x;
            uint outcontrol = parameters.y;

            float3 color;
            if (pixel == 0u)
            {
                color = BackgroundColor(background);
            }
            else if ((outcontrol & 0x02000000u) != 0u && (pixel & 0x8000u) != 0u)
            {
                color = FixedClut(pixel);
            }
            else
            {
                uint indexR = (pixel >> 10u) & 0x1fu;
                uint indexG = (pixel >> 5u) & 0x1fu;
                uint indexB = pixel & 0x1fu;
                color = float3(
                    Clut.Load(int3(int(indexR), coordinate.y, 0)).r,
                    Clut.Load(int3(int(indexG), coordinate.y, 0)).g,
                    Clut.Load(int3(int(indexB), coordinate.y, 0)).b);
            }

            return float4(color, 1.0);
        }
        """;

    internal const string Scale = Common + """

        Texture2D<float4> Expanded : register(t0);
        SamplerState LinearSampler : register(s0);

        float4 PSMain(PSInput input) : SV_TARGET
        {
            float2 uv = (input.position.xy - Rect.xy) / Rect.zw;
            if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
            {
                return float4(0.0, 0.0, 0.0, 1.0);
            }

            if (FilterMode == 0)
            {
                int2 source = clamp(
                    int2(floor(uv * (float2)SourceSize)),
                    int2(0, 0),
                    SourceSize - 1);
                return float4(Expanded.Load(int3(source, 0)).rgb, 1.0);
            }

            if (FilterMode == 1)
            {
                float2 texel = uv * (float2)SourceSize;
                float2 texelFloored = floor(texel);
                float2 texelFraction = frac(texel);
                float2 scale = max(floor(Rect.zw / (float2)SourceSize), float2(1.0, 1.0));
                float2 regionRange = float2(0.5, 0.5) - (float2(0.5, 0.5) / scale);
                float2 centerDistance = texelFraction - float2(0.5, 0.5);
                float2 blended = (centerDistance - clamp(centerDistance, -regionRange, regionRange))
                    * scale + float2(0.5, 0.5);
                uv = (texelFloored + blended) / (float2)SourceSize;
            }

            if (FilterMode == 2)
            {
                float2 centered = (uv * 2.0) - 1.0;
                centered *= 1.0 + (0.055 * dot(centered, centered));
                uv = (centered * 0.5) + 0.5;
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                {
                    return float4(0.0, 0.0, 0.0, 1.0);
                }
            }

            float3 color = Expanded.Sample(LinearSampler, saturate(uv)).rgb;
            if (FilterMode == 2)
            {
                float scanline = fmod(floor(input.position.y), 2.0);
                float aperture = fmod(floor(input.position.x), 3.0);
                float3 mask = aperture < 1.0
                    ? float3(1.0, 0.82, 0.82)
                    : aperture < 2.0
                        ? float3(0.82, 1.0, 0.82)
                        : float3(0.82, 0.82, 1.0);
                color *= lerp(float3(1.0, 1.0, 1.0), mask, Params.x * 0.5);
                color *= 1.0 - (Params.x * scanline);
                float2 centered = (uv * 2.0) - 1.0;
                color *= clamp(1.0 - (0.12 * dot(centered, centered)), 0.0, 1.0);

                if (Params.y > 0.0)
                {
                    float2 texel = 2.0 / (float2)SourceSize;
                    float3 bloomColor = float3(0.0, 0.0, 0.0);
                    float weightSum = 0.0;
                    [unroll]
                    for (int y = -1; y <= 1; y++)
                    {
                        [unroll]
                        for (int x = -1; x <= 1; x++)
                        {
                            float weight = (x == 0 ? 2.0 : 1.0) * (y == 0 ? 2.0 : 1.0);
                            float2 offset = float2(float(x), float(y)) * texel;
                            float3 sampleColor = Expanded.Sample(
                                LinearSampler,
                                saturate(uv + offset)).rgb;
                            bloomColor += max(sampleColor - float3(Params.z, Params.z, Params.z), 0.0) * weight;
                            weightSum += weight;
                        }
                    }

                    color += (bloomColor / weightSum) * Params.y;
                }
            }

            return float4(color, 1.0);
        }
        """;

    internal const string Xbr = Common + """

        Texture2D<float4> Expanded : register(t0);

        static const float3 rgbw = float3(14.352, 28.176, 5.472);
        static const float eqThreshold = 15.0;
        static const float lv2Coefficient = 2.0;
        static const float4 Ao = float4(1.0, -1.0, -1.0, 1.0);
        static const float4 Bo = float4(1.0, 1.0, -1.0, -1.0);
        static const float4 Co = float4(1.5, 0.5, -0.5, 0.5);
        static const float4 Ax = float4(1.0, -1.0, -1.0, 1.0);
        static const float4 Bx = float4(0.5, 2.0, -0.5, -2.0);
        static const float4 Cx = float4(1.0, 1.0, -0.5, 0.0);
        static const float4 Ay = float4(1.0, -1.0, -1.0, 1.0);
        static const float4 By = float4(2.0, 0.5, -2.0, -0.5);
        static const float4 Cy = float4(2.0, 0.0, -1.0, 0.5);
        static const float4 Ci = float4(0.25, 0.25, 0.25, 0.25);
        static const float4 delta = float4(0.25, 0.25, 0.25, 0.25);
        static const float4 deltaL = float4(0.125, 0.25, 0.125, 0.25);
        static const float4 deltaU = float4(0.25, 0.125, 0.25, 0.125);

        float4 df(float4 a, float4 b) { return abs(a - b); }
        float4 diff(float4 a, float4 b) { return float4(1.0, 1.0, 1.0, 1.0) - step(df(a, b), float4(0.0, 0.0, 0.0, 0.0)); }
        float4 eq(float4 a, float4 b) { return step(df(a, b), float4(eqThreshold, eqThreshold, eqThreshold, eqThreshold)); }
        float4 neq(float4 a, float4 b) { return float4(1.0, 1.0, 1.0, 1.0) - eq(a, b); }

        float4 WeightedDistance(float4 a, float4 b, float4 c, float4 d, float4 e, float4 f, float4 g, float4 h)
        {
            return df(a, b) + df(a, c) + df(d, e) + df(d, f) + 4.0 * df(g, h);
        }

        float ColorDistance(float3 a, float3 b)
        {
            float3 d = abs(a - b);
            return d.r + d.g + d.b;
        }

        float3 SampleFrame(float2 uv)
        {
            int2 coordinate = int2(floor(uv * (float2)SourceSize));
            return Expanded.Load(int3(clamp(coordinate, int2(0, 0), SourceSize - 1), 0)).rgb;
        }

        float4 PSMain(PSInput input) : SV_TARGET
        {
            float2 texCoord = input.position.xy / OutputSize;
            float2 ps = float2(1.0, 1.0) / (float2)SourceSize;
            float2 fp = frac(texCoord * (float2)SourceSize);

            float3 A1 = SampleFrame(texCoord + float2(-ps.x, -2.0 * ps.y));
            float3 B1 = SampleFrame(texCoord + float2(0.0, -2.0 * ps.y));
            float3 C1 = SampleFrame(texCoord + float2(ps.x, -2.0 * ps.y));
            float3 A = SampleFrame(texCoord + float2(-ps.x, -ps.y));
            float3 B = SampleFrame(texCoord + float2(0.0, -ps.y));
            float3 C = SampleFrame(texCoord + float2(ps.x, -ps.y));
            float3 D = SampleFrame(texCoord + float2(-ps.x, 0.0));
            float3 E = SampleFrame(texCoord);
            float3 F = SampleFrame(texCoord + float2(ps.x, 0.0));
            float3 G = SampleFrame(texCoord + float2(-ps.x, ps.y));
            float3 H = SampleFrame(texCoord + float2(0.0, ps.y));
            float3 I = SampleFrame(texCoord + float2(ps.x, ps.y));
            float3 G5 = SampleFrame(texCoord + float2(-ps.x, 2.0 * ps.y));
            float3 H5 = SampleFrame(texCoord + float2(0.0, 2.0 * ps.y));
            float3 I5 = SampleFrame(texCoord + float2(ps.x, 2.0 * ps.y));
            float3 A0 = SampleFrame(texCoord + float2(-2.0 * ps.x, -ps.y));
            float3 D0 = SampleFrame(texCoord + float2(-2.0 * ps.x, 0.0));
            float3 G0 = SampleFrame(texCoord + float2(-2.0 * ps.x, ps.y));
            float3 C4 = SampleFrame(texCoord + float2(2.0 * ps.x, -ps.y));
            float3 F4 = SampleFrame(texCoord + float2(2.0 * ps.x, 0.0));
            float3 I4 = SampleFrame(texCoord + float2(2.0 * ps.x, ps.y));

            float4 b = float4(dot(B, rgbw), dot(D, rgbw), dot(H, rgbw), dot(F, rgbw));
            float4 c = float4(dot(C, rgbw), dot(A, rgbw), dot(G, rgbw), dot(I, rgbw));
            float4 d = b.yzwx;
            float4 e = float4(dot(E, rgbw), dot(E, rgbw), dot(E, rgbw), dot(E, rgbw));
            float4 f = b.wxyz;
            float4 g = c.zwxy;
            float4 h = b.zwxy;
            float4 i = c.wxyz;
            float4 i4 = float4(dot(I4, rgbw), dot(C1, rgbw), dot(A0, rgbw), dot(G5, rgbw));
            float4 i5 = float4(dot(I5, rgbw), dot(C4, rgbw), dot(A1, rgbw), dot(G0, rgbw));
            float4 h5 = float4(dot(H5, rgbw), dot(F4, rgbw), dot(B1, rgbw), dot(D0, rgbw));
            float4 f4 = h5.yzwx;

            float4 fx = (Ao * fp.y) + (Bo * fp.x);
            float4 fxL = (Ax * fp.y) + (Bx * fp.x);
            float4 fxU = (Ay * fp.y) + (By * fp.x);

            float4 irlv0 = diff(e, f) * diff(e, h);
            float4 irlv1 = irlv0 * (
                neq(f, b) * neq(f, c)
                + neq(h, d) * neq(h, g)
                + eq(e, i) * (neq(f, f4) * neq(f, i4) + neq(h, h5) * neq(h, i5))
                + eq(e, g)
                + eq(e, c));
            float4 irlv2l = diff(e, g) * diff(d, g);
            float4 irlv2u = diff(e, c) * diff(b, c);

            float4 fx45i = clamp((fx + delta - Co - Ci) / (2.0 * delta), 0.0, 1.0);
            float4 fx45 = clamp((fx + delta - Co) / (2.0 * delta), 0.0, 1.0);
            float4 fx30 = clamp((fxL + deltaL - Cx) / (2.0 * deltaL), 0.0, 1.0);
            float4 fx60 = clamp((fxU + deltaU - Cy) / (2.0 * deltaU), 0.0, 1.0);

            float4 wd1 = WeightedDistance(e, c, g, i, h5, f4, h, f);
            float4 wd2 = WeightedDistance(h, d, i5, f, i4, b, e, i);

            float4 edri = step(wd1, wd2) * irlv0;
            float4 edr = step(wd1 + float4(0.1, 0.1, 0.1, 0.1), wd2) * step(float4(0.5, 0.5, 0.5, 0.5), irlv1);
            float4 edrL = step(lv2Coefficient * df(f, g), df(h, c)) * irlv2l * edr;
            float4 edrU = step(lv2Coefficient * df(h, c), df(f, g)) * irlv2u * edr;

            fx45 = edr * fx45;
            fx30 = edrL * fx30;
            fx60 = edrU * fx60;
            fx45i = edri * fx45i;

            float4 px = step(df(e, f), df(e, h));
            float4 maximos = max(max(fx30, fx60), max(fx45, fx45i));

            float3 res1 = E;
            res1 = lerp(res1, lerp(H, F, px.x), maximos.x);
            res1 = lerp(res1, lerp(B, D, px.z), maximos.z);

            float3 res2 = E;
            res2 = lerp(res2, lerp(F, B, px.y), maximos.y);
            res2 = lerp(res2, lerp(D, H, px.w), maximos.w);

            float3 result = lerp(res1, res2, step(ColorDistance(E, res1), ColorDistance(E, res2)));
            return float4(result, 1.0);
        }
        """;

    internal const string FsrEasu = Common + """

        Texture2D<float4> Expanded : register(t0);

        float3 Tap(int2 p)
        {
            return Expanded.Load(int3(clamp(p, int2(0, 0), SourceSize - 1), 0)).rgb;
        }

        float Luminance(float3 c)
        {
            return (c.r * 0.5) + c.g + (c.b * 0.5);
        }

        void AccumulateTap(
            inout float3 color,
            inout float weight,
            float2 offset,
            float2 direction,
            float2 scale,
            float lobe,
            float clipValue,
            float3 tapColor)
        {
            float2 v = float2(
                (offset.x * direction.x) + (offset.y * direction.y),
                (offset.x * (-direction.y)) + (offset.y * direction.x));
            v *= scale;
            float d2 = min(dot(v, v), clipValue);
            float wB = (2.0 / 5.0) * d2 - 1.0;
            float wA = lobe * d2 - 1.0;
            wB *= wB;
            wA *= wA;
            wB = (25.0 / 16.0) * wB - ((25.0 / 16.0) - 1.0);
            float w = wB * wA;
            color += tapColor * w;
            weight += w;
        }

        void AccumulateDirection(
            inout float2 direction,
            inout float len,
            float2 pp,
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
            len += lenX * w;

            float ec = lE - lC;
            float ca = lC - lA;
            float lenY = max(abs(ec), abs(ca));
            lenY = 1.0 / max(lenY, 1e-6);
            float dirY = lE - lA;
            direction.y += dirY * w;
            lenY = clamp(abs(dirY) * lenY, 0.0, 1.0);
            lenY *= lenY;
            len += lenY * w;
        }

        float4 PSMain(PSInput input) : SV_TARGET
        {
            int2 ip = int2(input.position.xy);
            float2 pp = ((float2)ip * (float2)SourceSize / OutputSize)
                + ((float2(0.5, 0.5) * (float2)SourceSize / OutputSize) - float2(0.5, 0.5));
            float2 fp = floor(pp);
            pp -= fp;
            int2 f = int2(fp);

            float3 b = Tap(f + int2(0, -1));
            float3 c = Tap(f + int2(1, -1));
            float3 e = Tap(f + int2(-1, 0));
            float3 f0 = Tap(f + int2(0, 0));
            float3 g = Tap(f + int2(1, 0));
            float3 h = Tap(f + int2(2, 0));
            float3 i = Tap(f + int2(-1, 1));
            float3 j = Tap(f + int2(0, 1));
            float3 k = Tap(f + int2(1, 1));
            float3 l = Tap(f + int2(2, 1));
            float3 n = Tap(f + int2(0, 2));
            float3 o = Tap(f + int2(1, 2));

            float bL = Luminance(b);
            float cL = Luminance(c);
            float eL = Luminance(e);
            float fL = Luminance(f0);
            float gL = Luminance(g);
            float hL = Luminance(h);
            float iL = Luminance(i);
            float jL = Luminance(j);
            float kL = Luminance(k);
            float lL = Luminance(l);
            float nL = Luminance(n);
            float oL = Luminance(o);

            float2 direction = float2(0.0, 0.0);
            float len = 0.0;
            AccumulateDirection(direction, len, pp, (1.0 - pp.x) * (1.0 - pp.y), bL, eL, fL, gL, jL);
            AccumulateDirection(direction, len, pp, pp.x * (1.0 - pp.y), cL, fL, gL, hL, kL);
            AccumulateDirection(direction, len, pp, (1.0 - pp.x) * pp.y, fL, iL, jL, kL, nL);
            AccumulateDirection(direction, len, pp, pp.x * pp.y, gL, jL, kL, lL, oL);

            float2 dir2 = direction * direction;
            float dirR = dir2.x + dir2.y;
            if (dirR < (1.0 / 32768.0))
            {
                direction = float2(1.0, 0.0);
            }
            else
            {
                direction *= rsqrt(dirR);
            }

            len *= 0.5;
            len *= len;
            float stretch = dot(direction, direction) / max(abs(direction.x), abs(direction.y));
            float2 length2 = float2(1.0 + (stretch - 1.0) * len, 1.0 - 0.5 * len);
            float lobe = 0.5 + (((1.0 / 4.0) - 0.04) - 0.5) * len;
            float clipValue = 1.0 / max(lobe, 1e-6);

            float3 min4 = min(min(f0, k), min(j, g));
            float3 max4 = max(max(f0, k), max(j, g));

            float3 color = float3(0.0, 0.0, 0.0);
            float weight = 0.0;
            AccumulateTap(color, weight, float2(0.0, -1.0) - pp, direction, length2, lobe, clipValue, b);
            AccumulateTap(color, weight, float2(1.0, -1.0) - pp, direction, length2, lobe, clipValue, c);
            AccumulateTap(color, weight, float2(-1.0, 1.0) - pp, direction, length2, lobe, clipValue, i);
            AccumulateTap(color, weight, float2(0.0, 1.0) - pp, direction, length2, lobe, clipValue, j);
            AccumulateTap(color, weight, float2(0.0, 0.0) - pp, direction, length2, lobe, clipValue, f0);
            AccumulateTap(color, weight, float2(-1.0, 0.0) - pp, direction, length2, lobe, clipValue, e);
            AccumulateTap(color, weight, float2(1.0, 1.0) - pp, direction, length2, lobe, clipValue, k);
            AccumulateTap(color, weight, float2(2.0, 1.0) - pp, direction, length2, lobe, clipValue, l);
            AccumulateTap(color, weight, float2(2.0, 0.0) - pp, direction, length2, lobe, clipValue, h);
            AccumulateTap(color, weight, float2(1.0, 0.0) - pp, direction, length2, lobe, clipValue, g);
            AccumulateTap(color, weight, float2(1.0, 2.0) - pp, direction, length2, lobe, clipValue, o);
            AccumulateTap(color, weight, float2(0.0, 2.0) - pp, direction, length2, lobe, clipValue, n);

            return float4(min(max4, max(min4, color / max(weight, 1e-6))), 1.0);
        }
        """;

    internal const string FsrRcas = Common + """

        Texture2D<float4> FsrInput : register(t0);

        float3 LoadTap(int2 p)
        {
            return FsrInput.Load(int3(clamp(p, int2(0, 0), SourceSize - 1), 0)).rgb;
        }

        float4 PSMain(PSInput input) : SV_TARGET
        {
            int2 ip = int2(input.position.xy) - int2(Rect.xy);
            float3 b = LoadTap(ip + int2(0, -1));
            float3 d = LoadTap(ip + int2(-1, 0));
            float3 e = LoadTap(ip);
            float3 f = LoadTap(ip + int2(1, 0));
            float3 h = LoadTap(ip + int2(0, 1));

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
            float sharpness = exp2(-Params.w);
            float lobe = max(-0.1875, min(max(max(lobeR, lobeG), lobeB), 0.0)) * sharpness;

            float rcpL = 1.0 / max((4.0 * lobe) + 1.0, 1e-6);
            float3 color = ((b + d + f + h) * lobe + e) * rcpL;
            return float4(clamp(color, 0.0, 1.0), 1.0);
        }
        """;

    internal static readonly (string Name, string Profile, string Source)[] All =
    [
        ("Frame.vert", "vs_5_0", Vertex),
        ("Palette.frag", "ps_5_0", Palette),
        ("Scale.frag", "ps_5_0", Scale),
        ("Xbr.frag", "ps_5_0", Xbr),
        ("FsrEasu.frag", "ps_5_0", FsrEasu),
        ("FsrRcas.frag", "ps_5_0", FsrRcas),
    ];
}


