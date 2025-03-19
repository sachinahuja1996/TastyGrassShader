using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader
{
    public class MinMaxAttribute : PropertyAttribute
    {
        public bool MarkNegativeArea;
        public float MinValue, MaxValue;

        public MinMaxAttribute(float minValue, float maxValue, bool markNegativeArea = false)
        {
            MinValue = minValue;
            MaxValue = maxValue;
            MarkNegativeArea = markNegativeArea;
        }
    }
}