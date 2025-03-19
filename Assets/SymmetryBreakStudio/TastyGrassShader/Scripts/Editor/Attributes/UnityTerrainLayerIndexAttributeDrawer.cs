using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    [CustomPropertyDrawer(typeof(UnityTerrainLayerIndexAttribute))]
    public class UnityTerrainLayerIndexAttributeDrawer : PropertyDrawer
    {
        static readonly List<GUIContent> _labels = new();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // get the terrain 
            TgsForUnityTerrain target = (TgsForUnityTerrain)property.serializedObject.targetObject;
            if (target == null)
            {
                Debug.LogError(
                    "UnityTerrainLayerIndexAttributeDrawer requires to be used within the context of a TgsForUnityTerrain component.");
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            Terrain terrain = target.GetComponent<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("UnityTerrainLayerIndexAttributeDrawer requires a valid Unity Terrain.");
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null)
            {
                Debug.LogError(
                    "UnityTerrainLayerIndexAttributeDrawer requires the TerrainData field to be set inside the Unity Terrain Component.");
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var layers = terrainData.terrainLayers;

            _labels.Clear();
            foreach (TerrainLayer layer in layers)
            {
                _labels.Add(new GUIContent(layer.name));
            }

            property.intValue = EditorGUI.Popup(position, label, property.intValue, _labels.ToArray());
        }
    }
}