using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace SymmetryBreakStudio.TastyGrassShader
{
    /// <summary>
    ///     This is the global manager for Tasty Grass Shader.
    ///     For performance reasons, rendering all instances happens here in a central place.
    ///     Before 2.0, each mesh or terrain was responsible for rendering. Since 2.0 all this work is bundled here.
    ///     This removes duplicate work (like loading the collider positions) and allows to move expensive work into Burst
    ///     kernels.
    /// </summary>
    public static class TgsManager
    {
        public const int MaxColliderPerInstance = 8;
        public const string UrpDefaultMaterialPath = "Materials/TGS_URP_Default";
        public const string HdrpDefaultMaterialPath = "Materials/TGS_HDRP_Default";

        public const string SetupMenuItem = "Tools/Symmetry Break Studio/Tasty Grass Shader/Manually Run Setup";
        public static bool Enable = true;

        static float _activeGlobalDensityValue;
        static bool _isInitialize;

        static Vector4[] _colliderBuffer;
        static readonly int TgsUseAlphaToCoverage = Shader.PropertyToID("_Tgs_UseAlphaToCoverage");

        /// <summary>
        /// Sometimes, the resource loading gets messed up and gives false positives on missing assets. By tracking how much this error occured, we improve UX.
        /// </summary>
        private static uint missingRenderMaterialErrorCount, missingResoucesErrorCount;

        public static ComputeShader tgsComputeShader { get; private set; }
        public static Material PipelineDefaultRenderingMaterial { get; private set; }

        public static Material tgsMatNoAlphaClipping { get; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void SafeInitialize()
        {
            if (!_isInitialize)
            {
                _isInitialize = true;
                _activeGlobalDensityValue = TgsGlobalSettings.GlobalDensityScale;

                Enable = true;
                RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            }
        }

        static void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            RenderForCamera(renderingCamera);
        }

        public static void RenderForCamera(Camera renderingCamera)
        {
            if (!Enable)
            {
                return;
            }

            Profiler.BeginSample("Tgs Per Camera");
            Profiler.BeginSample("Collect Data and Bake");

            if (tgsComputeShader == null)
            {
                tgsComputeShader = Resources.Load<ComputeShader>("Shaders/TastyGrassShaderCompute");
            }

            if (tgsComputeShader != null)
            {
                if (PipelineDefaultRenderingMaterial == null)
                {
#if TGS_URP_INSTALLED
                    PipelineDefaultRenderingMaterial = Resources.Load<Material>(UrpDefaultMaterialPath);
#endif

#if TGS_HDRP_INSTALLED
                    if (PipelineDefaultRenderingMaterial == null)
                    {
                        PipelineDefaultRenderingMaterial = Resources.Load<Material>(HdrpDefaultMaterialPath);
                    }
#endif
                }

                Material renderingMaterial = TgsGlobalSettings.CustomRenderingMaterial != null
                    ? TgsGlobalSettings.CustomRenderingMaterial
                    : PipelineDefaultRenderingMaterial;

                if (renderingMaterial != null)
                {
                    List<TgsInstance> tgsInstances = TgsInstance.AllInstances;

                    NativeArray<TgsInstancePreRendering> tgsInstancesNative =
                        new NativeArray<TgsInstancePreRendering>(tgsInstances.Count, Allocator.TempJob);

                    // Check if the global density has changed.
                    if (!Mathf.Approximately(_activeGlobalDensityValue, TgsGlobalSettings.GlobalDensityScale))
                    {
                        foreach (TgsInstance instance in tgsInstances)
                        {
                            instance.MarkGeometryDirty();
                        }

                        _activeGlobalDensityValue = TgsGlobalSettings.GlobalDensityScale;
                    }

                    // TODO: use a global native list instead, so there is no overhead each frame collecting the instances.
                    // Iterate trough all instances and issue bake, if geometry is dirty.
                    int bakedInstances = 0;
                    for (int i = 0; i < tgsInstances.Count; i++)
                    {
                        TgsInstance instance = tgsInstances[i];
                        if (instance.isGeometryDirty && bakedInstances < TgsGlobalSettings.GlobalMaxBakesPerFrame)
                        {
                            instance.BakeNextRecipe();
                            bakedInstances++;
                        }

                        tgsInstancesNative[i] = new TgsInstancePreRendering(instance);
                    }

                    var activeColliders = TgsCollider._activeColliders;
                    var activeCollidersNative = new NativeArray<float4>(activeColliders.Count, Allocator.TempJob);
                    for (int i = 0; i < activeColliders.Count; i++)
                    {
                        TgsCollider collider = activeColliders[i];
                        Transform transform = collider.transform;

                        float4 colliderXyzw = float4.zero;
                        colliderXyzw.xyz = transform.position;
                        colliderXyzw.w = collider.radius * collider.radius * transform.localScale.magnitude;
                        activeCollidersNative[i] = colliderXyzw;
                    }

                    var collidersOut =
                        new NativeArray<float4>(tgsInstancesNative.Length * MaxColliderPerInstance, Allocator.TempJob);
                    TgsPreRenderJob job = new()
                    {
                        Instances = tgsInstancesNative,
                        CollidersIn = activeCollidersNative,
                        CollidersOut = collidersOut,
                        CameraPosition = renderingCamera.transform.position,
                        CameraFovScalingFactor = TgsInstance.ComputeCameraFovScalingFactor(renderingCamera),
                        LodScale = TgsGlobalSettings.GlobalLodScale,
                        LodFalloffExp = TgsGlobalSettings.GlobalLodFalloffExponent
                    };

                    Profiler.EndSample();
                    Profiler.BeginSample("Execute PreRendering Job");
                    job.Schedule(tgsInstancesNative.Length, 64).Complete();
                    Profiler.EndSample();
                    Profiler.BeginSample("Submit Drawcalls");

                    _colliderBuffer ??= new Vector4[MaxColliderPerInstance];

                    // Handle Single Pass VR (if the package is installed)
                    bool singlePassVr = false;
#if TGS_UNITY_XR_MODULE_INSTALLED
                singlePassVr = renderingCamera.stereoEnabled &&
                                    UnityEngine.XR.XRSettings.stereoRenderingMode != UnityEngine.XR.XRSettings.StereoRenderingMode.MultiPass;
#endif


                    if (renderingMaterial.shader)
                    {
                        const string alphaClipKeywordString = "TGS_USE_ALPHACLIP";
                        LocalKeyword alphaClip =
                            renderingMaterial.shader.keywordSpace.FindKeyword(alphaClipKeywordString);
                        if (alphaClip.isValid)
                        {
                            renderingMaterial.SetKeyword(alphaClip, TgsGlobalSettings.NoAlphaToCoverage);
                        }

                        renderingMaterial.SetInteger(TgsUseAlphaToCoverage,
                            TgsGlobalSettings.NoAlphaToCoverage ? 0 : 1);

                        bool isPreviewCamera = renderingCamera.cameraType == CameraType.Preview;
                        //TODO: use a list instead to reduce the amount of iterations.
                        for (int index = 0; index < tgsInstancesNative.Length; index++)
                        {
                            TgsInstancePreRendering instancePreRendering = tgsInstancesNative[index];

                            if (instancePreRendering.renderingVertexCount <= 0)
                            {
                                continue;
                            }

                            if (instancePreRendering.colliderCount > 0)
                            {
                                int baseIndex = index * MaxColliderPerInstance;
                                for (int i = 0; i < instancePreRendering.colliderCount; i++)
                                {
                                    _colliderBuffer[i] = collidersOut[baseIndex + i];
                                }
                            }

                            TgsInstance instance = tgsInstances[index];

                            if (!instance.Hide) // TODO filter out invisible instances earlier.
                            {
                                instance.DrawAndUpdateMaterialPropertyBlock(instancePreRendering, renderingCamera,
                                    _colliderBuffer,
                                    instancePreRendering.colliderCount, singlePassVr, renderingMaterial);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError("Tasty Grass Shader: shader for rendering material is invalid.");
                    }

                    collidersOut.Dispose();
                    activeCollidersNative.Dispose();
                    tgsInstancesNative.Dispose();
                }
                else
                {
                    missingRenderMaterialErrorCount++;
                    if (missingRenderMaterialErrorCount == 5)
                    {
                        Debug.LogError(
                            $"Tasty Grass Shader: Please run the Tasty Grass Setup ({SetupMenuItem}). (No rendering material found. Tasty Grass Shader will not work.)");
                    }
                }
            }
            else
            {
                missingResoucesErrorCount++;
                if (missingResoucesErrorCount == 5)
                {
                    Debug.LogError(
                        "Tasty Grass Shader: unable to locate resources. Ensure that the plugin is installed correctly and that all files in the Resource folder are present. Tasty Grass Shader will not work.");
                }
            }

            Profiler.EndSample();
            Profiler.EndSample();
        }


        public struct TgsInstancePreRendering
        {
            public Bounds aabb;
            public float lodBiasByPreset;

            public int bladeCount;

            // outputs
            public int colliderCount;
            public int renderingVertexCount;


            public TgsInstancePreRendering(TgsInstance instance)
            {
                aabb = instance.tightBounds;
                lodBiasByPreset = instance.GetActiveTgsInstanceRecipeBaseLodFactor();
                colliderCount = 0;
                renderingVertexCount = 0;
                bladeCount = instance.bladeCount;
            }
        }

        [BurstCompile]
        struct TgsPreRenderJob : IJobParallelFor
        {
            public NativeArray<TgsInstancePreRendering> Instances;

            [WriteOnly] [NativeDisableParallelForRestriction]
            public NativeArray<float4> CollidersOut;

            [ReadOnly] public NativeArray<float4> CollidersIn;

            [ReadOnly] public float3 CameraPosition;
            [ReadOnly] public float CameraFovScalingFactor;
            [ReadOnly] public float LodScale;
            [ReadOnly] public float LodFalloffExp;

            // Using Bounds.DistanceSqr might not be burst compatible, so we write it ourselves.
            static float DistanceToAabbSqr(float3 pos, float3x2 aabb)
            {
                float3 pointInAABB = math.clamp(pos, aabb.c0, aabb.c1);
                return math.distancesq(pos, pointInAABB);
            }

            public void Execute(int index)
            {
                TgsInstancePreRendering instancePre = Instances[index];
                float3x2 instanceAabb = new(instancePre.aabb.min, instancePre.aabb.max);
                int writtenColliders = 0;

                // Compute LOD based on approximate screen size.
                float distance = math.sqrt(DistanceToAabbSqr(CameraPosition, instanceAabb));
                const float referenceLodSize = 32.0f;
                float objectPixelHeightApprox = referenceLodSize / distance / CameraFovScalingFactor;

                float lodIndexRaw01 = objectPixelHeightApprox * LodScale * instancePre.lodBiasByPreset;
                lodIndexRaw01 = math.pow(lodIndexRaw01, LodFalloffExp);
                int renderingBladeCount = (int)math.round(math.saturate(lodIndexRaw01) * instancePre.bladeCount);

                if (renderingBladeCount > 0)
                {
                    int baseIndex = index * MaxColliderPerInstance;

                    for (int colliderIdx = 0;
                         colliderIdx < CollidersIn.Length && writtenColliders < MaxColliderPerInstance;
                         colliderIdx++)
                    {
                        float4 collider = CollidersIn[colliderIdx];
                        float3 colliderCenter = collider.xyz;
                        float colliderRadiusSqr = collider.w;

                        if (DistanceToAabbSqr(colliderCenter, instanceAabb) < colliderRadiusSqr)
                        {
                            CollidersOut[baseIndex + writtenColliders] =
                                new float4(colliderCenter.x, colliderCenter.y, colliderCenter.z,
                                    math.sqrt(colliderRadiusSqr));

                            writtenColliders++;
                        }
                    }
                }

                instancePre.colliderCount = writtenColliders;
                instancePre.renderingVertexCount = renderingBladeCount * 3;

                Instances[index] = instancePre; // TODO: seperate in/out?
            }
        }
    }
}