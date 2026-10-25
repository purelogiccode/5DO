namespace ThreeDo.Video.OpenGL;

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
                uint index = pixel & 0x1fu;
                color = texelFetch(uClut, ivec2(int(index), coordinate.y), 0).rgb;
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
            }

            fragColor = vec4(color, 1.0);
        }
        """;
}
