using UnityEngine;

namespace PhysicsFloatingOrigin
{
    public class CollisionResolver : MonoBehaviour
    {
        [SerializeField] private float frequency = 1;
        [SerializeField] private float damping = 1;
        private float lastCollisionTime = float.NegativeInfinity;
        public bool IsMainBodyColliding => (Time.time - lastCollisionTime) <= (Time.fixedDeltaTime * 1.5f);
        private Body mainBody;

        private void Awake()
        {
            mainBody = GetComponent<Body>();
        }

        private void FixedUpdate()
        {
            if (mainBody == null || PhysicsManager.Instance == null || mainBody != PhysicsManager.mainBody || mainBody.rb == null || mainBody.rb.isKinematic)
            {
                return;
            }

            float kp = frequency * frequency;
            float kd = 2f * frequency * damping;
            float dt = Time.fixedDeltaTime;
            float g = 1 / (1 + kd * dt + kp * dt * dt);
            float ksg = kp * g;
            float kdg = (kd + kp * dt) * g;
            float mainMass = Mathf.Max(mainBody.rb.mass, 1e-6f);
            Vector3 Pt0 = mainBody.rb.worldCenterOfMass;
            Vector3 Vt0 = mainBody.rb.velocity;
            Vector3 F = (Vector3.zero - Pt0) * ksg + (Vector3.zero - Vt0) * kdg;
            mainBody.rb.AddForce(F);

            foreach (var body in PhysicsManager.bodies)
            {
                if (!body || body == mainBody || body.rb == null) { continue; }

                if (body.rb.isKinematic)
                {
                    body.bodyData.velocity += (Vector3d)(F / mainMass) * dt;
                }
                else
                {
                    body.rb.AddForce(F / mainMass, ForceMode.Acceleration);
                    body.bodyData.position = (Vector3d)body.rb.position;
                    body.bodyData.velocity = (Vector3d)body.rb.velocity;
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            lastCollisionTime = Time.time;
        }

        private void OnCollisionStay(Collision collision)
        {
            lastCollisionTime = Time.time;
        }
    }
}
