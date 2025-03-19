using UnityEditor;

namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    public static class TgsManagerEditorBootstrap
    {
        [InitializeOnLoadMethod]
        static void InitializeTgsManager()
        {
            // Initialize the TgsManager for editor usage.
            TgsManager.SafeInitialize();
        }
    }
}