using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;
#if TASTY_GRASS_SHADER_DEBUG
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
#endif

namespace SymmetryBreakStudio.TastyGrassShader
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [HelpURL("https://github.com/SymmetryBreakStudio/TastyGrassShader/wiki/Quick-Start")]
    [RequireComponent(typeof(Terrain))]
    [AddComponentMenu("Symmetry Break Studio/Tasty Grass Shader/Tasty Grass Shader For Terrain")]
    public class TgsForUnityTerrain : MonoBehaviour
    {
        [SerializeField] List<TgsTerrainLayer> layers = new();

        [Tooltip("Wind setting used for this object.")]
        public TgsWindSettings windSettings;

        [Header("Debugging")] [Tooltip("Shows the bounds of all terrain chunks in editor.")]
        public bool showChunksBounds;


#if TASTY_GRASS_SHADER_DEBUG
        public bool forceUpdate;
#endif

        readonly List<RectInt> _changedTerrainRegions = new();

        /// <summary>
        ///     Workaround: In the very first frame after loading, the terrain heightmap is not loaded yet.
        ///     We therefore check if the first frame has passed to be certain that the terrain has done loading.
        /// </summary>
        bool _passedFirstFrame;


        bool IsThisSelectedInEditor()
        {
#if UNITY_EDITOR
            return UnityEditor.Selection.Contains(gameObject);
#else
            return false;
#endif
        }

        private void OnValidate()
        {
            Update();
        }

        void Update()
        {
            if (!Application.isPlaying && IsThisSelectedInEditor())
            {
                // Since a change may occur any frame in editor mode, this function is called regular.
                MarkMaterialDirty(); // textures might have changed, so we also need to update the properties regularly.
                OnPropertiesMayChanged();
            }
        }

        void OnEnable()
        {
#if TASTY_GRASS_SHADER_DEBUG
            UnsafeUtility.SetLeakDetectionMode(NativeLeakDetectionMode.EnabledWithStackTrace);
#endif
            TerrainCallbacks.heightmapChanged += TerrainCallbacksOnheightmapChanged;
            TerrainCallbacks.textureChanged += TerrainCallbacksOntextureChanged;

            InstancesUpdateGrass();
        }

        void OnDisable()
        {
            TerrainCallbacks.heightmapChanged -= TerrainCallbacksOnheightmapChanged;
            TerrainCallbacks.textureChanged -= TerrainCallbacksOntextureChanged;

            foreach (TgsTerrainLayer layerInstance in layers)
            {
                layerInstance.Release();
            }
        }


        void OnDrawGizmosSelected()
        {
            if (showChunksBounds)
            {
                foreach (TgsTerrainLayer instance in layers)
                {
                    instance.DrawGizmos();
                }
            }
        }

        /// <summary>
        ///     Call this function if you changed any settings by code.
        ///     Checks if any settings have changed and issues a rebake if needed.
        ///     This function may have significant overhead, so use it only when needed.
        /// </summary>
        public void OnPropertiesMayChanged()
        {
#if TASTY_GRASS_SHADER_DEBUG
            if (forceUpdate)
            {
                MarkGeometryDirty();
            }
#endif

            foreach (TgsTerrainLayer tgsTerrainLayer in layers)
            {
                tgsTerrainLayer.CheckForSettingsChange();
            }

            InstancesUpdateGrass();
        }


        public void MarkGeometryDirty()
        {
            foreach (TgsTerrainLayer tgsTerrainLayer in layers)
            {
                tgsTerrainLayer.MarkGeometryDirty();
            }
        }

        public void MarkMaterialDirty()
        {
            foreach (TgsTerrainLayer tgsTerrainLayer in layers)
            {
                tgsTerrainLayer.MarkMaterialDirty();
            }
        }

        public int GetChunkCount()
        {
            int chunkCount = 0;
            foreach (TgsTerrainLayer tgsTerrainLayer in layers)
            {
                chunkCount += tgsTerrainLayer.GetChunkCount();
            }

            return chunkCount;
        }

        public int GetGrassMemoryBufferByteSize()
        {
            int byteSize = 0;
            foreach (TgsTerrainLayer tgsTerrainLayer in layers)
            {
                byteSize += tgsTerrainLayer.GetGrassMemoryBufferByteSize();
            }

            return byteSize;
        }

        public int GetPlacementMemoryBufferByteSize()
        {
            int byteSize = 0;
            foreach (TgsTerrainLayer tgsTerrainLayer in layers)
            {
                byteSize += tgsTerrainLayer.GetPlacementMemoryBufferByteSize();
            }

            return byteSize;
        }


        /// <summary>
        ///     Get the layer at the given index. Will throw an exception if the index does not exist.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public TgsTerrainLayer GetLayerByIndex(int index)
        {
            return layers[index];
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
            TgsTerrainLayer newLayer = new();
            layers.Add(newLayer);
            return layers.Count - 1;
        }

        /// <summary>
        ///     Removes the layer at the given index. This function may throw an exception if the index is invalid.
        /// </summary>
        /// <param name="index"></param>
        public void RemoveLayerAt(int index)
        {
            TgsTerrainLayer tgsTerrainLayer = layers[index];
            tgsTerrainLayer.Release();
            layers.RemoveAt(index);
        }

        void TerrainCallbacksOntextureChanged(Terrain terrain, string texturename, RectInt texelregion, bool synched)
        {
            if (terrain != GetComponent<Terrain>())
            {
                return;
            }

            if (!synched)
            {
                return;
            }

            float textureToHeightmapRatio =
                terrain.terrainData.heightmapResolution / (float)terrain.terrainData.alphamapResolution;

            texelregion = new RectInt(
                (int)(texelregion.min.x * textureToHeightmapRatio),
                (int)(texelregion.min.y * textureToHeightmapRatio),
                (int)(texelregion.max.x * textureToHeightmapRatio),
                (int)(texelregion.max.y * textureToHeightmapRatio)
            );

            _changedTerrainRegions.Add(texelregion);
        }

        void TerrainCallbacksOnheightmapChanged(Terrain terrain, RectInt heightregion, bool synched)
        {
            if (terrain != GetComponent<Terrain>())
            {
                return;
            }

            if (!synched)
            {
                return;
            }

            _changedTerrainRegions.Add(heightregion);
        }

        public void InstancesUpdateGrass()
        {
            Transform thisTransform = transform;
            Terrain terrain = GetComponent<Terrain>();

            TerrainData terrainData = terrain.terrainData;
            var terrainDensityMaps = terrainData.alphamapTextures;
            var terrainLayers = terrainData.terrainLayers;

            int unityLayer = gameObject.layer;
            foreach (TgsTerrainLayer layerInstance in layers)
            {
                layerInstance.CheckForChange(thisTransform, terrain,
                    terrainDensityMaps, terrainLayers, _changedTerrainRegions, unityLayer);
                layerInstance.SetWindSettings(windSettings);
            }

            _changedTerrainRegions.Clear();
        }


#if TASTY_GRASS_SHADER_DEBUG
        public bool forceAlwaysUpdate;
        public Vector3 minBoundsWs, maxBoundsWs;
        public int bladeCapacity;
        public int placedBlades;


#endif
    }

    [Serializable]
    public class TgsTerrainLayer
    {
        public enum TerrainLayerDistribution
        {
            Fill,
            TastyGrassShaderPaintTool,
            ByTerrainLayer,
            ByCustomTexture
        }

        static readonly int DensityMapRW = Shader.PropertyToID("_DensityMapRW");
        static readonly int PaintBrush = Shader.PropertyToID("_PaintBrush");
        static readonly int DensityMapDimensionX = Shader.PropertyToID("_DensityMap_DimensionX");
        static readonly int DensityMapDimensionY = Shader.PropertyToID("_DensityMap_DimensionY");

        static readonly int PaintNoise = Shader.PropertyToID("_PaintNoise");
        [HideInInspector] public bool hide;

        [FormerlySerializedAs("quickSettings")]
        public TgsPreset.Settings settings = TgsPreset.Settings.GetDefault();

        [Header("Terrain Specific")]
        [FormerlySerializedAs("densityMode")]
        [Tooltip("What mode is used to control the amount of the layer.")]
        public TerrainLayerDistribution distribution;

        [Space] [HideInInspector] public Texture2D paintedDensityMapStorage;

        [FormerlySerializedAs("unityTerrainLayerIndex")]
        [Tooltip(
            "The index of the Unity Terrain Layer. This is both used for getting the splatmap (if the amount mode is FromTerrainSplatmap) and what color texture to use for blending in.")
        ]
        [UnityTerrainLayerIndex]
        public int targetTerrainLayer;


        [HideGroup(nameof(distribution), (int)TerrainLayerDistribution.ByCustomTexture)]
        public Texture2D distributionTexture;

        [HideGroup(nameof(distribution), (int)TerrainLayerDistribution.ByCustomTexture)]
        public Vector2 scaling = Vector2.one;

        [HideGroup(nameof(distribution), (int)TerrainLayerDistribution.ByCustomTexture)]
        public Vector2 offset;

        [HideGroup(nameof(distribution), (int)TerrainLayerDistribution.ByCustomTexture)]
        public Color colorMask = Color.white;

        [FormerlySerializedAs("sizePerChunk")] [Min(8)]
        public int chunkSize = 32;


        Hash128 _bakeSettingsHash;

        List<TgsInstance> _chunks = new();
        List<Bounds> _dirtyChunks = new();
        float _drawNoiseIntensity;
        float _drawNoiseScale;
        float _drawOpacity;


        bool _drawPending;
        Vector3 _drawPointWs;
        float _drawRadius;
        bool _fillAdditiveBlend;
        Vector4 _fillChannelWeights;

        bool _fillPending;
        Texture2D _fillTexture;

        RenderTexture _paintedDensityMapRt;

        /// <summary>
        ///     Tgs Terrain Layers may only be created by Tasty Grass Shader.
        /// </summary>
        internal TgsTerrainLayer()
        {
        }


        /// <summary>
        ///     Checks if any bake-related value has changed and sets all chunks dirty if so.
        /// </summary>
        public void CheckForSettingsChange()
        {
            Profiler.BeginSample("TgsTerrainLayer.GetSettingsHash");
            Hash128 newHash = new();

            newHash.Append(targetTerrainLayer);
            newHash.Append(ref distribution);

            newHash.Append(distributionTexture == null ? 0 : distributionTexture.GetInstanceID());
            newHash.Append(ref scaling);
            newHash.Append(ref scaling);
            newHash.Append(ref colorMask);
            newHash.Append(ref chunkSize);

            if (settings.HasChangedSinceLastCall() || _bakeSettingsHash != newHash)
            {
                MarkGeometryDirty();
                _bakeSettingsHash = newHash;
            }

            Profiler.EndSample();
        }

        public void DrawToUserDetailMapDeferred(Vector3 worldSpaceCoord, float brushRadius, float brushOpacity,
            float noiseIntensity = 0.0f, float noiseScale = 1.0f)
        {
            _drawPending = true;
            _drawPointWs = worldSpaceCoord;
            _drawRadius = brushRadius * 0.5f;
            _drawOpacity = brushOpacity;
            _drawNoiseIntensity = noiseIntensity;
            _drawNoiseScale = noiseScale;
        }

        public void FillUserDetailMapDeferred(Texture2D texture, Vector4 channelWeights, bool additiveBlend)
        {
            _fillPending = true;
            _fillTexture = texture == null ? Texture2D.blackTexture : texture;
            _fillChannelWeights = channelWeights;
            _fillAdditiveBlend = additiveBlend;
        }

        public void SetOverlappingChunksDirty(Bounds changedArea)
        {
            foreach (TgsInstance instance in _chunks)
            {
                if (instance.looseBounds.Intersects(changedArea))
                {
                    instance.MarkGeometryDirty();
                }
            }
        }

        /// <summary>
        ///     Marks all chunks as dirty. Used after applying undo.
        /// </summary>
        public void MarkGeometryDirty()
        {
            foreach (TgsInstance instance in _chunks)
            {
                instance.MarkGeometryDirty();
            }
        }

        /// <summary>
        ///     Applies changes regarding to rendering, such as WindSettings or shading settings from the preset.
        /// </summary>
        public void MarkMaterialDirty()
        {
            foreach (TgsInstance instance in _chunks)
            {
                instance.MarkMaterialDirty();
            }
        }

        public void SetWindSettings(TgsWindSettings windSettings)
        {
            foreach (TgsInstance tgsInstance in _chunks)
            {
                tgsInstance.UsedWindSettings = windSettings;
            }
        }


        public void CheckForChange(Transform transform,
            Terrain terrain, Texture2D[] terrainDensityMaps, TerrainLayer[] terrainLayers,
            List<RectInt> changedTexelRegions, int unityLayerMask)
        {
            Profiler.BeginSample("CheckForChange");

            if (settings.preset == null)
            {
                Profiler.EndSample();
                return;
            }

            // initialize resources
            TerrainData terrainData = terrain.terrainData;

            // Cache properties for performance reasons.
            // TODO: set chunks dirty if the terrain has changed.
            Vector3 terrainPosition = transform.position;

            Profiler.BeginSample("Check Any Chunks Dirty");
            bool anyChunkDirty = false;

            // Compute drawing coordinates here.
            Bounds paintBrushBoundsWs = new();
            Vector2Int paintBrushPixelCoord = Vector2Int.zero;
            float paintBrushDiameterInPixelSpace = 0;

            Bounds terrainFullBounds = GetTerrainWorldSpaceBounds(terrain, terrainPosition);
            Vector3 pixelSize = terrain.terrainData.heightmapScale;
            Vector2Int terrainHeightmapRes =
                new(terrainData.heightmapTexture.width, terrainData.heightmapTexture.height);

            Vector2Int chunkSizePixels = new(
                (int)TgsInstance.CeilingDivisionFloat(chunkSize, pixelSize.x),
                (int)TgsInstance.CeilingDivisionFloat(chunkSize, pixelSize.z));

            Vector2Int chunksPerAxis = new(TgsInstance.CeilingDivision(terrainHeightmapRes.x, chunkSizePixels.x),
                TgsInstance.CeilingDivision(terrainHeightmapRes.y, chunkSizePixels.y));

            int chunksTotal = chunksPerAxis.x * chunksPerAxis.y;

            // Setup the user painted texture.
            if (distribution == TerrainLayerDistribution.TastyGrassShaderPaintTool)
            {
                if (_paintedDensityMapRt == null || _paintedDensityMapRt.width != terrainHeightmapRes.x ||
                    _paintedDensityMapRt.height != terrainHeightmapRes.y)
                {
                    if (_paintedDensityMapRt != null)
                    {
                        _paintedDensityMapRt.Release();
                    }

                    _paintedDensityMapRt = new RenderTexture(terrainHeightmapRes.x, terrainHeightmapRes.y,
                        1, RenderTextureFormat.R8)
                    {
                        enableRandomWrite = true
                    };

                    _paintedDensityMapRt.Create();

                    // clear with black
                    Graphics.Blit(Texture2D.blackTexture, _paintedDensityMapRt);
                }
            }

            SetupChunks(chunksTotal);

            // figure out the bounding box of the brush, snapped to the pixel grid.
            if (_drawPending)
            {
                Vector2 terrainSizeXZ = new(terrainData.size.x, terrainData.size.z);
                Vector2 terrainPosXZ = new(terrainPosition.x, terrainPosition.z);

                Vector2 drawPointXZ = new(_drawPointWs.x, _drawPointWs.z);
                Vector2 paintLayerRes = new(_paintedDensityMapRt.width, _paintedDensityMapRt.height);

                // Figure out the coordinate in pixel space
                Vector2 relativeCoord = drawPointXZ - terrainPosXZ;
                Vector2 coordTerrain01 = relativeCoord / terrainSizeXZ;

                paintBrushPixelCoord = Vector2Int.FloorToInt(coordTerrain01 * paintLayerRes);

                paintBrushDiameterInPixelSpace = _drawRadius * terrainData.heightmapScale.x;

                // Compute chunks that are affected by drawing to them and mark them as dirty.
                Vector2Int brushBoundsMinPx =
                    Vector2Int.FloorToInt(paintBrushPixelCoord -
                                          new Vector2(paintBrushDiameterInPixelSpace, paintBrushDiameterInPixelSpace) *
                                          0.5f);
                Vector2Int brushBoundsMaxPx =
                    Vector2Int.CeilToInt(paintBrushPixelCoord +
                                         new Vector2(paintBrushDiameterInPixelSpace, paintBrushDiameterInPixelSpace) *
                                         0.5f);

                Vector2 brushBoundsMinWs =
                    brushBoundsMinPx / paintLayerRes * terrainSizeXZ + terrainPosXZ;
                Vector2 brushBoundsMaxWs =
                    brushBoundsMaxPx / paintLayerRes * terrainSizeXZ + terrainPosXZ;

                paintBrushBoundsWs.SetMinMax(
                    new Vector3(brushBoundsMinWs.x, _drawPointWs.y - _drawRadius, brushBoundsMinWs.y),
                    new Vector3(brushBoundsMaxWs.x, _drawPointWs.y + _drawRadius, brushBoundsMaxWs.y)
                );
            }

            // Since looping over all instances is expensive, we do more than one thing in this loop.
            for (int chunkIndex = 0; chunkIndex < _chunks.Count; chunkIndex++)
            {
                TgsInstance instance = _chunks[chunkIndex];
                instance.Hide = hide;
                instance.UnityLayer = unityLayerMask;
                Vector2Int chunkXy = new(chunkIndex / chunksPerAxis.x, chunkIndex % chunksPerAxis.x);
                Vector2Int pixelBoundsMin = chunkXy * chunkSizePixels;
                Vector2Int pixelBoundsMax = (chunkXy + Vector2Int.one) * chunkSizePixels;
                RectInt pixelRect = new();
                pixelRect.SetMinMax(pixelBoundsMin, pixelBoundsMax);
                pixelBoundsMax = Vector2Int.Min(terrainHeightmapRes - Vector2Int.one, pixelBoundsMax);

                Vector3 chunkStart = new(pixelBoundsMin.x * pixelSize.x, 0.0f, pixelBoundsMin.y * pixelSize.z);
                Vector3 chunkEnd = new(pixelBoundsMax.x * pixelSize.x, terrainData.size.y,
                    pixelBoundsMax.y * pixelSize.z);

                Bounds chunkBounds = new()
                {
                    min = terrainFullBounds.min + chunkStart,
                    max = terrainFullBounds.min + chunkEnd
                };

                foreach (RectInt changedTerrainRegion in changedTexelRegions)
                {
                    if (changedTerrainRegion.Overlaps(pixelRect))
                    {
                        instance.MarkGeometryDirty();
                        break;
                    }
                }

                if ((_drawPending || _fillPending) && instance.looseBounds.Intersects(paintBrushBoundsWs))
                {
                    instance.MarkGeometryDirty();
                }

                if (instance.isGeometryDirty)
                {
                    anyChunkDirty = true;
                }
            }

            // TODO: this assumes that 0 == we just load. But is this true??
            if (!anyChunkDirty && _chunks.Count > 0)
            {
                Profiler.EndSample(); // Check Any Chunks Dirty
                Profiler.EndSample(); // TgsForUnityTerrain.BakeGraSS
                return;
            }

            Texture2D densityMap = Texture2D.whiteTexture;
            Vector4 densityMapMask = Vector4.zero;
            switch (distribution)
            {
                case TerrainLayerDistribution.Fill:
                    break;
                case TerrainLayerDistribution.ByTerrainLayer:
                    int alphaMapIndex = targetTerrainLayer / 4;

                    if (alphaMapIndex < terrainDensityMaps.Length)
                    {
                        densityMap = terrainDensityMaps[alphaMapIndex];
                    }

                    // Calculate the amount mask vector from the selected terrain layer.
                    // In the shader, this is used to mask out the different channels from the terrains alpha map.
                    int alphaMapSubIndex = targetTerrainLayer % 4;
                    densityMapMask = new Vector4(
                        alphaMapSubIndex == 0 ? 1.0f : 0.0f,
                        alphaMapSubIndex == 1 ? 1.0f : 0.0f,
                        alphaMapSubIndex == 2 ? 1.0f : 0.0f,
                        alphaMapSubIndex == 3 ? 1.0f : 0.0f);

                    break;
                case TerrainLayerDistribution.TastyGrassShaderPaintTool:
                    // TODO: This is a bit over the top, but it is the only way to make the Undo happening consistently.
                    Graphics.Blit(paintedDensityMapStorage, _paintedDensityMapRt);

                    ComputeShader tgsComputeShader = TgsManager.tgsComputeShader;
                    bool paintedDensityMapDirty = false;
                    if (_fillPending)
                    {
                        _fillPending = false;
                        paintedDensityMapDirty = true;
                        int fillKernel = tgsComputeShader.FindKernel("FillToDensityMap");

                        tgsComputeShader.SetTexture(fillKernel, DensityMapRW, _paintedDensityMapRt);
                        tgsComputeShader.SetTexture(fillKernel, "_FillMap", _fillTexture);
                        tgsComputeShader.SetVector("_Fill_ChannelWeights", _fillChannelWeights);
                        tgsComputeShader.SetInt("_Fill_Additive", _fillAdditiveBlend ? 1 : 0);
                        tgsComputeShader.SetInt(DensityMapDimensionX, _paintedDensityMapRt.width);
                        tgsComputeShader.SetInt(DensityMapDimensionY, _paintedDensityMapRt.height);
                        {
                            Graphics.SetRandomWriteTarget(0, _paintedDensityMapRt);
                            tgsComputeShader.GetKernelThreadGroupSizes(fillKernel, out uint threadsX,
                                out uint threadsY, out _);
                            int dispatchX = TgsInstance.CeilingDivision(Mathf.CeilToInt(_paintedDensityMapRt.width),
                                (int)threadsX);
                            int dispatchY = TgsInstance.CeilingDivision(Mathf.CeilToInt(_paintedDensityMapRt.height),
                                (int)threadsY);
                            tgsComputeShader.Dispatch(fillKernel, dispatchX, dispatchY, 1);
                            Graphics.ClearRandomWriteTargets();
                        }
                        MarkGeometryDirty();
                    }

                    // Draw to the amount map using a compute shader
                    if (_drawPending)
                    {
                        _drawPending = false;
                        paintedDensityMapDirty = true;

                        // Prepare and dispatch compute shader.
                        int drawKernel = tgsComputeShader.FindKernel("DrawToDensityMap");
                        Graphics.SetRandomWriteTarget(0, _paintedDensityMapRt);
                        tgsComputeShader.SetTexture(drawKernel, DensityMapRW, _paintedDensityMapRt);
                        tgsComputeShader.SetInt(DensityMapDimensionX, _paintedDensityMapRt.width);
                        tgsComputeShader.SetInt(DensityMapDimensionY, _paintedDensityMapRt.height);
                        tgsComputeShader.SetVector(PaintBrush,
                            new Vector4(paintBrushPixelCoord.x, paintBrushPixelCoord.y,
                                paintBrushDiameterInPixelSpace * 0.5f,
                                _drawOpacity * Time.deltaTime));
                        tgsComputeShader.SetVector(PaintNoise,
                            new Vector4(_drawNoiseScale, _drawNoiseIntensity));
                        {
                            tgsComputeShader.GetKernelThreadGroupSizes(drawKernel, out uint threadsX,
                                out uint threadsY, out _);
                            int dispatchX = TgsInstance.CeilingDivision(Mathf.CeilToInt(paintBrushDiameterInPixelSpace),
                                (int)threadsX);
                            int dispatchY = TgsInstance.CeilingDivision(Mathf.CeilToInt(paintBrushDiameterInPixelSpace),
                                (int)threadsY);
                            tgsComputeShader.Dispatch(drawKernel, dispatchX, dispatchY, 1);
                        }
                        Graphics.ClearRandomWriteTargets();

                        _dirtyChunks.Add(paintBrushBoundsWs);
                    }

                    if (paintedDensityMapDirty || paintedDensityMapStorage == null)
                    {
                        // Read the current texture back to the CPU. (Needed for Undo to work)
                        if (paintedDensityMapStorage == null ||
                            paintedDensityMapStorage.width != _paintedDensityMapRt.width ||
                            paintedDensityMapStorage.height != _paintedDensityMapRt.height)
                        {
                            paintedDensityMapStorage = new Texture2D(_paintedDensityMapRt.width,
                                _paintedDensityMapRt.height,
                                TextureFormat.R8, false);
                        }

                        paintedDensityMapStorage.wrapMode = TextureWrapMode.Clamp;

                        RenderTexture currentActive = RenderTexture.active;
                        RenderTexture.active = _paintedDensityMapRt;
                        paintedDensityMapStorage.ReadPixels(
                            new Rect(0, 0, paintedDensityMapStorage.width,
                                paintedDensityMapStorage.height), 0, 0);
                        paintedDensityMapStorage.Apply();
                        RenderTexture.active = currentActive;
                    }

                    break;
                case TerrainLayerDistribution.ByCustomTexture:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Matrix4x4 localToWorldMatrix = transform.localToWorldMatrix;
            for (int chunkIndex = 0; chunkIndex < chunksTotal; chunkIndex++)
            {
                // TODO: this is computed twice.
                Vector2Int chunkXy = new(chunkIndex / chunksPerAxis.x, chunkIndex % chunksPerAxis.x);
                Vector2Int pixelBoundsMin = chunkXy * chunkSizePixels;
                Vector2Int pixelBoundsMax = (chunkXy + Vector2Int.one) * chunkSizePixels;
                RectInt pixelRect = new();
                pixelRect.SetMinMax(pixelBoundsMin, pixelBoundsMax);
                pixelBoundsMax = Vector2Int.Min(terrainHeightmapRes - Vector2Int.one, pixelBoundsMax);

                Vector3 chunkStart = new(pixelBoundsMin.x * pixelSize.x, 0.0f, pixelBoundsMin.y * pixelSize.z);
                Vector3 chunkEnd = new(pixelBoundsMax.x * pixelSize.x, terrainData.size.y,
                    pixelBoundsMax.y * pixelSize.z);

                Bounds chunkBounds = new()
                {
                    min = terrainFullBounds.min + chunkStart,
                    max = terrainFullBounds.min + chunkEnd
                };

                Vector2Int pixelBoundsSize = pixelBoundsMax - pixelBoundsMin;
                if (pixelBoundsSize.x <= 0 || pixelBoundsSize.y <= 0)
                {
                    continue;
                }

                if (!_chunks[chunkIndex].isGeometryDirty)
                {
                    continue;
                }

                TgsInstance.TgsInstanceRecipe tgsInstanceRecipe = TgsInstance.TgsInstanceRecipe.BakeFromHeightmap(
                    localToWorldMatrix, settings, terrainData.heightmapTexture, chunkBounds,
                    pixelBoundsMin, pixelBoundsSize);

                switch (distribution)
                {
                    case TerrainLayerDistribution.Fill:
                        break;
                    case TerrainLayerDistribution.ByTerrainLayer:
                        tgsInstanceRecipe.SetupDistributionByTexture(densityMap, densityMapMask,
                            new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
                        break;
                    case TerrainLayerDistribution.TastyGrassShaderPaintTool:
                        tgsInstanceRecipe.SetupDistributionByTexture(paintedDensityMapStorage,
                            new Vector4(1.0f, 0.0f, 0.0f, 0.0f), new Vector4(1.0f, 1.0f, 0.0f, 0.0f));
                        break;
                    case TerrainLayerDistribution.ByCustomTexture:
                        tgsInstanceRecipe.SetupDistributionByTexture(distributionTexture, colorMask,
                            new Vector4(scaling.x, scaling.y, offset.x, offset.y));

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // Setup terrain blending by terrain layer.
                if (targetTerrainLayer < terrainLayers.Length)
                {
                    TerrainLayer layer = terrainLayers[targetTerrainLayer];
                    Vector4 colorMapScaleOffset;

                    colorMapScaleOffset.x = 1.0f / layer.tileSize.x;
                    colorMapScaleOffset.y = 1.0f / layer.tileSize.y;

                    Vector3 position = transform.position;
                    colorMapScaleOffset.z = position.x;
                    colorMapScaleOffset.w = position.y;
                    tgsInstanceRecipe.SetupCamouflage(layer.diffuseTexture, colorMapScaleOffset,
                        settings.camouflage);
                }

                _chunks[chunkIndex].SetBakeParameters(tgsInstanceRecipe);


// #if TASTY_GRASS_SHADER_DEBUG
//                 if (chunkIndex != 0) continue;
//                 placedBlades = _instances[chunkIndex].PlacedBlades;
//                 bladeCapacity = _instances[chunkIndex].BladeCapacity;
// #endif
            }

            Profiler.EndSample();
        }

        void SetupChunks(int reqCount)
        {
            Profiler.BeginSample("TgsForUnityTerrain.SetupInstances");

            if (reqCount == _chunks.Count)
            {
                goto End;
            }

            foreach (TgsInstance instance in _chunks)
            {
                instance.Release();
            }

            _chunks.Clear();
            _chunks.Capacity = Mathf.Max(1, reqCount);
            for (int i = 0; i < reqCount; i++)
            {
                TgsInstance newInstance = new();
                newInstance.MarkGeometryDirty();
                newInstance.MarkMaterialDirty();
                _chunks.Add(newInstance);
            }

            End:
            Profiler.EndSample();
        }


        public int GetChunkCount()
        {
            return _chunks.Count;
        }

        public int GetGrassMemoryBufferByteSize()
        {
            int totalMemorySize = 0;
            foreach (TgsInstance instance in _chunks)
            {
                totalMemorySize += instance.GetGrassBufferMemoryByteSize();
            }

            return totalMemorySize;
        }

        public int GetPlacementMemoryBufferByteSize()
        {
            int totalMemorySize = 0;
            foreach (TgsInstance instance in _chunks)
            {
                totalMemorySize += instance.GetPlacementBufferMemoryByteSize();
            }

            return totalMemorySize;
        }

        public void Release()
        {
            if (_paintedDensityMapRt != null)
            {
                _paintedDensityMapRt.Release();
                _paintedDensityMapRt = null;
            }

            SetupChunks(-1);
        }

        public string GetEditorName(int index)
        {
            string layerName =
                $"#{index} - {(hide ? "[Hidden]" : "")} {(settings.preset != null ? settings.preset.name : "NO PRESET")}";
            return layerName;
        }

        public void DrawGizmos()
        {
            if (hide)
            {
                return;
            }

            foreach (TgsInstance instance in _chunks)
            {
                Gizmos.color = Color.red;
                Bounds chunk = instance.tightBounds;
                Gizmos.DrawWireCube(chunk.center, chunk.size);
                Gizmos.color = Color.black;
                Bounds chunkLoose = instance.looseBounds;
                Gizmos.DrawWireCube(chunkLoose.center, chunkLoose.size);
            }
        }

        static Bounds GetTerrainWorldSpaceBounds(Terrain terrain, Vector3 worldPos)
        {
            TerrainData terrainData = terrain.terrainData;
            Vector3 terrainSize = terrainData.size;
            Bounds terrainBounds = new()
            {
                min = worldPos
            };
            terrainBounds.max = terrainBounds.min + terrainSize;
// #if TASTY_GRASS_SHADER_DEBUG
//             minBoundsWs = terrainBounds.min;
//             maxBoundsWs = terrainBounds.max;
// #endif
            return terrainBounds;
        }
    }
}