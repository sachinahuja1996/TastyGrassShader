using UnityEditor;
using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    [CustomPropertyDrawer(typeof(MinMaxAttribute))]
    public class MinMaxAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            MinMaxAttribute minMaxAttribute = (MinMaxAttribute)attribute;
            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                Debug.LogError("Improper use of attribute. Target must be a Vector2");
                return;
            }

            // Multi editing is breaking stuff, so early out here.
            if (property.serializedObject.isEditingMultipleObjects)
            {
                return;
            }


            Vector2 value = property.vector2Value;
            const float labelSize = 60.0f, labelSpacing = 10.0f, floatFieldEmptyLabelSize = 15.0f;

            Rect rightSideRect = EditorGUI.PrefixLabel(position, label);

            Rect sliderRect = rightSideRect;
            sliderRect.x += labelSize;
            sliderRect.width -= labelSize * 2.0f + labelSpacing * 2.0f;
            EditorGUI.BeginChangeCheck();

            if (minMaxAttribute.MarkNegativeArea)
            {
                Rect grayZoneRect = sliderRect;
                grayZoneRect.width /= 2.0f;
                grayZoneRect.y += grayZoneRect.height / 3.0f;
                grayZoneRect.height /= 3.0f;

                grayZoneRect.x += labelSpacing;
                grayZoneRect.width -= labelSpacing;
                EditorGUI.DrawRect(grayZoneRect, Color.gray);
            }

            EditorGUI.MinMaxSlider(sliderRect, string.Empty, ref value.x, ref value.y, minMaxAttribute.MinValue,
                minMaxAttribute.MaxValue);

            {
                Rect leftValuePos = rightSideRect;
                leftValuePos.width = labelSize;
                //EditorGUI.DrawRect(leftValuePos, Color.red);
                leftValuePos.x -= floatFieldEmptyLabelSize;
                leftValuePos.width += floatFieldEmptyLabelSize;
                value.x = EditorGUI.FloatField(leftValuePos, value.x);
            }
            {
                Rect rightValuePos = rightSideRect;
                rightValuePos.x = position.width - labelSize + labelSpacing;
                rightValuePos.width = labelSize;
                //EditorGUI.DrawRect(rightValuePos, Color.green);
                rightValuePos.x -= floatFieldEmptyLabelSize;
                rightValuePos.width += floatFieldEmptyLabelSize;
                value.y = EditorGUI.FloatField(rightValuePos, value.y);
            }
            property.vector2Value = value;
            EditorGUI.EndChangeCheck();
        }
    }
}