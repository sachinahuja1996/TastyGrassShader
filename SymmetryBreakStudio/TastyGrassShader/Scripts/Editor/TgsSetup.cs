using System;
using UnityEditor;
using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    public static class TgsSetup
    {
        [InitializeOnLoadMethod]
        static void RunTgsSetup()
        {
            SharedEditorTools.UnityRp pipeline = SharedEditorTools.GetActiveRenderPipeline(false);

            string pipelinePackageName = string.Empty;
            bool validPipeline = false;
            bool needsReinstall = false;
            switch (pipeline)
            {
                case SharedEditorTools.UnityRp.Unknown:
                    if (EditorUtility.DisplayDialog(SharedEditorTools.productName,
                            "Unknown Render Pipeline detected. This might be a bug, we would be happy if you reach out to us.",
                            "Open Contact Options", "Don't Ask Again"))
                    {
                        Application.OpenURL("https://github.com/SymmetryBreakStudio/TastyGrassShader/wiki#contact");
                    }

                    break;
                case SharedEditorTools.UnityRp.Universal:
                    pipelinePackageName = "URP";
                    validPipeline = true;
                    if (SharedEditorTools.GetUrpSubpackageVersion() != UpdateHandler.ThisVersion)
                    {
                        needsReinstall = true;
                    }

                    break;
                case SharedEditorTools.UnityRp.HighDefinition:
                    pipelinePackageName = "HDRP";
                    validPipeline = true;
                    if (SharedEditorTools.GetHdrpSubpackageVersion() != UpdateHandler.ThisVersion)
                    {
                        needsReinstall = true;
                    }

                    break;
                case SharedEditorTools.UnityRp.BuiltIn:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (validPipeline && needsReinstall)
            {
                if (EditorUtility.DisplayDialog(SharedEditorTools.productName,
                        $"An update/install for the Tasty Grass Shader {pipelinePackageName} back-end is required. (NOTE: If this message appeared again after you clicked install, you may click \"Later\".)",
                        $"Install {pipelinePackageName} Back-End (Required)", "Later"))
                {
                    DoHdrpPackage(pipeline == SharedEditorTools.UnityRp.HighDefinition);
                    DoUrpPackage(pipeline == SharedEditorTools.UnityRp.Universal);
                }
            }
        }

        static void TryInstallPackage(string path)
        {
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
            {
                EditorUtility.DisplayDialog(SharedEditorTools.productName,
                    "Unable to locate Unity-Package. Please re-install Tasty Grass Shader.", "Ok");
            }
            else
            {
                AssetDatabase.ImportPackage(path, false);
            }
        }

        public static void DoUrpPackage(bool reinstall)
        {
            AssetDatabase.DeleteAsset(SharedEditorTools.urpExamplesPath);
            AssetDatabase.DeleteAsset(SharedEditorTools.urpBackendPath);
            if (reinstall)
            {
                TryInstallPackage(SharedEditorTools.urpPackagePath);
            }
        }

        public static void DoHdrpPackage(bool reinstall)
        {
            AssetDatabase.DeleteAsset(SharedEditorTools.hdrpExamplesPath);
            AssetDatabase.DeleteAsset(SharedEditorTools.hdrpBackendPath);
            if (reinstall)
            {
                TryInstallPackage(SharedEditorTools.hdrpPackagePath);
            }
        }

        [MenuItem(TgsManager.SetupMenuItem)]
        public static void RunSetup()
        {
            RunTgsSetup();
            EditorUtility.DisplayDialog(SharedEditorTools.productName, "Setup finished.", "Ok");
        }
    }
}