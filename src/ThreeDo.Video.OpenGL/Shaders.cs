namespace ThreeDo.Video.OpenGL;

/// <summary>GLSL 330 core shader sources for the palette-expanding presenter.</summary>
internal static class Shaders
{
    internal const string Vertex = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;
        uniform vec4 uRect;      // x, y, width, height in output pixels
        uniform vec2 uOutput;    // output width, height
        void main()
        {
            vec2 pixel = uRect.xy + (aPosition * uRect.zw);
            vec2 ndc = (pixel / uOutput) * 2.0 - 1.0;
            gl_Position = vec4(ndc.x, -ndc.y, 0.0, 1.0);
        }
        """;

    internal const string Fragment = """
        #version 330 core
        uniform usampler2D uPixels;  // R16UI indexed pixels
        uniform sampler2D uClut;     // RGB8, 32 entries x height
        uniform usampler2D uParams;  // RGBA32UI: background, out-control
        uniform ivec2 uSource;
        uniform vec4 uRect;
        uniform int uFilter;         // 0 accurate/nearest, 1 sharp-bilinear, 2 CRT
        uniform float uScanline;
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

        vec2 sourceCoordinate(vec2 pixel)
        {
            vec2 uv = (pixel - uRect.xy) / uRect.zw;
            if (uFilter == 1)
            {
                // Light center-sampling smoothing; the dedicated multi-tap sharp-bilinear
                // pass is a later filter-polish item and does not change emulated state.
                vec2 texel = uv * vec2(uSource);
                vec2 rounded = floor(texel + 0.5);
                vec2 blend = clamp((fract(texel) - 0.5) * 0.75 + 0.5, 0.0, 1.0);
                uv = (mix(rounded - 0.5, rounded + 0.5, blend)) / vec2(uSource);
            }

            return uv;
        }

        void main()
        {
            vec2 uv = sourceCoordinate(gl_FragCoord.xy);
            ivec2 coordinate = ivec2(clamp(uv, vec2(0.0), vec2(0.999999)) * vec2(uSource));
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

            if (uFilter == 2)
            {
                float line = mod(floor(gl_FragCoord.y), 2.0);
                color *= 1.0 - (uScanline * line);
            }

            fragColor = vec4(color, 1.0);
        }
        """;
}
