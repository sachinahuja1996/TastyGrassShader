using System;
using UnityEditor;
using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    [CustomEditor(typeof(TgsForMesh))]
    [CanEditMultipleObjects]
    public class TgsForMeshEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            SharedEditorTools.SharedEditorGUIHeader();
            TgsForMesh tgsForMesh = (TgsForMesh)serializedObject.targetObject;
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            MeshFilter meshFilter = tgsForMesh.GetComponent<MeshFilter>();
            TgsForMesh.GrassMeshError error = TgsForMesh.CheckForErrorsMeshFilter(tgsForMesh, meshFilter);
            switch (error)
            {
                case TgsForMesh.GrassMeshError.None:
                    break;
                case TgsForMesh.GrassMeshError.MissingMeshFilter:
                    EditorGUILayout.HelpBox("Missing MeshFilter component.", MessageType.Error, true);
                    break;
                case TgsForMesh.GrassMeshError.MeshNoReadWrite:
                    EditorGUILayout.HelpBox(
                        "Mesh in MeshFilter component is not readable. (Read/Write flag in import settings)",
                        MessageType.Error, true);

                    Mesh mesh = meshFilter.sharedMesh;
                    string path = AssetDatabase.GetAssetPath(mesh);
                    if (path != null)
                    {
                        if (GUILayout.Button("Enable Read/Write"))
                        {
                            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
                            importer.isReadable = true;
                            importer.SaveAndReimport();
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Mesh is not an asset. Cannot automatically enable Read/Write.",
                            MessageType.Info, true);
                    }

                    break;
                case TgsForMesh.GrassMeshError.MissingMesh:
                    EditorGUILayout.HelpBox("Missing Mesh on MeshFilter component.", MessageType.Error, true);
                    break;
                case TgsForMesh.GrassMeshError.MissingVertexColor:
                    EditorGUILayout.HelpBox(
                        $"One or more layers need vertex color, but the mesh \"{meshFilter?.sharedMesh?.name}\" does not have them.",
                        MessageType.Error, true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            SerializedProperty layers = serializedObject.FindProperty("layers");
            SharedEditorTools.DrawLayerInspector(
                layers,
                tgsForMesh,
                index => tgsForMesh.GetLayerByIndex(index).GetEditorName(index),
                tgsForMesh.RemoveLayerAt,
                tgsForMesh.AddNewLayer,
                tgsForMesh.GetLayerCount,
                index => tgsForMesh.GetLayerByIndex(index).hide,
                (index, hide) => tgsForMesh.GetLayerByIndex(index).hide = hide);


            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("windSettings"));

            if (GUILayout.Button("Manual Update"))
            {
                tgsForMesh.OnPropertiesMayChanged();
            }

            EditorGUILayout.HelpBox(
                $"Memory Statistics:\nGeometry Buffer Size: {tgsForMesh.GetMemoryBufferByteSize() / (1024.0 * 1024.0):F2}MB.",
                MessageType.Info);

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                tgsForMesh.OnPropertiesMayChanged();
            }

            SharedEditorTools.SharedEditorFooter();
        }
    }
}