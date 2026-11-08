namespace FiveDo.Video.OpenGL;

/// <summary>GLSL shader sources for the two-stage presenter (desktop GL 3.3 and GLES 3.0).</summary>
/// <remarks>
/// Stage 1 expands the indexed pixels and per-line palette into a native-resolution RGB
/// texture. Stage 2 samples that texture at output resolution and applies the selected
/// filter. Keeping palette work at native resolution is what makes 4K output affordable:
/// ~77k palette pixels per frame instead of ~8.3M.
/// </remarks>
internal static class Shaders
{
    private const string DesktopHeader = "#version 330 core\n";

    private const string EsHeader = """
        #version 300 es
        precision highp float;
        precision highp int;
        precision highp sampler2D;
        precision highp usampler2D;
        """;

    internal static string Vertex(bool es)
    {
        return Header(es) + VertexBody;
    }

    /// <summary>Stage 1: palette expansion at native resolution.</summary>
    internal static string FragmentPalette(bool es)
    {
        return Header(es) + FragmentPaletteBody;
    }

    /// <summary>Stage 2: scaling/filtering at output resolution.</summary>
    internal static string FragmentScale(bool es)
    {
        return Header(es) + FragmentScaleBody;
    }

    /// <summary>
    /// Optional xBR pass: edge-directed 4x expansion of the native-resolution expanded frame.
    /// </summary>
    internal static string FragmentXbr(bool es)
    {
        return Header(es) + FragmentXbrBody;
    }

    /// <summary>
    /// HQx 2x/3x/4x expansion pass driven by the shared rule table.
    /// </summary>
    internal static string FragmentHqx(bool es)
    {
        return Header(es) + FragmentHqxBody;
    }

    /// <summary>
    /// FSR 1.0 EASU pass: edge-adaptive spatial upsampling of the native-resolution frame
    /// into the viewport-sized target.
    /// </summary>
    internal static string FragmentFsrEasu(bool es)
    {
        return Header(es) + FragmentFsrEasuBody;
    }

    /// <summary>FSR 1.0 RCAS pass: contrast-adaptive sharpening at output resolution.</summary>
    internal static string FragmentFsrRcas(bool es)
    {
        return Header(es) + FragmentFsrRcasBody;
    }

    private static string Header(bool es)
    {
        return es ? EsHeader + "\n" : DesktopHeader;
    }

    private const string VertexBody = """
                                      layout (location = 0) in vec2 aPosition;
                                      uniform vec4 uRect;      // x, y, width, height in target pixels
                                      uniform vec2 uOutput;    // target width, height
                                      void main()
                                      {
                                          vec2 pixel = uRect.xy + (aPosition * uRect.zw);
                                          vec2 ndc = (pixel / uOutput) * 2.0 - 1.0;
                                          gl_Position = vec4(ndc.x, -ndc.y, 0.0, 1.0);
                                      }
                                      """;

    private const string FragmentPaletteBody = """
        uniform usampler2D uPixels;  // R16UI indexed pixels
        uniform sampler2D uClut;     // RGB8, 32 entries x height
        uniform usampler2D uParams;  // RGBA32UI: background, out-control
        uniform ivec2 uSource;
        out vec4 fragColor;

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
            ivec2 coordinate = clamp(ivec2(gl_FragCoord.xy), ivec2(0), uSource - 1);
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
        """;

    private const string FragmentScaleBody = """
        uniform sampler2D uExpanded;  // native-resolution RGB texture
        uniform ivec2 uSource;
        uniform vec4 uRect;           // x, y, width, height in output pixels
        uniform int uFilter;          // 0 nearest, 1 sharp-bilinear, 2 CRT
        uniform float uScanline;
        uniform float uBloom;
        uniform float uBloomThreshold;
        out vec4 fragColor;

        void main()
        {
            vec2 uv = (gl_FragCoord.xy - uRect.xy) / uRect.zw;
            // The expanded source texture keeps row 0 (the frame's top line) at the
            // bottom of GL texture space. Flip the sampled V coordinate here so the
            // window shows the frame top-up instead of mirrored vertically.
            uv.y = 1.0 - uv.y;
            if (uFilter == 1)
            {
                // Themaister/rsn8887 sharp-bilinear: prescale by the integer
                // output/source factor, then let hardware bilinear filtering do
                // the final sub-texel blend. This keeps integer scales crisp and
                // removes the blur of a plain bilinear stretch.
                vec2 texel = uv * vec2(uSource);
                vec2 texelFloored = floor(texel);
                vec2 texelFraction = fract(texel);
                vec2 scale = max(floor(uRect.zw / vec2(uSource)), vec2(1.0));
                vec2 regionRange = vec2(0.5) - (vec2(0.5) / scale);
                vec2 centerDistance = texelFraction - vec2(0.5);
                vec2 blended = (centerDistance - clamp(centerDistance, -regionRange, regionRange))
                    * scale + vec2(0.5);
                uv = (texelFloored + blended) / vec2(uSource);
            }

            if (uFilter == 2)
            {
                // Mild barrel distortion for the CRT mode. The output stays black
                // outside the curved screen instead of stretching the edge texels.
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
            if (uFilter == 2)
            {
                float line = mod(floor(gl_FragCoord.y), 2.0);
                float aperture = mod(floor(gl_FragCoord.x), 3.0);
                vec3 mask = aperture < 1.0
                    ? vec3(1.0, 0.82, 0.82)
                    : aperture < 2.0
                        ? vec3(0.82, 1.0, 0.82)
                        : vec3(0.82, 0.82, 1.0);
                color *= mix(vec3(1.0), mask, uScanline * 0.5);
                color *= 1.0 - (uScanline * line);
                vec2 centered = uv * 2.0 - 1.0;
                color *= clamp(1.0 - (0.12 * dot(centered, centered)), 0.0, 1.0);

                // A small native-resolution glow around bright areas. Sampling the
                // source (not the output) keeps the bloom soft and resolution
                // independent; the 3x3 kernel is cheap enough for the opt-in CRT mode.
                if (uBloom > 0.0)
                {
                    vec2 texel = 2.0 / vec2(uSource);
                    vec3 bloom = vec3(0.0);
                    float weightSum = 0.0;
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int x = -1; x <= 1; x++)
                        {
                            float weight = (x == 0 ? 2.0 : 1.0) * (y == 0 ? 2.0 : 1.0);
                            vec2 offset = vec2(float(x), float(y)) * texel;
                            vec3 sampleColor = texture(uExpanded, clamp(uv + offset, vec2(0.0), vec2(1.0))).rgb;
                            bloom += max(sampleColor - vec3(uBloomThreshold), vec3(0.0)) * weight;
                            weightSum += weight;
                        }
                    }

                    color += (bloom / weightSum) * uBloom;
                }
            }

            fragColor = vec4(color, 1.0);
        }
        """;

    /// <summary>
    /// A single-pass xBR-lv2 edge-directed 4x expansion adapted to our native-resolution
    /// expanded frame. The algorithm is Hyllian's xBR-lv2 (MIT), simplified to the
    /// small-details-off path and driven by uniforms instead of libretro parameters.
    /// </summary>
    /// <remarks>
    /// xBR-lv2 Copyright (C) 2011-2016 Hyllian - sergiogdb@gmail.com. Permission is
    /// hereby granted, free of charge, to any person obtaining a copy of this software
    /// and associated documentation files (the "Software"), to deal in the Software
    /// without restriction, including without limitation the rights to use, copy,
    /// modify, merge, publish, distribute, sublicense, and/or sell copies of the
    /// Software, subject to the conditions in the upstream MIT notice. The Software is
    /// provided "as is", without warranty of any kind.
    /// </remarks>
    private const string FragmentXbrBody = """
        uniform sampler2D uPalette;  // native-resolution RGB texture
        uniform ivec2 uSource;        // native source width, height
        uniform ivec2 uXbrOutput;     // xBR output width, height (source * 4)
        out vec4 fragColor;

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
            ivec2 coordinate = ivec2(floor(uv * vec2(uSource)));
            return texelFetch(uPalette, clamp(coordinate, ivec2(0), uSource - 1), 0).rgb;
        }

        void main()
        {
            vec2 texCoord = gl_FragCoord.xy / vec2(uXbrOutput);
            vec2 ps = vec2(1.0) / vec2(uSource);
            vec2 fp = fract(texCoord * vec2(uSource));

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
        """;

    /// <summary>
    /// HQx expansion driven by the shared rule table. Each fragment reads the 3x3
    /// native-resolution neighbourhood, computes the HQx pattern, fetches the
    /// pattern/subpixel rule, and evaluates the same integer blend as the CPU
    /// <c>HqxFilter</c>.
    /// </summary>
    private const string FragmentHqxBody = """
        uniform sampler2D uExpanded;  // native-resolution RGB texture
        uniform usampler2D uRules;    // RG32UI: pattern x (scale row + subpixel)
        uniform ivec2 uSource;        // native source width, height
        uniform int uScale;           // 2, 3, or 4
        out vec4 fragColor;

        uvec3 fetchColor(ivec2 p)
        {
            vec3 color = texelFetch(uExpanded, clamp(p, ivec2(0), uSource - 1), 0).rgb;
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
            ivec2 sourceCoord = outCoord / uScale;
            int subpixel = ((outCoord.y % uScale) * uScale) + (outCoord.x % uScale);

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

            int rowOffset = uScale == 2 ? 0 : (uScale == 3 ? 4 : 13);
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
        """;

    /// <summary>
    /// FSR 1.0 EASU (edge-adaptive spatial upsampling), adapted to GLSL 3.30/ES 3.00.
    /// </summary>
    /// <remarks>
    /// FidelityFX Super Resolution 1.0, Copyright (c) 2021 Advanced Micro Devices, Inc.
    /// Distributed under the MIT license; see THIRD-PARTY-NOTICES.md. The reference
    /// implementation uses textureGather; this variant reads the same twelve integer
    /// taps with texelFetch so it works on both desktop GL and GLES 3.0. Pixels are
    /// addressed top-down and the source texture's row 0 is the frame's top line.
    /// </remarks>
    private const string FragmentFsrEasuBody = """
        uniform sampler2D uExpanded;  // native-resolution RGB texture
        uniform ivec2 uSource;        // native source width, height
        uniform ivec2 uFsrOutput;     // viewport (FSR target) width, height
        out vec4 fragColor;

        vec3 tap(ivec2 p)
        {
            return texelFetch(uExpanded, clamp(p, ivec2(0), uSource - 1), 0).rgb;
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
            // Convert the bottom-up framebuffer coordinate into top-down source space.
            ivec2 ip = ivec2(int(gl_FragCoord.x), uFsrOutput.y - 1 - int(gl_FragCoord.y));
            vec2 pp = (vec2(ip) * vec2(uSource) / vec2(uFsrOutput))
                + (vec2(0.5) * vec2(uSource) / vec2(uFsrOutput) - vec2(0.5));
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
        """;

    /// <summary>
    /// FSR 1.0 RCAS (robust contrast-adaptive sharpening), applied to the EASU result.
    /// </summary>
    /// <remarks>
    /// FidelityFX Super Resolution 1.0, Copyright (c) 2021 Advanced Micro Devices, Inc.
    /// Distributed under the MIT license; see THIRD-PARTY-NOTICES.md. The sharpness
    /// uniform is in stops: 0 is maximum sharpening and larger values soften it.
    /// </remarks>
    private const string FragmentFsrRcasBody = """
        uniform sampler2D uFsr;       // EASU output at viewport resolution
        uniform ivec2 uFsrSize;       // viewport width, height
        uniform vec4 uRect;           // x, y, width, height in output pixels
        uniform float uSharpness;     // stops: 0 = maximum, larger = softer
        out vec4 fragColor;

        vec3 load(ivec2 p)
        {
            return texelFetch(uFsr, clamp(p, ivec2(0), uFsrSize - 1), 0).rgb;
        }

        void main()
        {
            ivec2 ip = ivec2(gl_FragCoord.xy) - ivec2(uRect.xy);
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
            float sharpness = exp2(-uSharpness);
            float lobe = max(-0.1875, min(max(max(lobeR, lobeG), lobeB), 0.0)) * sharpness;

            float rcpL = 1.0 / max((4.0 * lobe) + 1.0, 1e-6);
            vec3 color = ((b + d + f + h) * lobe + e) * rcpL;
            fragColor = vec4(clamp(color, vec3(0.0), vec3(1.0)), 1.0);
        }
        """;
}
