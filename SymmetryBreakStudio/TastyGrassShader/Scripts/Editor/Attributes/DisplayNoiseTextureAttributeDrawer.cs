using UnityEditor;
using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    [CustomPropertyDrawer(typeof(DisplayNoiseTextureAttribute))]
    public class DisplayNoiseTextureAttributeDrawer : PropertyDrawer
    {
        const float TextureHeight = 100.0f;
        static Material _previewMaterial;
        static readonly int ValueScale = Shader.PropertyToID("_ValueScale");
        static readonly int ValueOffset = Shader.PropertyToID("_ValueOffset");
        static readonly int Tiling = Shader.PropertyToID("_Tiling");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Texture3D texture = (Texture3D)property.objectReferenceValue;
            if (_previewMaterial == null)
            {
                _previewMaterial =
                    new Material(Shader.Find("Hidden/Symmetry Break Studio/Tasty Grass Shader/Editor Preview"))
                    {
                        hideFlags = HideFlags.DontSave
                    };
            }

            //TODO: can be done faster by getting the containing object and use use FindPropertyRelative?
            float valueScale = property.serializedObject.FindProperty(
                AttributeCommon.ResolveRelativePath(property.propertyPath, "valueScale")).floatValue;
            float valueOffset = property.serializedObject.FindProperty(
                AttributeCommon.ResolveRelativePath(property.propertyPath, "valueOffset")).floatValue;
            float tiling = property.serializedObject.FindProperty(
                AttributeCommon.ResolveRelativePath(property.propertyPath, "tiling")).floatValue;

            _previewMaterial.SetFloat(ValueScale, valueScale);
            _previewMaterial.SetFloat(ValueOffset, valueOffset);
            _previewMaterial.SetFloat(Tiling, tiling);

            float textureYOffset = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            Rect propertyPosition = position;
            propertyPosition.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(propertyPosition, property, label);

            //EditorGUI.DrawRect(position, Color.cyan);
            Rect texturePosition = position;
            texturePosition.y += textureYOffset;
            texturePosition.height -= textureYOffset;
            texturePosition.x += EditorGUIUtility.labelWidth;
            texturePosition.width -= EditorGUIUtility.labelWidth;

            if (texture)
            {
                EditorGUI.DrawPreviewTexture(texturePosition, texture, _previewMaterial, ScaleMode.ScaleToFit);
            }


            //GUI.DrawTexture(position, texture, ScaleMode.ScaleToFit);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return
                EditorGUIUtility.singleLineHeight +
                EditorGUIUtility.standardVerticalSpacing +
                TextureHeight;
        }
    }
}