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

    internal static string Vertex(bool es) => Header(es) + VertexBody;

    /// <summary>Stage 1: palette expansion at native resolution.</summary>
    internal static string FragmentPalette(bool es) => Header(es) + FragmentPaletteBody;

    /// <summary>Stage 2: scaling/filtering at output resolution.</summary>
    internal static string FragmentScale(bool es) => Header(es) + FragmentScaleBody;

    /// <summary>
    /// Optional xBR pass: edge-directed 4x expansion of the native-resolution expanded frame.
    /// </summary>
    internal static string FragmentXbr(bool es) => Header(es) + FragmentXbrBody;

    private static string Header(bool es) => es ? EsHeader + "\n" : DesktopHeader;

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
            uint b = background & 0x1fu;
            uint g = (background >> 5u) & 0x1fu;
            uint r = (background >> 10u) & 0x1fu;
            return vec3(float(expand5(r)), float(expand5(g)), float(expand5(b))) / 255.0;
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
}
