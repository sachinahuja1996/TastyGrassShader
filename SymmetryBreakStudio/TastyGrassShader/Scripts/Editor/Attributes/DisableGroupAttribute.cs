using UnityEditor;
using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    [CustomPropertyDrawer(typeof(DisableGroupAttribute))]
    public class DisableGroupAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            DisableGroupAttribute disableGroupAttribute = (DisableGroupAttribute)attribute;

            SerializedProperty disableProperty =
                property.serializedObject.FindProperty(
                    AttributeCommon.ResolveRelativePath(property.propertyPath, disableGroupAttribute.valueName));

            bool prevState = GUI.enabled;
            if (disableProperty == null)
            {
                Debug.LogError($"Unable to find property \"{disableGroupAttribute.valueName}\".",
                    property.serializedObject.targetObject);
            }
            else
            {
                if (disableGroupAttribute.valueIsFloatAndMustBeGreaterZero)
                {
                    GUI.enabled = (disableProperty.floatValue > 0.0f) ^ disableGroupAttribute.flipCondition;
                }
                else
                {
                    GUI.enabled = disableProperty.boolValue ^ disableGroupAttribute.flipCondition;
                }
                
            }

            EditorGUI.PropertyField(position, property, label);
            GUI.enabled = prevState;
        }
    }
}