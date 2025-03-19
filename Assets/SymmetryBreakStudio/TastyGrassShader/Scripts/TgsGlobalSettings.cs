using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader
{
    public static class TgsGlobalSettings
    {
        public static float GlobalDensityScale = 1.0f;
        public static float GlobalLodScale = 1.0f;
        public static float GlobalLodFalloffExponent = 2.5f;
        public static int GlobalMaxBakesPerFrame = 32;
        public static bool NoAlphaToCoverage;
        public static Material CustomRenderingMaterial;
    }
}