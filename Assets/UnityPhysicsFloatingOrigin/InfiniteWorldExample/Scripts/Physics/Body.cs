using UnityEngine;

namespace UnityPhysicsFloatingOrigin
{
    public enum BodyType
    {
        Celestial, Vessel, Missile, Bullet, Temporary
    }

    [ExecuteAlways, RequireComponent(typeof(Rigidbody))]
    public class Body : MonoBehaviour
    {
        public BodyType type = BodyType.Temporary;
        public int faction = 0;
        public int targetingCount;
        public int missilesTargetingCount;
        [Space]
        public BodyData bodyData = new BodyData();
        [HideInInspector] public Rigidbody rb;

        private const int ScaledLayer = 3;
        public Transform scaledTransform;

        private void OnEnable()
        {
            if (rb == null) { rb = GetComponent<Rigidbody>(); }

            if (scaledTransform == null)
            {
                var root = GameObject.Find("Scaled");
                if (root == null)
                {
                    root = new GameObject("Scaled");
                    root.layer = ScaledLayer;
                }

                scaledTransform = new GameObject(gameObject.name).transform;
                scaledTransform.SetParent(root.transform, false);
                scaledTransform.localScale = Vector3.one;
                scaledTransform.gameObject.layer = ScaledLayer;
                scaledTransform.SetPositionAndRotation(transform.position * Constant.INVERSE_SCALE, transform.rotation);
            }

            if (scaledTransform.gameObject.layer != ScaledLayer)
            {
                scaledTransform.gameObject.layer = ScaledLayer;
            }

            if (!string.Equals(scaledTransform.gameObject.name, gameObject.name, System.StringComparison.Ordinal))
            {
                scaledTransform.gameObject.name = gameObject.name;
            }

            if (scaledTransform && !scaledTransform.gameObject.activeSelf)
            {
                scaledTransform.gameObject.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (scaledTransform && scaledTransform.gameObject.activeSelf)
            {
                scaledTransform.gameObject.SetActive(false);
            }
        }

#if UNITY_EDITOR
        //public Keplerian keplerian;
        private bool inEditor => Application.isEditor && !Application.isPlaying;
        private Vector3 lastTransformPostion;
        private Vector3d lastBodyDataPostion;
        private Vector3 lastScaledTransfromPostion;
        private Quaternion lastTransformRotation;
        private Quaternion lastScaledTransfromRotation;

        private void Update()
        {
            if (!inEditor) { return; }

            // if (keplerian.parentBody != null)
            // {
            //     keplerian.CartesianToKeplerian(bodyData);
            // }

            if (transform.position != lastTransformPostion)
            {
                lastTransformPostion = transform.position;
                bodyData.position = (Vector3d)transform.position;
                if (scaledTransform == null) { return; }
                scaledTransform.position = transform.position * Constant.INVERSE_SCALE;
            }
            else if (scaledTransform != null && scaledTransform.position != lastScaledTransfromPostion)
            {
                lastScaledTransfromPostion = scaledTransform.position;
                bodyData.position = (Vector3d)scaledTransform.position * Constant.SCALE;
                transform.position = scaledTransform.position * Constant.SCALE;
                scaledTransform.rotation = transform.rotation;
            }
            else if (bodyData.position != lastBodyDataPostion)
            {
                lastBodyDataPostion = bodyData.position;
                transform.position = (Vector3)bodyData.position;
                if (scaledTransform == null) { return; }
                scaledTransform.position = transform.position * Constant.INVERSE_SCALE;
            }

            if (transform.rotation != lastTransformRotation)
            {
                lastTransformRotation = transform.rotation;
                if (scaledTransform == null) { return; }
                scaledTransform.rotation = transform.rotation;
            }
            else if (scaledTransform != null && scaledTransform.rotation != lastScaledTransfromRotation)
            {
                lastScaledTransfromRotation = scaledTransform.rotation;
                transform.rotation = scaledTransform.rotation;
            }

            if (scaledTransform != null)
            {
                if (!string.Equals(scaledTransform.gameObject.name, gameObject.name, System.StringComparison.Ordinal))
                {
                    scaledTransform.gameObject.name = gameObject.name;
                }
            }
        }
#endif

        // private void ApplyKinematicTorque(Vector3 worldTorque)
        // {
        //     if (body == null || rb == null) { return; }

        //     Vector3 inertia = rb.inertiaTensor;
        //     Quaternion inertiaWorldRotation = rb.rotation * rb.inertiaTensorRotation;
        //     Vector3 localTorque = Quaternion.Inverse(inertiaWorldRotation) * worldTorque;
        //     Vector3 localAngularAcceleration = new Vector3(
        //         localTorque.x / Mathf.Max(1e-4f, inertia.x),
        //         localTorque.y / Mathf.Max(1e-4f, inertia.y),
        //         localTorque.z / Mathf.Max(1e-4f, inertia.z));
        //     Vector3 worldAngularAcceleration = inertiaWorldRotation * localAngularAcceleration;
        //     if (!IsFiniteVector(worldAngularAcceleration)) { return; }

        //     body.bodyData.angularVelocity += worldAngularAcceleration * Time.fixedDeltaTime;
        // }

        public void AddForce(Vector3 force)
        {
            var acceleration = (Vector3)((Vector3d)force / rb.mass);

            if (this == PhysicsManager.mainBody)
            {
                foreach (var body in PhysicsManager.bodies)
                {
                    if (body == PhysicsManager.mainBody) { continue; }

                    if (body.rb.isKinematic)
                    {
                        body.bodyData.velocity -= (Vector3d)acceleration * Time.fixedDeltaTime * 1;
                    }
                    else
                    {
                        body.rb.AddForce(-acceleration, ForceMode.Acceleration);
                    }
                }
            }
            else
            {
                if (rb.isKinematic)
                {
                    bodyData.velocity += (Vector3d)acceleration * Time.fixedDeltaTime * 1;
                }
                else
                {
                    rb.AddForce(acceleration, ForceMode.Acceleration);
                }
            }
        }

        public void Collisions(bool state)
        {
            if (bodyData.forceKinematic)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.isKinematic = true;
                return;
            }

            rb.mass = (float)bodyData.mass;
            rb.angularVelocity = bodyData.angularVelocity;
            rb.velocity = (Vector3)bodyData.velocity;
            rb.position = (Vector3)bodyData.position;

            if (state)
            {
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.detectCollisions = true;
            }
            else
            {
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
        }
    }
}
