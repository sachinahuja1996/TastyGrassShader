using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    [CustomEditor(typeof(TgsForUnityTerrain))]
    [CanEditMultipleObjects]
    public class TgsGrassFieldFromTerrainEditor : UnityEditor.Editor
    {
        void OnEnable()
        {
            Undo.undoRedoPerformed += UndoRedoPerformed;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= UndoRedoPerformed;
        }

        void UndoRedoPerformed()
        {
            TgsForUnityTerrain tgsut = (TgsForUnityTerrain)serializedObject.targetObject;
            // At this point, we assume that the texture containing the amount information has been reverted.
            // We still need set all chunks dirty however, so that the change is applied to the grass.
            tgsut.MarkGeometryDirty();
        }

        // static string styleName = "";
        public override void OnInspectorGUI()
        {
            SharedEditorTools.SharedEditorGUIHeader();

            TgsForUnityTerrain tgsut = (TgsForUnityTerrain)serializedObject.targetObject;
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            // styleName = EditorGUILayout.TextField("style index", styleName);
            // GUIStyle styleVertical = GUI.skin.FindStyle(styleName);
            GUIStyle style = GUI.skin.FindStyle("FrameBox");

            SerializedProperty layers = serializedObject.FindProperty("layers");
            SharedEditorTools.DrawLayerInspector(
                layers,
                tgsut,
                index => tgsut.GetLayerByIndex(index).GetEditorName(index),
                tgsut.RemoveLayerAt,
                tgsut.AddNewLayer,
                tgsut.GetLayerCount,
                index => tgsut.GetLayerByIndex(index).hide,
                (index, hide) => tgsut.GetLayerByIndex(index).hide = hide);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("windSettings"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("showChunksBounds"));


            if (GUILayout.Button("Manual Update"))
            {
                tgsut.OnPropertiesMayChanged();
            }

            EditorGUILayout.Space();

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                tgsut.OnPropertiesMayChanged();
            }

            int instanceCount = tgsut.GetChunkCount();
            long grassMemoryBufferByte = tgsut.GetGrassMemoryBufferByteSize();
            long placementMemoryByteCount = tgsut.GetPlacementMemoryBufferByteSize();

            EditorGUILayout.HelpBox(
                $"Memory Statistics:\nGeometry Buffer Size: {grassMemoryBufferByte / (1024.0 * 1024.0):F2}MB ({instanceCount} Chunks)\nPlacement Buffer Size: {placementMemoryByteCount / (1024.0 * 1024.0):F2}MB ({instanceCount} Chunks)",
                MessageType.Info);
            SharedEditorTools.SharedEditorFooter();
        }
    }

    [EditorTool("Tasty Grass Shader - Terrain Painter", typeof(TgsForUnityTerrain))]
    internal class TgsTerrainPainterTool : EditorTool
    {
        static readonly GUID EditorIcon = new("d4444677ba7fa1b459c1d1341e86d5a6");
        float _brushNoiseIntensity;

        float _brushNoiseScale = 1.0f;

        float _brushOpacity = 5.0f;

        float _brushRadius = 8.0f;
        float _floodValue = 1.0f;

        bool _isMouseDown, _isShiftDown, _isInsideGui;

        Rect _lastWindowRect;
        Vector2 _splatMapScrollView;
        int _targetIndex;

        // Called when the active tool is set to this tool instance. Global tools are persisted by the ToolManager,
        // so usually you would use OnEnable and OnDisable to manage native resources, and OnActivated/OnWillBeDeactivated
        // to set up state. See also `EditorTools.{ activeToolChanged, activeToolChanged }` events.
        // public override void OnActivated()
        // {
        //     SceneView.lastActiveSceneView.ShowNotification(new GUIContent("Entering TGS Painting Tool"), .25f);
        // }
        //
        // // Called before the active tool is changed, or destroyed. The exception to this rule is if you have manually
        // // destroyed this tool (ex, calling `Destroy(this)` will skip the OnWillBeDeactivated invocation).
        // public override void OnWillBeDeactivated()
        // {
        //     SceneView.lastActiveSceneView.ShowNotification(new GUIContent("Exiting TGS Painting Tool"), .25f);
        // }

        public override GUIContent toolbarIcon
        {
            get
            {
                Sprite texture = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(EditorIcon));
                return new GUIContent(texture.texture);
            }
        }

        // // The second "context" argument accepts an EditorWindow type.
        // [Shortcut("Activate Platform Tool", typeof(SceneView))]
        public static void OpenTool()
        {
            if (Selection.GetFiltered<TgsForUnityTerrain>(SelectionMode.TopLevel).Length > 0)
            {
                ToolManager.SetActiveTool<TgsTerrainPainterTool>();
            }
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView)
            {
                return;
            }

            Event e = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlID);
            EventType eventType = e.GetTypeForControl(controlID);

            TgsForUnityTerrain tgsForUnityTerrain = (TgsForUnityTerrain)target;

            bool isMouseEvent = false;
            switch (eventType)
            {
                case EventType.MouseDown:
                    if (e.button == 0) // allow only left mousebutton
                    {
                        _isMouseDown = true;
                    }

                    break;
                case EventType.MouseUp:
                    if (e.button == 0) // allow only left mousebutton
                    {
                        _isMouseDown = false;
                    }

                    break;
                case EventType.MouseMove:
                    isMouseEvent = true;
                    break;
            }

            if (isMouseEvent)
            {
                // Only check if the mouse overlaps with the tool window if the mouse is moving.
                // This is not super elegant, but works reliable. (In contrast to many online solutions...)
                _isInsideGui = _lastWindowRect.Contains(e.mousePosition);
                if (_isInsideGui)
                {
                    _isMouseDown = false;
                }
            }


            TerrainCollider terrainCollider = tgsForUnityTerrain.GetComponent<TerrainCollider>();
            Terrain terrain = tgsForUnityTerrain.GetComponent<Terrain>();
            TerrainData terrainData = terrain.terrainData;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            SceneView sceneView = (SceneView)window;

            bool didPainted = false;

            bool allowedToDraw =
                tgsForUnityTerrain.GetLayerCount() != 0 && _targetIndex < tgsForUnityTerrain.GetLayerCount() &&
                tgsForUnityTerrain.GetLayerByIndex(_targetIndex).distribution ==
                TgsTerrainLayer.TerrainLayerDistribution.TastyGrassShaderPaintTool &&
                sceneView.sceneViewState.alwaysRefresh;
            bool mouseOverTerrain = terrainCollider.Raycast(ray, out RaycastHit hit, float.MaxValue);
            if (!_isInsideGui && mouseOverTerrain && allowedToDraw)
            {
                float opacity = _brushOpacity;
                if (e.shift)
                {
                    opacity = -opacity;
                }

                Handles.color = e.shift ? Color.red : Color.green;
                Handles.color = Color.Lerp(_isMouseDown ? Color.white : Color.black, Handles.color, 0.75f);

                Handles.DrawWireDisc(hit.point, hit.normal, _brushRadius);

                if (_isMouseDown)
                {
                    Undo.RecordObject(tgsForUnityTerrain.GetLayerByIndex(_targetIndex).paintedDensityMapStorage,
                        "Paint to Density Map");
                    tgsForUnityTerrain.GetLayerByIndex(_targetIndex).DrawToUserDetailMapDeferred(hit.point,
                        _brushRadius,
                        opacity, _brushNoiseIntensity, _brushNoiseScale);
                    didPainted = true;
                }
            }

            Handles.BeginGUI();
            GUIStyle style = EditorStyles.helpBox;

            // Adjusting the GUI tint for better readability.
            Color backgroundTint = Color.white;
            backgroundTint.a *= 4.0f;
            Color prevGuiBackgroundColor = GUI.backgroundColor;
            Color prevGuiContentColor = GUI.contentColor;
            GUI.backgroundColor = backgroundTint;

            GUI.contentColor = Color.white;

            const float width = 300.0f;

            if (!sceneView.sceneViewState.alwaysRefresh)
            {
                GUILayout.Space(12.0f);
                GUI.backgroundColor = Color.yellow;
                using (new GUILayout.VerticalScope("Warning", style, GUILayout.Width(width)))
                {
                    GUILayout.Space(12.0f);
                    EditorGUILayout.LabelField("Always Refresh needs to be enabled.");
                    if (GUILayout.Button("Enable Always Refresh"))
                    {
                        sceneView.sceneViewState.alwaysRefresh = true;
                    }
                }

                GUI.backgroundColor = backgroundTint;
                _lastWindowRect = GUILayoutUtility.GetLastRect();
            }

            GUILayout.Space(12.0f);
            using (new GUILayout.VerticalScope("Target", style, GUILayout.Width(width)))
            {
                GUILayout.Space(12.0f);
                EditorGUILayout.LabelField(
                    $"Which layer to edit. Only layer with distribution set to \n\"{TgsTerrainLayer.TerrainLayerDistribution.TastyGrassShaderPaintTool}\" are valid.",
                    EditorStyles.miniLabel);
                int index = 0;
                for (int layerIndex = 0; layerIndex < tgsForUnityTerrain.GetLayerCount(); layerIndex++)
                {
                    TgsTerrainLayer o = tgsForUnityTerrain.GetLayerByIndex(layerIndex);
                    bool canBePaintedTo = o.settings.preset == null || o.distribution !=
                        TgsTerrainLayer.TerrainLayerDistribution.TastyGrassShaderPaintTool;
                    EditorGUI.BeginDisabledGroup(canBePaintedTo);

                    bool selected = GUILayout.Toggle(index == _targetIndex, o.GetEditorName(layerIndex));
                    EditorGUI.EndDisabledGroup();
                    if (selected)
                    {
                        _targetIndex = index;
                    }

                    index++;
                }
            }


            _lastWindowRect = GUILayoutUtility.GetLastRect();

            GUILayout.Space(12.0f);


            using (new EditorGUI.DisabledScope(!allowedToDraw))
            {
                using (new GUILayout.VerticalScope("Brush Settings", style, GUILayout.Width(width)))
                {
                    GUILayout.Space(12.0f);
                    EditorGUILayout.LabelField("Which instance of TGS to edit.", EditorStyles.miniLabel);
                    _brushRadius = GUISlider("Size", _brushRadius, 0.0f, 50.0f);
                    //_brushHardness = GUISlider("Hardness", _brushHardness, 0.0f, 1.0f);

                    _brushOpacity = GUISlider("Opacity", _brushOpacity, 0.0f, 10.0f);

                    _brushNoiseIntensity = GUISlider("Noise Intensity", _brushNoiseIntensity, 0.0f, 1.0f);
                    _brushNoiseScale = GUISlider("Noise Tiling", _brushNoiseScale, 0.0f, 8.0f);
                    EditorGUILayout.LabelField("Left-Click: Draw", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField("Hold Shift: Erase", EditorStyles.miniLabel);
                }
            }


            _lastWindowRect = RectJoin(_lastWindowRect, GUILayoutUtility.GetLastRect());
            GUILayout.Space(12.0f);
            using (new EditorGUI.DisabledScope(!allowedToDraw))
            {
                using (new GUILayout.VerticalScope("Terrain Splatmap", style, GUILayout.Width(width)))
                {
                    GUILayout.Space(12.0f);
                    EditorGUILayout.LabelField("Use the amount from a Terrain Layer.", EditorStyles.miniLabel);
                    //_splatMapScrollView = EditorGUILayout.BeginScrollView(_splatMapScrollView, false, true, GUILayout.Height(75.0f));
                    {
                        int unityTerrainLayerIndex = 0;
                        foreach (TerrainLayer layer in terrainData.terrainLayers)
                        {
                            int alphaMapIndex = unityTerrainLayerIndex / 4;
                            int alphaMapSubIndex = unityTerrainLayerIndex % 4;
                            Vector4 densityMapMask = new(
                                alphaMapSubIndex == 0 ? 1.0f : 0.0f,
                                alphaMapSubIndex == 1 ? 1.0f : 0.0f,
                                alphaMapSubIndex == 2 ? 1.0f : 0.0f,
                                alphaMapSubIndex == 3 ? 1.0f : 0.0f);

                            GUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"\"{layer.name}\"");
                            if (GUILayout.Button("+"))
                            {
                                Undo.RecordObject(
                                    tgsForUnityTerrain.GetLayerByIndex(_targetIndex).paintedDensityMapStorage,
                                    $"Added {layer.name} to Density Map");
                                tgsForUnityTerrain.GetLayerByIndex(_targetIndex)
                                    .FillUserDetailMapDeferred(terrainData.alphamapTextures[alphaMapIndex],
                                        densityMapMask,
                                        true);
                                didPainted = true;
                            }

                            if (GUILayout.Button("-"))
                            {
                                Undo.RecordObject(
                                    tgsForUnityTerrain.GetLayerByIndex(_targetIndex).paintedDensityMapStorage,
                                    $"Subtracted {layer.name} to Density Map");
                                tgsForUnityTerrain.GetLayerByIndex(_targetIndex)
                                    .FillUserDetailMapDeferred(terrainData.alphamapTextures[alphaMapIndex],
                                        -densityMapMask,
                                        true);
                                didPainted = true;
                            }

                            if (GUILayout.Button("="))
                            {
                                Undo.RecordObject(
                                    tgsForUnityTerrain.GetLayerByIndex(_targetIndex).paintedDensityMapStorage,
                                    $"Set {layer.name} to Density Map");
                                tgsForUnityTerrain.GetLayerByIndex(_targetIndex)
                                    .FillUserDetailMapDeferred(terrainData.alphamapTextures[alphaMapIndex],
                                        densityMapMask,
                                        false);
                                didPainted = true;
                            }

                            GUILayout.EndHorizontal();
                            unityTerrainLayerIndex++;
                        }
                    }
                    //  EditorGUILayout.EndScrollView();
                }
            }

            _lastWindowRect = RectJoin(_lastWindowRect, GUILayoutUtility.GetLastRect());
            GUILayout.Space(12.0f);
            using (new EditorGUI.DisabledScope(!allowedToDraw))
            {
                using (new GUILayout.VerticalScope("Flood Fill", style, GUILayout.Width(width)))
                {
                    GUILayout.Space(12.0f);

                    EditorGUILayout.LabelField("Set the amount to a constant value.", EditorStyles.miniLabel);
                    using (new GUILayout.HorizontalScope())
                    {
                        _floodValue = GUISlider("Fill Value", _floodValue, 0.0f, 1.0f);
                        if (GUILayout.Button("="))
                        {
                            Undo.RecordObject(tgsForUnityTerrain.GetLayerByIndex(_targetIndex).paintedDensityMapStorage,
                                "Set amount to Density Map");
                            tgsForUnityTerrain.GetLayerByIndex(_targetIndex).FillUserDetailMapDeferred(
                                Texture2D.whiteTexture,
                                new Vector4(_floodValue, 0.0f), false);
                            didPainted = true;
                        }
                    }
                }
            }

            _lastWindowRect = RectJoin(_lastWindowRect, GUILayoutUtility.GetLastRect());
            GUI.backgroundColor = prevGuiBackgroundColor;
            GUI.contentColor = prevGuiContentColor;
            Handles.EndGUI();

            if (didPainted)
            {
                // Force refresh if we changed everything. Helps with laggy editors.
                tgsForUnityTerrain.InstancesUpdateGrass();
            }
        }


        float GUISlider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100.0f));
            value = GUILayout.HorizontalSlider(value, min, max);
            value = EditorGUILayout.FloatField(string.Empty, value, GUILayout.Width(40.0f));
            GUILayout.EndHorizontal();
            //GUILayout.Space(8.0f);
            return value;
        }

        static Rect RectJoin(Rect a, Rect b)
        {
            float xMin = Mathf.Min(a.min.x, b.min.x);
            float yMin = Mathf.Min(a.min.y, b.min.y);
            float xMax = Mathf.Max(a.max.x, b.max.x);
            float yMax = Mathf.Max(a.max.y, b.max.y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}