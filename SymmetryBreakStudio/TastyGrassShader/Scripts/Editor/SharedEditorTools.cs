using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    public static class SharedEditorTools
    {
        public delegate int LayerAdd();

        public delegate int LayerGetCount();

        public delegate bool LayerGetHideStatusAt(int index);

        // NOTE: I use delegates in this case over interfaces,
        // because interfaces can't take all methods that are dealing with layers without introducing templates.
        // Once templates are introduced, it's not possible to have a common function to draw the inspector anymore, since the type MUST be specified.
        // Delegates are more flexible in that regard. 
        public delegate string LayerGetName(int index);

        public delegate void LayerRemoveAt(int index);

        public delegate void LayerSetHideStatusAt(int index, bool hide);

        public const string urpPackagePath = "Assets/SymmetryBreakStudio/TastyGrassShader/TGS_URP.unitypackage";
        public const string urpBackendPath = "Assets/SymmetryBreakStudio/TastyGrassShader/URP";
        public const string urpExamplesPath = "Assets/SymmetryBreakStudio/TastyGrassShader/Examples/URP";

        public const string hdrpPackagePath = "Assets/SymmetryBreakStudio/TastyGrassShader/TGS_HDRP.unitypackage";
        public const string hdrpBackendPath = "Assets/SymmetryBreakStudio/TastyGrassShader/HDRP";
        public const string hdrpExamplesPath = "Assets/SymmetryBreakStudio/TastyGrassShader/Examples/HDRP";

        public static readonly string productName = "Tasty Grass Shader " + UpdateHandler.ThisVersion;

        public static void DrawLayerInspector(
            SerializedProperty layers,
            Object instanceAsObject,
            LayerGetName layerGetName,
            LayerRemoveAt layerRemoveAt,
            LayerAdd layerAdd,
            LayerGetCount getLayerCount,
            LayerGetHideStatusAt getHideStatusAt,
            LayerSetHideStatusAt setHideStatusAt)
        {
            GUIStyle style = GUI.skin.FindStyle("FrameBox");
            layers.isExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(layers.isExpanded, "Layers");

            if (layers.isExpanded)
            {
                EditorGUI.indentLevel += 1;
                using (new GUILayout.VerticalScope(style))
                {
                    for (int i = 0; i < layers.arraySize; i++)
                    {
                        SerializedProperty arrayItem = layers.GetArrayElementAtIndex(i);
                        EditorGUILayout.BeginHorizontal(style);
                        EditorGUILayout.PropertyField(arrayItem, new GUIContent(layerGetName(i)));

                        setHideStatusAt(i, GUILayout.Toggle(getHideStatusAt(i), "Hide", GUILayout.Width(60.0f)));

                        if (GUILayout.Button(new GUIContent("X", null, "Delete this layer."), GUILayout.Width(20.0f)))
                        {
                            Undo.RecordObject(instanceAsObject, "Remove Layer");
                            layerRemoveAt(i);
                            break;
                        }


                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.Space();
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label($"Layers: {getLayerCount()}");
                        if (GUILayout.Button("Add New Layer"))
                        {
                            Undo.RecordObject(instanceAsObject, "Add Layer");
                            layerAdd();
                        }
                    }
                }

                EditorGUI.indentLevel -= 1;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        public enum UnityRp
        {
            Unknown,
            BuiltIn,
            Universal,
            HighDefinition,
        }

        public static UnityRp GetActiveRenderPipeline(bool logErrors)
        {
            // NOTE/HACK: Just checking the string for a keyword,
            // instead comparing for the type prevents us from referencing URP or HDRP assemblies.
            RenderPipelineAsset activePipeline = GraphicsSettings.currentRenderPipeline;
            if (activePipeline == null)
            {
                if (logErrors)
                {
                    Debug.LogError("Tasty Grass Shader: Using the Built-In pipeline is not supported.");
                }

                return UnityRp.BuiltIn;
            }

            string activePipelineType = activePipeline.GetType().ToString();
#if TGS_URP_INSTALLED
            if (activePipelineType.Contains("Universal"))
            {
                return UnityRp.Universal;
            }
#endif

#if TGS_HDRP_INSTALLED
            if (activePipelineType.Contains("HighDefinition"))
            {
                return UnityRp.HighDefinition;
            }
#endif

            if (logErrors)
            {
                Debug.LogError(
                    "Tasty Grass Shader: unable to determine the used render pipeline. If you have the Universal- or High-Definition Rendering Pipeline active, then is this is likely a bug. We would be very happy if you reach out to us in that case. ");
            }

            return UnityRp.Unknown;
        }

        private static GlobalKeyword tgsEditorUseUrp = GlobalKeyword.Create("TGS_EDITOR_USE_URP");
        private static GlobalKeyword tgsEditorUseHdrp = GlobalKeyword.Create("TGS_EDITOR_USE_HDRP");

        public static void UpdateEditorShaderRenderPipelineVariants()
        {
            UnityRp activePipeline = GetActiveRenderPipeline(true);
            Shader.SetKeyword(tgsEditorUseUrp, activePipeline == UnityRp.Universal);
            Shader.SetKeyword(tgsEditorUseHdrp, activePipeline == UnityRp.HighDefinition);
        }

        static string LoadTextAssetOrEmpty(string path)
        {
            string result = string.Empty;
            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (textAsset != null)
            {
                result = textAsset.text;
            }

            return result;
        }

        private static string urpVersionPath =
            "Assets/SymmetryBreakStudio/TastyGrassShader/URP/TGS_URP_BackendVersion.txt";

        public static string GetUrpSubpackageVersion()
        {
            return LoadTextAssetOrEmpty(urpVersionPath);
        }

        public static bool IsUrpPackageInstalled()
        {
            return !string.IsNullOrEmpty(GetUrpSubpackageVersion());
        }


        private static string hdrpVersionPath =
            "Assets/SymmetryBreakStudio/TastyGrassShader/HDRP/TGS_HDRP_BackendVersion.txt";

        public static string GetHdrpSubpackageVersion()
        {
            return LoadTextAssetOrEmpty(hdrpVersionPath);
        }

        public static bool IsHdrpPackageInstalled()
        {
            return !string.IsNullOrEmpty(GetHdrpSubpackageVersion());
        }

        public static void SharedEditorGUIHeader()
        {
            bool tgsHdrpInstalled = IsHdrpPackageInstalled();
            bool tgsUrpInstalled = IsUrpPackageInstalled();
            if (!tgsHdrpInstalled && !tgsUrpInstalled)
            {
                EditorGUILayout.HelpBox("No HDRP or URP package for Tasty Grass Shader installed.", MessageType.Error);
                if (GUILayout.Button("Open Setup"))
                {
                    TgsSetup.RunSetup();
                }
            }

            UpdateHandler.DisplayUpdateBox();
        }

        public static void SharedEditorFooter()
        {
            EditorGUILayout.Separator();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Links: ");
                if (GUILayout.Button("Open Documentation..."))
                {
                    Application.OpenURL("https://github.com/SymmetryBreakStudio/TastyGrassShader/wiki");
                }
            }
        }
    }
}