using UnityEditor;
using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    [CustomPropertyDrawer(typeof(IntAsCheckboxAttribute))]
    public class IntAsCheckboxAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Multi editing is breaking stuff, so early out here.
            if (property.serializedObject.isEditingMultipleObjects)
            {
                return;
            }

            property.boolValue = EditorGUI.Toggle(position, label, property.boolValue);
        }
    }
}