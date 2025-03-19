using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader
{
    public class UvMappingTextureAttribute : PropertyAttribute
    {
        public string LowerArcAttribute;
        public string ProceduralShapeBlend;
        public string StylePropertyName;
        public string TipRounding;
        public string UpperArcAttribute;


        public UvMappingTextureAttribute(string stylePropertyName, string proceduralShapeBlend,
            string upperArcAttribute, string lowerArcAttribute, string tipRounding)
        {
            StylePropertyName = stylePropertyName;
            ProceduralShapeBlend = proceduralShapeBlend;
            UpperArcAttribute = upperArcAttribute;
            LowerArcAttribute = lowerArcAttribute;
            TipRounding = tipRounding;
        }
    }
}