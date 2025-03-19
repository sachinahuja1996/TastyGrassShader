using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;

namespace SymmetryBreakStudio.TastyGrassShader
{
    [CreateAssetMenu(menuName = "Symmetry Break Studio/Tasty Grass Shader/Preset")]
    [HelpURL("https://github.com/SymmetryBreakStudio/TastyGrassShader/wiki")]
    public class TgsPreset : ScriptableObject
    {
        public enum BladeTextureUvLayout
        {
            TriangleSingle,

            TriangleCenter
            // TrianglePair,//TODO: in the future support TwoTriangles,
        }

        static NoiseSettingGPU[] _noiseLayerBuffers;

        static readonly int Density = Shader.PropertyToID("_Density");
        static readonly int ClumpCount = Shader.PropertyToID("_ClumpCount");
        static readonly int BladeLimpness = Shader.PropertyToID("_BladeLimpness");
        static readonly int GrowDirectionBySurfaceNormal = Shader.PropertyToID("_GrowDirectionBySurfaceNormal");
        static readonly int BladeScaleThreshold = Shader.PropertyToID("_BladeScaleThreshold");

        static readonly int ArcUp = Shader.PropertyToID("_ArcUp");
        static readonly int ArcDown = Shader.PropertyToID("_ArcDown");
        static readonly int TipRounding = Shader.PropertyToID("_TipRounding");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int Scruffiness = Shader.PropertyToID("_Scruffiness");
        static readonly int WindIntensityScale = Shader.PropertyToID("_WindIntensityScale");
        static readonly int NoiseTexture0 = Shader.PropertyToID("_NoiseTexture0");
        static readonly int NoiseTexture1 = Shader.PropertyToID("_NoiseTexture1");
        static readonly int NoiseTexture2 = Shader.PropertyToID("_NoiseTexture2");
        static readonly int NoiseTexture3 = Shader.PropertyToID("_NoiseTexture3");
        static readonly int GrowDirection = Shader.PropertyToID("_GrowDirection");
        static readonly int WindPivotOffsetByHeight = Shader.PropertyToID("_WindPivotOffsetByHeight");
        static readonly int TwirlStrength = Shader.PropertyToID("_TwirlStrength");
        static readonly int PostVerticalScale = Shader.PropertyToID("_PostVerticalScale");
        static readonly int StemGenerate = Shader.PropertyToID("_StemGenerate");
        static readonly int StemColor = Shader.PropertyToID("_StemColor");
        static readonly int StemThickness = Shader.PropertyToID("_StemThickness");
        static readonly int ColliderInfluence = Shader.PropertyToID("_ColliderInfluence");

        static readonly int GrowSlopeLimit = Shader.PropertyToID("_GrowSlopeLimit");


        static readonly int ProceduralShapeBlend = Shader.PropertyToID("_ProceduralShapeBlend");
        static readonly int FakeThicknessIntensity = Shader.PropertyToID("_FakeThicknessIntensity");
        static readonly int OcclusionByHeight = Shader.PropertyToID("_OcclusionByHeight");
        static readonly int UvUseCenterTriangle = Shader.PropertyToID("_UvUseCenterTriangle");
        static readonly int MainTex = Shader.PropertyToID("_MainTex");
        static readonly int ProcedualShapeParams = Shader.PropertyToID("_ProcedualShapeParams");
        static readonly int ShadingParams = Shader.PropertyToID("_ShadingParams");
        static readonly int UvSettings = Shader.PropertyToID("_UvSettings");
        static readonly int PhysicalParamers = Shader.PropertyToID("_PhysicalParamers");

        static readonly int ClumpPercentage = Shader.PropertyToID("_ClumpPercentage");
        static readonly int ClumpLimitVariation = Shader.PropertyToID("_ClumpLimitVariation");

        [Header("Grow Settings")] [Tooltip("How much grass to create approximately per square meter.")] [Min(0.0f)]
        public float density = 60;

        [Tooltip(
            "Maximum allowed angle between normal and grow direction.")]
        [Range(0.0f, 180.0f)]
        public float angleLimit = 85.0f;

        [Tooltip(
            "The growing direction of the grass. Other values than (0, 1, 0) can be used to create ground clutter.")]
        public Vector3 growDirection = Vector3.up;

        [Tooltip("Whether to grow in the upwards direction (at 0.0) or the surface normals (at 1.0).")]
        [Range(0.0f, 1.0f)]
        public float growDirectionBySurfaceNormal;

        [Tooltip("If a blade is smaller than this threshold, it will be removed.")] [Min(0.0f)]
        public float minimalHeight = 0.05f;

        [Header("Clumping")]
        [Tooltip(
            "How many blades should be created at the same position. Note that blades can't be created across multiple triangles.")]
        [Min(1)]
        public int clumpCount = 2;

        [Tooltip("How strong the clumping is.")] [Range(0.0f, 1.0f)]
        public float clumpPercentage = 0.9f;

        [Tooltip(
            "If active, noise layers will used the clumped position. This is usefully for objects that are formed from multiple blades, such as some flowers.")]
        public bool clumpLimitNoiseVariation;


        [Header("Stem")]
        [Tooltip("Generates a stem. Useful for flowers or other plants. Will create one stem per clump.")]
        public bool stemGenerate;

        [Tooltip("The color of the stem.")] [DisableGroup(nameof(stemGenerate))] [ColorUsage(false)]
        public Color stemColor = new(0.396f, 0.506f, 0.133f);

        [Tooltip("The thickness of the stem in relation to the blade thickness.")] [DisableGroup(nameof(stemGenerate))]
        public float stemThicknessRatio = 0.25f;

        [Header("Detail Layers")] [Tooltip("The noise texture used for Layer 0.")] [Obsolete] [HideInInspector]
        public Texture3D noiseTexture0;

        [Header("Detail Layers")] [Tooltip("The settings used for Layer 0.")]
        public NoiseSetting noiseLayer0 = new()
        {
            tiling = 30.0f,
            valueScale = 3f,
            valueOffset = -0.25f,
            height = new Vector2(0.15f, 0.8f),
            offset = new Vector2(-0.35f, -0.15f),
            angle = new Vector2(0.02f, 0.2f),
            thickness = new Vector2(-0.5f, -.5f),
            skew = new Vector2(-0.0f, -.25f),
            colorInfluence = new Vector2(1f, .1f),
            color = new Color(0.3942137f, 0.5058824f, 0.1320353f)
        };

        [Space] [Tooltip("The noise texture used for Layer 1.")] [Obsolete] [HideInInspector]
        public Texture3D noiseTexture1;

        [Tooltip("The settings used for Layer 1.")]
        public NoiseSetting noiseLayer1 = NoiseSetting.GetDefault(true);

        [Space] [Tooltip("The noise texture used for Layer 2.")] [Obsolete] [HideInInspector]
        public Texture3D noiseTexture2;

        [Tooltip("The settings used for Layer 2.")]
        public NoiseSetting noiseLayer2 = NoiseSetting.GetDefault(true);

        [Space] [Tooltip("The noise texture used for Layer 3.")] [Obsolete] [HideInInspector]
        public Texture3D noiseTexture3;

        [Tooltip("The settings used for Layer 3.")]
        public NoiseSetting noiseLayer3 = NoiseSetting.GetDefault(true);


        [Header("Effects")]
        [Tooltip(
            "How much the blade is twisted around its center. Even small values greatly enhance the voluminousness of a grass field.")]
        [Range(0.0f, 0.25f)]
        public float twirl = 0.03f;

        [FormerlySerializedAs("verticalScale")]
        [Tooltip(
            "How much the blade vertices are flattened, based on the ground normal. Can be used to create ground clutter.")]
        public float flatten;

        [Tooltip("Randomization of growing direction. Also affects shading normals.")] [Range(0.0f, 2.0f)]
        public float scruffiness;


        [Tooltip(
            "How much the tip should be moved back to the ground. Moderate values are good to create ground clutter.")]
        [Range(0.0f, 1.0f)]
        public float limpness;

        [Header("Blade Shape")]
        [Tooltip("The optional texture to use.")]
        [UvMappingTexture(nameof(textureUvLayout), nameof(proceduralShapeBlend), nameof(upperArc), nameof(lowerArc),
            nameof(tipRounding))]
        public Texture2D texture;

        [FormerlySerializedAs("textureUvStyle")] [Tooltip("What UV layout to use.")]
        public BladeTextureUvLayout textureUvLayout;

        [Tooltip("Controls the mix between the procedural shape or the texture.")] [Range(0.0f, 1.0f)]
        public float proceduralShapeBlend;

        [Tooltip("The strength of the upper arc. Lower values lead to stronger arcs.")]
        [DisableGroup(nameof(proceduralShapeBlend), false, true)]
        [Range(0.0f, 4.0f)]
        public float upperArc = 0.03f;

        [Tooltip("The strength of the lower arc. Higher values lead to stronger arcs.")]
        [DisableGroup(nameof(proceduralShapeBlend), false, true)]
        [Range(0.0f, 4.0f)]
        public float lowerArc = 3.5f;

        [Tooltip("Values greater than 0 will introduce a more rounded shape. Helpful for stylization.")]
        [DisableGroup(nameof(proceduralShapeBlend), false, true)]
        [Range(0.0f, 0.1f)]
        public float tipRounding;

        [Header("Wind")]
        [Tooltip(
            "Scaling factor for the current wind settings. A value of 0 will disable wind interaction entirely, which is useful for ground clutter.")]
        [Min(0.0f)]
        public float windIntensityScale = 1.0f;

        [Tooltip(
            "The offset of the pivot along the growing direction, used for rotating the grass blades in the wind. If you create things like flowers that have height offset applied to them, use this value to make the rotation look correct again.")]
        public float windPivotOffset;

        [Header("Shading")] [Tooltip("The PBR smoothness used for shading.")] [Range(0.0f, 1.0f)]
        public float smoothness;

        [Tooltip("Strength of the occlusion factor that is guessed by the height of the grass blade.")]
        [Range(0.0f, 1.0f)]
        public float occlusionByHeight = 1.0f;

        [Space] [Tooltip("How strongly the blades should be pushed away by colliders.")] [Range(0.0f, 1.0f)]
        public float colliderInfluence = 1.0f;

        [Header("Runtime Quality")]
        [Min(0.0f)]
        [Tooltip(
            "The base LOD height for this bake setting. For settings that create smaller blades, also use smaller values here.")]
        public float baseLodFactor = 1f;

        [Tooltip("Whether to cast shadows.")] public bool castShadows = true;


        /// <summary>
        ///     A list of TgsInstances that are using this preset. If the preset is changed, all the instances are set dirty.
        /// </summary>
        [NonSerialized] [HideInInspector] public HashSet<TgsInstance> SetDirtyOnChangeList = new();

        public Hash128 currentBakeSettingsHash { get; private set; }
        public Hash128 currentRenderingSettingsHash { get; private set; }


        void OnValidate()
        {
            ApplyChanges();
            UpdateFromV1ToV2();
        }

        public void UpdateFromV1ToV2()
        {
#pragma warning disable CS0612 // Type or member is obsolete
            if (noiseLayer0.texture == null && noiseTexture0 != null)
            {
                noiseLayer0.texture = noiseTexture0;
            }

            if (noiseLayer1.texture == null && noiseTexture1 != null)
            {
                noiseLayer1.texture = noiseTexture1;
            }

            if (noiseLayer2.texture == null && noiseTexture2 != null)
            {
                noiseLayer2.texture = noiseTexture2;
            }

            if (noiseLayer3.texture == null && noiseTexture3 != null)
            {
                noiseLayer3.texture = noiseTexture3;
            }
#pragma warning restore CS0612 // Type or member is obsolete
        }


        public void ApplyToComputeShader(ComputeShader tgsComputeShader, Settings settings,
            int csBakePassId)
        {
            tgsComputeShader.SetFloat(Density,
                density * TgsGlobalSettings.GlobalDensityScale * settings.amount);
            tgsComputeShader.SetInt(ClumpCount, clumpCount);
            tgsComputeShader.SetFloat(ClumpPercentage, clumpPercentage);
            tgsComputeShader.SetFloat(ClumpLimitVariation, clumpLimitNoiseVariation ? 1.0f : 0.0f);

            tgsComputeShader.SetFloat(Scruffiness, scruffiness);
            tgsComputeShader.SetFloat(BladeLimpness, limpness);
            tgsComputeShader.SetFloat(TwirlStrength, twirl);
            tgsComputeShader.SetFloat(PostVerticalScale, flatten);
            tgsComputeShader.SetFloat(GrowDirectionBySurfaceNormal, growDirectionBySurfaceNormal);
            tgsComputeShader.SetFloat(GrowSlopeLimit,
                Mathf.Cos(Mathf.Clamp(angleLimit + settings.slopeBias, 0.0f, 180.0f) * Mathf.Deg2Rad));
            tgsComputeShader.SetVector(GrowDirection, growDirection);

            tgsComputeShader.SetInt(StemGenerate, stemGenerate ? 1 : 0);
            tgsComputeShader.SetVector(StemColor, stemColor);
            tgsComputeShader.SetFloat(StemThickness, stemThicknessRatio);


            tgsComputeShader.SetFloat(BladeScaleThreshold, minimalHeight * settings.height);

            // TODO: get proper 3D dummy texture
            tgsComputeShader.SetTexture(csBakePassId, NoiseTexture0,
                noiseLayer0.texture == null ? Texture2D.whiteTexture : noiseLayer0.texture);
            tgsComputeShader.SetTexture(csBakePassId, NoiseTexture1,
                noiseLayer1.texture == null ? Texture2D.whiteTexture : noiseLayer1.texture);
            tgsComputeShader.SetTexture(csBakePassId, NoiseTexture2,
                noiseLayer2.texture == null ? Texture2D.whiteTexture : noiseLayer2.texture);
            tgsComputeShader.SetTexture(csBakePassId, NoiseTexture3,
                noiseLayer3.texture == null ? Texture2D.whiteTexture : noiseLayer3.texture);
        }

        public void ApplyLayerSettingsToBuffer(ComputeBuffer buffer, Settings settings)
        {
            if (_noiseLayerBuffers is not { Length: NoiseSettingGPU.MaxCount })
            {
                _noiseLayerBuffers = new NoiseSettingGPU[NoiseSettingGPU.MaxCount];
            }


            bool anyLayerSolo = noiseLayer0.solo || noiseLayer1.solo || noiseLayer2.solo || noiseLayer3.solo;

            _noiseLayerBuffers[0] =
                new NoiseSettingGPU(HandleNoiseSettingSolo(noiseLayer0, anyLayerSolo), settings);
            _noiseLayerBuffers[1] =
                new NoiseSettingGPU(HandleNoiseSettingSolo(noiseLayer1, anyLayerSolo), settings);
            _noiseLayerBuffers[2] =
                new NoiseSettingGPU(HandleNoiseSettingSolo(noiseLayer2, anyLayerSolo), settings);
            _noiseLayerBuffers[3] =
                new NoiseSettingGPU(HandleNoiseSettingSolo(noiseLayer3, anyLayerSolo), settings);
            buffer.SetData(_noiseLayerBuffers);
        }

        static NoiseSetting HandleNoiseSettingSolo(NoiseSetting noiseSetting, bool anySolo)
        {
            if (anySolo)
            {
                return noiseSetting.solo ? noiseSetting : NoiseSetting.GetDefault(true);
            }

            return noiseSetting;
        }


        public void ApplyToMaterialPropertyBlock(MaterialPropertyBlock materialPropertyBlock)
        {
            Profiler.BeginSample("TgsBakeSettings.ApplyToMaterialPropertyBlock");
            materialPropertyBlock.SetFloat(ArcUp, upperArc);
            materialPropertyBlock.SetFloat(ArcDown, lowerArc);
            materialPropertyBlock.SetFloat(TipRounding, tipRounding);
            materialPropertyBlock.SetFloat(ProceduralShapeBlend, proceduralShapeBlend);

            materialPropertyBlock.SetFloat(Smoothness, smoothness);
            materialPropertyBlock.SetFloat(OcclusionByHeight, occlusionByHeight);
            materialPropertyBlock.SetFloat(WindPivotOffsetByHeight, windPivotOffset);
            materialPropertyBlock.SetFloat(WindIntensityScale, windIntensityScale);
            materialPropertyBlock.SetFloat(ColliderInfluence, colliderInfluence);

            materialPropertyBlock.SetFloat(UvUseCenterTriangle,
                textureUvLayout == BladeTextureUvLayout.TriangleCenter ? 1.0f : 0.0f);

            materialPropertyBlock.SetTexture(MainTex, texture == null ? Texture2D.whiteTexture : texture);

            // TODO: implement these
            // materialPropertyBlock.SetVector(ProcedualShapeParams, new Vector4(upperArc, lowerArc, tipRounding, proceduralShapeBlend));
            // materialPropertyBlock.SetVector(ShadingParams, new Vector4(smoothness, occlusionByHeight));
            // materialPropertyBlock.SetVector(UvSettings, new Vector4(textureUvStyle == BladeTextureUvStyle.TriangleCenter ? 1.0f : 0.0f, 0.0f));
            // materialPropertyBlock.SetVector(PhysicalParamers, new Vector4(WindPivotOffsetByHeight, windIntensityScale /*TODO: just combine with wind intensity*/, colliderInfluence));
            //

            Profiler.EndSample();
        }


        /// <summary>
        ///     Recomputes the hash of the settings that are used to create the grass.
        ///     Call this function after you changed settings via code, so that the system will recompute the affected geometry
        ///     automatically.
        /// </summary>
        public void ApplyChanges()
        {
            {
                Hash128 bakeSettingsHash = new();
                float finalDensity = density * TgsGlobalSettings.GlobalDensityScale;
                bakeSettingsHash.Append(finalDensity);
                bakeSettingsHash.Append(clumpCount);
                bakeSettingsHash.Append(clumpPercentage);
                bakeSettingsHash.Append(ref clumpLimitNoiseVariation);

                bakeSettingsHash.Append(scruffiness);
                bakeSettingsHash.Append(limpness);
                bakeSettingsHash.Append(minimalHeight);
                bakeSettingsHash.Append(twirl);
                bakeSettingsHash.Append(flatten);
                bakeSettingsHash.Append(ref stemColor);
                bakeSettingsHash.Append(ref stemGenerate);
                bakeSettingsHash.Append(stemThicknessRatio);

                Hash128 noise0Hash = noiseLayer0.GetHash128();
                bakeSettingsHash.Append(ref noise0Hash);

                Hash128 noise1Hash = noiseLayer1.GetHash128();
                bakeSettingsHash.Append(ref noise1Hash);

                Hash128 noise2Hash = noiseLayer2.GetHash128();
                bakeSettingsHash.Append(ref noise2Hash);

                Hash128 noise3Hash = noiseLayer3.GetHash128();
                bakeSettingsHash.Append(ref noise3Hash);

                bakeSettingsHash.Append(growDirectionBySurfaceNormal);
                bakeSettingsHash.Append(growDirection.x);
                bakeSettingsHash.Append(growDirection.y);
                bakeSettingsHash.Append(growDirection.z);
                bakeSettingsHash.Append(angleLimit);
                // newHash.Append(noiseTexture0 != null ? noiseTexture0.GetInstanceID() : string.Empty);
                // newHash.Append(noiseTexture1 != null ? noiseTexture1.GetInstanceID : string.Empty);
                // newHash.Append(noiseTexture2 != null ? noiseTexture2.GetInstanceID : string.Empty);
                // newHash.Append(noiseTexture3 != null ? noiseTexture3.GetInstanceID() : string.Empty);
                if (currentBakeSettingsHash != bakeSettingsHash)
                {
                    foreach (TgsInstance instance in SetDirtyOnChangeList)
                    {
                        if (instance == null)
                        {
                            Debug.LogError(
                                "Caught null instance. This should not happen, and indicates a bug in clean up code.");
                            continue;
                        }

                        instance.MarkGeometryDirty();
                    }
                }

                currentBakeSettingsHash = bakeSettingsHash;
            }
            {
                Hash128 renderSettingsHash = new();
                renderSettingsHash.Append(ref upperArc);
                renderSettingsHash.Append(ref lowerArc);
                renderSettingsHash.Append(ref tipRounding);
                renderSettingsHash.Append(ref proceduralShapeBlend);
                renderSettingsHash.Append(ref smoothness);
                renderSettingsHash.Append(ref occlusionByHeight);
                renderSettingsHash.Append(ref windPivotOffset);
                renderSettingsHash.Append(ref windIntensityScale);
                renderSettingsHash.Append(ref colliderInfluence);
                renderSettingsHash.Append(ref textureUvLayout);
                renderSettingsHash.Append(texture == null ? 0 : texture.GetInstanceID());

                if (currentRenderingSettingsHash != renderSettingsHash)
                {
                    foreach (TgsInstance instance in SetDirtyOnChangeList)
                    {
                        if (instance == null)
                        {
                            Debug.LogError(
                                "Caught null instance. This should not happen, and indicates a bug in clean up code.");
                            continue;
                        }

                        instance.MarkMaterialDirty();
                    }
                }

                currentRenderingSettingsHash = renderSettingsHash;
            }
        }

        [Serializable]
        public struct NoiseSetting
        {
            [Tooltip("If set to true, this layer will not be applied.")] [IntAsCheckbox]
            public int
                disable; // Using an int instead of a bool, so the struct can be send straight to the GPU. (It used to be like this pre 2.0)

            [Tooltip("Isolate this layer.")] public bool solo;

            [Tooltip("The noise texture used. Must not be null.")] [DisplayNoiseTexture]
            public Texture3D texture;

            [Space]
            [Tooltip(
                "Tiling factor of the noise texture. Larger values will lead to more repetition. Note that the final values gets divided by 100.")]
            [DisableGroup(nameof(disable), true)]
            public float tiling;

            [Tooltip(
                "Scale applied to the raw noise value texture, before being passed to the modifiers. Practically the same as a contrast modifier in image editing.")]
            [DisableGroup(nameof(disable), true)]
            public float valueScale;

            [Tooltip(
                "Offset applied to the raw noise value texture, before being passed to the modifiers. Practically the same as a brightness modifier in image editing.")]
            [DisableGroup(nameof(disable), true)]
            public float valueOffset;

            //[Header("Modifiers"), TrianglePreview(nameof(height), nameof(width), nameof(thickness), nameof(thicknessApex))] public bool Dummy;
            [Header("Modifiers")]
            [Tooltip(
                "The height of a grass blade. Negative values will internally be clamped to 0.0, so you can have negative numbers here for masking.")]
            [DisableGroup(nameof(disable), true)]
            [MinMax(-2.0f, 2.0f, true)]
            public Vector2 height;

            [Tooltip(
                "How far the blade should be created from the geometry. Negative values will internally be clamped to > 0.0, so you can have negative numbers here for masking. You can use this settings to get experimental, for example to create simple flowers (see the flowers preset for that).")]
            [DisableGroup(nameof(disable), true)]
            [MinMax(-2f, 2f, true)]
            public Vector2 offset;

            [Space]
            [FormerlySerializedAs("width")]
            [Tooltip(
                "The side-extending width of a blade. Negative values will internally be clamped to 0.0, so you can have negative numbers here for masking.")]
            [DisableGroup(nameof(disable), true)]
            [MinMax(-1f, 1f, true)]
            public Vector2 angle;

            [Space]
            [Tooltip(
                "The thickness of a blade. Negative values will lead to inwards bended blades, while positive to outwards bended.")]
            [DisableGroup(nameof(disable), true)]
            [MinMax(-0.5f, 0.5f)]
            public Vector2 thickness;

            [FormerlySerializedAs("thicknessApex")]
            [Tooltip("The apex or mid-point of the thickness.")]
            [DisableGroup(nameof(disable), true)]
            [MinMax(-1.0f, 1.0f)]
            public Vector2 skew;

            [Space]
            [Tooltip(
                "How much the color property will blend in color to the blade. Negative values will internally be clamped to 0.0, so you can have negative numbers here for masking.")]
            [DisableGroup(nameof(disable), true)]
            [MinMax(-1.0f, 1.0f, true)]
            public Vector2 colorInfluence;

            [Tooltip("The color to blend in on top of the grass blade, controlled by Color Influence.")]
            [DisableGroup(nameof(disable), true)]
            [ColorUsage(false)]
            public Color color;

            public static NoiseSetting GetDefault(bool withDisabled = false)
            {
                NoiseSetting ns = new()
                {
                    disable = withDisabled ? 1 : 0,
                    tiling = 1.0f,
                    valueScale = 1.0f,
                    color = Color.white
                };

                return ns;
            }

            public Hash128 GetHash128()
            {
                Hash128 newHash = new();
                newHash.Append(disable);
                newHash.Append(ref solo);


                newHash.Append(texture == null ? 0 : texture.GetInstanceID());
                newHash.Append(tiling);
                newHash.Append(valueScale);
                newHash.Append(valueOffset);
                newHash.Append(ref height);
                newHash.Append(ref offset);
                newHash.Append(ref angle);
                newHash.Append(ref thickness);
                newHash.Append(ref skew);
                newHash.Append(ref colorInfluence);
                newHash.Append(ref color);
                return newHash;
            }
        }

        /// <summary>
        ///     This is the GPU usable variant of NoiseSettings.
        /// </summary>
        public struct NoiseSettingGPU
        {
            const int FloatFields = 4;
            const int Vector2Fields = 6;
            const int ColorFields = 1;

            public const int Stride = sizeof(float) * FloatFields + sizeof(float) * 2 * Vector2Fields +
                                      sizeof(float) * 4 * ColorFields;

            public const int MaxCount = 4;

            public int disabled;
            public float tiling;
            public float valueScale;
            public float valueOffset;
            public Vector2 height;
            public Vector2 offset;
            public Vector2 width;
            public Vector2 thickness;
            public Vector2 thicknessApex;
            public Vector2 colorInfluence;
            public Color color;

            public NoiseSettingGPU(NoiseSetting noiseSetting, Settings settings)
            {
                disabled = noiseSetting.disable;
                tiling = noiseSetting.tiling * settings.tiling;
                valueScale = noiseSetting.valueScale;
                valueOffset = noiseSetting.valueOffset;
                height = noiseSetting.height * settings.height;
                offset = noiseSetting.offset * settings.height;
                width = noiseSetting.angle * settings.angle;
                thickness = noiseSetting.thickness * settings.thickness;
                thicknessApex = noiseSetting.skew;
                colorInfluence = noiseSetting.colorInfluence;

                Color settingColor = noiseSetting.color;
                settingColor *= settings.tint;
                Color.RGBToHSV(settingColor, out float hue, out float saturation, out float value);
                hue += settings.hueShift * (1.0f / 360.0f);
                hue = Mathf.Repeat(hue, 1.0f);
                saturation *= settings.saturation;
                color = Color.HSVToRGB(hue, saturation, value);
            }
        }

        /// <summary>
        /// Contain all information of *what* kind of grass to grow.
        /// <remarks>
        /// This is a struct and thus copied by-value, not by-reference.
        /// </remarks>
        /// </summary>
        [Serializable]
        public struct Settings
        {
            [NotNull] [Tooltip("The preset used. Must not be null.")]
            public TgsPreset preset;

            [Header("Common")]
            [FormerlySerializedAs("density")]
            [Tooltip("Final scaling factor for the amount of the grass blades.")]
            [Min(0.0f)]
            public float amount;

            [FormerlySerializedAs("scale")]
            [Tooltip("Additional scaling factor for the height of the grass blades.")]
            [Min(0.0f)]
            public float height;

            [Tooltip("Additional scaling factor for the thickness of the grass blades.")]
            public float thickness;

            [Tooltip("Additional scaling factor for the thickness of the grass blades.")] [Min(0.0f)]
            public float angle;

            [Tooltip("The additional tiling factor for noise textures of the grass.")]
            public float tiling;

            [Tooltip("Additional bias for the maximal allowed slope/angle in degree.")] [Range(-180.0f, 180.0f)]
            public float slopeBias;


            [Header("Color")] [Tooltip("The final color tint for any grass blades.")] [ColorUsage(false)]
            public Color tint;

            [Tooltip("Shifts the hue for all colors. Useful to alter the look.")] [Range(-180.0f, 180.0f)]
            public float hueShift;

            [Tooltip("Controls the saturation of the grass. Useful to alter the look.")] [Range(0.0f, 3.0f)]
            public float saturation;

            [Space]
            [Tooltip("How much the color of the ground should be adapted. Currently only works with terrain. ")]
            [Range(0.0f, 1.0f)]
            public float camouflage;

            /// <summary>
            ///     The current hash value of all settings. See HasChangedSinceLastCall()
            /// </summary>
            Hash128 _currentHash;

            public static Settings GetDefault()
            {
                return new Settings
                {
                    height = 1.0f,
                    tiling = 1.0f,
                    amount = 1.0f,
                    thickness = 1.0f,
                    angle = 1.0f,
                    tint = Color.white,
                    saturation = 1.0f
                };
            }

            /// <summary>
            ///     Call this function once per frame before baking. It updates the hash value that is uses to determine any changes.
            /// </summary>
            public bool HasChangedSinceLastCall()
            {
                Profiler.BeginSample("QuickSettings.UpdateHash");
                Hash128 hash128 = new();
                Hash128 presetHash = preset != null ? preset.currentBakeSettingsHash : new Hash128();
                hash128.Append(ref presetHash);
                hash128.Append(height);
                hash128.Append(tiling);
                hash128.Append(amount);
                hash128.Append(thickness);
                hash128.Append(angle);
                hash128.Append(slopeBias);

                hash128.Append(ref tint);
                hash128.Append(hueShift);
                hash128.Append(saturation);
                hash128.Append(camouflage);

                bool hasChanged = _currentHash != hash128;
                _currentHash = hash128;
                Profiler.EndSample();
                return hasChanged;
            }

            public bool IsValid()
            {
                return preset != null;
            }
        }
    }
}