using System.Collections.Generic;
using UnityEngine;

namespace PhysicsFloatingOrigin
{
    public class ShadowCaster : MonoBehaviour
    {
        [SerializeField] private bool useScaled = true;
        [SerializeField] private Mesh scaledMesh;
        [SerializeField] private Transform scaledTransform;

        [SerializeField] private Material shadowMaterial;
        [SerializeField, Min(32)] private int shadowTextureSize = 512;
        [SerializeField, Min(0.001f)] private float radius = 1f;

        private Body body;
        private Scaled scaled;
        private bool registeredWithManager;
        private bool attemptedShadowMaterialLoad;

        public Material ShadowMaterial => shadowMaterial;

        private void OnEnable()
        {
            ResolveReferences();
            RegisterWithManager();
        }

        private void OnValidate()
        {
            shadowTextureSize = Mathf.Max(32, shadowTextureSize);
            radius = Mathf.Max(0.001f, radius);
            ResolveReferences();
        }

        private void Update()
        {
            if (!registeredWithManager)
            {
                RegisterWithManager();
            }
        }

        private void OnDisable()
        {
            UnregisterFromManager();
        }

        private void OnDestroy()
        {
            UnregisterFromManager();
        }

        public bool TryGetShadowData(List<ShadowCasterManager.ShadowMeshProxy> output, out Vector3 anchorPosition, out float worldRadius, out int textureSize)
        {
            ResolveReferences();

            output.Clear();
            textureSize = GetShadowTextureSize();
            GetFallbackShadowBounds(out anchorPosition, out worldRadius);

            if (useScaled &&
                scaled != null &&
                scaled.TryGetShadowData(output, out Vector3 scaledAnchorPosition, out _))
            {
                anchorPosition = scaledAnchorPosition;
                return true;
            }

            return TryGetFallbackShadowData(output);
        }

        public bool TryGetShadowBounds(out float worldRadius)
        {
            ResolveReferences();
            GetFallbackShadowBounds(out _, out worldRadius);

            if (useScaled && scaled != null && scaled.TryGetShadowBounds(out _))
            {
                return true;
            }

            return HasFallbackShadowSource();
        }

        private void ResolveReferences()
        {
            if (body == null) { body = GetComponent<Body>(); }
            if (scaled == null) { scaled = GetComponent<Scaled>(); }
            scaledTransform = body != null ? body.scaledTransform : null;

            EnsureShadowMaterialLoaded();
        }

        private int GetShadowTextureSize()
        {
            return Mathf.Max(32, shadowTextureSize);
        }

        private void GetFallbackShadowBounds(out Vector3 anchorPosition, out float worldRadius)
        {
            anchorPosition = scaledTransform != null
                ? scaledTransform.position
                : transform.position * Constant.INVERSE_SCALE;
            worldRadius = Mathf.Max(0.001f, radius * (scaledTransform != null ? scaledTransform.lossyScale.x : 1f));
        }

        private bool HasFallbackShadowSource()
        {
            return scaledMesh != null && scaledTransform != null;
        }

        private bool TryGetFallbackShadowData(List<ShadowCasterManager.ShadowMeshProxy> output)
        {
            if (!HasFallbackShadowSource()) { return false; }

            output.Add(new ShadowCasterManager.ShadowMeshProxy
            {
                mesh = scaledMesh,
                localToWorld = scaledTransform.localToWorldMatrix,
                layer = scaledTransform.gameObject.layer
            });
            return true;
        }

        private void EnsureShadowMaterialLoaded()
        {
            if (shadowMaterial != null || attemptedShadowMaterialLoad) { return; }

            attemptedShadowMaterialLoad = true;
            shadowMaterial = Resources.Load<Material>("Materials/Shadow");
            if (shadowMaterial == null)
            {
                Debug.LogWarning("ShadowCaster: Missing Materials/Shadow material resource.");
            }
        }

        private void RegisterWithManager()
        {
            if (registeredWithManager) { return; }
            if (ShadowCasterManager.Instance == null) { return; }

            ShadowCasterManager.Instance.AddShadowCaster(this);
            registeredWithManager = true;
        }

        private void UnregisterFromManager()
        {
            if (!registeredWithManager) { return; }
            if (ShadowCasterManager.Instance != null)
            {
                ShadowCasterManager.Instance.RemoveShadowCaster(this);
            }
            registeredWithManager = false;
        }
    }
}
