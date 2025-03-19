using System;
using UnityEditor;
using UnityEditor.Graphs;
using UnityEngine;
using UnityEngine.Rendering;

namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    [CustomPropertyDrawer(typeof(UvMappingTextureAttribute))]
    public class UvMappingTextureAttributeDrawer : PropertyDrawer
    {
        const float TextureHeight = 100.0f;
        static Material _previewMaterial;
        static readonly int UpperLeftTriangle = Shader.PropertyToID("_UpperLeftTriangle");
        static readonly int LowerRightTriangle = Shader.PropertyToID("_LowerRightTriangle");
        static readonly int CenterTriangle = Shader.PropertyToID("_CenterTriangle");
        static readonly int ProceduralShapeBlend = Shader.PropertyToID("_ProceduralShapeBlend");


        float GetFloatProperty(string name, SerializedProperty property)
        {
            SerializedProperty propertyOut =
                property.serializedObject.FindProperty(name);

            return propertyOut.floatValue;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SharedEditorTools.UpdateEditorShaderRenderPipelineVariants();
            if (_previewMaterial == null)
            {
                _previewMaterial =
                    new Material(
                        Shader.Find("Hidden/Symmetry Break Studio/Tasty Grass Shader/Editor/Preview UV Layout"))
                    {
                        hideFlags = HideFlags.DontSave
                    };
            }

            UvMappingTextureAttribute uvMappingTextureAttribute = (UvMappingTextureAttribute)attribute;

            SerializedProperty uvStyleProperty =
                property.serializedObject.FindProperty(uvMappingTextureAttribute.StylePropertyName);
            TgsPreset.BladeTextureUvLayout uvLayout = (TgsPreset.BladeTextureUvLayout)uvStyleProperty.intValue;

            {
                float upperArc = GetFloatProperty(uvMappingTextureAttribute.UpperArcAttribute, property);
                _previewMaterial.SetFloat("_ArcUp", upperArc);
            }
            {
                float lowerArc = GetFloatProperty(uvMappingTextureAttribute.LowerArcAttribute, property);
                _previewMaterial.SetFloat("_ArcDown", lowerArc);
            }
            {
                float tipRounding = GetFloatProperty(uvMappingTextureAttribute.TipRounding, property);
                _previewMaterial.SetFloat("_TipRounding", tipRounding);
            }
            {
                float proceduralShapeBlend = GetFloatProperty(uvMappingTextureAttribute.ProceduralShapeBlend, property);
                _previewMaterial.SetFloat("_ProceduralShapeBlend", proceduralShapeBlend);
            }


            float textureYOffset = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            _previewMaterial.SetFloat(UpperLeftTriangle,
                uvLayout is TgsPreset.BladeTextureUvLayout.TriangleSingle ? 1.0f : 0.0f);
            //_previewMaterial.SetFloat(LowerRightTriangle, uvStyle == TgsPreset.BladeTextureUvStyle.TrianglePair ? 1.0f : 0.0f);
            _previewMaterial.SetFloat(CenterTriangle,
                uvLayout == TgsPreset.BladeTextureUvLayout.TriangleCenter ? 1.0f : 0.0f);

            _previewMaterial.SetFloat("_UvUseCenterTriangle",
                uvLayout is TgsPreset.BladeTextureUvLayout.TriangleCenter ? 1.0f : 0.0f);

            Rect propertyPosition = position;
            propertyPosition.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(propertyPosition, property, label);

            //EditorGUI.DrawRect(position, Color.cyan);
            Rect texturePosition = position;
            texturePosition.y += textureYOffset;
            texturePosition.height -= textureYOffset;
            texturePosition.x += EditorGUIUtility.labelWidth;
            texturePosition.width -= EditorGUIUtility.labelWidth;


            Texture2D texture2D = (Texture2D)property.objectReferenceValue;
            if (texture2D == null)
            {
                texture2D = Texture2D.whiteTexture;
            }

            EditorGUI.DrawPreviewTexture(texturePosition, texture2D, _previewMaterial, ScaleMode.ScaleToFit);
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