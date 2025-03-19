using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader
{
    public class TrianglePreviewAttribute : PropertyAttribute
    {
        public string HeightName, WidthName, ThicknessName, ThicknessApexName;

        public TrianglePreviewAttribute(string heightName, string widthName, string thicknessName,
            string thicknessApexName)
        {
            HeightName = heightName;
            WidthName = widthName;
            ThicknessName = thicknessName;
            ThicknessApexName = thicknessApexName;
        }
    }
}