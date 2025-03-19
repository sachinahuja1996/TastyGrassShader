using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader
{
    /// <summary>
    /// NOTE: Sometimes doesn't work if the order of attributes is not correct. This attribute should not be the last one.
    /// </summary>
    public class DisableGroupAttribute : PropertyAttribute
    {
        public bool flipCondition;
        public string valueName;
        public bool valueIsFloatAndMustBeGreaterZero;

        public DisableGroupAttribute(string valueName, bool flipCondition = false,
            bool valueIsFloatAndMustBeGreaterZero = false)
        {
            this.valueName = valueName;
            this.flipCondition = flipCondition;
            this.valueIsFloatAndMustBeGreaterZero = valueIsFloatAndMustBeGreaterZero;
        }
    }
}