using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader
{
    public class HideGroupAttribute : PropertyAttribute
    {
        public readonly int ShowIfValue;
        public readonly string ValueName;

        public HideGroupAttribute(string valueName, int showIfValue)
        {
            ValueName = valueName;
            ShowIfValue = showIfValue;
        }
    }
}