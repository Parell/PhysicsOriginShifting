using UnityEngine;

namespace PhysicsFloatingOrigin
{
    [DefaultExecutionOrder(-100)]
    public class ExamplePhysicsManager : MonoBehaviour
    {
        public enum RebasingState { None, FloatingOrigin, PhysicsFloatingOrigin }

        [Header("Rebasing")]
        [SerializeField] private RebasingState rebasingState;
        [SerializeField] private Rigidbody mainBody;
        [SerializeField] private Rigidbody[] bodies;
        [SerializeField] private float rebaseDistanceThreshold = 1000f;
        [SerializeField] private float rebaseVelocityThreshold = 1000f;
        [SerializeField] private float frequency = 1f;
        [SerializeField] private float damping = 1f;

        private Vector3 originPositionOffset;
        private Vector3 originVelocityOffset;
        private Vector3 previousMainBodyVelocity;
        private bool hasPreviousMainBodyVelocity;

        private Vector3 mainBodyPhysicalPosition;
        private Vector3 mainBodyPhysicalVelocity;
        private Vector3 mainBodyPhysicalAcceleration;
        private Vector3 imaginaryAcceleration;
        private Vector3 previousImaginaryAcceleration;

        public Vector3 MainBodyPhysicalPosition => mainBodyPhysicalPosition;
        public Vector3 MainBodyPhysicalVelocity => mainBodyPhysicalVelocity;
        public Vector3 MainBodyPhysicalAcceleration => mainBodyPhysicalAcceleration;
        public RebasingState CurrentRebasingState => rebasingState;
        public Rigidbody MainBody => mainBody;

        private void Start()
        {
            bodies = FindObjectsOfType<Rigidbody>();
            if (mainBody == null && bodies.Length > 0)
            {
                mainBody = bodies[0];
            }
            UpdatePhysicalState();
        }

        private void FixedUpdate()
        {
            ApplyRebasingIfNeeded();
            ApplyPhysicsRebasing();
            UpdatePhysicalState();
            Physics.Simulate(Time.fixedDeltaTime);
        }

        private void ApplyPhysicsRebasing()
        {
            if (rebasingState != RebasingState.PhysicsFloatingOrigin || mainBody == null || bodies == null)
            {
                imaginaryAcceleration = Vector3.zero;
                return;
            }

            float kp = frequency * frequency;
            float kd = 2f * frequency * damping;
            float dt = Time.fixedDeltaTime;
            float g = 1f / (1f + kd * dt + kp * dt * dt);
            float ksg = kp * g;
            float kdg = (kd + kp * dt) * g;

            Vector3 pt0 = mainBody.worldCenterOfMass;
            Vector3 vt0 = mainBody.velocity;
            Vector3 acceleration = (Vector3.zero - pt0) * ksg + (Vector3.zero - vt0) * kdg;
            imaginaryAcceleration = acceleration;

            mainBody.AddForce(acceleration, ForceMode.Acceleration);

            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];
                if (body == null || body == mainBody)
                {
                    continue;
                }

                body.AddForce(acceleration, ForceMode.Acceleration);
            }
        }

        public void SetRebasingState(RebasingState state)
        {
            if (rebasingState == state)
            {
                return;
            }

            rebasingState = state;
            hasPreviousMainBodyVelocity = false;
            previousImaginaryAcceleration = Vector3.zero;
        }

        private void ApplyRebasingIfNeeded()
        {
            if (rebasingState != RebasingState.FloatingOrigin || mainBody == null || bodies == null)
            {
                return;
            }

            Vector3 positionShift = mainBody.position;
            if (positionShift.magnitude > rebaseDistanceThreshold)
            {
                for (int i = 0; i < bodies.Length; i++)
                {
                    bodies[i].position -= positionShift;
                }

                originPositionOffset += positionShift;
            }

            Vector3 velocityShift = mainBody.velocity;
            if (velocityShift.magnitude > rebaseVelocityThreshold)
            {
                for (int i = 0; i < bodies.Length; i++)
                {
                    bodies[i].velocity -= velocityShift;
                }

                originVelocityOffset += velocityShift;
            }
        }

        private void UpdatePhysicalState()
        {
            if (mainBody == null)
            {
                mainBodyPhysicalPosition = Vector3.zero;
                mainBodyPhysicalVelocity = Vector3.zero;
                mainBodyPhysicalAcceleration = Vector3.zero;
                hasPreviousMainBodyVelocity = false;
                imaginaryAcceleration = Vector3.zero;
                previousImaginaryAcceleration = Vector3.zero;
                return;
            }

            if (rebasingState == RebasingState.PhysicsFloatingOrigin && Time.fixedDeltaTime > 0f)
            {
                // Counteract the artificial acceleration used to keep the origin centered.
                originVelocityOffset -= previousImaginaryAcceleration * Time.fixedDeltaTime;
                originPositionOffset += originVelocityOffset * Time.fixedDeltaTime;
            }

            mainBodyPhysicalPosition = mainBody.position + originPositionOffset;
            mainBodyPhysicalVelocity = mainBody.velocity + originVelocityOffset;

            if (hasPreviousMainBodyVelocity && Time.fixedDeltaTime > 0f)
            {
                mainBodyPhysicalAcceleration = (mainBodyPhysicalVelocity - previousMainBodyVelocity) / Time.fixedDeltaTime;
            }
            else
            {
                mainBodyPhysicalAcceleration = Vector3.zero;
            }

            previousMainBodyVelocity = mainBodyPhysicalVelocity;
            hasPreviousMainBodyVelocity = true;
            previousImaginaryAcceleration = imaginaryAcceleration;
        }

        private void OnValidate()
        {
            rebaseDistanceThreshold = Mathf.Max(0f, rebaseDistanceThreshold);
            rebaseVelocityThreshold = Mathf.Max(0f, rebaseVelocityThreshold);
            frequency = Mathf.Max(0f, frequency);
            damping = Mathf.Max(0f, damping);
        }
    }
}
