using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SymmetryBreakStudio.TastyGrassShader
{
    /// <summary>
    ///     The core of the Tasty Grass Shader. For most cases, the wrapper components GrasFieldMesh.cs and
    ///     GrassFieldTerrain.cs are sufficient.
    /// 
    ///     If you want to manage the grass differently, use this class directly.
    ///     Note that baking or rendering happens in TgsManager.cs.
    ///     <remarks>
    ///     You only need to create an instance once, it is intended to be recyclable for minimal Garbage Collection overhead.</remarks>
    /// </summary>
    public class TgsInstance
    {
#if TASTY_GRASS_SHADER_DEBUG
        /// <summary>
        ///     Enable to verify that the compute shader generated unique blades, and thus no memory is wasted.
        ///     The blade format must be GrassNodeReference.
        /// </summary>
        const bool ValidateNoDuplicateBlades = false;
#endif

        // NOTE: these also must be adjusted with TastyGrassShaderCommon.hlsl
        const int GrassNodeCompressedStride = sizeof(uint) * 4;
        const int GrassNodeReferenceStride = sizeof(float) * 15;

        const int GrassNodeStride = GrassNodeCompressedStride;
        const int BladesPerTriangleUpperLimit = 4096;

        const float GrassMaxVertexRangeSize = 2.0f;

        const float MaxGrassRootOffset = 4.0f;

        const int SizeofPlacementTriangle =
                sizeof(float) * 3 * 3 //positions
                + sizeof(float) * 3 * 3 //normals
                + sizeof(float) * 3 //amount
                + sizeof(float) * 3 // geometric_normal
                + sizeof(int) //buffer offset
                + sizeof(int) //blade count
            ;

        static readonly int[] _bladeCountTmpBuffer = new int[1];
        public static List<TgsInstance> AllInstances = new();

        static readonly int PlacementVertices = Shader.PropertyToID("_PlacementVertices");
        static readonly int PlacementIndices = Shader.PropertyToID("_PlacementIndices");
        static readonly int GrassNodePrimitivesAppend = Shader.PropertyToID("_GrassFieldPrimitivesAppend");
        static readonly int IndirectDrawArgs = Shader.PropertyToID("_IndirectDrawArgs");
        static readonly int GrassNodePrimitives = Shader.PropertyToID("_GrassFieldPrimitives");
        static readonly int PositionBoundMin = Shader.PropertyToID("_PositionBoundMin");
        static readonly int PositionBoundMax = Shader.PropertyToID("_PositionBoundMax");
        static readonly int ObjectToWorld = Shader.PropertyToID("_ObjectToWorld");
        static readonly int Heightmap = Shader.PropertyToID("_Heightmap");
        static readonly int HeightmapResolutionXy = Shader.PropertyToID("_HeightmapResolutionXy");
        static readonly int HeightmapChunkOffsetSize = Shader.PropertyToID("_HeightmapChunkOffsetSize");
        static readonly int DensityUseChannelMask = Shader.PropertyToID("_DensityUseChannelMask");


        static readonly int DensityMapChannelMask = Shader.PropertyToID("_DensityMapChannelMask");
        static readonly int DensityMap = Shader.PropertyToID("_DensityMap");
        static readonly int DensityMapUvFromHeightmapIdx = Shader.PropertyToID("_DensityMapUvFromHeightmapIdx");
        static readonly int NoiseParams = Shader.PropertyToID("_NoiseParams");


        static readonly int SphereCollider = Shader.PropertyToID("_SphereCollider");
        static readonly int SphereColliderCount = Shader.PropertyToID("_SphereColliderCount");

        static readonly int PlacementTrianglesR = Shader.PropertyToID("_PlacementTrianglesR");
        static readonly int PlacementTriangleCount = Shader.PropertyToID("_PlacementTriangleCount");
        static readonly int UsedPlacementTriangleCount = Shader.PropertyToID("_UsedPlacementTriangleCount");
        static readonly int PlacementTrianglesAppend = Shader.PropertyToID("_PlacementTrianglesAppend");
        static readonly int MetaDataRW = Shader.PropertyToID("_MetaDataRW");

        static readonly int PlacementTriangleOffset = Shader.PropertyToID("_PlacementTriangleOffset");

        static readonly int ColorMap = Shader.PropertyToID("_ColorMap");
        static readonly int ColorMapBlend = Shader.PropertyToID("_ColorMapBlend");
        static readonly int ColorMapSt = Shader.PropertyToID("_ColorMapST");

        static readonly int PlacementNormals = Shader.PropertyToID("_PlacementNormals");
        static readonly int PlacementColors = Shader.PropertyToID("_PlacementColors");

        readonly InstanceMetaDataGPU[] _instanceMetaDataCPU = new InstanceMetaDataGPU[1];


        /// <summary>
        ///     Holds the actual grass geometry.
        /// </summary>
        ComputeBuffer _bakeOutputBuffer;

        public int bladeCount { get; private set; }

        /// <summary>
        ///     Holds the settings for each noise layer.
        /// </summary>
        ComputeBuffer _grassNoiseLayers, _instanceMetaData; //TODO: recycle across instances

        bool _hasFinishedBaking;


        /// <summary>
        ///     Per-instance settings for the material, such as ground color, wind speed, ...
        /// </summary>
        MaterialPropertyBlock _materialPropertyBlock;

        ComputeBuffer _placementTriangleBuffer;


        /// <summary>
        ///     If true, the instance will not be rendered.
        /// </summary>
        public bool Hide;

        TgsInstanceRecipe _nextTgsInstanceRecipe;

        public TgsWindSettings UsedWindSettings;

        public TgsInstance()
        {
            TgsGlobalStatus.instances++;
            AllInstances.Add(this);
        }

        private TgsInstanceRecipe activeTgsInstanceRecipe;

        /// <summary>
        /// Fast-Path for getting baseLodFactor from the preset. This is used by TgsInstancePreRendering, because it it uses activeTgsInstanceRecipe, it will make a huge copy of that struct first.
        /// </summary>
        /// <returns></returns>
        public float GetActiveTgsInstanceRecipeBaseLodFactor()
        {
            float output = 0.0f;
            if (activeTgsInstanceRecipe.Settings.preset)
            {
                output = activeTgsInstanceRecipe.Settings.preset.baseLodFactor;
            }

            return output;
        }

        public bool isGeometryDirty { get; private set; }

        public bool isMaterialDirty { get; private set; }


#if TASTY_GRASS_SHADER_DEBUG
        public GameObject debugUsingGameObject;
#endif
        /// <summary>
        ///     AABB that enclose the grass.
        /// </summary>
        public Bounds tightBounds { get; private set; }

        /// <summary>
        ///     AABB that encloses the placement mesh.
        /// </summary>
        public Bounds looseBounds { get; private set; }

        /// <summary>
        ///     The layer used for rendering.
        /// </summary>
        public int UnityLayer = 0;

        /// <summary>
        ///     Marks the instance for re-bake. Call this function after changing settings like the preset or settings.
        /// </summary>
        public void MarkGeometryDirty()
        {
            isGeometryDirty = true;
            if (_hasFinishedBaking)
            {
                TgsGlobalStatus.instancesReady--;
                _hasFinishedBaking = false;
            }
        }

        /// <summary>
        ///     Marks the Instance for updating any purely material related properties (smoothness, texture, etc.).
        /// </summary>
        public void MarkMaterialDirty()
        {
            isMaterialDirty = true;
        }

        public static bool MeshHasVertexColor(Mesh placementMesh, Object errorMessageContext, bool requested)
        {
            int vertexColorOffset = placementMesh.GetVertexAttributeOffset(VertexAttribute.Color);
            if (vertexColorOffset == -1)
            {
                if (requested)
                {
                    Debug.LogError(
                        "Density by vertex color was requested, but no color attribute could be found. Will use constant amount.",
                        errorMessageContext);
                }

                return false;
            }

            return true;
        }

        void BakeFromMesh(ComputeShader tgsComputeShader,
            Mesh sharedMesh, TgsInstanceRecipe tgsInstanceRecipe, Object errorMessageContext = null)
        {
            if (tgsInstanceRecipe.Settings.preset == null)
            {
                return;
            }


            looseBounds = tgsInstanceRecipe.WorldSpaceBounds;
            activeTgsInstanceRecipe = tgsInstanceRecipe;

            int csMeshPassId = tgsComputeShader.FindKernel("MeshPass");

            bool hasVertexColor =
                MeshHasVertexColor(sharedMesh, errorMessageContext, tgsInstanceRecipe.DistributionByVertexColorEnabled);

            // Mesh to bindable mesh
            // --------------------------------
            // At best, we would bind the vertex and index buffer straight to the compute shader as a ByteAddressBuffer.
            // However, that requires "mesh.vertex/indexBufferTarget |= GraphicsBuffer.Target.Raw" to be executed, which breaks the 
            // mesh in build. This is an acknowledge bug in Unity, but they will likely not fix it. 
            //
            // As a likely permanent workaround, we use the MeshAPI to get vertex and index buffer from the mesh,
            // and push them again to the GPU.
            // (Which wasts bandwidth, if you think about that this data is already on the GPU, just not in a way that is accessible.)

            Mesh.MeshDataArray meshDataArray = Mesh.AcquireReadOnlyMeshData(sharedMesh);
            Mesh.MeshData meshData = meshDataArray[0];

            var vertices = new NativeArray<Vector3>(meshData.vertexCount, Allocator.Temp);
            var normals = new NativeArray<Vector3>(meshData.vertexCount, Allocator.Temp);
            var colors = new NativeArray<Color>(meshData.vertexCount, Allocator.Temp);
            var indices = new NativeArray<int>(meshData.GetSubMesh(0).indexCount, Allocator.Temp);

            meshData.GetVertices(vertices);
            meshData.GetNormals(normals);
            if (hasVertexColor)
            {
                meshData.GetColors(colors);
            }
            else
            {
                // do nothing, the native array is initialized with 0 anyways.
            }


            meshData.GetIndices(indices, 0);

            ComputeBuffer verticesGPU = new(vertices.Length, sizeof(float) * 3);
            ComputeBuffer normalsGPU = new(normals.Length, sizeof(float) * 3);
            ComputeBuffer colorsGPU = new(colors.Length, sizeof(float) * 4);
            ComputeBuffer indicesGPU = new(indices.Length, sizeof(int) * 1);

            verticesGPU.SetData(vertices);
            normalsGPU.SetData(normals);
            colorsGPU.SetData(colors);
            indicesGPU.SetData(indices);

            // placement mesh -> compute shader
            // =============================================================================================================
            int placementMeshTriangleCount = indices.Length / 3;

            tgsComputeShader.SetVector(DensityMapChannelMask, tgsInstanceRecipe.DistributionByVertexColorMask);
            tgsComputeShader.SetInt(DensityUseChannelMask,
                tgsInstanceRecipe.DistributionByVertexColorEnabled && hasVertexColor ? 1 : 0);

            tgsComputeShader.SetBuffer(csMeshPassId, PlacementVertices, verticesGPU);
            tgsComputeShader.SetBuffer(csMeshPassId, PlacementNormals, normalsGPU);
            tgsComputeShader.SetBuffer(csMeshPassId, PlacementColors, colorsGPU);
            tgsComputeShader.SetBuffer(csMeshPassId, PlacementIndices, indicesGPU);

            SetupBuffersForCompute(
                tgsComputeShader,
                tgsInstanceRecipe.Settings,
                placementMeshTriangleCount,
                tgsInstanceRecipe.CamouflageTexture,
                tgsInstanceRecipe.CamouflageFactor,
                tgsInstanceRecipe.CamouflageTextureScaleOffset,
                ref _instanceMetaData,
                _instanceMetaDataCPU,
                ref _placementTriangleBuffer,
                ref _grassNoiseLayers);

            // Generative compute shader dispatch
            // =============================================================================================================
            tgsComputeShader.SetMatrix(ObjectToWorld, tgsInstanceRecipe.LocalToWorldMatrix);
            tgsComputeShader.SetBuffer(csMeshPassId, PlacementTrianglesAppend, _placementTriangleBuffer);
            tgsComputeShader.SetBuffer(csMeshPassId, MetaDataRW, _instanceMetaData);
            tgsComputeShader.SetVector(PositionBoundMin, tgsInstanceRecipe.WorldSpaceBounds.min);
            tgsComputeShader.SetVector(PositionBoundMax, tgsInstanceRecipe.WorldSpaceBounds.max);
            tgsComputeShader.GetKernelThreadGroupSizes(
                csMeshPassId,
                out uint csMainKernelThreadCount,
                out _,
                out _);

            int dispatchCount = CeilingDivision(placementMeshTriangleCount, (int)csMainKernelThreadCount);
            tgsComputeShader.Dispatch(csMeshPassId, dispatchCount, 1, 1);

            // Clean up
            indicesGPU.Dispose();
            colorsGPU.Dispose();
            normalsGPU.Dispose();
            verticesGPU.Dispose();

            indices.Dispose();
            colors.Dispose();
            normals.Dispose();
            vertices.Dispose();

            meshDataArray.Dispose();

            _instanceMetaData.GetData(_instanceMetaDataCPU);

            UnpackAndApplyTightBounds(
                tgsComputeShader,
                _instanceMetaDataCPU[0],
                _materialPropertyBlock,
                tgsInstanceRecipe.WorldSpaceBounds,
                out Bounds outTightBounds,
                out float outTightBoundsMaxSideLength
            );
            tightBounds = outTightBounds;

            int estMaxBlades = (int)_instanceMetaDataCPU[0].estMaxBlades;
            BakeFromPlacementBufferOrSetNull(
                tgsComputeShader,
                ref _bakeOutputBuffer,
                _placementTriangleBuffer,
                _materialPropertyBlock,
                (int)_instanceMetaDataCPU[0].placementTriangles,
                estMaxBlades,
                out int bladeCountTmp
            );

            bladeCount = bladeCountTmp;

#if TASTY_GRASS_SHADER_DEBUG
            CheckForDuplicates();
#endif
        }


        internal void BakeNextRecipe()
        {
            ComputeShader tgsComputeShader = TgsManager.tgsComputeShader;
            isGeometryDirty = false;

            if (activeTgsInstanceRecipe.Settings.preset != null)
            {
                activeTgsInstanceRecipe.Settings.preset.SetDirtyOnChangeList.Remove(this);
            }

            if (_nextTgsInstanceRecipe.Settings.preset != null)
            {
                _nextTgsInstanceRecipe.Settings.preset.SetDirtyOnChangeList.Add(this);
            }

            _materialPropertyBlock ??= new MaterialPropertyBlock();

            switch (_nextTgsInstanceRecipe.BakeMode)
            {
                case TgsInstanceRecipe.InstanceBakeMode.FromMesh:
                    BakeFromMesh(
                        tgsComputeShader,
                        _nextTgsInstanceRecipe.SharedMesh,
                        _nextTgsInstanceRecipe);

                    break;
                case TgsInstanceRecipe.InstanceBakeMode.FromHeightmap:
                    BakeFromHeightmap(
                        tgsComputeShader,
                        _nextTgsInstanceRecipe,
                        _nextTgsInstanceRecipe.HeightmapTexture,
                        _nextTgsInstanceRecipe.WorldSpaceBounds,
                        _nextTgsInstanceRecipe.HeightmapChunkPixelOffset,
                        _nextTgsInstanceRecipe.ChunkPixelSize);

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            activeTgsInstanceRecipe = _nextTgsInstanceRecipe;

            if (!_hasFinishedBaking)
            {
                TgsGlobalStatus.instancesReady++;
            }

            _hasFinishedBaking = true;
        }

        void BakeFromHeightmap(
            ComputeShader tgsComputeShader,
            TgsInstanceRecipe tgsInstanceRecipe,
            Texture heightmapTexture,
            Bounds heightmapChunkBoundsWs,
            Vector2Int heightmapChunkPixelOffset,
            Vector2Int chunkPixelSize)
        {
            looseBounds = heightmapChunkBoundsWs;
            int csTerrainPassId = tgsComputeShader.FindKernel("TerrainPass");

            // Setup the heightmap for the compute shader.
            // =============================================================================================================
            tgsComputeShader.SetTexture(csTerrainPassId, Heightmap, heightmapTexture);

            tgsComputeShader.SetVector(HeightmapResolutionXy,
                new Vector2(heightmapTexture.width, heightmapTexture.height));

            tgsComputeShader.SetTexture(csTerrainPassId, DensityMap, tgsInstanceRecipe.DistributionTexture);
            tgsComputeShader.SetVector(DensityMapChannelMask, tgsInstanceRecipe.DistributionTextureChannelMask);
            tgsComputeShader.SetInt(DensityUseChannelMask, tgsInstanceRecipe.DistributionByTextureEnabled ? 1 : 0);

            {
                float heightmapPxToDensityMapUvX =
                    tgsInstanceRecipe.DistributionTexture.width / (float)heightmapTexture.width /
                    tgsInstanceRecipe.DistributionTexture.width;
                float heightmapPxToDensityMapUvY =
                    tgsInstanceRecipe.DistributionTexture.height / (float)heightmapTexture.height /
                    tgsInstanceRecipe.DistributionTexture.height;
                tgsComputeShader.SetVector(DensityMapUvFromHeightmapIdx,
                    new Vector4(
                        heightmapPxToDensityMapUvX * tgsInstanceRecipe.DistributionTextureScaleOffset.x,
                        heightmapPxToDensityMapUvY * tgsInstanceRecipe.DistributionTextureScaleOffset.y,
                        tgsInstanceRecipe.DistributionTextureScaleOffset.z,
                        tgsInstanceRecipe.DistributionTextureScaleOffset.w));
            }

            Debug.Assert(chunkPixelSize is { x: > 0, y: > 0 });

            tgsComputeShader.SetVector(HeightmapChunkOffsetSize,
                new Vector4(heightmapChunkPixelOffset.x, heightmapChunkPixelOffset.y, chunkPixelSize.x,
                    chunkPixelSize.y));

            int placementMeshTriangleCount =
                chunkPixelSize.x * chunkPixelSize.y * 2; // * 2, because two triangles per pixel.


            SetupBuffersForCompute(
                tgsComputeShader,
                tgsInstanceRecipe.Settings,
                placementMeshTriangleCount,
                tgsInstanceRecipe.CamouflageTexture,
                tgsInstanceRecipe.CamouflageFactor,
                tgsInstanceRecipe.CamouflageTextureScaleOffset,
                ref _instanceMetaData,
                _instanceMetaDataCPU,
                ref _placementTriangleBuffer,
                ref _grassNoiseLayers);


            // Generate placing triangles from heightmap
            // =============================================================================================================
            {
                tgsComputeShader.SetVector(PositionBoundMin, heightmapChunkBoundsWs.min);
                tgsComputeShader.SetVector(PositionBoundMax, heightmapChunkBoundsWs.max);
                tgsComputeShader.SetBuffer(csTerrainPassId, PlacementTrianglesAppend, _placementTriangleBuffer);
                tgsComputeShader.SetBuffer(csTerrainPassId, MetaDataRW, _instanceMetaData);
                tgsComputeShader.SetMatrix(ObjectToWorld, tgsInstanceRecipe.LocalToWorldMatrix);

                tgsComputeShader.GetKernelThreadGroupSizes(csTerrainPassId, out uint xThreads, out _, out _);

                int dispatchCount = CeilingDivision(placementMeshTriangleCount, (int)xThreads);
                tgsComputeShader.Dispatch(csTerrainPassId, dispatchCount, 1, 1);
            }

            _instanceMetaData.GetData(_instanceMetaDataCPU);

            int estMaxBlades = (int)_instanceMetaDataCPU[0].estMaxBlades;
            int placementTrianglesValid = (int)_instanceMetaDataCPU[0].placementTriangles;

            UnpackAndApplyTightBounds(
                tgsComputeShader,
                _instanceMetaDataCPU[0],
                _materialPropertyBlock,
                tgsInstanceRecipe.WorldSpaceBounds,
                out Bounds outTightBounds,
                out float outTightBoundsMaxSideLength
            );
            tightBounds = outTightBounds;

#if TASTY_GRASS_SHADER_DEBUG
            BladeCapacity = estMaxBlades;
            UsedPlacementTriangles = placementTrianglesValid;
#endif


            BakeFromPlacementBufferOrSetNull(
                tgsComputeShader,
                ref _bakeOutputBuffer,
                _placementTriangleBuffer,
                _materialPropertyBlock,
                placementTrianglesValid,
                estMaxBlades,
                out int bladeCountTmp
            );

            bladeCount = bladeCountTmp;

#if TASTY_GRASS_SHADER_DEBUG
            {
                ComputeBuffer tmp = new(1, 4, ComputeBufferType.Raw);
                ComputeBuffer.CopyCount(_bakeOutputBuffer, tmp, 0);
                uint[] tmpDst = new uint[1];
                tmp.GetData(tmpDst);
                PlacedBlades = (int)tmpDst[0];
                tmp.Release();
            }
#endif

#if TASTY_GRASS_SHADER_DEBUG
            CheckForDuplicates();
#endif
        }


        /// <summary>
        ///     Sets the bake parameters for the next bake. Don't forget to call MarkGeometryDirty() to apply the
        ///     changes.
        /// </summary>
        /// <param name="nextTgsInstanceRecipe"></param>
        public void SetBakeParameters(TgsInstanceRecipe nextTgsInstanceRecipe)
        {
            _nextTgsInstanceRecipe = nextTgsInstanceRecipe;
        }

        internal void DrawAndUpdateMaterialPropertyBlock(TgsManager.TgsInstancePreRendering instance,
            Camera renderingCamera, Vector4[] colliderBuffer, int colliderCount, bool singlePassVr,
            Material renderingMaterial)
        {
            TgsPreset tgsPreset = activeTgsInstanceRecipe.Settings.preset;

            Profiler.BeginSample("DrawAndUpdateMaterialPropertyBlock");
            // If we have an exception during Drawing, it will teardown the entire frame. Therefore, carefully check for nulls, even if it degrades performance.
            // TODO: these null checks are somewhat expensive.
            if (_bakeOutputBuffer != null && UsedWindSettings != null)
            {
                _materialPropertyBlock.SetInt(SphereColliderCount, colliderCount);
                if (colliderCount > 0)
                {
                    _materialPropertyBlock.SetVectorArray(SphereCollider, colliderBuffer);
                }

                if (isMaterialDirty)
                {
                    isMaterialDirty = false;
                    tgsPreset.ApplyToMaterialPropertyBlock(_materialPropertyBlock);
                }

                // Always apply the wind settings 
                UsedWindSettings.ApplyToMaterialPropertyBlock(_materialPropertyBlock);

                bool useShadows = tgsPreset.castShadows;
                Graphics.DrawProcedural(
                    renderingMaterial,
                    tightBounds,
                    MeshTopology.Triangles,
                    instance.renderingVertexCount,
                    singlePassVr ? 2 : 1,
                    renderingCamera,
                    _materialPropertyBlock,
                    useShadows ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off,
                    true,
                    UnityLayer);
            }
            else
            {
                Debug.LogError(
                    $"Tasty Grass Shader: Unable to render instance. _bakeOutputBuffer={_bakeOutputBuffer}, UsedWindSettings={UsedWindSettings}");
            }

            Profiler.EndSample();
        }

        public int GetGrassBufferMemoryByteSize()
        {
            if (_bakeOutputBuffer != null && _bakeOutputBuffer.IsValid())
            {
                return _bakeOutputBuffer.count * _bakeOutputBuffer.stride;
            }

            return 0;
        }

        public int GetPlacementBufferMemoryByteSize()
        {
            if (_placementTriangleBuffer != null && _placementTriangleBuffer.IsValid())
            {
                return _placementTriangleBuffer.count * _placementTriangleBuffer.stride;
            }

            return 0;
        }


        public void Release()
        {
            _placementTriangleBuffer?.Release();
            _placementTriangleBuffer = null;

            _instanceMetaData?.Release();
            _instanceMetaData = null;

            _bakeOutputBuffer?.Release();
            _bakeOutputBuffer = null;

            _grassNoiseLayers?.Release();
            _grassNoiseLayers = null;

            TgsGlobalStatus.instances--;
            if (_hasFinishedBaking)
            {
                TgsGlobalStatus.instancesReady--;
            }

            AllInstances.Remove(this);

            if (activeTgsInstanceRecipe.Settings.preset != null)
            {
                activeTgsInstanceRecipe.Settings.preset.SetDirtyOnChangeList.Remove(this);
            }
        }

        public static float ComputeCameraFovScalingFactor(Camera camera)
        {
            return 2.0f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        }

        public static int CeilingDivision(int lhs, int rhs)
        {
            return (lhs + rhs - 1) / rhs;
        }

        public static float CeilingDivisionFloat(float lhs, float rhs)
        {
            return (lhs + rhs - 1) / rhs;
        }

        struct InstanceMetaDataGPU
        {
            public const int Stride =
                    sizeof(uint) // estMaxBlades
                    + sizeof(uint) // placementTriangles
                    + sizeof(uint) * 3 // boundsMin
                    + sizeof(uint) * 3 // boundsMax
                ;

            public uint estMaxBlades;
            public uint placementTriangles;
            public uint boundsMinX, boundsMinY, boundsMinZ;
            public uint boundsMaxX, boundsMaxY, boundsMaxZ;
        }

        /// <summary>
        ///     A container for *what* kind of grass to grow and *where* (Mesh, chunk of a heightmap, ...) to grow that grass.
        ///     Use BakeFromHeightmap() or BakeFromMesh() to properly create an Recipe.
        /// </summary>
        public struct TgsInstanceRecipe
        {
            internal TgsPreset.Settings Settings;

            internal Matrix4x4 LocalToWorldMatrix;
            internal Bounds WorldSpaceBounds;

            // Heightmap distribution texture
            internal bool DistributionByTextureEnabled;
            internal Texture DistributionTexture;
            internal Vector4 DistributionTextureChannelMask;
            internal Vector4 DistributionTextureScaleOffset;

            // Camouflage
            internal float CamouflageFactor;
            internal Texture CamouflageTexture;
            internal Vector4 CamouflageTextureScaleOffset;

            // Heightmap related settings.
            internal Texture HeightmapTexture;
            internal Vector2Int HeightmapChunkPixelOffset;
            internal Vector2Int ChunkPixelSize;

            // Mesh related settings.
            internal Mesh SharedMesh;
            internal InstanceBakeMode BakeMode;

            // Mesh distribution by vertex color
            internal bool DistributionByVertexColorEnabled;
            internal Color DistributionByVertexColorMask;

            static TgsInstanceRecipe GetDefaultInstance()
            {
                TgsInstanceRecipe tgsInstanceRecipe = new()
                {
                    DistributionTexture = Texture2D.whiteTexture,
                    DistributionTextureChannelMask = new Vector4(1.0f, 0.0f, 0.0f, 0.0f),
                    CamouflageTexture = Texture2D.whiteTexture,
                    HeightmapTexture = Texture2D.blackTexture,
                    DistributionByVertexColorMask = Color.white
                };

                return tgsInstanceRecipe;
            }


            public static TgsInstanceRecipe BakeFromHeightmap(
                Matrix4x4 localToWorldMatrix,
                TgsPreset.Settings settings,
                Texture heightmapTexture,
                Bounds heightmapChunkBounds,
                Vector2Int heightmapChunkPixelOffset,
                Vector2Int chunkPixelSize)
            {
                TgsInstanceRecipe newParameters = GetDefaultInstance();
                newParameters.BakeMode = InstanceBakeMode.FromHeightmap;

                newParameters.LocalToWorldMatrix = localToWorldMatrix;
                newParameters.Settings = settings;
                newParameters.HeightmapTexture = heightmapTexture;
                newParameters.WorldSpaceBounds = heightmapChunkBounds;
                newParameters.HeightmapChunkPixelOffset = heightmapChunkPixelOffset;
                newParameters.ChunkPixelSize = chunkPixelSize;

                return newParameters;
            }

            public static TgsInstanceRecipe BakeFromMesh(
                Matrix4x4 localToWorldMatrix,
                TgsPreset.Settings settings,
                Mesh sharedMesh,
                Bounds worldSpaceBounds)
            {
                TgsInstanceRecipe newParameters = GetDefaultInstance();
                newParameters.BakeMode = InstanceBakeMode.FromMesh;

                newParameters.LocalToWorldMatrix = localToWorldMatrix;
                newParameters.Settings = settings;
                newParameters.SharedMesh = sharedMesh;
                newParameters.WorldSpaceBounds = worldSpaceBounds;


                return newParameters;
            }

            public void SetupDistributionByTexture(Texture densityTexture, Vector4 channelMask, Vector4 scaleOffset)
            {
                if (BakeMode == InstanceBakeMode.FromMesh)
                {
                    Debug.LogError("SetupDistributionByTexture() is not supported with meshes currently.");
                    return;
                }

                DistributionByTextureEnabled = true;
                DistributionTexture = densityTexture == null ? Texture2D.whiteTexture : densityTexture;
                DistributionTextureChannelMask = channelMask;
                DistributionTextureScaleOffset = scaleOffset;
            }

            public void SetupCamouflage(Texture colorMap, Vector4 colorMapScaleOffset, float blendFactor)
            {
                if (BakeMode == InstanceBakeMode.FromMesh)
                {
                    Debug.LogError("SetupCamouflage() is not supported with meshes currently.");
                    return;
                }

                CamouflageTexture = colorMap == null ? Texture2D.whiteTexture : colorMap;
                CamouflageTextureScaleOffset = colorMapScaleOffset;
                CamouflageFactor = blendFactor;
            }

            public void SetupDistributionByVertexColor(Color mask)
            {
                if (BakeMode == InstanceBakeMode.FromHeightmap)
                {
                    Debug.LogError("SetupDistributionByVertexColor() is not supported with heightmaps.");
                    return;
                }

                DistributionByVertexColorEnabled = true;
                DistributionByVertexColorMask = mask;
            }


            internal enum InstanceBakeMode
            {
                FromMesh,
                FromHeightmap
            }
        }

        #region Internal Shared Functions

        static Vector3 UnpackVector3_32Bit(uint vX, uint vY, uint vZ, Vector3 min, Vector3 max)
        {
            double range = 4294967294.0; // == (1 << 32) - 1
            double x = vX / range;
            double y = vY / range;
            double z = vZ / range;
            return new Vector3(
                Mathf.Lerp(min.x, max.x, (float)x),
                Mathf.Lerp(min.y, max.y, (float)y),
                Mathf.Lerp(min.z, max.z, (float)z));
        }

        static void UnpackAndApplyTightBounds(
            ComputeShader tgsComputeShader,
            InstanceMetaDataGPU instanceMetaDataGPU,
            MaterialPropertyBlock materialPropertyBlock,
            Bounds looseBounds,
            out Bounds tightBounds,
            out float tightBoundsMaxSideLength
        )
        {
            Vector3 minBounds = UnpackVector3_32Bit(
                instanceMetaDataGPU.boundsMinX,
                instanceMetaDataGPU.boundsMinY,
                instanceMetaDataGPU.boundsMinZ,
                looseBounds.min,
                looseBounds.max);

            Vector3 maxBounds = UnpackVector3_32Bit(
                instanceMetaDataGPU.boundsMaxX,
                instanceMetaDataGPU.boundsMaxY,
                instanceMetaDataGPU.boundsMaxZ,
                looseBounds.min,
                looseBounds.max);

            Bounds gpuComputedBounds = new();
            gpuComputedBounds.SetMinMax(minBounds, maxBounds);
            // Rendering Bounds
            // =============================================================================================================
            // NOTE: can't use Expand() on looseGrassFieldBounds directly, since its a get/set thing and the new value will never be written.
            gpuComputedBounds.Expand(MaxGrassRootOffset);

            tightBounds = gpuComputedBounds;

            tightBoundsMaxSideLength = Mathf.Max(tightBounds.size.x,
                Mathf.Max(tightBounds.size.y, tightBounds.size.z));

            materialPropertyBlock.SetVector(PositionBoundMin, tightBounds.min);
            materialPropertyBlock.SetVector(PositionBoundMax, tightBounds.max);

            tgsComputeShader.SetVector(PositionBoundMin, tightBounds.min);
            tgsComputeShader.SetVector(PositionBoundMax, tightBounds.max);
        }

        static void SetupBuffersForCompute(
            ComputeShader tgsComputeShader,
            TgsPreset.Settings settings,
            int placementMeshTriangleCount,
            Texture colorMap,
            float colorMapBlend,
            Vector4 colorMapScaleOffset,
            ref ComputeBuffer instanceMetaData,
            InstanceMetaDataGPU[] instanceMetaDataCPU,
            ref ComputeBuffer placementTriangleBuffer,
            ref ComputeBuffer grassNoiseLayers)
        {
            // grass field buffers -> compute shader
            // =============================================================================================================

            int csBakePassId = tgsComputeShader.FindKernel("BakePass");
            instanceMetaData?.Release();
            instanceMetaData = new ComputeBuffer(
                1,
                InstanceMetaDataGPU.Stride);

            instanceMetaDataCPU[0] = new InstanceMetaDataGPU
            {
                boundsMinX = 0xFFFFFFFF,
                boundsMinY = 0xFFFFFFFF,
                boundsMinZ = 0xFFFFFFFF
            };
            instanceMetaData.SetData(instanceMetaDataCPU);

            // Prepare placement triangle buffer
            if (placementTriangleBuffer == null || placementMeshTriangleCount != placementTriangleBuffer.count)
            {
                placementTriangleBuffer?.Release();
                placementTriangleBuffer = new ComputeBuffer(
                    placementMeshTriangleCount,
                    SizeofPlacementTriangle,
                    ComputeBufferType.Append);
            }

            placementTriangleBuffer.SetCounterValue(0);


            // Noise Layers
            grassNoiseLayers ??= new ComputeBuffer(
                TgsPreset.NoiseSettingGPU.MaxCount,
                TgsPreset.NoiseSettingGPU.Stride);


            settings.preset.ApplyLayerSettingsToBuffer(grassNoiseLayers, settings);
            tgsComputeShader.SetBuffer(csBakePassId, NoiseParams, grassNoiseLayers);
            tgsComputeShader.SetBuffer(csBakePassId, PlacementTrianglesR, placementTriangleBuffer);

            tgsComputeShader.SetInt(PlacementTriangleCount, placementMeshTriangleCount);
            settings.preset.ApplyToComputeShader(tgsComputeShader, settings, csBakePassId);

            tgsComputeShader.SetTexture(csBakePassId, ColorMap, colorMap);
            tgsComputeShader.SetFloat(ColorMapBlend, colorMapBlend);
            tgsComputeShader.SetVector(ColorMapSt, colorMapScaleOffset);
        }

        static void BakeFromPlacementBufferOrSetNull(
            ComputeShader tgsComputeShader,
            ref ComputeBuffer bakeOutputBuffer,
            ComputeBuffer placementTriangleBuffer,
            MaterialPropertyBlock materialPropertyBlock,
            int placementTriangleCount,
            int estMaxBlades,
            out int bladeCount)
        {
            bool shouldBake = placementTriangleCount > 0 && estMaxBlades > 0;

            if (shouldBake)
            {
                if (bakeOutputBuffer == null || bakeOutputBuffer.count != estMaxBlades)
                {
                    bakeOutputBuffer?.Release();
                    bakeOutputBuffer = new ComputeBuffer(
                        estMaxBlades,
                        GrassNodeStride,
                        ComputeBufferType.Append);
                }

                bakeOutputBuffer.SetCounterValue(0);

                int csBakePassId = tgsComputeShader.FindKernel("BakePass");

                // Dispatch Bake Pass
                // =========================================================================================================
                tgsComputeShader.SetBuffer(csBakePassId, PlacementTrianglesR, placementTriangleBuffer);
                tgsComputeShader.SetBuffer(csBakePassId, GrassNodePrimitivesAppend, bakeOutputBuffer);
                tgsComputeShader.SetInt(UsedPlacementTriangleCount, placementTriangleCount);

                // Allows to handle more than 65535 triangles.
                const int maxThreadsPerDispatch = 65535;
                int requiredDispatches = CeilingDivision(placementTriangleCount, maxThreadsPerDispatch);

                for (int dispatchIndex = 0; dispatchIndex < requiredDispatches; dispatchIndex++)
                {
                    tgsComputeShader.SetInt(PlacementTriangleOffset, dispatchIndex * maxThreadsPerDispatch);
                    tgsComputeShader.Dispatch(csBakePassId, placementTriangleCount, 1, 1);
                }

                // TODO: this buffer may be only allocated once.
                ComputeBuffer bladeCounterTarget = new(1, sizeof(int), ComputeBufferType.Raw);
                ComputeBuffer.CopyCount(bakeOutputBuffer, bladeCounterTarget, 0);
                bladeCounterTarget.GetData(_bladeCountTmpBuffer);
                bladeCounterTarget.Dispose();
                bladeCount = _bladeCountTmpBuffer[0];
            }
            else
            {
                bladeCount = 0;
                // Release the blade buffer to indicate that there is nothing to render.
                bakeOutputBuffer?.Release();
                bakeOutputBuffer = null;
            }

            materialPropertyBlock.SetBuffer(GrassNodePrimitives, bakeOutputBuffer);
        }

        #endregion

#if TASTY_GRASS_SHADER_DEBUG
        void CheckForDuplicates()
        {
            if (ValidateNoDuplicateBlades && _bakeOutputBuffer != null)
            {
                ComputeBuffer count = new(1, 4, ComputeBufferType.Raw);
                ComputeBuffer.CopyCount(_bakeOutputBuffer, count, 0);
                int[] bladeCount = new int[1];
                count.GetData(bladeCount);
                count.Release();

                var blades = new GrassBladeReference[bladeCount[0]];
                _bakeOutputBuffer.GetData(blades);

                var bladeMap = new HashSet<GrassBladeReference>(blades.Length);

                int duplicateCount = 0;
                foreach (GrassBladeReference blade in blades)
                {
                    if (bladeMap.Contains(blade))
                    {
                        duplicateCount++;
                    }

                    bladeMap.Add(blade);
                }

                if (duplicateCount > 0)
                {
                    Debug.LogError($"Found {duplicateCount} duplicates!");
                }
                else
                {
                    Debug.Log("Found no duplicates!");
                }
            }
        }

        struct GrassBladeReference
        {
            Vector3 root, side, tip;
            Vector3 normal;
            Vector3 color;
        }
#endif
#if TASTY_GRASS_SHADER_DEBUG
        public int PlacedBlades;
        public int BladeCapacity;
        public int UsedPlacementTriangles;


#endif
    }
}