#version 450

layout(push_constant) uniform PushConstants
{
    vec4 rect;
    vec2 outputSize;
    ivec2 sourceSize;
    int filterMode;
    float scanline;
    float bloom;
    float bloomThreshold;
} pc;

void main()
{
    vec2 corner = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
    vec2 pixel = pc.rect.xy + (corner * pc.rect.zw);
    vec2 ndc = ((pixel / pc.outputSize) * 2.0) - 1.0;
    gl_Position = vec4(ndc, 0.0, 1.0);
}
