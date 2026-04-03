using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityPhysicsFloatingOrigin
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Body))]
    public class Scaled : MonoBehaviour
    {
        [Serializable]
        private struct SourceRenderer
        {
            public MeshRenderer renderer;
            public MeshFilter filter;
        }

        private struct DynamicProxy
        {
            public Transform sourceTransform;
            public MeshRenderer sourceRenderer;
            public MeshFilter sourceFilter;
            public Transform proxyTransform;
            public MeshRenderer proxyRenderer;
            public MeshFilter proxyFilter;
            public bool sourceCastShadows;
            public bool sourceReceiveShadows;
        }

        [SerializeField] private bool autoEnabled = true;
        [SerializeField] private bool unityShadows = true;
        [SerializeField] private bool eclipseShadows = true;
        [SerializeField] private bool includeInactive = true;
        [SerializeField, Min(0.05f)] private float rescanInterval = 0.25f;
        [SerializeField] private int layerScaled = 3;

        private Body body;

        private GameObject staticProxyObject;
        private MeshRenderer staticProxyRenderer;
        private MeshFilter staticProxyFilter;
        private Mesh staticProxyMesh;

        private readonly List<SourceRenderer> staticSources = new List<SourceRenderer>(64);
        private readonly List<SourceRenderer> dynamicSources = new List<SourceRenderer>(64);
        private readonly List<DynamicProxy> dynamicProxies = new List<DynamicProxy>(64);
        private readonly List<Material> uniqueMaterials = new List<Material>(32);
        private readonly HashSet<Material> uniqueMaterialSet = new HashSet<Material>();
        private readonly List<Mesh> tempSubmeshes = new List<Mesh>(16);
        private readonly Dictionary<Transform, byte> sourceFlagsCache = new Dictionary<Transform, byte>(128);

        private bool hierarchyDirty = true;
        private float nextRescanTime;
        private bool hasBuiltAnyProxy;
        private bool hasSourceSignatures;
        private int staticSourceSignature;
        private int dynamicSourceSignature;

        private const int Mesh16BitLimit = 65535;
        private const float MinShadowRadius = 0.001f;
        private const string StaticProxyPrefix = "_Static";
        private const string DynamicProxyPrefix = "_Dynamic";
        private const byte SourceFlagDynamic = 1 << 0;
        private const byte SourceFlagIgnored = 1 << 1;
        private static readonly HideFlags GeneratedHideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) { return; }

            ResolveReferences();
            MarkHierarchyDirty();
        }

        private void OnValidate()
        {
            rescanInterval = Mathf.Max(0.05f, rescanInterval);
            layerScaled = Mathf.Clamp(layerScaled, 0, 31);
        }

        private void OnTransformChildrenChanged()
        {
            MarkHierarchyDirty();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) { return; }

            ResolveReferences();
            if (!autoEnabled || body == null)
            {
                if (hasBuiltAnyProxy) { ClearAllProxies(); }
                return;
            }

            if (hierarchyDirty || Time.realtimeSinceStartup >= nextRescanTime)
            {
                RebuildProxySet();
            }

            SyncDynamicProxies();
        }

        private void OnDestroy()
        {
            ClearAllProxies();
        }

        public void MarkHierarchyDirty()
        {
            hierarchyDirty = true;
            nextRescanTime = 0f;
        }

        public bool TryGetShadowData(List<ShadowCasterManager.ShadowMeshProxy> output, out Vector3 anchorPosition, out float radius)
        {
            if (output == null) { throw new ArgumentNullException(nameof(output)); }

            output.Clear();
            anchorPosition = body != null && body.scaledTransform != null
                ? body.scaledTransform.position
                : transform.position * Constant.INVERSE_SCALE;
            radius = MinShadowRadius;

            if (!autoEnabled || !eclipseShadows || body == null)
            {
                return false;
            }

            TryAppendShadowProxy(output, staticProxyFilter, staticProxyRenderer, requireEnabled: false);

            for (int i = 0; i < dynamicProxies.Count; i++)
            {
                var proxy = dynamicProxies[i];
                TryAppendShadowProxy(output, proxy.proxyFilter, proxy.proxyRenderer, requireEnabled: true);
            }

            radius = MinShadowRadius;
            return output.Count > 0;
        }

        public bool TryGetShadowBounds(out float radius)
        {
            radius = MinShadowRadius;
            if (!autoEnabled || !eclipseShadows || !hasBuiltAnyProxy || body == null)
            {
                return false;
            }

            radius = MinShadowRadius;
            return true;
        }

        private void ResolveReferences()
        {
            if (body == null)
            {
                body = GetComponent<Body>();
            }
        }

        private void RebuildProxySet()
        {
            if (body == null || body.scaledTransform == null)
            {
                hierarchyDirty = true;
                return;
            }

            CollectSources();
            int newStaticSignature = ComputeSourceSignature(staticSources, includeTransformHash: true, includeShadowFlags: false, requireMesh: true);
            int newDynamicSignature = ComputeSourceSignature(dynamicSources, includeTransformHash: false, includeShadowFlags: true, requireMesh: false);

            bool staticChanged = !hasSourceSignatures || newStaticSignature != staticSourceSignature;
            bool dynamicChanged = !hasSourceSignatures || newDynamicSignature != dynamicSourceSignature;
            bool staticProxyInvalid = staticSources.Count > 0 && (staticProxyObject == null || staticProxyFilter == null || staticProxyRenderer == null || staticProxyFilter.sharedMesh == null);
            bool dynamicProxyInvalid = IsDynamicProxySetInvalid();

            if (staticChanged || staticProxyInvalid)
            {
                BuildStaticProxyMesh();
            }

            if (dynamicChanged || dynamicProxyInvalid)
            {
                BuildDynamicProxies();
            }

            staticSourceSignature = newStaticSignature;
            dynamicSourceSignature = newDynamicSignature;
            hasSourceSignatures = true;

            hierarchyDirty = false;
            nextRescanTime = Time.realtimeSinceStartup + rescanInterval;
            hasBuiltAnyProxy = HasAnyProxyMeshes();
        }

        private void CollectSources()
        {
            staticSources.Clear();
            dynamicSources.Clear();
            sourceFlagsCache.Clear();

            var meshRenderers = GetComponentsInChildren<MeshRenderer>(includeInactive);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                var renderer = meshRenderers[i];
                if (renderer == null) { continue; }

                byte flags = GetSourceFlags(renderer.transform);
                if ((flags & SourceFlagIgnored) != 0) { continue; }
                if (renderer.gameObject.layer == layerScaled) { continue; }
                if (body != null
                    && body.scaledTransform != null
                    && (renderer.transform == body.scaledTransform || renderer.transform.IsChildOf(body.scaledTransform)))
                {
                    continue;
                }

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) { continue; }

                var source = new SourceRenderer { renderer = renderer, filter = filter };
                if ((flags & SourceFlagDynamic) != 0) { dynamicSources.Add(source); }
                else { staticSources.Add(source); }
            }
        }

        private byte GetSourceFlags(Transform source)
        {
            if (source == null) { return 0; }
            if (sourceFlagsCache.TryGetValue(source, out byte cached)) { return cached; }

            var current = source;
            byte flags = 0;

            while (current != null)
            {
                if (current.TryGetComponent<ScaledIgnore>(out _))
                {
                    flags |= SourceFlagIgnored;
                }

                if (current.TryGetComponent<ScaledDynamicRoot>(out _))
                {
                    flags |= SourceFlagDynamic;
                }

                if (flags == (SourceFlagDynamic | SourceFlagIgnored)) { break; }
                if (current == transform) { break; }
                current = current.parent;
            }

            sourceFlagsCache[source] = flags;
            return flags;
        }

        private void BuildStaticProxyMesh()
        {
            if (staticSources.Count == 0)
            {
                DestroyStaticProxy();
                return;
            }

            EnsureStaticProxyObject();
            Mesh combined = CombineStaticMeshes();

            if (staticProxyMesh != null)
            {
                DestroySafe(staticProxyMesh);
                staticProxyMesh = null;
            }

            staticProxyMesh = combined;
            staticProxyFilter.sharedMesh = staticProxyMesh;
            staticProxyRenderer.sharedMaterials = uniqueMaterials.Count > 0
                ? uniqueMaterials.ToArray()
                : Array.Empty<Material>();

            bool anyCast = false;
            bool anyReceive = false;
            for (int i = 0; i < staticSources.Count; i++)
            {
                anyCast |= staticSources[i].renderer.shadowCastingMode != ShadowCastingMode.Off;
                anyReceive |= staticSources[i].renderer.receiveShadows;
            }

            ConfigureRenderer(staticProxyRenderer, anyCast, anyReceive);
        }

        private Mesh CombineStaticMeshes()
        {
            uniqueMaterials.Clear();
            uniqueMaterialSet.Clear();
            tempSubmeshes.Clear();

            for (int i = 0; i < staticSources.Count; i++)
            {
                var mats = staticSources[i].renderer.sharedMaterials;
                if (mats == null) { continue; }
                for (int j = 0; j < mats.Length; j++)
                {
                    var mat = mats[j];
                    if (mat == null || !uniqueMaterialSet.Add(mat)) { continue; }
                    uniqueMaterials.Add(mat);
                }
            }

            var finalCombine = new List<CombineInstance>(uniqueMaterials.Count);
            long totalVerts = 0;
            Matrix4x4 toBodyLocal = transform.worldToLocalMatrix;
            Matrix4x4 toScaledUnits = Matrix4x4.Scale(Vector3.one * Constant.INVERSE_SCALE);

            for (int materialIndex = 0; materialIndex < uniqueMaterials.Count; materialIndex++)
            {
                var material = uniqueMaterials[materialIndex];
                var submeshCombine = new List<CombineInstance>(staticSources.Count);

                for (int sourceIndex = 0; sourceIndex < staticSources.Count; sourceIndex++)
                {
                    var source = staticSources[sourceIndex];
                    var mats = source.renderer.sharedMaterials;
                    if (mats == null) { continue; }

                    int maxSubmesh = Mathf.Min(source.filter.sharedMesh.subMeshCount, mats.Length);
                    for (int submesh = 0; submesh < maxSubmesh; submesh++)
                    {
                        if (mats[submesh] != material) { continue; }

                        var ci = new CombineInstance
                        {
                            mesh = source.filter.sharedMesh,
                            subMeshIndex = submesh,
                            transform = toScaledUnits * toBodyLocal * source.filter.transform.localToWorldMatrix
                        };

                        submeshCombine.Add(ci);
                        totalVerts += ci.mesh.vertexCount;
                    }
                }

                if (submeshCombine.Count == 0) { continue; }

                var submeshMesh = new Mesh { name = $"StaticSubmesh_{materialIndex}" };
                if (totalVerts > Mesh16BitLimit) { submeshMesh.indexFormat = IndexFormat.UInt32; }
                submeshMesh.CombineMeshes(submeshCombine.ToArray(), true, true, false);
                tempSubmeshes.Add(submeshMesh);

                finalCombine.Add(new CombineInstance
                {
                    mesh = submeshMesh,
                    subMeshIndex = 0,
                    transform = Matrix4x4.identity
                });
            }

            var combined = new Mesh { name = $"{name}_ScaledStatic" };
            combined.hideFlags = GeneratedHideFlags;
            if (totalVerts > Mesh16BitLimit) { combined.indexFormat = IndexFormat.UInt32; }
            if (finalCombine.Count > 0)
            {
                combined.CombineMeshes(finalCombine.ToArray(), false, false, false);
            }
            combined.RecalculateBounds();

            for (int i = 0; i < tempSubmeshes.Count; i++)
            {
                DestroySafe(tempSubmeshes[i]);
            }
            tempSubmeshes.Clear();

            return combined;
        }

        private void BuildDynamicProxies()
        {
            if (body == null || body.scaledTransform == null) { return; }

            DestroyGeneratedChildrenByPrefix(DynamicProxyPrefix);
            ClearDynamicProxies();

            for (int i = 0; i < dynamicSources.Count; i++)
            {
                var source = dynamicSources[i];
                if (source.renderer == null || source.filter == null) { continue; }

                var proxyObject = new GameObject($"{DynamicProxyPrefix}{source.renderer.name}_{i}");
                proxyObject.layer = layerScaled;

                var proxyTransform = proxyObject.transform;
                proxyTransform.SetParent(body.scaledTransform, false);

                var proxyFilter = proxyObject.AddComponent<MeshFilter>();
                var proxyRenderer = proxyObject.AddComponent<MeshRenderer>();

                proxyFilter.sharedMesh = source.filter.sharedMesh;
                proxyRenderer.sharedMaterials = source.renderer.sharedMaterials;
                bool sourceCast = source.renderer.shadowCastingMode != ShadowCastingMode.Off;
                bool sourceReceive = source.renderer.receiveShadows;
                ConfigureRenderer(proxyRenderer, sourceCast, sourceReceive);
                proxyObject.SetActive(source.renderer.gameObject.activeInHierarchy);
                ApplyGeneratedHideFlags(proxyObject);

                dynamicProxies.Add(new DynamicProxy
                {
                    sourceTransform = source.renderer.transform,
                    sourceRenderer = source.renderer,
                    sourceFilter = source.filter,
                    proxyTransform = proxyTransform,
                    proxyRenderer = proxyRenderer,
                    proxyFilter = proxyFilter,
                    sourceCastShadows = sourceCast,
                    sourceReceiveShadows = sourceReceive
                });
            }
        }

        private void SyncDynamicProxies()
        {
            if (dynamicProxies.Count == 0) { return; }

            if (body == null || body.scaledTransform == null)
            {
                hierarchyDirty = true;
                return;
            }

            Vector3 rootLossyScale = body.scaledTransform.lossyScale;
            Quaternion inverseRootRotation = Quaternion.Inverse(body.scaledTransform.rotation);

            for (int i = 0; i < dynamicProxies.Count; i++)
            {
                var proxy = dynamicProxies[i];
                if (proxy.sourceRenderer == null || proxy.sourceTransform == null || proxy.proxyTransform == null || proxy.proxyRenderer == null || proxy.proxyFilter == null)
                {
                    hierarchyDirty = true;
                    continue;
                }

                bool active = proxy.sourceRenderer.gameObject.activeInHierarchy;
                if (proxy.proxyRenderer.gameObject.activeSelf != active)
                {
                    proxy.proxyRenderer.gameObject.SetActive(active);
                }

                proxy.proxyRenderer.enabled = proxy.sourceRenderer.enabled;
                if (proxy.proxyFilter.sharedMesh != proxy.sourceFilter.sharedMesh)
                {
                    proxy.proxyFilter.sharedMesh = proxy.sourceFilter.sharedMesh;
                }

                if (!active) { continue; }

                bool sourceCast = proxy.sourceRenderer.shadowCastingMode != ShadowCastingMode.Off;
                bool sourceReceive = proxy.sourceRenderer.receiveShadows;
                if (proxy.sourceCastShadows != sourceCast || proxy.sourceReceiveShadows != sourceReceive)
                {
                    ConfigureRenderer(proxy.proxyRenderer, sourceCast, sourceReceive);
                    proxy.sourceCastShadows = sourceCast;
                    proxy.sourceReceiveShadows = sourceReceive;
                }

                Vector3 scaledWorldPosition = proxy.sourceTransform.position * Constant.INVERSE_SCALE;
                proxy.proxyTransform.localPosition = body.scaledTransform.InverseTransformPoint(scaledWorldPosition);
                proxy.proxyTransform.localRotation = inverseRootRotation * proxy.sourceTransform.rotation;
                proxy.proxyTransform.localScale = GetScaledLocalScale(proxy.sourceTransform, rootLossyScale);

                dynamicProxies[i] = proxy;
            }

        }

        private Vector3 GetScaledLocalScale(Transform sourceTransform, Vector3 parentScale)
        {
            Vector3 scaledWorldScale = sourceTransform.lossyScale * Constant.INVERSE_SCALE;
            return new Vector3(
                parentScale.x != 0f ? scaledWorldScale.x / parentScale.x : scaledWorldScale.x,
                parentScale.y != 0f ? scaledWorldScale.y / parentScale.y : scaledWorldScale.y,
                parentScale.z != 0f ? scaledWorldScale.z / parentScale.z : scaledWorldScale.z
            );
        }

        private void EnsureStaticProxyObject()
        {
            if (body == null || body.scaledTransform == null) { return; }

            if (staticProxyObject != null && staticProxyRenderer != null && staticProxyFilter != null) { return; }

            Transform existing = null;
            for (int i = body.scaledTransform.childCount - 1; i >= 0; i--)
            {
                var child = body.scaledTransform.GetChild(i);
                if (child == null) { continue; }
                if (child.name != StaticProxyPrefix) { continue; }

                if (existing == null)
                {
                    existing = child;
                    existing.name = StaticProxyPrefix;
                    continue;
                }

                DestroySafe(child.gameObject);
            }

            staticProxyObject = existing != null ? existing.gameObject : new GameObject(StaticProxyPrefix);
            staticProxyObject.layer = layerScaled;

            var staticTransform = staticProxyObject.transform;
            staticTransform.SetParent(body.scaledTransform, false);
            staticTransform.localPosition = Vector3.zero;
            staticTransform.localRotation = Quaternion.identity;
            staticTransform.localScale = Vector3.one;

            staticProxyFilter = staticProxyObject.GetComponent<MeshFilter>();
            staticProxyRenderer = staticProxyObject.GetComponent<MeshRenderer>();
            if (staticProxyFilter == null) { staticProxyFilter = staticProxyObject.AddComponent<MeshFilter>(); }
            if (staticProxyRenderer == null) { staticProxyRenderer = staticProxyObject.AddComponent<MeshRenderer>(); }
            ApplyGeneratedHideFlags(staticProxyObject);
        }

        private void ConfigureRenderer(MeshRenderer renderer, bool sourceCast, bool sourceReceive)
        {
            if (renderer == null) { return; }

            renderer.shadowCastingMode = (unityShadows && sourceCast) ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = unityShadows && sourceReceive;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private void DestroyStaticProxy()
        {
            if (staticProxyMesh != null)
            {
                DestroySafe(staticProxyMesh);
                staticProxyMesh = null;
            }

            if (staticProxyObject != null)
            {
                DestroySafe(staticProxyObject);
                staticProxyObject = null;
            }

            staticProxyRenderer = null;
            staticProxyFilter = null;
        }

        private void ClearDynamicProxies()
        {
            for (int i = 0; i < dynamicProxies.Count; i++)
            {
                var proxy = dynamicProxies[i];
                if (proxy.proxyTransform != null)
                {
                    DestroySafe(proxy.proxyTransform.gameObject);
                }
            }
            dynamicProxies.Clear();
        }

        private void ClearAllProxies()
        {
            DestroyStaticProxy();
            DestroyGeneratedChildrenByPrefix(StaticProxyPrefix);
            DestroyGeneratedChildrenByPrefix(DynamicProxyPrefix);
            ClearDynamicProxies();
            ResetProxyState();
        }

        private bool HasAnyProxyMeshes()
        {
            if (staticProxyFilter != null && staticProxyFilter.sharedMesh != null && staticProxyRenderer != null) { return true; }
            return dynamicProxies.Count > 0;
        }

        private bool IsDynamicProxySetInvalid()
        {
            if (dynamicSources.Count != dynamicProxies.Count) { return true; }

            for (int i = 0; i < dynamicProxies.Count; i++)
            {
                var proxy = dynamicProxies[i];
                if (proxy.sourceTransform == null
                    || proxy.sourceRenderer == null
                    || proxy.sourceFilter == null
                    || proxy.proxyTransform == null
                    || proxy.proxyRenderer == null
                    || proxy.proxyFilter == null)
                {
                    return true;
                }
            }

            return false;
        }

        private int ComputeSourceSignature(List<SourceRenderer> sources, bool includeTransformHash, bool includeShadowFlags, bool requireMesh)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + sources.Count;
                hash = (hash * 31) + layerScaled;
                hash = (hash * 31) + (unityShadows ? 1 : 0);

                for (int i = 0; i < sources.Count; i++)
                {
                    var source = sources[i];
                    if (source.renderer == null || source.filter == null) { continue; }
                    Mesh mesh = source.filter.sharedMesh;
                    if (requireMesh && mesh == null) { continue; }

                    hash = (hash * 31) + source.renderer.GetInstanceID();
                    hash = (hash * 31) + (mesh != null ? mesh.GetInstanceID() : 0);
                    if (includeTransformHash)
                    {
                        hash = (hash * 31) + source.filter.transform.localToWorldMatrix.GetHashCode();
                    }
                    if (includeShadowFlags)
                    {
                        hash = (hash * 31) + (int)source.renderer.shadowCastingMode;
                        hash = (hash * 31) + (source.renderer.receiveShadows ? 1 : 0);
                    }
                    hash = AppendMaterialsToHash(hash, source.renderer.sharedMaterials);
                }

                return hash;
            }
        }

        private static int AppendMaterialsToHash(int hash, Material[] materials)
        {
            unchecked
            {
                if (materials == null) { return (hash * 31); }

                hash = (hash * 31) + materials.Length;
                for (int i = 0; i < materials.Length; i++)
                {
                    hash = (hash * 31) + (materials[i] != null ? materials[i].GetInstanceID() : 0);
                }

                return hash;
            }
        }

        private static bool TryAppendShadowProxy(List<ShadowCasterManager.ShadowMeshProxy> output, MeshFilter filter, MeshRenderer renderer, bool requireEnabled)
        {
            if (output == null || filter == null || renderer == null || filter.sharedMesh == null) { return false; }
            if (requireEnabled && !renderer.enabled) { return false; }
            if (!renderer.gameObject.activeInHierarchy) { return false; }

            output.Add(new ShadowCasterManager.ShadowMeshProxy
            {
                mesh = filter.sharedMesh,
                localToWorld = filter.transform.localToWorldMatrix,
                layer = filter.gameObject.layer
            });
            return true;
        }

        private void ResetProxyState()
        {
            hasBuiltAnyProxy = false;
            hasSourceSignatures = false;
            staticSourceSignature = 0;
            dynamicSourceSignature = 0;
        }

        private void DestroyGeneratedChildrenByPrefix(string prefix)
        {
            if (body == null || body.scaledTransform == null || string.IsNullOrEmpty(prefix)) { return; }

            for (int i = body.scaledTransform.childCount - 1; i >= 0; i--)
            {
                var child = body.scaledTransform.GetChild(i);
                if (child == null) { continue; }
                if (!child.name.StartsWith(prefix, StringComparison.Ordinal)) { continue; }

                DestroySafe(child.gameObject);
            }
        }

        private static void ApplyGeneratedHideFlags(GameObject gameObject)
        {
            if (gameObject == null) { return; }

            gameObject.hideFlags = GeneratedHideFlags;
            var components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component != null)
                {
                    component.hideFlags = GeneratedHideFlags;
                }
            }
        }

        private static void DestroySafe(UnityEngine.Object obj)
        {
            if (obj == null) { return; }

#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(obj); }
            else { UnityEngine.Object.Destroy(obj); }
#else
            UnityEngine.Object.Destroy(obj);
#endif
        }
    }
}
