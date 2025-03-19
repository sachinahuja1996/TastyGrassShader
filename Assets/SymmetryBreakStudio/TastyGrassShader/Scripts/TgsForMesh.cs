using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
#if TASTY_GRASS_SHADER_DEBUG
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
#endif

namespace SymmetryBreakStudio.TastyGrassShader
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [HelpURL("https://github.com/SymmetryBreakStudio/TastyGrassShader/wiki/Quick-Start")]
    [AddComponentMenu("Symmetry Break Studio/Tasty Grass Shader/Tasty Grass Shader For Mesh")]
    public class TgsForMesh : MonoBehaviour
    {
        public enum GrassMeshError
        {
            None,
            MissingMeshFilter,
            MeshNoReadWrite,
            MissingVertexColor,
            MissingMesh
        }

        [SerializeField] List<TgsMeshLayer> layers = new();

        [Tooltip("Wind setting used for this object.")]
        public TgsWindSettings windSettings;

        // In case the mesh is replaced by static batching, we have no reliable way of getting the original one.
        // Therefore, we keep a reference around 
        public Mesh sharedMeshReference;

        Matrix4x4 _previousLocalToWorld;
        [NonSerialized] [HideInInspector] public bool UpdateOnNextTick;

        void Update()
        {
            if (!Application.isPlaying || UpdateOnNextTick)
            {
                // Update the mesh filter, so that PolyBrush works in edit mode.
                // Note that this should not be done in play mode, because the user may use static batching,
                // which breaks TGS.
                if (!Application.isPlaying)
                {
                    sharedMeshReference = GetComponent<MeshFilter>().sharedMesh;
                }

                OnPropertiesMayChanged();

                UpdateOnNextTick = false;
            }
        }


        void OnEnable()
        {
#if TASTY_GRASS_SHADER_DEBUG
            UnsafeUtility.SetLeakDetectionMode(NativeLeakDetectionMode.EnabledWithStackTrace);
#endif
            MarkGeometryDirty();
            OnPropertiesMayChanged();
        }

        void OnDisable()
        {
            foreach (TgsMeshLayer tgsMeshLayer in layers)
            {
                tgsMeshLayer.Release();
            }
        }

        void OnDrawGizmosSelected()
        {
            foreach (TgsMeshLayer layer in layers)
            {
                if (layer.Instance != null)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireCube(layer.Instance.tightBounds.center, layer.Instance.tightBounds.size);
                }
            }
        }


        void OnTransformParentChanged()
        {
            MarkGeometryDirty();
        }

        void OnValidate()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            sharedMeshReference = meshFilter.sharedMesh;
        }


        /// <summary>
        ///     Get the layer at the given index. Will throw an exception if the index does not exist.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public TgsMeshLayer GetLayerByIndex(int index)
        {
            return index < layers.Count ? layers[index] : null;
        }

        /// <summary>
        ///     Gets the count of layers.
        /// </summary>
        /// <returns></returns>
        public int GetLayerCount()
        {
            return layers.Count;
        }

        /// <summary>
        ///     Adds a new layer.
        /// </summary>
        /// <returns>The index of the new layer</returns>
        public int AddNewLayer()
        {
            TgsMeshLayer newLayer = new();
            layers.Add(newLayer);
            return layers.Count - 1;
        }

        /// <summary>
        ///     Removes the layer at the given index. This function may throw an exception if the index is invalid.
        /// </summary>
        /// <param name="index"></param>
        public void RemoveLayerAt(int index)
        {
            TgsMeshLayer tgsMeshLayer = layers[index];
            tgsMeshLayer.Release();
            layers.RemoveAt(index);
        }

        public static GrassMeshError CheckForErrorsMeshFilter(TgsForMesh tgsForMesh, MeshFilter meshFilter)
        {
            if (!meshFilter)
            {
                return GrassMeshError.MissingMeshFilter;
            }

            if (!meshFilter.sharedMesh)
            {
                return GrassMeshError.MissingMesh;
            }

            if (!meshFilter.sharedMesh.isReadable)
            {
                return GrassMeshError.MeshNoReadWrite;
            }

            bool anyLayerNeedsVertexColor = false;
            foreach (var layer in tgsForMesh.layers)
            {
                if (layer.distribution != TgsMeshLayer.DensityColorChannelMask.Fill)
                {
                    anyLayerNeedsVertexColor = true;
                    break;
                }
            }

            if (anyLayerNeedsVertexColor && !meshFilter.sharedMesh.HasVertexAttribute(VertexAttribute.Color))
            {
                return GrassMeshError.MissingVertexColor;
            }

            return GrassMeshError.None;
        }

        public GrassMeshError CheckForErrors()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            return CheckForErrorsMeshFilter(this, meshFilter);
        }

        public void MarkGeometryDirty()
        {
            foreach (TgsMeshLayer tgsMeshLayer in layers)
            {
                tgsMeshLayer.MarkGeometryDirty();
            }
        }

        public void MarkMaterialDirty()
        {
            foreach (TgsMeshLayer tgsMeshLayer in layers)
            {
                tgsMeshLayer.MarkMaterialDirty();
            }
        }


        bool IsThisSelectedInEditor()
        {
#if UNITY_EDITOR
            return UnityEditor.Selection.Contains(gameObject);
#else
            return false;
#endif
        }


        public void OnPropertiesMayChanged()
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            if (localToWorld != _previousLocalToWorld)
            {
                MarkGeometryDirty();
            }

            int unityLayer = gameObject.layer;
            _previousLocalToWorld = localToWorld;
            foreach (TgsMeshLayer tgsMeshLayer in layers)
            {
                // For the Editor mode, always rebuild the mesh when selected, since we can't be certain if something changed.
                if (IsThisSelectedInEditor())
                {
                    tgsMeshLayer.MarkGeometryDirty();
                    tgsMeshLayer.MarkMaterialDirty();
                }


#if TASTY_GRASS_SHADER_DEBUG
                tgsMeshLayer.debugUsingGameObject = gameObject;
#endif
                tgsMeshLayer.CheckForChange(localToWorld, sharedMeshReference, meshRenderer.bounds, unityLayer);
                tgsMeshLayer.Instance.UsedWindSettings = windSettings;
            }
        }

        public int GetMemoryBufferByteSize()
        {
            int size = 0;
            foreach (TgsMeshLayer meshLayer in layers)
            {
                if (meshLayer.Instance != null)
                {
                    size += meshLayer.Instance.GetGrassBufferMemoryByteSize();
                }
            }

            return size;
        }
    }

    [Serializable]
    public class TgsMeshLayer
    {
        public enum DensityColorChannelMask
        {
            Fill,
            Red,
            Green,
            Blue,
            Alpha
        }

        [HideInInspector] public bool hide;

        [FormerlySerializedAs("quickSettings")]
        public TgsPreset.Settings settings = TgsPreset.Settings.GetDefault();

        public DensityColorChannelMask distribution;

        internal TgsInstance Instance;

        internal TgsMeshLayer()
        {
        }

#if TASTY_GRASS_SHADER_DEBUG
        public GameObject debugUsingGameObject;
#endif
        public void CheckForChange(Matrix4x4 localToWorldMatrix, Mesh mesh, Bounds worldSpaceBounds, int unityLayer)
        {
            if (Instance == null)
            {
                Instance = new TgsInstance();
                Instance.MarkGeometryDirty();
                Instance.MarkMaterialDirty();
            }

#if TASTY_GRASS_SHADER_DEBUG
            Instance.debugUsingGameObject = debugUsingGameObject;
#endif
            Instance.Hide = hide;
            Instance.UnityLayer = unityLayer;

            if (Instance.isGeometryDirty || settings.HasChangedSinceLastCall())
            {
                TgsInstance.TgsInstanceRecipe tgsInstanceRecipe = TgsInstance.TgsInstanceRecipe.BakeFromMesh(
                    localToWorldMatrix,
                    settings,
                    mesh,
                    worldSpaceBounds);

                if (distribution != DensityColorChannelMask.Fill)
                {
                    Vector4 densityMask = new(
                        distribution == DensityColorChannelMask.Red ? 1.0f : 0.0f,
                        distribution == DensityColorChannelMask.Green ? 1.0f : 0.0f,
                        distribution == DensityColorChannelMask.Blue ? 1.0f : 0.0f,
                        distribution == DensityColorChannelMask.Alpha ? 1.0f : 0.0f);

                    tgsInstanceRecipe.SetupDistributionByVertexColor(densityMask);
                }

                Instance.SetBakeParameters(tgsInstanceRecipe);
                Instance.MarkGeometryDirty();
                Instance.MarkMaterialDirty();
            }
        }

        public void MarkGeometryDirty()
        {
            Instance?.MarkGeometryDirty();
        }

        public void MarkMaterialDirty()
        {
            Instance?.MarkMaterialDirty();
        }

        public void Release()
        {
            Instance?.Release();
            Instance = null;
        }

        public string GetEditorName(int index)
        {
            string layerName =
                $"#{index} - {(settings.preset != null ? settings.preset.name : "NO PRESET DEFINED")} ({distribution.ToString()}) {(hide ? "(Hidden)" : "")}";
            return layerName;
        }
    }
}