#ifndef TGS_LIBRARY_HLSL
#define TGS_LIBRARY_HLSL

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

float3 SafeNormalizeOutLength(float3 inVec, out float length)
{
    const float dp3 = max(FLT_MIN, dot(inVec, inVec));
    length = dp3;
    return inVec * rsqrt(dp3);
}

float lengthSqr(float3 v)
{
    return dot(v, v);
}

float lengthSqr(float2 v)
{
    return dot(v, v);
}

#define InverseLerp(a, b, x) ((x - a) / (b - a))

uint CeilingDivision(uint lhs, uint rhs)
{
    return (lhs + rhs - 1) / rhs;
}

float3 ProjectToNormal(float3 normal, float3 position_relative)
{
    return position_relative - dot(position_relative, normal) * normal;
}

uint PackFloat(float value, uint offset, float minRange, float maxRange, uint bits)
{
    const uint encodingRange = (1 << bits) - 1u; // minus one, so that a value of 1.0 doesnt overflow.
    const uint valueAsUint = saturate(InverseLerp(minRange, maxRange, value)) * encodingRange;
    return valueAsUint << offset;
}

uint PackXYZ565(float3 value, uint offset, float3 boundsMin, float3 boundsMax)
{
    uint output = 0;
    output |= PackFloat(value.x, 0, 0.0, 1.0, 5);
    output |= PackFloat(value.y, 5, 0.0, 1.0, 6);
    output |= PackFloat(value.z, 5 + 6, 0.0, 1.0, 5);
    return output << offset;
}

// TODO: check that these get properly vectorized.
float UnpackFloat(uint src, uint offset, float minRange, float maxRange, uint bits)
{
    uint rawValue = src >> offset & 1 << bits;
    return lerp(minRange, maxRange, UnpackUIntToFloat(src, offset, bits));
}

float3 UnpackXYZ565(uint src, uint offset, float3 boundsMin, float3 boundsMax)
{
    float3 value01 = 0;
    value01.x = UnpackUIntToFloat(src, offset, 5);
    value01.y = UnpackUIntToFloat(src, offset + 5, 6);
    value01.z = UnpackUIntToFloat(src, offset + 5 + 6, 5);
    return lerp(boundsMin, boundsMax, value01);
}

// ==========================================

float3 Rnd2ToBarycentric(float2 r)
{
    #if 0
    const float rxSqrt = SafeSqrt(r.x);
    return float3(1.0 - rxSqrt, rxSqrt * (1.0 - r.y), rxSqrt * r.y);
    #else

    if (r.x + r.y > 1.0) {
        r = 1.0 - r;
    }
    float3 result = float3(1.0 - r.x - r.y, r.x, r.y);
    return result;
    #endif
}

uint PackFloat3ToUint(float3 vertex, float3 boundsMin, float3 boundsMax)
{
    const float3 maxRange = 1.0 - 1.0 / float3(2047.0, 2047.0, 1023.0);
    float3 value = clamp(InverseLerp(boundsMin, boundsMax, vertex), 0.00, maxRange);

    uint
        payload = PackFloatToUInt(value.x, 0, 11);
    payload |= PackFloatToUInt(value.y, 11, 11);
    payload |= PackFloatToUInt(value.z, 22, 10);

    return payload;
}

float3 UnpackUnitToFloat3(uint vertex, float3 boundsMin, float3 boundsMax)
{
    float3 value = float3(
        UnpackUIntToFloat(vertex, 0, 11),
        UnpackUIntToFloat(vertex, 11, 11),
        UnpackUIntToFloat(vertex, 22, 10));

    return lerp(boundsMin, boundsMax, value);
}

uint PackFloat4ToUint(float4 value)
{
    const float4 maxRange = 1.0 - 1.0 / float4(255.0, 255.0, 255.0, 255.0);
    value = clamp(InverseLerp(0.0, 1.0, value), 0.00, maxRange); // can't 1.0, since it will lead to broken blades.
    uint payload = 0;
    payload |= PackFloatToUInt(value.x, 0, 8);
    payload |= PackFloatToUInt(value.y, 8, 8);
    payload |= PackFloatToUInt(value.z, 16, 8);
    payload |= PackFloatToUInt(value.w, 24, 8);

    return payload;
}

float4 UnpackUnitToFloat4(uint payload)
{
    float4 value = float4(
        UnpackUIntToFloat(payload, 0, 8),
        UnpackUIntToFloat(payload, 8, 8),
        UnpackUIntToFloat(payload, 16, 8),
        UnpackUIntToFloat(payload, 24, 8));

    return value;
}

float PackFloat2ToUint(float2 value)
{
    uint payload = PackFloatToUInt(value.x, 0, 16);
    payload |= PackFloatToUInt(value.y, 16, 16);

    return payload;
}

float2 UnpackUnitToFloat2(uint payload)
{
    float2 value = float2(
        UnpackUIntToFloat(payload, 0, 16),
        UnpackUIntToFloat(payload, 16, 16));

    return value;
}

float3 GetNormalFromDirectionalDerivative(float3 positionWS)
{
    return -normalize(cross(ddx(positionWS), ddy(positionWS)));;
}


// Hash without Sine, https://www.shadertoy.com/view/4djSRW
// MIT License...
/* Copyright (c)2014 David Hoskins.
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.*/

float RndHash11(float p)
{
    p = frac(p * .1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float RndHash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * .1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float RndHash13(float3 p3)
{
    p3 = frac(p3 * .1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float RndHash14(float4 p4)
{
    p4 = frac(p4 * .1031);
    p4 += dot(p4, p4.yzxw + 33.33);
    return frac(p4.x + p4.y * p4.z * p4.w);
}

float2 RndHash21(float p)
{
    float3 p3 = frac(float3(p, p, p) * float3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float2 RndHash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float2 RndHash23(float3 p3)
{
    p3 = frac(p3 * float3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float3 RndHash31(float p)
{
    float3 p3 = frac(float3(p, p, p) * float3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xxy + p3.yzz) * p3.zyx);
}

float3 RndHash32(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return frac((p3.xxy + p3.yzz) * p3.zyx);
}

float3 RndHash33(float3 p3)
{
    p3 = frac(p3 * float3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return frac((p3.xxy + p3.yxx) * p3.zyx);
}

float4 RndHash41(float p)
{
    float4 p4 = frac(float4(p, p, p, p) * float4(.1031, .1030, .0973, .1099));
    p4 += dot(p4, p4.wzxy + 33.33);
    return frac((p4.xxyz + p4.yzzw) * p4.zywx);
}

float4 RndHash42(float2 p)
{
    float4 p4 = frac(float4(p.xyxy) * float4(.1031, .1030, .0973, .1099));
    p4 += dot(p4, p4.wzxy + 33.33);
    return frac((p4.xxyz + p4.yzzw) * p4.zywx);
}

float4 RndHash43(float3 p)
{
    float4 p4 = frac(float4(p.xyzx) * float4(.1031, .1030, .0973, .1099));
    p4 += dot(p4, p4.wzxy + 33.33);
    return frac((p4.xxyz + p4.yzzw) * p4.zywx);
}

float4 RndHash44(float4 p4)
{
    p4 = frac(p4 * float4(.1031, .1030, .0973, .1099));
    p4 += dot(p4, p4.wzxy + 33.33);
    return frac((p4.xxyz + p4.yzzw) * p4.zywx);
}

//
// Noise Shader Library for Unity - https://github.com/keijiro/NoiseShader
//
// Original work (webgl-noise) Copyright (C) 2011 Ashima Arts.
// Translation and modification was made by Keijiro Takahashi.
//
// This shader is based on the webgl-noise GLSL shader. For further details
// of the original shader, please see the following description from the
// original source code.
//

//
// Description : Array and textureless GLSL 2D/3D/4D simplex
//               noise functions.
//      Author : Ian McEwan, Ashima Arts.
//  Maintainer : ijm
//     Lastmod : 20110822 (ijm)
//     License : Copyright (C) 2011 Ashima Arts. All rights reserved.
//               Distributed under the MIT License. See LICENSE file.
//               https://github.com/ashima/webgl-noise
//

float3 mod289(float3 x)
{
    return x - floor(x / 289.0) * 289.0;
}

float4 mod289(float4 x)
{
    return x - floor(x / 289.0) * 289.0;
}

float4 permute(float4 x)
{
    return mod289((x * 34.0 + 1.0) * x);
}

float4 taylorInvSqrt(float4 r)
{
    return 1.79284291400159 - r * 0.85373472095314;
}

float snoise(float3 v)
{
    const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);

    // First corner
    float3 i = floor(v + dot(v, C.yyy));
    float3 x0 = v - i + dot(i, C.xxx);

    // Other corners
    float3 g = step(x0.yzx, x0.xyz);
    float3 l = 1.0 - g;
    float3 i1 = min(g.xyz, l.zxy);
    float3 i2 = max(g.xyz, l.zxy);

    // x1 = x0 - i1  + 1.0 * C.xxx;
    // x2 = x0 - i2  + 2.0 * C.xxx;
    // x3 = x0 - 1.0 + 3.0 * C.xxx;
    float3 x1 = x0 - i1 + C.xxx;
    float3 x2 = x0 - i2 + C.yyy;
    float3 x3 = x0 - 0.5;

    // Permutations
    i = mod289(i); // Avoid truncation effects in permutation
    float4 p =
        permute(permute(permute(i.z + float4(0.0, i1.z, i2.z, 1.0))
                + i.y + float4(0.0, i1.y, i2.y, 1.0))
            + i.x + float4(0.0, i1.x, i2.x, 1.0));

    // Gradients: 7x7 points over a square, mapped onto an octahedron.
    // The ring size 17*17 = 289 is close to a multiple of 49 (49*6 = 294)
    float4 j = p - 49.0 * floor(p / 49.0); // mod(p,7*7)

    float4 x_ = floor(j / 7.0);
    float4 y_ = floor(j - 7.0 * x_); // mod(j,N)

    float4 x = (x_ * 2.0 + 0.5) / 7.0 - 1.0;
    float4 y = (y_ * 2.0 + 0.5) / 7.0 - 1.0;

    float4 h = 1.0 - abs(x) - abs(y);

    float4 b0 = float4(x.xy, y.xy);
    float4 b1 = float4(x.zw, y.zw);

    //float4 s0 = float4(lessThan(b0, 0.0)) * 2.0 - 1.0;
    //float4 s1 = float4(lessThan(b1, 0.0)) * 2.0 - 1.0;
    float4 s0 = floor(b0) * 2.0 + 1.0;
    float4 s1 = floor(b1) * 2.0 + 1.0;
    float4 sh = -step(h, 0.0);

    float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

    float3 g0 = float3(a0.xy, h.x);
    float3 g1 = float3(a0.zw, h.y);
    float3 g2 = float3(a1.xy, h.z);
    float3 g3 = float3(a1.zw, h.w);

    // Normalise gradients
    float4 norm = taylorInvSqrt(float4(dot(g0, g0), dot(g1, g1), dot(g2, g2), dot(g3, g3)));
    g0 *= norm.x;
    g1 *= norm.y;
    g2 *= norm.z;
    g3 *= norm.w;

    // Mix final noise value
    float4 m = max(0.6 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
    m = m * m;
    m = m * m;

    float4 px = float4(dot(x0, g0), dot(x1, g1), dot(x2, g2), dot(x3, g3));
    return 42.0 * dot(m, px);
}

float4 snoise_grad(float3 v)
{
    const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);

    // First corner
    float3 i = floor(v + dot(v, C.yyy));
    float3 x0 = v - i + dot(i, C.xxx);

    // Other corners
    float3 g = step(x0.yzx, x0.xyz);
    float3 l = 1.0 - g;
    float3 i1 = min(g.xyz, l.zxy);
    float3 i2 = max(g.xyz, l.zxy);

    // x1 = x0 - i1  + 1.0 * C.xxx;
    // x2 = x0 - i2  + 2.0 * C.xxx;
    // x3 = x0 - 1.0 + 3.0 * C.xxx;
    float3 x1 = x0 - i1 + C.xxx;
    float3 x2 = x0 - i2 + C.yyy;
    float3 x3 = x0 - 0.5;

    // Permutations
    i = mod289(i); // Avoid truncation effects in permutation
    float4 p =
        permute(permute(permute(i.z + float4(0.0, i1.z, i2.z, 1.0))
                + i.y + float4(0.0, i1.y, i2.y, 1.0))
            + i.x + float4(0.0, i1.x, i2.x, 1.0));

    // Gradients: 7x7 points over a square, mapped onto an octahedron.
    // The ring size 17*17 = 289 is close to a multiple of 49 (49*6 = 294)
    float4 j = p - 49.0 * floor(p / 49.0); // mod(p,7*7)

    float4 x_ = floor(j / 7.0);
    float4 y_ = floor(j - 7.0 * x_); // mod(j,N)

    float4 x = (x_ * 2.0 + 0.5) / 7.0 - 1.0;
    float4 y = (y_ * 2.0 + 0.5) / 7.0 - 1.0;

    float4 h = 1.0 - abs(x) - abs(y);

    float4 b0 = float4(x.xy, y.xy);
    float4 b1 = float4(x.zw, y.zw);

    //float4 s0 = float4(lessThan(b0, 0.0)) * 2.0 - 1.0;
    //float4 s1 = float4(lessThan(b1, 0.0)) * 2.0 - 1.0;
    float4 s0 = floor(b0) * 2.0 + 1.0;
    float4 s1 = floor(b1) * 2.0 + 1.0;
    float4 sh = -step(h, 0.0);

    float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

    float3 g0 = float3(a0.xy, h.x);
    float3 g1 = float3(a0.zw, h.y);
    float3 g2 = float3(a1.xy, h.z);
    float3 g3 = float3(a1.zw, h.w);

    // Normalise gradients
    float4 norm = taylorInvSqrt(float4(dot(g0, g0), dot(g1, g1), dot(g2, g2), dot(g3, g3)));
    g0 *= norm.x;
    g1 *= norm.y;
    g2 *= norm.z;
    g3 *= norm.w;

    // Compute noise and gradient at P
    float4 m = max(0.6 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
    float4 m2 = m * m;
    float4 m3 = m2 * m;
    float4 m4 = m2 * m2;
    float3 grad =
        -6.0 * m3.x * x0 * dot(x0, g0) + m4.x * g0 +
        -6.0 * m3.y * x1 * dot(x1, g1) + m4.y * g1 +
        -6.0 * m3.z * x2 * dot(x2, g2) + m4.z * g2 +
        -6.0 * m3.w * x3 * dot(x3, g3) + m4.w * g3;
    float4 px = float4(dot(x0, g0), dot(x1, g1), dot(x2, g2), dot(x3, g3));
    return 42.0 * float4(grad, dot(m4, px));
}

#endif