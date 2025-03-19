using UnityEditor;
using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    [CustomPropertyDrawer(typeof(HideGroupAttribute))]
    public class HideGroupAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (ShouldShow(property))
            {
                EditorGUI.PropertyField(position, property, label);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return ShouldShow(property)
                ? base.GetPropertyHeight(property, label)
                : -EditorGUIUtility.standardVerticalSpacing;
        }

        bool ShouldShow(SerializedProperty property)
        {
            HideGroupAttribute hideGroupAttribute = (HideGroupAttribute)attribute;
            SerializedProperty hideConditionProperty =
                property.serializedObject.FindProperty(
                    AttributeCommon.ResolveRelativePath(property.propertyPath, hideGroupAttribute.ValueName));

            if (hideConditionProperty == null)
            {
                Debug.LogError($"Unable to find property \"{hideGroupAttribute.ValueName}\".",
                    property.serializedObject.targetObject);

                return true;
            }

            {
                if (hideConditionProperty.intValue == hideGroupAttribute.ShowIfValue)
                {
                    return true;
                }
            }

            return false;
        }
    }
}