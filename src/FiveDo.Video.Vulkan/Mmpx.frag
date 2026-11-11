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

// MMPX, Copyright 2020 Morgan McGuire and Mara Gagiu, MIT license.
// See THIRD-PARTY-NOTICES.md. VDL frames are opaque (alpha 255), so the reference
// luma term (r + g + b + 1) * (256 - alpha) reduces to r + g + b + 1 and the
// luminance tie-break and triangle-tip rules stay active.

uvec3 fetchColor(ivec2 p)
{
    vec3 color = texelFetch(uExpanded, clamp(p, ivec2(0), pc.sourceSize - 1), 0).rgb;
    return uvec3(trunc((color * 255.0) + 0.5));
}

bool allEq2(uvec3 b, uvec3 a0, uvec3 a1)
{
    return all(equal(b, a0)) && all(equal(b, a1));
}

bool allEq3(uvec3 b, uvec3 a0, uvec3 a1, uvec3 a2)
{
    return all(equal(b, a0)) && all(equal(b, a1)) && all(equal(b, a2));
}

bool allEq4(uvec3 b, uvec3 a0, uvec3 a1, uvec3 a2, uvec3 a3)
{
    return all(equal(b, a0)) && all(equal(b, a1)) && all(equal(b, a2)) && all(equal(b, a3));
}

bool anyEq3(uvec3 b, uvec3 a0, uvec3 a1, uvec3 a2)
{
    return any(equal(b, a0)) || any(equal(b, a1)) || any(equal(b, a2));
}

bool noneEq2(uvec3 b, uvec3 a0, uvec3 a1)
{
    return !any(equal(b, a0)) && !any(equal(b, a1));
}

bool noneEq4(uvec3 b, uvec3 a0, uvec3 a1, uvec3 a2, uvec3 a3)
{
    return !any(equal(b, a0)) && !any(equal(b, a1)) && !any(equal(b, a2)) && !any(equal(b, a3));
}

bool neColor(uvec3 a, uvec3 b)
{
    return any(notEqual(a, b));
}

int luma(uvec3 c)
{
    // VDL frames are opaque (alpha 255), so the reference luma term
    // (r + g + b + 1) * (256 - alpha) is r + g + b + 1.
    return int(c.r + c.g + c.b) + 1;
}

void main()
{
    ivec2 outCoord = ivec2(gl_FragCoord.xy);
    ivec2 sourceCoord = outCoord / 2;
    int subpixel = ((outCoord.y % 2) * 2) + (outCoord.x % 2);

    uvec3 a = fetchColor(sourceCoord + ivec2(-1, -1));
    uvec3 b = fetchColor(sourceCoord + ivec2( 0, -1));
    uvec3 c = fetchColor(sourceCoord + ivec2( 1, -1));
    uvec3 d = fetchColor(sourceCoord + ivec2(-1,  0));
    uvec3 e = fetchColor(sourceCoord);
    uvec3 f = fetchColor(sourceCoord + ivec2( 1,  0));
    uvec3 g = fetchColor(sourceCoord + ivec2(-1,  1));
    uvec3 h = fetchColor(sourceCoord + ivec2( 0,  1));
    uvec3 i = fetchColor(sourceCoord + ivec2( 1,  1));
    uvec3 p = fetchColor(sourceCoord + ivec2( 0, -2));
    uvec3 s = fetchColor(sourceCoord + ivec2( 0,  2));
    uvec3 q = fetchColor(sourceCoord + ivec2(-2,  0));
    uvec3 r = fetchColor(sourceCoord + ivec2( 2,  0));

    uvec3 j = e;
    uvec3 k = e;
    uvec3 l = e;
    uvec3 m = e;

    if (any(notEqual(a, e)) || any(notEqual(b, e)) || any(notEqual(c, e)) || any(notEqual(d, e))
        || any(notEqual(f, e)) || any(notEqual(g, e)) || any(notEqual(h, e)) || any(notEqual(i, e)))
    {
        int bl = luma(b);
        int dl = luma(d);
        int el = luma(e);
        int fl = luma(f);
        int hl = luma(h);

        // 1:1 slope rules.
        if (all(equal(d, b)) && any(notEqual(d, h)) && any(notEqual(d, f))
            && (el >= dl || all(equal(e, a)))
            && anyEq3(e, a, c, g)
            && (el < dl || any(notEqual(a, d)) || any(notEqual(e, p)) || any(notEqual(e, q))))
        {
            j = d;
        }

        if (all(equal(b, f)) && any(notEqual(b, d)) && any(notEqual(b, h))
            && (el >= bl || all(equal(e, c)))
            && anyEq3(e, a, c, i)
            && (el < bl || any(notEqual(c, b)) || any(notEqual(e, p)) || any(notEqual(e, r))))
        {
            k = b;
        }

        if (all(equal(h, d)) && any(notEqual(h, f)) && any(notEqual(h, b))
            && (el >= hl || all(equal(e, g)))
            && anyEq3(e, a, g, i)
            && (el < hl || any(notEqual(g, h)) || any(notEqual(e, s)) || any(notEqual(e, q))))
        {
            l = h;
        }

        if (all(equal(f, h)) && any(notEqual(f, b)) && any(notEqual(f, d))
            && (el >= fl || all(equal(e, i)))
            && anyEq3(e, c, g, i)
            && (el < fl || any(notEqual(i, h)) || any(notEqual(e, r)) || any(notEqual(e, s))))
        {
            m = f;
        }

        // Intersection rules.
        if (any(notEqual(e, f)) && allEq4(e, c, i, d, q) && allEq2(f, b, h)
            && neColor(f, fetchColor(sourceCoord + ivec2(3, 0))))
        {
            k = f;
            m = f;
        }

        if (any(notEqual(e, d)) && allEq4(e, a, g, f, r) && allEq2(d, b, h)
            && neColor(d, fetchColor(sourceCoord + ivec2(-3, 0))))
        {
            j = d;
            l = d;
        }

        if (any(notEqual(e, h)) && allEq4(e, g, i, b, p) && allEq2(h, d, f)
            && neColor(h, fetchColor(sourceCoord + ivec2(0, 3))))
        {
            l = h;
            m = h;
        }

        if (any(notEqual(e, b)) && allEq4(e, a, c, h, s) && allEq2(b, d, f)
            && neColor(b, fetchColor(sourceCoord + ivec2(0, -3))))
        {
            j = b;
            k = b;
        }

        if (bl < el && allEq4(e, g, h, i, s) && noneEq4(e, a, d, c, f))
        {
            j = b;
            k = b;
        }

        if (hl < el && allEq4(e, a, b, c, p) && noneEq4(e, d, g, i, f))
        {
            l = h;
            m = h;
        }

        if (fl < el && allEq4(e, a, d, g, q) && noneEq4(e, b, c, i, h))
        {
            k = f;
            m = f;
        }

        if (dl < el && allEq4(e, c, f, i, r) && noneEq4(e, b, a, g, h))
        {
            j = d;
            l = d;
        }

        // 2:1 slope rules.
        if (any(notEqual(h, b)))
        {
            if (any(notEqual(h, a)) && any(notEqual(h, e)) && any(notEqual(h, c)))
            {
                if (allEq3(h, g, f, r)
                    && noneEq2(h, d, fetchColor(sourceCoord + ivec2(2, -1))))
                {
                    l = m;
                }

                if (allEq3(h, i, d, q)
                    && noneEq2(h, f, fetchColor(sourceCoord + ivec2(-2, -1))))
                {
                    m = l;
                }
            }

            if (any(notEqual(b, i)) && any(notEqual(b, g)) && any(notEqual(b, e)))
            {
                if (allEq3(b, a, f, r)
                    && noneEq2(b, d, fetchColor(sourceCoord + ivec2(2, 1))))
                {
                    j = k;
                }

                if (allEq3(b, c, d, q)
                    && noneEq2(b, f, fetchColor(sourceCoord + ivec2(-2, 1))))
                {
                    k = j;
                }
            }
        }

        if (any(notEqual(f, d)))
        {
            if (any(notEqual(d, i)) && any(notEqual(d, e)) && any(notEqual(d, c)))
            {
                if (allEq3(d, a, h, s)
                    && noneEq2(d, b, fetchColor(sourceCoord + ivec2(1, 2))))
                {
                    j = l;
                }

                if (allEq3(d, g, b, p)
                    && noneEq2(d, h, fetchColor(sourceCoord + ivec2(1, -2))))
                {
                    l = j;
                }
            }

            if (any(notEqual(f, e)) && any(notEqual(f, a)) && any(notEqual(f, g)))
            {
                if (allEq3(f, c, h, s)
                    && noneEq2(f, b, fetchColor(sourceCoord + ivec2(-1, 2))))
                {
                    k = m;
                }

                if (allEq3(f, i, b, p)
                    && noneEq2(f, h, fetchColor(sourceCoord + ivec2(-1, -2))))
                {
                    m = k;
                }
            }
        }
    }

    uvec3 result = subpixel == 0 ? j : (subpixel == 1 ? k : (subpixel == 2 ? l : m));
    fragColor = vec4(vec3(result) / 255.0, 1.0);
}
