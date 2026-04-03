using System.Collections.Generic;
using UnityEngine;

namespace UnityPhysicsFloatingOrigin
{
    public class Celestial : MonoBehaviour
    {
        // 3000m planet 800m moon
        [SerializeField] private float surfaceGravity = Constant.G0;
        public float radius = 1;
        [SerializeField] private Mesh localMesh;
        [SerializeField] private Material material;
        [SerializeField] private GameObject visuals;
        private bool inEditor => Application.isEditor && !Application.isPlaying;
        private Body body;

        private void OnValidate()
        {
            if (inEditor)
            {
                body = GetComponent<Body>();
                body.bodyData.mass = MassOfSphere(surfaceGravity, radius);
            }
        }

        private double MassOfSphere(float surfaceGravity, float radius)
        {
            return surfaceGravity * (radius * radius) / Constant.G;
        }

        [ContextMenu("Create Surface")]
        private void CreateSurface()
        {
            DeleteSurface();
            if (visuals == null)
            {
                visuals = new GameObject("Surface");
                visuals.layer = 0;
                var localTransform = visuals.transform;
                localTransform.parent = transform;
                localTransform.localScale = Vector3.one * radius;
                localTransform.localPosition = Vector3.zero;

                var meshFilter = visuals.AddComponent<MeshFilter>();
                var meshRenderer = visuals.AddComponent<MeshRenderer>();
                var meshCollider = visuals.AddComponent<MeshCollider>();

                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

                meshFilter.sharedMesh = localMesh;
                meshRenderer.sharedMaterial = material;
                meshCollider.sharedMesh = localMesh;
            }
        }

        [ContextMenu("Delete Surface")]
        private void DeleteSurface()
        {
            if (visuals != null)
            {
                DestroyImmediate(visuals);
            }
        }
    }
}
