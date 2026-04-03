using System.Collections.Generic;
using UnityEngine;

namespace UnityPhysicsFloatingOrigin
{
    [DefaultExecutionOrder(-10)]
    public class ShadowCasterManager : MonoBehaviour
    {
        private struct CasterCandidate
        {
            public ShadowCaster caster;
            public float radius;
        }

        public struct ShadowMeshProxy
        {
            public Mesh mesh;
            public Matrix4x4 localToWorld;
            public int layer;
        }

        public static ShadowCasterManager Instance;

        [SerializeField] private Transform starScaledTransform;
        private readonly List<ShadowCaster> shadowCasters = new List<ShadowCaster>(16);
        [SerializeField] private float angularDiameterDeg = 6f;
        [SerializeField] private int scaledLayer = 3;

        private const int MAX_SHADOWS = 4;
        private const float WarningCheckInterval = 2f;
        private static readonly int[] ShadowTextureIds =
        {
            Shader.PropertyToID("_ShadowTexture0"),
            Shader.PropertyToID("_ShadowTexture1"),
            Shader.PropertyToID("_ShadowTexture2"),
            Shader.PropertyToID("_ShadowTexture3")
        };
        private static readonly int[] WorldToShadowIds =
        {
            Shader.PropertyToID("_WorldToShadow0"),
            Shader.PropertyToID("_WorldToShadow1"),
            Shader.PropertyToID("_WorldToShadow2"),
            Shader.PropertyToID("_WorldToShadow3")
        };
        private static readonly int[] ShadowPositionIds =
        {
            Shader.PropertyToID("_ShadowPosition0"),
            Shader.PropertyToID("_ShadowPosition1"),
            Shader.PropertyToID("_ShadowPosition2"),
            Shader.PropertyToID("_ShadowPosition3")
        };
        private static readonly int[] ShadowRadiusIds =
        {
            Shader.PropertyToID("_ShadowRadius0"),
            Shader.PropertyToID("_ShadowRadius1"),
            Shader.PropertyToID("_ShadowRadius2"),
            Shader.PropertyToID("_ShadowRadius3")
        };
        private static readonly int[] ShadowOrthoHalfIds =
        {
            Shader.PropertyToID("_ShadowOrthoHalf0"),
            Shader.PropertyToID("_ShadowOrthoHalf1"),
            Shader.PropertyToID("_ShadowOrthoHalf2"),
            Shader.PropertyToID("_ShadowOrthoHalf3")
        };
        private static readonly int[] ShadowAspectIds =
        {
            Shader.PropertyToID("_ShadowAspect0"),
            Shader.PropertyToID("_ShadowAspect1"),
            Shader.PropertyToID("_ShadowAspect2"),
            Shader.PropertyToID("_ShadowAspect3")
        };
        private static readonly int[] ShadowTexWidthIds =
        {
            Shader.PropertyToID("_ShadowTexWidth0"),
            Shader.PropertyToID("_ShadowTexWidth1"),
            Shader.PropertyToID("_ShadowTexWidth2"),
            Shader.PropertyToID("_ShadowTexWidth3")
        };
        private static readonly int ShadowCountId = Shader.PropertyToID("_ShadowCount");
        private static readonly int StarHalfAngleTanId = Shader.PropertyToID("_StarHalfAngleTan");

        [SerializeField] private RenderTexture[] renderTextures;
        private Camera silhouetteCamera;
        private Material fallbackShadowMaterial;

        private readonly List<CasterCandidate> candidates = new List<CasterCandidate>(32);
        private readonly List<ShadowMeshProxy> shadowProxyBuffer = new List<ShadowMeshProxy>(64);

        private bool warnedMainLightNoShadows;
        private bool warnedMainLightLayer;
        private bool warnedNoScaledCamera;
        private float nextWarningCheckTime;

        private void OnEnable()
        {
            Instance = this;
            renderTextures = new RenderTexture[MAX_SHADOWS];
            CreateSilhouetteCamera();
            nextWarningCheckTime = 0f;
        }

        private void OnDisable()
        {
            if (silhouetteCamera != null)
            {
                DestroyImmediate(silhouetteCamera.gameObject);
                silhouetteCamera = null;
            }

            if (fallbackShadowMaterial != null)
            {
                DestroyImmediate(fallbackShadowMaterial);
                fallbackShadowMaterial = null;
            }

            for (int i = 0; i < renderTextures.Length; i++)
            {
                ReleaseRenderTexture(i);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void AddShadowCaster(ShadowCaster shadowCaster)
        {
            if (shadowCaster == null) { return; }
            if (shadowCasters.Contains(shadowCaster)) { return; }
            shadowCasters.Add(shadowCaster);
        }

        public void RemoveShadowCaster(ShadowCaster shadowCaster)
        {
            if (shadowCaster == null) { return; }
            shadowCasters.Remove(shadowCaster);
        }

        private void Update()
        {
            if (starScaledTransform == null || silhouetteCamera == null)
            {
                ResetAllShadowGlobals();
                return;
            }

            if (Time.unscaledTime >= nextWarningCheckTime)
            {
                WarnSceneSetupIfNeeded();
                nextWarningCheckTime = Time.unscaledTime + WarningCheckInterval;
            }

            for (int i = shadowCasters.Count - 1; i >= 0; i--)
            {
                if (shadowCasters[i] == null)
                {
                    shadowCasters.RemoveAt(i);
                }
            }

            BuildShadowCandidates();

            int count = Mathf.Min(MAX_SHADOWS, candidates.Count);
            for (int i = 0; i < count; i++)
            {
                var caster = candidates[i].caster;
                if (caster == null) { continue; }

                if (!caster.TryGetShadowData(shadowProxyBuffer, out Vector3 anchorPosition, out float radius, out int textureSize) || shadowProxyBuffer.Count == 0)
                {
                    continue;
                }

                var texture = EnsureRenderTexture(i, textureSize, caster.name);
                if (texture == null) { continue; }

                RenderSilhouetteToRenderTexture(caster, shadowProxyBuffer, anchorPosition, radius, texture, out float orthoHalf, out float aspect);

                Shader.SetGlobalTexture(ShadowTextureIds[i], texture);
                Matrix4x4 projection = GL.GetGPUProjectionMatrix(silhouetteCamera.projectionMatrix, false) * silhouetteCamera.worldToCameraMatrix;
                Shader.SetGlobalMatrix(WorldToShadowIds[i], projection);
                Shader.SetGlobalVector(ShadowPositionIds[i], anchorPosition);
                Shader.SetGlobalFloat(ShadowRadiusIds[i], radius);
                Shader.SetGlobalFloat(ShadowOrthoHalfIds[i], orthoHalf);
                Shader.SetGlobalFloat(ShadowAspectIds[i], aspect);
                Shader.SetGlobalFloat(ShadowTexWidthIds[i], texture.width);
            }

            for (int i = count; i < MAX_SHADOWS; i++)
            {
                Shader.SetGlobalTexture(ShadowTextureIds[i], Texture2D.whiteTexture);
                Shader.SetGlobalMatrix(WorldToShadowIds[i], Matrix4x4.identity);
                Shader.SetGlobalVector(ShadowPositionIds[i], Vector4.zero);
                Shader.SetGlobalFloat(ShadowRadiusIds[i], 0f);
                Shader.SetGlobalFloat(ShadowOrthoHalfIds[i], 0f);
                Shader.SetGlobalFloat(ShadowAspectIds[i], 0f);
                Shader.SetGlobalFloat(ShadowTexWidthIds[i], 0f);
            }

            Shader.SetGlobalInt(ShadowCountId, count);
            Shader.SetGlobalFloat(StarHalfAngleTanId, Mathf.Tan(0.5f * angularDiameterDeg * Mathf.Deg2Rad));
        }

        private void BuildShadowCandidates()
        {
            candidates.Clear();

            for (int i = 0; i < shadowCasters.Count; i++)
            {
                var caster = shadowCasters[i];
                if (caster == null) { continue; }

                if (!caster.TryGetShadowBounds(out float radius))
                {
                    continue;
                }

                InsertCandidate(caster, radius);
            }
        }

        private void InsertCandidate(ShadowCaster caster, float radius)
        {
            int insertIndex = -1;
            int instanceId = caster.GetInstanceID();
            for (int i = 0; i < candidates.Count; i++)
            {
                var existing = candidates[i];
                if (radius > existing.radius || (Mathf.Approximately(radius, existing.radius) && instanceId < existing.caster.GetInstanceID()))
                {
                    insertIndex = i;
                    break;
                }
            }

            if (insertIndex < 0)
            {
                if (candidates.Count < MAX_SHADOWS)
                {
                    candidates.Add(new CasterCandidate
                    {
                        caster = caster,
                        radius = radius
                    });
                }
                return;
            }

            if (candidates.Count < MAX_SHADOWS)
            {
                candidates.Add(default);
            }

            int upper = Mathf.Min(candidates.Count - 1, MAX_SHADOWS - 1);
            for (int i = upper; i > insertIndex; i--)
            {
                candidates[i] = candidates[i - 1];
            }

            candidates[insertIndex] = new CasterCandidate
            {
                caster = caster,
                radius = radius
            };
        }

        private void RenderSilhouetteToRenderTexture(
            ShadowCaster caster,
            List<ShadowMeshProxy> proxies,
            Vector3 anchorPosition,
            float radius,
            RenderTexture renderTexture,
            out float orthoHalf,
            out float aspect)
        {
            Vector3 starDirection = starScaledTransform.forward;
            if (starDirection.sqrMagnitude < 1e-6f) { starDirection = Vector3.forward; }
            starDirection.Normalize();

            orthoHalf = Mathf.Max(0.001f, radius * 1.1f);
            aspect = (float)renderTexture.width / renderTexture.height;

            silhouetteCamera.transform.SetPositionAndRotation(
                anchorPosition - (starDirection * orthoHalf),
                Quaternion.LookRotation(starDirection, Vector3.up));

            silhouetteCamera.cullingMask = 0;
            silhouetteCamera.targetTexture = renderTexture;
            silhouetteCamera.ResetProjectionMatrix();
            silhouetteCamera.projectionMatrix = Matrix4x4.Ortho(
                -orthoHalf * aspect,
                orthoHalf * aspect,
                -orthoHalf,
                orthoHalf,
                0.01f,
                orthoHalf * 2f);

            Material drawMaterial = caster.ShadowMaterial != null ? caster.ShadowMaterial : GetFallbackShadowMaterial();
            if (drawMaterial == null)
            {
                silhouetteCamera.targetTexture = null;
                return;
            }

            for (int i = 0; i < proxies.Count; i++)
            {
                var proxy = proxies[i];
                if (proxy.mesh == null) { continue; }
                Graphics.DrawMesh(proxy.mesh, proxy.localToWorld, drawMaterial, proxy.layer, silhouetteCamera, 0);
            }

            silhouetteCamera.Render();
            silhouetteCamera.targetTexture = null;
        }

        private void CreateSilhouetteCamera()
        {
            if (silhouetteCamera != null) { return; }

            var cameraObject = new GameObject("SilhouetteCamera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            silhouetteCamera = cameraObject.GetComponent<Camera>();
            silhouetteCamera.enabled = false;
            silhouetteCamera.orthographic = true;
            silhouetteCamera.clearFlags = CameraClearFlags.SolidColor;
            silhouetteCamera.backgroundColor = Color.white;
            silhouetteCamera.allowHDR = false;
            silhouetteCamera.allowMSAA = false;
            silhouetteCamera.allowDynamicResolution = false;
            silhouetteCamera.useOcclusionCulling = false;
            silhouetteCamera.renderingPath = RenderingPath.Forward;
            silhouetteCamera.cullingMask = 0;
            silhouetteCamera.orthographicSize = 1f;
            silhouetteCamera.nearClipPlane = 0.001f;
            silhouetteCamera.farClipPlane = 100f;
        }

        private RenderTexture EnsureRenderTexture(int index, int requestedSize, string debugName)
        {
            int size = Mathf.Max(32, requestedSize);

            if (renderTextures[index] != null &&
                renderTextures[index].width == size &&
                renderTextures[index].height == size)
            {
                return renderTextures[index];
            }

            ReleaseRenderTexture(index);

            renderTextures[index] = new RenderTexture(size, size, 0, RenderTextureFormat.R8)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                name = debugName + "_Shadow"
            };

            return renderTextures[index];
        }

        private void ReleaseRenderTexture(int index)
        {
            if (renderTextures == null || index < 0 || index >= renderTextures.Length) { return; }
            if (renderTextures[index] == null) { return; }

            renderTextures[index].Release();
            DestroyImmediate(renderTextures[index]);
            renderTextures[index] = null;
        }

        private Material GetFallbackShadowMaterial()
        {
            if (fallbackShadowMaterial != null) { return fallbackShadowMaterial; }

            Shader shader = Shader.Find("Custom/Shadow");
            if (shader == null) { return null; }

            fallbackShadowMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return fallbackShadowMaterial;
        }

        private void WarnSceneSetupIfNeeded()
        {
            Light mainLight = RenderSettings.sun;
            if (mainLight == null)
            {
                var lights = FindObjectsOfType<Light>();
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null && lights[i].enabled && lights[i].type == LightType.Directional)
                    {
                        mainLight = lights[i];
                        break;
                    }
                }
            }

            if (!warnedNoScaledCamera)
            {
                bool hasScaledCamera = false;
                int scaledMask = 1 << scaledLayer;
                var cameras = Camera.allCameras;
                for (int i = 0; i < cameras.Length; i++)
                {
                    var camera = cameras[i];
                    if (camera == null || !camera.enabled) { continue; }
                    if ((camera.cullingMask & scaledMask) == 0) { continue; }
                    hasScaledCamera = true;
                    break;
                }

                if (!hasScaledCamera)
                {
                    warnedNoScaledCamera = true;
                    Debug.LogWarning($"ShadowCasterManager: No enabled camera is currently rendering scaled layer {scaledLayer}.");
                }
            }
        }

        private static void ResetAllShadowGlobals()
        {
            for (int i = 0; i < MAX_SHADOWS; i++)
            {
                Shader.SetGlobalTexture(ShadowTextureIds[i], Texture2D.whiteTexture);
                Shader.SetGlobalMatrix(WorldToShadowIds[i], Matrix4x4.identity);
                Shader.SetGlobalVector(ShadowPositionIds[i], Vector4.zero);
                Shader.SetGlobalFloat(ShadowRadiusIds[i], 0f);
                Shader.SetGlobalFloat(ShadowOrthoHalfIds[i], 0f);
                Shader.SetGlobalFloat(ShadowAspectIds[i], 0f);
                Shader.SetGlobalFloat(ShadowTexWidthIds[i], 0f);
            }

            Shader.SetGlobalInt(ShadowCountId, 0);
            Shader.SetGlobalFloat(StarHalfAngleTanId, 0f);
        }

    }
}
