Shader "Hidden/Symmetry Break Studio/Tasty Grass Shader/Editor/Preview UV Layout"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
     
        HLSLINCLUDE
        #ifdef TGS_EDITOR_USE_URP
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #endif
        #ifdef TGS_EDITOR_USE_HDRP
    
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariablesFunctions.hlsl"
        // Needed, because otherwise TransformObjectToHClip() will output nothing usefully. 
        #define SCENEPICKINGPASS
        #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/PickingSpaceTransforms.hlsl"

        
        #endif
        
        #if !defined(TGS_EDITOR_USE_URP) && !defined(TGS_EDITOR_USE_HDRP)
          //  #error No Render Pipeline defined, going into dummy mode.
            // Dummy functions, so the shader will not fail to compile if TGS_EDITOR_USE_URP or TGS_EDITOR_USE_HDRP aren't defined.
            float4 TransformObjectToHClip(float3 position)
            {
                return float4(clamp(position * 1000.0, -1.0, 1.0), 1.0);
            }
            float4 _ScreenParams;
            
        #endif
        
        #include "../../Shaders/TastyGrassShaderCommon.hlsl"
        struct editor_preview_uv_layout_appdata
        {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct editor_preview_uv_layout_v2f
        {
            float4 vertex : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        float _UpperLeftTriangle;
        float _LowerRightTriangle;
        float _CenterTriangle;



        editor_preview_uv_layout_v2f tgs_editor_preview_uv_vert (editor_preview_uv_layout_appdata v, uint vertexId : SV_VertexID)
        {
            editor_preview_uv_layout_v2f o;
            float4 positions[] =
            {
                float4( 0.0,  0.0, 0.1, 1.0),
                float4( 0.0,  0.5, 0.1, 1.0),
                float4( 1.0,  0.5, 0.1, 1.0),
                float4( 1.0, -0.5, 0.1, 1.0),
                float4( 0.5, -0.5, 0.1, 1.0),
                float4( 0.5, -0.5, 0.1, 1.0),
            };
            o.vertex =   TransformObjectToHClip(v.vertex.xyz);
            o.uv = float2(1.0 - v.uv.y, v.uv.x) ;
            return o;
        }
        

        float sdTriangle( in float2 p, in float2 p0, in float2 p1, in float2 p2 )
        {
            float2 e0 = p1-p0, e1 = p2-p1, e2 = p0-p2;
            float2 v0 = p -p0, v1 = p -p1, v2 = p -p2;
            float2 pq0 = v0 - e0*clamp( dot(v0,e0)/dot(e0,e0), 0.0, 1.0 );
            float2 pq1 = v1 - e1*clamp( dot(v1,e1)/dot(e1,e1), 0.0, 1.0 );
            float2 pq2 = v2 - e2*clamp( dot(v2,e2)/dot(e2,e2), 0.0, 1.0 );
            float s = sign( e0.x*e2.y - e0.y*e2.x );
            float2 d = min(min(float2(dot(pq0,pq0), s*(v0.x*e0.y-v0.y*e0.x)),
                             float2(dot(pq1,pq1), s*(v1.x*e1.y-v1.y*e1.x))),
                             float2(dot(pq2,pq2), s*(v2.x*e2.y-v2.y*e2.x)));
            return -sqrt(d.x)*sign(d.y);
        }

        float SdfToLine(float sdf, float pixelSize)
        {
            return abs(sdf) < pixelSize * 2 ? 1.0 : 0.0;;
        }

        float SdfToMask(float sdf, float pixelSize)
        {
            return sdf < pixelSize * 2 ? 1.0 : 0.0;;
        }

        float4 tgs_editor_preview_uv_frag (editor_preview_uv_layout_v2f i) : SV_Target
        {
            // sample the texture
           // float4 val = tex2D(_MainTex, i.uv);
            //val.rgb *= val.a;
            float3 barycentricCoord = float3(i.uv.x,  i.uv.y, 1.0 - (i.uv.x + i.uv.y));
            float3 albedo;
            float alpha;
            float smoothness;
            float occlusion;
            TgsGetSurfaceParameters(barycentricCoord, 0xFFFFFFFF, /*out*/ albedo, /*out*/ alpha, /*out*/  smoothness, /*out*/  occlusion);
            float4 vista = 0;
            vista.rgb = albedo;
            vista.a = saturate(alpha);
            
            const float pixelSize = 1.0 / _ScreenParams.y;
            const float pixelSize2x = pixelSize * 2.0;

            const float leftTriangleSdf = sdTriangle(i.uv, float2(1.0, 0.0) + pixelSize2x , float2(0.0, 1.0) + pixelSize2x , float2(0.0, 0.0) + pixelSize2x );
            
            float2 checker = floor(i.uv * 4.0);
            float bg = fmod(checker.x + checker.y, 2.0) * 0.25 + 0.125;

            // Mask out the area that is actually usable.
            clip(SdfToMask(leftTriangleSdf, pixelSize) - 0.5);
            float4 vis = lerp(float4(bg.xxx, 1.0), vista.rgba, vista.a);

            return vis ;
        }
        ENDHLSL
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex tgs_editor_preview_uv_vert
            #pragma fragment tgs_editor_preview_uv_frag
            #pragma shader_feature _ TGS_EDITOR_USE_URP TGS_EDITOR_USE_HDRP
            
            ENDHLSL
        }
    }
}