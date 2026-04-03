using System.Collections.Generic;
using UnityEngine;

namespace UnityPhysicsFloatingOrigin
{
    public class Mover : MonoBehaviour
    {
        public Vector3 translation;
        public int mainEngine;
        public Quaternion targetRotation;
        public float maxAcceleration;
        public Vector3 force;
        public Vector3 torque;
        [SerializeField] private Vector3 maxPositiveTorque;
        [SerializeField] private Vector3 maxNegativeTorque;
        [SerializeField] private Vector3 maxPositiveForce;
        [SerializeField] private Vector3 maxNegativeForce;
        [SerializeField] private float frequency = 1;
        [SerializeField] private float damping = 1;

        private Vector3 targetPreviousVelocity;
        private float tolerance = 1e-2f;
        private int maxIterations = 12;
        private ThrusterEffect[] thrusters = new ThrusterEffect[0];
        private Body body;

        private void Start()
        {
            body = GetComponent<Body>();
            targetRotation = transform.rotation;

            thrusters = GetComponentsInChildren<ThrusterEffect>();
            InitializeThrusters();
        }

        private void InitializeThrusters()
        {
            void AccumulateAxisLimits(ref Vector3 positive, ref Vector3 negative, Vector3 value)
            {
                if (value.x >= 0f) { positive.x += value.x; } else { negative.x += -value.x; }
                if (value.y >= 0f) { positive.y += value.y; } else { negative.y += -value.y; }
                if (value.z >= 0f) { positive.z += value.z; } else { negative.z += -value.z; }
            }

            maxPositiveTorque = Vector3.zero;
            maxNegativeTorque = Vector3.zero;
            maxPositiveForce = Vector3.zero;
            maxNegativeForce = Vector3.zero;
            float mainThrust = 0f;
            for (int i = 0; i < thrusters.Length; i++)
            {
                var thruster = thrusters[i];
                if (thruster.mainEngine)
                {
                    mainThrust += Mathf.Max(0f, thruster.maxThrust);
                    continue;
                }

                thruster.position = transform.InverseTransformPoint(thruster.transform.position) - body.rb.centerOfMass;
                thruster.direction = transform.InverseTransformDirection(-thruster.transform.forward).normalized;

                Vector3 force = thruster.direction.normalized * thruster.maxThrust;
                Vector3 torque = Vector3.Cross(thruster.position, force);

                AccumulateAxisLimits(ref maxPositiveForce, ref maxNegativeForce, force);
                AccumulateAxisLimits(ref maxPositiveTorque, ref maxNegativeTorque, torque);
            }

            float mass = body.rb ? Mathf.Max(0.01f, body.rb.mass) : 0.01f;
            maxAcceleration = mainThrust / mass;
        }

        private void FixedUpdate()
        {
            if (PhysicsManager.timeScale > 1 || thrusters.Length == 0) { return; }

            torque = RotationController(frequency, damping, maxPositiveTorque, maxNegativeTorque);
            force = PositionController();

            AllocateGreedy(transform.InverseTransformDirection(force), transform.InverseTransformDirection(torque), body.bodyData.size, maxIterations, tolerance);

            foreach (var thruster in thrusters)
            {
                if (!thruster.mainEngine) { continue; }
                thruster.throttle = mainEngine / 2f;
                force += transform.forward * thruster.throttle * thruster.maxThrust;
            }

            body.rb.AddTorque(torque);
            body.AddForce(force);
        }

        private Vector3 RotationController(float frequency, float damping, Vector3 maxPositiveTorque, Vector3 maxNegativeTorque, float angleEpsRad = 0.01f, float omegaEps = 0.01f)
        {
            Vector3 TorqueFromAngularAccelWithSaturation(Vector3 angularAccelWorld)
            {
                Quaternion rotInertiaWorld = body.rb.rotation * body.rb.inertiaTensorRotation;
                Vector3 torque = Quaternion.Inverse(rotInertiaWorld) * angularAccelWorld;
                torque.Scale(body.rb.inertiaTensor);
                torque = rotInertiaWorld * torque;
                Vector3 torqueBody = Quaternion.Inverse(body.rb.rotation) * torque;

                float s = 1f;
                void Axis(float u, float pos, float neg)
                {
                    if (u > 0f) s = Mathf.Min(s, pos / u);
                    else if (u < 0f) s = Mathf.Min(s, neg / -u);
                }

                Axis(torqueBody.x, maxPositiveTorque.x, maxNegativeTorque.x);
                Axis(torqueBody.y, maxPositiveTorque.y, maxNegativeTorque.y);
                Axis(torqueBody.z, maxPositiveTorque.z, maxNegativeTorque.z);

                torqueBody *= Mathf.Clamp01(s);
                return body.rb.rotation * torqueBody;
            }

            Quaternion error = targetRotation * Quaternion.Inverse(body.rb.rotation);
            if (error.w < 0f) { error.x = -error.x; error.y = -error.y; error.z = -error.z; error.w = -error.w; }

            float invN = 1f / Mathf.Sqrt(error.x * error.x + error.y * error.y + error.z * error.z + error.w * error.w);
            error.x *= invN; error.y *= invN; error.z *= invN; error.w *= invN;

            Vector3 v = new Vector3(error.x, error.y, error.z);
            float sinHalf = v.magnitude;

            Vector3 angleErrorWorld;
            if (sinHalf > 1e-6f)
            {
                float angle = 2f * Mathf.Atan2(sinHalf, error.w);
                angleErrorWorld = v * (angle / sinHalf);
            }
            else
            {
                angleErrorWorld = 2f * v;
            }

            Vector3 omega = body.rb.angularVelocity;

            if (angleErrorWorld.sqrMagnitude < angleEpsRad * angleEpsRad && omega.sqrMagnitude < omegaEps * omegaEps)
            {
                return Vector3.zero;
            }

            float kp = frequency * frequency;
            float kd = 2f * frequency * damping;
            float dt = Time.fixedDeltaTime;
            float g = 1f / (1f + kd * dt + kp * dt * dt);
            float ksg = kp * g;
            float kdg = (kd + kp * dt) * g;

            Vector3 accelPD = ksg * angleErrorWorld - kdg * omega;

            return TorqueFromAngularAccelWithSaturation(accelPD);
        }

        private Vector3 PositionController()
        {
            float GetSignedAxisForce(float input, float positiveLimit, float negativeLimit)
            {
                if (input > 0f) { return input * Mathf.Max(0f, positiveLimit); }
                if (input < 0f) { return input * Mathf.Max(0f, negativeLimit); }
                return 0f;
            }

            Vector3 desiredForceLocal = new Vector3(
                GetSignedAxisForce(translation.x, maxPositiveForce.x, maxNegativeForce.x),
                GetSignedAxisForce(translation.y, maxPositiveForce.y, maxNegativeForce.y),
                GetSignedAxisForce(translation.z, maxPositiveForce.z, maxNegativeForce.z));
            Vector3 desiredForce = transform.TransformVector(desiredForceLocal);

            return desiredForce;
        }

        public void Intercept(Body targetBody, Vector3 offset)
        {
            if (targetBody == null || targetBody.rb == null)
            {
                targetPreviousVelocity = Vector3.zero;
                translation = Vector3.zero;
                mainEngine = 0;
                return;
            }

            var relativePosition = targetBody.rb.position + offset - body.rb.position;
            var relativeVelocity = targetBody.rb.velocity - body.rb.velocity;
            var targetAcceleration = (targetBody.rb.velocity - targetPreviousVelocity) / Time.fixedDeltaTime;
            targetPreviousVelocity = targetBody.rb.velocity;
            var relativeAcceleration = targetAcceleration - (transform.forward * maxAcceleration);

            var timeToHit = PhysicsManager.ClosestTimeToApproach(relativePosition, relativeVelocity, relativeAcceleration, 30f);
            var interceptDirection = MathExtentions.FastNorimalize(PhysicsManager.PredictPosition(relativePosition, relativeVelocity, relativeAcceleration, timeToHit));

            var direction = transform.forward;
            float desiredSpeed = Mathf.Max(25f, body.rb.velocity.magnitude);
            Vector3 accelCmdWorld = (interceptDirection * desiredSpeed - body.rb.velocity) / Time.fixedDeltaTime;
            Vector3 lateralAccelCmdLocal = transform.InverseTransformDirection(accelCmdWorld - Vector3.Dot(accelCmdWorld, direction) * direction);
            float accelNorm = Mathf.Max(1f, maxAcceleration);

            translation.x = Mathf.Clamp(lateralAccelCmdLocal.x / accelNorm, -1f, 1f);
            translation.y = Mathf.Clamp(lateralAccelCmdLocal.y / accelNorm, -1f, 1f);

            targetRotation = Quaternion.LookRotation(interceptDirection);
            mainEngine = 2;

            if (Vector3.Dot(body.rb.velocity.normalized, interceptDirection) > 0.95f && relativeVelocity.magnitude > 100)
            {
                translation.x *= 0.1f;
                translation.y *= 0.1f;
                mainEngine = 0;
            }
        }

        public void SteerTowardVelocity(Vector3 desiredVelocity, Vector3 desiredFacing, float throttleBias)
        {
            if (body.rb == null) { return; }

            Vector3 facing = desiredFacing.sqrMagnitude > 1e-6f
                ? desiredFacing.normalized
                : (desiredVelocity.sqrMagnitude > 1e-6f ? desiredVelocity.normalized : transform.forward);

            targetRotation = Quaternion.LookRotation(facing, transform.up);

            Vector3 dv = desiredVelocity - body.rb.velocity;
            Vector3 accelCmdWorld = dv / Mathf.Max(Time.fixedDeltaTime, 0.02f);

            Vector3 forward = transform.forward;
            Vector3 lateralAccelCmdLocal = transform.InverseTransformDirection(accelCmdWorld - Vector3.Dot(accelCmdWorld, forward) * forward);

            float accelNorm = Mathf.Max(1f, maxAcceleration);
            translation = Vector3.zero;
            translation.x = Mathf.Clamp(lateralAccelCmdLocal.x / accelNorm, -1f, 1f);
            translation.y = Mathf.Clamp(lateralAccelCmdLocal.y / accelNorm, -1f, 1f);
            translation.z = Mathf.Clamp(Vector3.Dot(accelCmdWorld, forward) / accelNorm, -1f, 1f);

            if (translation.z > 0.1f)
            {
                mainEngine = (translation.z > 0.6f || throttleBias > 0.75f) ? 2 : 1;
            }
            else
            {
                mainEngine = 0;
            }
        }

        private struct Wrench6
        {
            public Vector3 F;
            public Vector3 Tau;
            public static Wrench6 operator +(Wrench6 a, Wrench6 b) => new Wrench6() { F = a.F + b.F, Tau = a.Tau + b.Tau };
            public static Wrench6 operator -(Wrench6 a, Wrench6 b) => new Wrench6() { F = a.F - b.F, Tau = a.Tau - b.Tau };
            public static Wrench6 operator *(Wrench6 a, float s) => new Wrench6() { F = a.F * s, Tau = a.Tau * s };
        }

        private void AllocateGreedy(Vector3 Fd, Vector3 taud, float leverArmScale, int maxIters = 32, float tol = 1e-3f)
        {
            float DotWeighted(Wrench6 a, Wrench6 b, float sF, float sTau)
            {
                // (sF*F)^2 + (sTau*Tau)^2 cross terms via dot
                return sF * sF * Vector3.Dot(a.F, b.F) + sTau * sTau * Vector3.Dot(a.Tau, b.Tau);
            }

            int N = thrusters.Length;
            float[] u = new float[N];
            bool[] locked = new bool[N];

            // Scale torque by 1/L to compare with force
            float sF = 1f;
            float sTau = (leverArmScale > 1e-6f) ? (1f / leverArmScale) : 1f;

            Wrench6 wd = new Wrench6 { F = Fd, Tau = taud };
            Wrench6 r = wd;

            // Precompute columns a_i
            Wrench6[] a = new Wrench6[N];
            for (int i = 0; i < N; i++)
            {
                Vector3 Fi = thrusters[i].direction * thrusters[i].maxThrust;
                Vector3 taui = Vector3.Cross(thrusters[i].position, Fi);
                a[i] = new Wrench6 { F = Fi, Tau = taui };
            }

            for (int it = 0; it < maxIters; it++)
            {
                if (Mathf.Sqrt(DotWeighted(r, r, sF, sTau)) < tol) { break; }

                int best = -1;
                float bestScore = 0f;

                for (int i = 0; i < N; i++)
                {
                    if (locked[i]) { continue; }

                    float score = DotWeighted(a[i], r, sF, sTau);

                    // If one-directional throttles only, require positive alignment
                    if (score <= 0f) { continue; }

                    if (best == -1 || score > bestScore)
                    {
                        best = i;
                        bestScore = score;
                    }
                }

                if (best == -1) { break; } // no thruster can reduce residual further (with constraints)

                // 1D least squares step: du = (a·r)/(a·a)
                float denom = DotWeighted(a[best], a[best], sF, sTau);
                if (denom < 1e-9f) { locked[best] = true; continue; }

                float duStar = DotWeighted(a[best], r, sF, sTau) / denom;

                float duMin = thrusters[best].uMin - u[best];
                float duMax = thrusters[best].uMax - u[best];
                float du = Mathf.Clamp(duStar, duMin, duMax);

                if (Mathf.Abs(du) < 1e-6f) { locked[best] = true; continue; }

                u[best] += du;
                r = r - (a[best] * du);

                // lock if saturated
                if (Mathf.Abs(u[best] - thrusters[best].uMin) < 1e-6f ||
                    Mathf.Abs(u[best] - thrusters[best].uMax) < 1e-6f)
                {
                    locked[best] = true;
                }
            }

            for (int i = 0; i < thrusters.Length; i++)
            {
                thrusters[i].throttle = u[i];
            }
        }
    }
}
