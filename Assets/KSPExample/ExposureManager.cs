using UnityEngine;

namespace UnityPhysicsFloatingOrigin
{
    public class ExposureManager : MonoBehaviour
    {
        public static ExposureManager Instance;
        public Body starBody;
        [SerializeField] private Camera localCamera;

        private void Start()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            RenderSettings.skybox.SetFloat("_Exposure", 1);
        }

        private void Update()
        {
            var camPos = localCamera.transform.position;
            var camFov = localCamera.fieldOfView;
            var camAngle = localCamera.transform.forward;
            var colorScalar = 1f;

            var bodies = PhysicsManager.bodies;
            for (int i = 0; i < bodies.Count; ++i)
            {
                if (bodies[i].type == BodyType.Celestial)
                {
                    float bodyRadius = bodies[i].GetComponent<Celestial>().radius;
                    float sqrBodyDist = (bodies[i].rb.position - camPos).sqrMagnitude;
                    float bodySize = Mathf.Acos((float)((sqrBodyDist - bodyRadius * bodyRadius) / sqrBodyDist)) * Mathf.Rad2Deg;

                    if (bodySize > 1.0f)
                    {
                        Vector3 bodyPosition = bodies[i].rb.position;
                        Vector3 targetVectorToSun = starBody.rb.position - bodyPosition;
                        Vector3 targetVectorToCam = camPos - bodyPosition;

                        float targetRelAngle = (float)Vector3.Angle(targetVectorToSun, targetVectorToCam);
                        targetRelAngle = Mathf.Max(targetRelAngle, bodySize);
                        targetRelAngle = Mathf.Min(targetRelAngle, 100.0f);
                        targetRelAngle = 1.0f - ((targetRelAngle - bodySize) / (100.0f - bodySize));

                        float CBAngle = Mathf.Max(0.0f, Vector3.Angle((bodyPosition - camPos).normalized, camAngle) - bodySize);
                        CBAngle = 1.0f - Mathf.Min(1.0f, Mathf.Max(0.0f, CBAngle - (camFov / 2.0f) - 5.0f) / (camFov / 4.0f));
                        bodySize = Mathf.Min(bodySize, 100.0f);

                        colorScalar *= 1.0f - (targetRelAngle * Mathf.Sqrt(bodySize / 100.0f) * CBAngle);
                    }
                }
            }

            RenderSettings.skybox.SetFloat("_Exposure", colorScalar);
        }
    }
}
