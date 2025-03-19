Shader "Hidden/Symmetry Break Studio/Tasty Grass Shader/URP"
{
    Properties
    {
        [PerRendererData] _Tgs_UseAlphaToCoverage("_Tgs_UseAlphaToCoverage", Integer) = 0
        [PerRendererData] _MainTex ("Shape Texture", 2D) = "white" {}
        
        [Header(Debug)]
        [PerRendererData] _PositionBoundMin("BoundsMin", Vector) = (0,0,0,0)
        [PerRendererData] _PositionBoundMax("BoundsMax", Vector) = (0,0,0,0)     
        [Toggle(_DebugShowBarycentric)] _DebugShowBarycentric("Show Barycentric", int) = 0
        [Toggle(_DebugDisableWind)] _DebugDisableWind("Disable Wind", int) = 0
        [Toggle(_ShowBezierDistance)] _ShowBezierDistance("Show Bezier Distance", int) = 0
        _DebugBarycentricIso("Barycentric Isolation", Vector) = (1,1,1,1)
    }
    SubShader
    {
                    
        Cull Off
        AlphaToMask [_Tgs_UseAlphaToCoverage]
        HLSLINCLUDE
            //#define GRASS_DEBUG_MODE
            //#pragma enable_d3d11_debug_symbols
            
            #include "../Shaders/TastyGrassShaderCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct TgsFragmentInput {
                float4 vertex : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 barycentricCoord: TEXCOORD1;
                #ifdef TGS_USE_REFERENCE_NODE_FORMAT
                    float3 normal : NORMAL;
                    float3 color : COLOR;
                #else
                    int bin_normalXY_colorRGB : NORMAL;
                #endif
                
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    half4 fogFactorAndVertexLight : TEXCOORD2;
                #else
                    half fogFactor : TEXCOORD2;
                #endif

                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 3);
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Unity URP specific functions
		    // =============================================================================================================
            SurfaceData TgsGetUrpSurfaceData(float3 barycentricCoord, int packedNormalAncColor)
            {
                SurfaceData surfaceData = (SurfaceData)0;
                TgsGetSurfaceParameters(
                    barycentricCoord,
                    packedNormalAncColor,
                    /*out*/ surfaceData.albedo,
                    /*out*/ surfaceData.alpha,
                    /*out*/ surfaceData.smoothness,
                    /*out*/ surfaceData.occlusion);

                return surfaceData;
            }

            InputData TgsGetUrpInputData(TgsFragmentInput input)
            {
                InputData inputData = (InputData) 0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.vertex;
                inputData.normalWS = TgsUnpackNormal(input.bin_normalXY_colorRGB);
                
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
                    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
                #else
                    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                #endif
                                
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) || defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS); //NOTE: shadow coord is always computed in frag.
                #endif

            
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.vertex);
                
                #if defined(DYNAMICLIGHTMAP_ON)
                    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
                #else
                    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                #endif

                #if defined(DEBUG_DISPLAY)
                #if defined(DYNAMICLIGHTMAP_ON)
                inputData.dynamicLightmapUV = input.dynamicLightmapUV;
                #endif
                #if defined(LIGHTMAP_ON)
                inputData.staticLightmapUV = input.staticLightmapUV;
                #else
                inputData.vertexSH = input.vertexSH;
                #endif
                #endif

                return inputData;
            }

            
 
            
            // Vertex - Common Vertex Shader
		    // =============================================================================================================

            float3 _LightDirection;
			float3 _LightPosition;
            TgsFragmentInput tgsCommonVertexShader(Attributes input, uint vertexID: SV_VertexID) {
                TgsFragmentInput output = (TgsFragmentInput) 0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                float3 positionWS;
                int packedNormalColor;
                float3 barycentricCoord;
                TgsGetGrassVertex(vertexID, _Time.x, 1.0, /*out*/ positionWS, /*out*/ packedNormalColor, /*out*/ barycentricCoord);
                float3 normalWS = TgsUnpackNormal(packedNormalColor);
                
                #ifdef _TGS_SHADOW_PASS
                	#if _CASTING_PUNCTUAL_LIGHT_SHADOW
					    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
				    #else
					    float3 lightDirectionWS = _LightDirection;
				    #endif
                   output.vertex = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #else
                  output.vertex = TransformWorldToHClip(positionWS);
                #endif
             
                
                output.positionWS = positionWS;
                output.bin_normalXY_colorRGB = packedNormalColor;
                output.barycentricCoord = barycentricCoord;

                // ---------------------
                // Unity URP specific
                // ---------------------
            
                OUTPUT_SH(normalWS.xyz, output.vertexSH);
                
                half3 vertexLight = VertexLighting(output.positionWS, normalWS);
                
                half fogFactor = 0;
                #if !defined(_FOG_FRAGMENT)
                    fogFactor = ComputeFogFactor(output.vertex.z);
                #endif
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
                #else
                    output.fogFactor = fogFactor;
                #endif

                return output;
            }
            
            // Fragment - Forward
		    // =============================================================================================================
            half4 frag_forward(TgsFragmentInput input) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #ifdef GRASS_DEBUG_MODE
                if(_DebugShowBarycentric > 0) {
                    return float4(input.barycentricCoord.xyz * _DebugBarycentricIso.xyz, 1.0);
                }
                #endif
              
                #ifdef GRASS_DEBUG_MODE
                if(_ShowBezierDistance)
                {
                    float color = alpha;
                    return saturate(float4(color.xxx,  1.0));
                }
                #endif
                

                InputData urpInputData = TgsGetUrpInputData(input);
                SurfaceData urpSurfaceData = TgsGetUrpSurfaceData(input.barycentricCoord, input.bin_normalXY_colorRGB);

            #ifdef _DBUFFER
                ApplyDecalToSurfaceData(input.vertex, urpSurfaceData, urpInputData);
            #endif

                float4 color = UniversalFragmentPBR(urpInputData, urpSurfaceData);
                color.rgb = MixFog(color.rgb, urpInputData.fogCoord);
                
                color.a = urpSurfaceData.alpha;

                return TgsPreReturn(color);
            }
            
            // Fragment - Depth-Normal
		    // =============================================================================================================
            half4 frag_depth_normal(TgsFragmentInput input) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                const InputData urpInputData = TgsGetUrpInputData(input);
                const SurfaceData urpSurfaceData = TgsGetUrpSurfaceData(input.barycentricCoord, input.bin_normalXY_colorRGB);
                clip(urpSurfaceData.alpha - 0.5);
                return float4(urpInputData.normalWS, urpSurfaceData.alpha);
            }
            
            // Fragment - Depth-Only
		    // =============================================================================================================
            half4 frag_depth_only(TgsFragmentInput input) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                const SurfaceData urpSurfaceData = TgsGetUrpSurfaceData(input.barycentricCoord, input.bin_normalXY_colorRGB);
                return TgsPreReturn(float4(0.0, 0.0, 0.0, urpSurfaceData.alpha));
            }
            
            // Fragment - ShadowCaster
		    // =============================================================================================================
            half4 frag_shadow_caster(TgsFragmentInput input) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                const SurfaceData urpSurfaceData = TgsGetUrpSurfaceData(input.barycentricCoord, input.bin_normalXY_colorRGB);
                clip(urpSurfaceData.alpha - 0.5);
                return 0; //TODO: check if alpha to coverage
            }

            // Fragment - Deferred
		    // =============================================================================================================
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
            FragmentOutput frag_gbuffer(TgsFragmentInput input) {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                SurfaceData urpSurfaceData = TgsGetUrpSurfaceData(input.barycentricCoord, input.bin_normalXY_colorRGB);

                clip(urpSurfaceData.alpha - 0.5);
               
                
                InputData inputData = TgsGetUrpInputData(input);
            #ifdef _DBUFFER
                ApplyDecalToSurfaceData(input.vertex, urpSurfaceData, inputData);
            #endif

                BRDFData brdfData = (BRDFData) 0;
                InitializeBRDFData(urpSurfaceData.albedo, urpSurfaceData.metallic, urpSurfaceData.specular, urpSurfaceData.smoothness, urpSurfaceData.alpha, brdfData);
                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);


                MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);
                half3 color = GlobalIllumination(brdfData, inputData.bakedGI, urpSurfaceData.occlusion, inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);
                
                return BRDFDataToGbuffer(brdfData, inputData, urpSurfaceData.smoothness, urpSurfaceData.emission + color, urpSurfaceData.occlusion);
            }
        ENDHLSL

        Tags{"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode" = "UniversalForward"}

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 4.5
            
            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            
            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTERED_RENDERING

            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3 //Add support for DBuffer Decals.
            #pragma multi_compile _ _FORWARD_PLUS // Add support for Forward+ in Unity 2022+

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            // -------------------------------------
            // Tasty Grass Shader Keywords
            #pragma multi_compile _ TGS_USE_ALPHACLIP
            
            #pragma vertex tgsCommonVertexShader
            #pragma fragment frag_forward

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}
            
            ColorMask 0
            AlphaToMask Off
            
            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5
            
            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _TGS_SHADOW_PASS


            // -------------------------------------
            // Universal Pipeline keywords

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            
            #pragma vertex tgsCommonVertexShader
            #pragma fragment frag_shadow_caster
            //#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
          //  #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            
            #pragma vertex tgsCommonVertexShader
            #pragma fragment frag_depth_only

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            // -------------------------------------
            // Tasty Grass Shader Keywords
            #pragma multi_compile _ TGS_USE_ALPHACLIP

            
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags{"LightMode" = "DepthNormals"}

            ZWrite On

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma vertex tgsCommonVertexShader
            #pragma fragment frag_depth_normal

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            
            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            
            
            ENDHLSL
        }

        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "GBuffer"
            Tags{"LightMode" = "UniversalGBuffer"}
            
            AlphaToMask Off
            ZWrite On
            ZTest LEqual
            
            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5
            
            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            //#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            //#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            

            #pragma vertex tgsCommonVertexShader
            #pragma fragment frag_gbuffer
            
            ENDHLSL
        }
    }
}
