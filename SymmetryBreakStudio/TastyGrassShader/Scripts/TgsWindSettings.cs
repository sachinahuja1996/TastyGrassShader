using Unity.Mathematics;
using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader
{
    /// <summary>
    /// The wind settings to use. Since they are often shared across the entire scene, they are stored separately.
    /// Will also apply them to the material property block.
    /// </summary>
    [CreateAssetMenu(menuName = "Symmetry Break Studio/Tasty Grass Shader/Wind Settings")]
    public class TgsWindSettings : ScriptableObject
    {
        static readonly int WindDirection = Shader.PropertyToID("_WindDirection");
        static readonly int WindStrength = Shader.PropertyToID("_WindStrength");
        static readonly int WindPatchSize = Shader.PropertyToID("_WindPatchSize");
        static readonly int WindSpeed = Shader.PropertyToID("_WindSpeed");

        static readonly int WindParams = Shader.PropertyToID("_WindParams");


        [Tooltip("The direction of the wind in degrees.")] [Range(0.0f, 360.0f)]
        public float direction;

        [Tooltip("The strength of the wind.")] [Range(0.0f, 20.0f)]
        public float strength = 0.5f;

        [Tooltip("The size of a wind patch. Smaller values may create more believable settings.")] [Range(0.00f, 0.5f)]
        public float patchSize = 0.05f;

        [Tooltip("The speed of how fast the wind patches move.")] [Range(0.0f, 100.0f)]
        public float speed = 20.0f;

        private static readonly int WindRotationAxis = Shader.PropertyToID("_WindRotationAxis");

        public void ApplyToMaterialPropertyBlock(MaterialPropertyBlock materialPropertyBlock)
        {
            float windDirectionRad = direction * Mathf.Deg2Rad;
            materialPropertyBlock.SetVector(WindParams,
                new Vector4(windDirectionRad /* was wind direction, which is now precomputed.*/, strength, patchSize,
                    speed));

            // NOTE: Pre-Computing the wind vector gives +0.5ms on Intel (UHD, Raptor Lake) in the default benchmark scene.
            math.sincos(windDirectionRad, out float directionSin, out float directionCos);
            materialPropertyBlock.SetVector(WindRotationAxis, new Vector4(directionCos, 0, directionSin));
        }
    }
}