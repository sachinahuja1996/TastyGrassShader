using System.Collections.Generic;
using UnityEngine;

namespace SymmetryBreakStudio.TastyGrassShader
{
    [ExecuteAlways]
    [HelpURL("https://github.com/SymmetryBreakStudio/TastyGrassShader/wiki/Quick-Start")]
    [AddComponentMenu("Symmetry Break Studio/Tasty Grass Shader/Collider")]
    public class TgsCollider : MonoBehaviour
    {
        public static List<TgsCollider> _activeColliders = new(16);

        [Tooltip("The radius of the collider.")] [Min(0.0f)]
        public float radius = 1.0f;

        void OnEnable()
        {
            _activeColliders ??= new List<TgsCollider>(16);
            _activeColliders.Add(this);
        }

        void OnDisable()
        {
            _activeColliders ??= new List<TgsCollider>(16);
            _activeColliders.Remove(this);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            float radiusWs = transform.lossyScale.magnitude * radius;
            Gizmos.DrawWireSphere(transform.position, radiusWs);
        }
    }
}