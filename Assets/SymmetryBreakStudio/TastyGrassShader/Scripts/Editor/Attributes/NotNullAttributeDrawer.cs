using UnityEditor;
using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    [CustomPropertyDrawer(typeof(NotNull))]
    public class NotNullAttributeDrawer : PropertyDrawer
    {
        const float helpboxHeight = 35.0f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.objectReferenceValue == null)
            {
                Rect helpBox = position;
                helpBox.height = helpboxHeight;
                helpBox = EditorGUI.IndentedRect(helpBox);
                EditorGUI.HelpBox(helpBox, "This field must not be null.", MessageType.Error);

                Rect propertyRect = position;
                propertyRect.y += helpboxHeight + EditorGUIUtility.standardVerticalSpacing;
                propertyRect.height = EditorGUIUtility.singleLineHeight;

                EditorGUI.DrawRect(EditorGUI.IndentedRect(propertyRect), new Color(1.0f, 0.0f, 0.0f, 0.25f));
                EditorGUI.PropertyField(propertyRect, property, label);
            }
            else
            {
                EditorGUI.PropertyField(position, property, label);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = 0.0f;
            if (property.objectReferenceValue == null)
            {
                height += helpboxHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            return height + base.GetPropertyHeight(property, label);
        }
    }
}