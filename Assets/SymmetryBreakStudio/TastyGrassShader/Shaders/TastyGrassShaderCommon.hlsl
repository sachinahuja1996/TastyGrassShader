#ifndef TGS_COMMON_HLSL
#define TGS_COMMON_HLSL

#include "TastyGrassShaderLibrary.hlsl"


/* ------------------------------------------------------------------------------
   Global Constants
   
   Note: if you change anything here, you must also change the values defined in
   TgsInstance.cs
------------------------------------------------------------------------------ */

// The maximal size a grass blade can be. Higher values decrease precision,
// since vertices which aren't the root only have 8 bits per axis.

#define GRASS_MAX_VERTEX_RANGE_SIZE (2.0)

#define MAX_COLLIDER_PER_INSTANCE 8
#define MAX_BLADES_PER_TRIANGLE 4096

// For debugging purpose. Must also be aligned with the C# part.
//#define TGS_USE_REFERENCE_NODE_FORMAT

// ===========================================================================
// Shared Functions
// ===========================================================================

struct GrassNodeCompressed
{
    int bin_rootXY; // RootVertex.xy (16|16)
    int bin_rootZ_TipXY; // RootVertex.z (16) + Tip.xy (8|8)
    int bin_tipZ_sideXYZ; // Tip.z (8) + Side.xyz (8|8|8)
    int bin_normalXY_colorRGB; // Normal.xy (8|8) + Color.rgb (5|6|5)
};

struct GrassNodeReference
{
    float3 root, side, tip;
    float3 normal;
    float3 color;
};

// NOTE: taken from "Packages/com.unity.render-pipelines.core/ShaderLibrary/GeometricTools.hlsl"
// because this include causes issues with Amplify Shader Editor
// NOTE-2: Normalization for the rotationAxis was removed, since the most common use-case is an already normalized rotationAxis.

// Rotate around a pivot point and an axis
float3 TgsRotate(float3 pivot, float3 position, float3 normalizedRotationAxis, float angle)
{
    float3 cpa = pivot + normalizedRotationAxis * dot(normalizedRotationAxis, position - pivot);
    return cpa + ((position - cpa) * cos(angle) + cross(normalizedRotationAxis, (position - cpa)) * sin(angle));
}

// TODO: don't rely on packing functions, as they are slightly slower than direct bit manipulation.(generate 2 more instructions apparently)
GrassNodeCompressed GrassNodeCompress(GrassNodeReference node, float3 packBoundsMin, float3 packBoundsMax)
{
    GrassNodeCompressed prim = (GrassNodeCompressed)0;
    // Pack grass root vertex relative to the object AABB.
    {
        prim.bin_rootXY |= PackFloat(node.root.x, 0, packBoundsMin.x, packBoundsMax.x, 16);
        prim.bin_rootXY |= PackFloat(node.root.y, 16, packBoundsMin.y, packBoundsMax.y, 16);
        prim.bin_rootZ_TipXY |= PackFloat(node.root.z, 0, packBoundsMin.z, packBoundsMax.z, 16);
    }
    const float3 vertexBoundsMin = node.root - GRASS_MAX_VERTEX_RANGE_SIZE;
    const float3 vertexBoundsMax = node.root + GRASS_MAX_VERTEX_RANGE_SIZE;

    prim.bin_rootZ_TipXY |= PackFloat(node.tip.x, 16, vertexBoundsMin.x, vertexBoundsMax.x, 8);
    prim.bin_rootZ_TipXY |= PackFloat(node.tip.y, 24, vertexBoundsMin.y, vertexBoundsMax.y, 8);
    prim.bin_tipZ_sideXYZ |= PackFloat(node.tip.z, 0, vertexBoundsMin.z, vertexBoundsMax.z, 8);

    prim.bin_tipZ_sideXYZ |= PackFloat(node.side.x, 8, vertexBoundsMin.x, vertexBoundsMax.x, 8);
    prim.bin_tipZ_sideXYZ |= PackFloat(node.side.y, 16, vertexBoundsMin.y, vertexBoundsMax.y, 8);
    prim.bin_tipZ_sideXYZ |= PackFloat(node.side.z, 24, vertexBoundsMin.z, vertexBoundsMax.z, 8);

    float2 normalOct = PackNormalOctQuadEncode(node.normal);
    prim.bin_normalXY_colorRGB |= PackFloat(normalOct.x, 0, -1.0, 1.0, 8);
    prim.bin_normalXY_colorRGB |= PackFloat(normalOct.y, 8, -1.0, 1.0, 8);

    // encode colors srgb ish...
    prim.bin_normalXY_colorRGB |= PackXYZ565(saturate(node.color), 16, 0.0, 1.0);

    return prim;
}

void UnpackGrassVertices(GrassNodeCompressed prim, float3 boundsMin, float3 boundsMax, out float3 vertices[3])
{
    // Fun fact: using these plain bit shifting unpackers is 2 instructions faster per float3.
    const float3 root065535 = float3(
        prim.bin_rootXY >> 0 & 0xFFFF,
        prim.bin_rootXY >> 16 & 0xFFFF,
        prim.bin_rootZ_TipXY >> 0 & 0xFFFF);
    vertices[0] = lerp(boundsMin, boundsMax, root065535 / 65535.0);

    const float3 vtxBoundMin = vertices[0] - GRASS_MAX_VERTEX_RANGE_SIZE;
    const float3 vtxBoundMax = vertices[0] + GRASS_MAX_VERTEX_RANGE_SIZE;

    const float3 side0255 = float3(
        prim.bin_tipZ_sideXYZ >> 8 & 0xFF,
        prim.bin_tipZ_sideXYZ >> 16 & 0xFF,
        prim.bin_tipZ_sideXYZ >> 24 & 0xFF);

    vertices[1] = lerp(vtxBoundMin, vtxBoundMax, side0255 / 255.0);

    const float3 tip0255 = float3(
        prim.bin_rootZ_TipXY >> 16 & 0xFF,
        prim.bin_rootZ_TipXY >> 24 & 0xFF,
        prim.bin_tipZ_sideXYZ >> 0 & 0xFF);

    vertices[2] = lerp(vtxBoundMin, vtxBoundMax, tip0255 / 255.0);
}

float3 BezierInterpolation(float3 a, float3 b, float3 c, float t)
{
    return ((1.0 - t) * (1.0 - t)) * a +
        (2.0 * (1.0 - t) * t) * b +
        (t * t) * c;
}

float RndPhi(float seed)
{
    // aka the golden ratio
    const float phi = (1.0 + sqrt(5.0)) / 2.0;
    return frac(seed * phi);
}

// ========================================
// Common Unpacking Code
// ========================================

#ifdef TGS_USE_REFERENCE_NODE_FORMAT
StructuredBuffer<GrassNodeReference> _GrassFieldPrimitives;
#else
StructuredBuffer<GrassNodeCompressed> _GrassFieldPrimitives;
#endif
float4 _SphereCollider[MAX_COLLIDER_PER_INSTANCE];
int _SphereColliderCount;


float _Smoothness, _ArcUp, _ArcDown, _TipRounding;
float _WindPivotOffsetByHeight;
float _ColliderInfluence;
float _ProceduralShapeBlend;
float _UvUseCenterTriangle;
  
float4 _WindParams;
float3 _WindRotationAxis;

float3 _PositionBoundMin, _PositionBoundMax;
float4 _DebugBarycentricIso;
float _WindIntensityScale;

float _OcclusionByHeight, _FakeThicknessIntensity;
            
#ifdef GRASS_DEBUG_MODE
int _DebugShowBarycentric, _DebugDisableWind, _ShowBezierDistance;
#endif

// NOTE: needs out, so that it works with Amplify Shader Editor.
void TgsGetGrassVertex(uint vertexID, float time, float heightScale, out float3 positionWS, out uint normalColorBinRaw, out float3 barycentricCoord)
{
    barycentricCoord = float3(vertexID % 3 == 0, vertexID % 3 == 1, vertexID % 3 == 2);

    // Geometry unpacking
    // =====================================================================================================
    #ifdef TGS_USE_REFERENCE_NODE_FORMAT
    const GrassNodeReference tri = _GrassFieldPrimitives[vertexID / 3];
    float3 verticesWs[3];
    verticesWs[0] = tri.root;
    verticesWs[1] = tri.side;
    verticesWs[2] = tri.tip;

    const float3 normalWS = tri.normal;
    output.normal = normalWS;
    output.color = tri.color;

    output.positionWS = verticesWs[vertexID % 3];
    #else
    const GrassNodeCompressed tri = _GrassFieldPrimitives[vertexID / 3];
    float3 verticesWs[3];
    UnpackGrassVertices(tri, _PositionBoundMin, _PositionBoundMax, /*out*/ verticesWs);

    verticesWs[1] = lerp(verticesWs[0], verticesWs[1], heightScale);
    verticesWs[2] = lerp(verticesWs[0], verticesWs[2], heightScale);

    float2 normalOct = 0;
    normalOct.x = UnpackUIntToFloat(tri.bin_normalXY_colorRGB, 0, 8);
    normalOct.y = UnpackUIntToFloat(tri.bin_normalXY_colorRGB, 8, 8);
    float3 normalWS = normalize(UnpackNormalOctQuadEncode(normalOct * 2.0 - 1.0));
    
    positionWS = verticesWs[vertexID % 3];
    normalColorBinRaw = tri.bin_normalXY_colorRGB;
    
    #endif
    // Geometry unpacking
    // =====================================================================================================
    // Apply Wind
    // =====================================================================================================
    float3 pivot = verticesWs[0] + normalWS * _WindPivotOffsetByHeight;
    const float bladeLength = distance(pivot, positionWS);
    
    const float windStrength = _WindParams.y;
    const float windPatchSize = _WindParams.z;
    const float windSpeed = _WindParams.w;
    const float3 windRotationAxis = _WindRotationAxis;
    
    // Wind intensity is scaled by blade length.
    float windIntensity = snoise(positionWS.xyz * windPatchSize + time * windSpeed * -windRotationAxis.zyx) * windStrength * bladeLength;
    
    float normalisationFactor = 1.0 / (1.0 + abs(windIntensity));
    // Prevent grass from clipping trough the ground. Doesn't use clamp, because that makes grass "stick" to the ground.
    float windIntensityLimited = _WindIntensityScale * windIntensity * normalisationFactor * (PI / 2.0); 
    
    positionWS.xyz = TgsRotate(
        pivot.xyz,
        positionWS.xyz,
        _WindRotationAxis,
        windIntensityLimited);

    #ifdef GRASS_DEBUG_MODE
    if(_DebugDisableWind > 0)
    {
        output.positionWS.xyz = verticesWs[vertexID % 3];//UnpackUnitToFloat3(vertices[vertexID % 3], _PositionBoundMin, _PositionBoundMax).xyz;
    }
    #endif
    // Apply simply collision
    // =====================================================================================================
    for (int i = 0; i < _SphereColliderCount; i++)
    {
        float4 collider = _SphereCollider[i];
        // current, just check intersection with vertices
        // in the future, maybe offer more quality with line sphere intersection.
        
        const float3 sphereToTipVec = verticesWs[2] - collider.xyz;
        // TODO: this can be simplified.
        float sphereToTipVecLength;
        const float3 sphereToTipDir = SafeNormalizeOutLength(sphereToTipVec, /*out*/ sphereToTipVecLength);
        const float distanceToSphere = clamp(collider.w - sphereToTipVecLength, 0.0, bladeLength * _ColliderInfluence);
        positionWS += sphereToTipDir * distanceToSphere;
    }
    
}
float3 TgsUnpackColor(int packedNormalAndColor)
{
    #ifdef TGS_USE_REFERENCE_NODE_FORMAT
    float3 bladeColor = input.color;
    #else
    float3 bladeColor = UnpackXYZ565(packedNormalAndColor, 16, 0.0, 1.0);
    #endif

    return bladeColor;
}

float3 TgsUnpackNormal(int packedNormalAndColor)
{
    #ifdef TGS_USE_REFERENCE_NODE_FORMAT
    inputData.normalWS = input.normal;
    #else
    float2 normalOct = 0;
    normalOct.x = UnpackUIntToFloat(packedNormalAndColor, 0, 8);
    normalOct.y = UnpackUIntToFloat(packedNormalAndColor, 8, 8);
    return UnpackNormalOctQuadEncode(normalOct * 2.0 - 1.0);
    #endif
}

float EvaluateBladeShapeDistance(float3 tLoc)
{
    const float baseForm = tLoc.x  * tLoc.z;
    const float upperArch = baseForm - tLoc.y * _ArcUp;
    const float lowerArch = baseForm - tLoc.y * _ArcDown;
    const float body = -(upperArch * lowerArch);
    // return (tLoc.x * tLoc.y * tLoc.z) * 0.5;
    return  body - (1.0 - tLoc.x) * _TipRounding ;
}

float SharpenPixelPerfect(float alpha)
{
    const float alphaAbsSize = fwidth(alpha);
    const float alphaDivClamped = max(alphaAbsSize, FLT_MIN);
    return alpha / alphaDivClamped;
}

float4 TgsPreReturn(float4 color)
{
    #ifdef TGS_USE_ALPHACLIP
    // Workaround for when the alpha channel of the framebuffer is actually used, such as in XR-Passthrough. 
    // Since the alpha channel is used for alpha-to-coverage already, we will get weird blending artefacts. 
    clip(color.a - 0.5);
    return float4(color.rgb, 1.0);
    #else
    return color;
    #endif
}

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

void TgsGetSurfaceParametersInner(float3 barycentricCoord, float3 bladeColor, out float3 albedo, out float alpha, out float smoothness, out float occlusion)
{
    const float2 uvUpperRightTriangle = float2(barycentricCoord.z, 1.0 - barycentricCoord.x);
    const float2 uvFromQuad = float2(uvUpperRightTriangle.x + 0.5 * (1.0 - uvUpperRightTriangle.y), uvUpperRightTriangle.y);
    const float2 uv = lerp(uvUpperRightTriangle, uvFromQuad, _UvUseCenterTriangle);

    float4 textureSmp = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

    const float proceduralShape = saturate(EvaluateBladeShapeDistance(barycentricCoord) * 0.5 + 0.5);
    textureSmp.a = lerp(textureSmp.a, proceduralShape, _ProceduralShapeBlend);
    
    const float bladeRelativeHeight = barycentricCoord.x;
    
    albedo = textureSmp.rgb * bladeColor;
    smoothness = _Smoothness;
    occlusion = lerp(1.0, 1.0 - bladeRelativeHeight, _OcclusionByHeight);
    alpha = SharpenPixelPerfect(textureSmp.a - 0.5) + 0.5;
}


void TgsGetSurfaceParameters(float3 barycentricCoord, int packedNormalAndColor, out float3 albedo, out float alpha, out float smoothness, out float occlusion)
{
    float3 bladeColor = TgsUnpackColor(packedNormalAndColor);
    TgsGetSurfaceParametersInner(barycentricCoord, bladeColor, /*out*/ albedo,  /*out*/ alpha,  /*out*/ smoothness, /*out*/ occlusion);
}

// Wrappers for Shader-Graph
// --------------------------------------

void TgsGetGrassVertexShaderGraph_float(float vertexID, float time, float heightScale, out float3 positionWS, out float3 normal, out float3 color, out float3 barycentricCoord)
{
    uint normalColorBinRaw;
    TgsGetGrassVertex((uint) vertexID, time, heightScale, /*out*/ positionWS, /*out*/ normalColorBinRaw, /*out*/ barycentricCoord);
    color = TgsUnpackColor(normalColorBinRaw);
    normal = TgsUnpackNormal(normalColorBinRaw);
}

void TgsGetSurfaceParametersShaderGraph_float(float3 barycentricCoord, float3 color, out float3 albedo, out float alpha, out float smoothness, out float occlusion)
{
    TgsGetSurfaceParametersInner(barycentricCoord, color, /*out*/ albedo, /*out*/ alpha, /*out*/ smoothness, /*out*/ occlusion);
}

#endif