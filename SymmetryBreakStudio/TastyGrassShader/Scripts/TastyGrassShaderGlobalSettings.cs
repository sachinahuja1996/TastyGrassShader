#if TGS_URP_INSTALLED
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SymmetryBreakStudio.TastyGrassShader
{
    public class TastyGrassShaderGlobalSettings : ScriptableRendererFeature
    {
        public static TastyGrassShaderGlobalSettings LastActiveInstance;

        [Header("Visual")]
        [Tooltip(
            "Optional custom material for rendering. If None/Null the default internal material will be used. Helpful, when using custom lighting models or other assets/effects that affect the global rendering are used. See the TgsAmplify")]
        public Material customRenderingMaterial;

        [Tooltip(
            "Fixes alpha issues with XR by disabling alpha to coverage and using simple alpha clipping instead. Note that this prevents MSAA from working with the grass. May only work with the default TGS shader/customRenderingMaterial is set to null.")]
        public bool noAlphaToCoverage;

        [Header("Performance & Quality")]
        [Tooltip("The maximum amount of instances that are baked per frame.")]
        [Min(1)]
        public int maxBakesPerFrame = 32;

        [Tooltip("Global multiplication for the amount value.")] [Range(0.001f, 2.0f)]
        public float densityScale = 1.0f;

        [Tooltip(
            "The exponent for the internal LOD factor. Higher values will reduce the amount of blades visible at distance. This can be used to improve performance.")]
        [Range(0.5f, 10.0f)]
        public float lodFalloffExponent = 2.5f;

        [Tooltip("Global multiplication for of the LOD.")] [Range(0.001f, 4.0f)]
        public float lodScale = 1.0f;

        public override void Create()
        {
        }


        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            LastActiveInstance = this;
            // Apply the settings from this Scriptable Render Feature to the internal TgsGlobalSettings
            TgsGlobalSettings.GlobalDensityScale = densityScale;
            TgsGlobalSettings.GlobalLodScale = lodScale;
            TgsGlobalSettings.GlobalLodFalloffExponent = lodFalloffExponent;
            TgsGlobalSettings.GlobalMaxBakesPerFrame = maxBakesPerFrame;
            TgsGlobalSettings.CustomRenderingMaterial = customRenderingMaterial;
            TgsGlobalSettings.NoAlphaToCoverage = noAlphaToCoverage;
        }
    }
}
#endif