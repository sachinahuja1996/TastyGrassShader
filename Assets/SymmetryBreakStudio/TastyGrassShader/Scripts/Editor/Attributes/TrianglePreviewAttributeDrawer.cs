using UnityEditor;
using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    [CustomPropertyDrawer(typeof(TrianglePreviewAttribute))]
    public class TrianglePreviewAttributeDrawer : PropertyDrawer
    {
        const float height = 100.0f;
        static Material _previewMaterial;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (_previewMaterial == null)
            {
                _previewMaterial =
                    new Material(
                        Shader.Find("Hidden/Symmetry Break Studio/Tasty Grass Shader/Editor/Preview Triangle Layout"))
                    {
                        hideFlags = HideFlags.DontSave
                    };
            }


            TrianglePreviewAttribute previewAttribute = (TrianglePreviewAttribute)attribute;

            Vector2 heightMinMax = property.serializedObject
                .FindProperty(AttributeCommon.ResolveRelativePath(property.propertyPath, previewAttribute.HeightName))
                .vector2Value;
            Vector2 widthMinMax = property.serializedObject
                .FindProperty(AttributeCommon.ResolveRelativePath(property.propertyPath, previewAttribute.WidthName))
                .vector2Value;
            Vector2 thicknessMinMax = property.serializedObject
                .FindProperty(
                    AttributeCommon.ResolveRelativePath(property.propertyPath, previewAttribute.ThicknessName))
                .vector2Value;
            Vector2 thicknessApexMinMax = property.serializedObject
                .FindProperty(AttributeCommon.ResolveRelativePath(property.propertyPath,
                    previewAttribute.ThicknessApexName)).vector2Value;

            _previewMaterial.SetVector("_Height", heightMinMax);
            _previewMaterial.SetVector("_Width", widthMinMax);
            _previewMaterial.SetVector("_Thickness", thicknessMinMax);
            _previewMaterial.SetVector("_ThicknessApex", thicknessApexMinMax);


            EditorGUI.DrawPreviewTexture(position, Texture2D.whiteTexture, _previewMaterial, ScaleMode.ScaleToFit);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return height;
        }
    }
}