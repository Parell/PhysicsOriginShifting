using System.Collections.Generic;
using UnityEngine;

namespace UnityPhysicsFloatingOrigin
{
    public class ThrusterEffect : MonoBehaviour
    {
        [System.Serializable]
        public class Roots
        {
            public GameObject segmentPrefab;
            public Material[] segmentMaterials;
            public float offset;
            public Vector2 length;
            public Vector2 minRadius;
            public Vector2 maxRadius;
            public Vector3 minError;
            public Vector3 maxError;
            public float errorSpeed;
            public int noiseSamples = 10;
        }

        [Range(-1, 1)] public float throttle;
        [SerializeField] private Roots[] roots;
        public float maxThrust;
        public float uMin = 0;
        public float uMax = 1;
        public Vector3 position;
        public Vector3 direction;
        public bool mainEngine;
        private Light lightSource;
        private AudioSource audioSource;

        private struct RootData
        {
            public Roots p;
            public Transform root;
            public Transform[] segments;

            public float[] generatedNoise;
            public int noiseIndex;
            public Vector3 noise;
        }

        private readonly List<RootData> rootData = new List<RootData>();

        private void Start()
        {
            lightSource = GetComponent<Light>();
            audioSource = GetComponent<AudioSource>();
            BuildPlumes();
        }

        private void BuildPlumes()
        {
            foreach (var r in rootData)
            {
                if (r.root) { DestroySafe(r.root.gameObject); }
            }
            rootData.Clear();

            if (roots == null) { return; }

            for (int r = 0; r < roots.Length; r++)
            {
                var p = roots[r];
                if (p == null || p.segmentPrefab == null) { continue; }

                var rootGO = new GameObject($"{"Plume_"}{r}");
                rootGO.transform.SetParent(transform, false);
                rootGO.transform.localPosition = Vector3.zero;
                rootGO.transform.localRotation = Quaternion.identity;
                rootGO.AddComponent<ScaledDynamicRoot>();

                var segs = new Transform[p.segmentMaterials.Length];

                for (int i = 0; i < p.segmentMaterials.Length; i++)
                {
                    var segGO = Instantiate(p.segmentPrefab, rootGO.transform);
                    segGO.name = $"Segment_{i}";
                    segGO.transform.localPosition = Vector3.zero;
                    segGO.transform.localRotation = Quaternion.identity;
                    segGO.transform.localScale = Vector3.one;

                    // Segment_i gets material[i]
                    var rend = segGO.GetComponentInChildren<Renderer>();
                    if (rend && p.segmentMaterials != null && i < p.segmentMaterials.Length && p.segmentMaterials[i])
                    {
                        // assigns per-instance material (safe; doesn't mutate the asset)
                        rend.material = p.segmentMaterials[i];
                    }

                    segs[i] = segGO.transform;
                }

                var rd = new RootData
                {
                    p = p,
                    root = rootGO.transform,
                    segments = segs,
                    generatedNoise = new float[Mathf.Max(2, p.noiseSamples)],
                    noiseIndex = 0,
                    noise = Vector3.zero
                };

                BuildNoise(ref rd);
                rootData.Add(rd);
            }

            NotifyScaledVariantDirty();
        }

        private static void DestroySafe(Object obj)
        {
            if (obj == null) { return; }
#if UNITY_EDITOR
            if (!Application.isPlaying) { Object.DestroyImmediate(obj); }
            else { Object.Destroy(obj); }
#else
            Object.Destroy(obj);
#endif
        }

        private void NotifyScaledVariantDirty()
        {
            var body = GetComponentInParent<Body>();
            if (body == null) { return; }

            if (body.TryGetComponent(out Scaled scaledVariantAuto))
            {
                scaledVariantAuto.MarkHierarchyDirty();
            }
        }

        private void BuildNoise(ref RootData rd)
        {
            var noiseGen = new FastNoiseLite();
            noiseGen.SetNoiseType(FastNoiseLite.NoiseType.Perlin);

            float ratio = 2 * Mathf.PI / rd.generatedNoise.Length;
            for (int i = 0; i < rd.generatedNoise.Length; i++)
            {
                float x = 10 * Mathf.Cos(ratio * (i + 1));
                float y = 10 * Mathf.Sin(ratio * (i + 1));
                rd.generatedNoise[i] = noiseGen.GetNoise(x, y);
            }
        }

        private void Update()
        {
            float safeThrottle = IsFinite(throttle) ? throttle : -1f;
            bool active = safeThrottle != -1;
            for (int r = 0; r < rootData.Count; r++)
            {
                var go = rootData[r].root.gameObject;
                if (go.activeSelf != active) { go.SetActive(active); }
            }
            if (!active) { return; }

            Vector3 origin = transform.position;
            Vector3 dir = transform.forward; // adjust if needed for your thruster orientation
            if (!IsFinite(dir) || dir.sqrMagnitude <= Mathf.Epsilon) { dir = Vector3.forward; }

            for (int r = 0; r < rootData.Count; r++)
            {
                var rd = rootData[r];

                // noise (per-root)
                var currentError = Vector3.Lerp(rd.p.minError, rd.p.maxError, safeThrottle);
                if (!IsFinite(currentError)) { currentError = Vector3.zero; }

                if (rd.noise == rd.generatedNoise[rd.noiseIndex] * currentError)
                {
                    rd.noiseIndex++;
                    if (rd.noiseIndex > rd.generatedNoise.Length - 1) { rd.noiseIndex = 0; }
                }

                rd.noise = Vector3.MoveTowards(
                    rd.noise,
                    rd.generatedNoise[rd.noiseIndex] * currentError,
                    rd.p.errorSpeed * rd.generatedNoise.Length * Time.deltaTime
                );
                if (!IsFinite(rd.noise)) { rd.noise = Vector3.zero; }

                // shape
                var currentRadius = Vector2.Lerp(rd.p.minRadius, rd.p.maxRadius, safeThrottle);
                float currentLength = Mathf.Lerp(rd.p.length.x, rd.p.length.y, safeThrottle);
                if (!IsFinite(currentRadius)) { currentRadius = Vector2.zero; }
                if (!IsFinite(currentLength)) { currentLength = 0f; }

                currentRadius.x += rd.noise.x;
                currentRadius.y += rd.noise.y;

                int segCount = rd.segments.Length;
                float slope = (currentRadius.x - currentRadius.y) / Mathf.Max(1, segCount);

                // root pose
                rd.root.position = origin;
                rd.root.rotation = Quaternion.LookRotation(dir, transform.up) * Quaternion.Euler(90f, 0f, 0f);

                // independent segment placement + scale
                float step = currentLength / Mathf.Max(1, segCount);

                for (int i = 0; i < segCount; i++)
                {
                    float radiusAtI = currentRadius.x - (i * slope);
                    float segLen = step;

                    rd.segments[i].position = origin + dir * (i * step + rd.p.offset);
                    rd.segments[i].rotation = Quaternion.LookRotation(dir, transform.up) * Quaternion.Euler(90f, 0f, 0f);

                    Vector3 nextScale = new Vector3(radiusAtI, segLen + rd.noise.z, radiusAtI);
                    if (!IsFinite(nextScale)) { nextScale = Vector3.zero; }
                    rd.segments[i].localScale = nextScale;
                }

                rootData[r] = rd;
            }
        }

        private static bool IsFinite(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v);
        }

        private static bool IsFinite(Vector2 v)
        {
            return IsFinite(v.x) && IsFinite(v.y);
        }

        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }
    }
}
