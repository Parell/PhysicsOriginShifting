using System.Collections.Generic;
using UnityEngine;

namespace UnityPhysicsFloatingOrigin
{
    [DefaultExecutionOrder(10)]
    public class PhysicsManager : MonoBehaviour
    {
        public static PhysicsManager Instance;
        [SerializeField] private Body _mainBody;
        [SerializeField] private float _deltaTime;
        [SerializeField] private double _currentTime;
        [SerializeField] private float _timeScale;
        [SerializeField] private float _physicsRange;
        [SerializeField] private List<BodyData> _bodyData = new List<BodyData>();
        private List<Body> _bodies = new List<Body>();
        private int tightTimeScale;
        private float looseTimeScale;
        private int _timeScaleIndex = 1;
        private float[] _timeScales = { 0, 1, 5, 10, 20 };
        private bool oneInPhysicsRange;

        public static Body mainBody
        {
            get { return Instance._mainBody; }
            set { Instance._mainBody = value; }
        }

        public static List<Body> bodies
        {
            get { return Instance._bodies; }
        }

        public static List<BodyData> bodyData
        {
            get { return Instance._bodyData; }
        }

        public static float deltaTime
        {
            get { return Instance._deltaTime; }
        }

        public static double currentTime
        {
            get { return Instance._currentTime; }
        }

        public static float physicsRange
        {
            get { return Instance._physicsRange; }
        }

        public static float timeScale
        {
            get { return Instance._timeScale; }
            set { Instance._timeScale = value; }
        }

        public static int timeScaleIndex
        {
            get { return Instance._timeScaleIndex; }
            set { Instance._timeScaleIndex = value; }
        }

        public static float[] timeScales
        {
            get { return Instance._timeScales; }
            set { Instance._timeScales = value; }
        }

        private void Awake()
        {
            Instance = this;
            FindAllBodies();
            Time.fixedDeltaTime = _deltaTime;
            Instance.ResetPosition();
            Instance.ResetVelocity();
        }

        private void Update()
        {
            _timeScale = Mathf.Clamp(_timeScale, 0, 100);
            tightTimeScale = _timeScale < 1 ? 1 : (int)_timeScale;
            looseTimeScale = _timeScale < 1 ? _timeScale : 1 + _timeScale - tightTimeScale;
            Time.timeScale = looseTimeScale;
            _deltaTime = Time.fixedDeltaTime * _timeScale;

            TimeScales();
        }

        private void FixedUpdate()
        {
            UpdateStates();
            Simulate(Time.fixedDeltaTime);
        }

        private void UpdateStates()
        {
            oneInPhysicsRange = false;
            for (int j = 0; j < _bodies.Count; j++)
            {
                float distance;
                if (_bodies[j] == _mainBody || _bodies[j].type == BodyType.Bullet) { continue; }

                if (_bodies[j].type == BodyType.Celestial)
                {
                    distance = MathExtentions.FastMagnitude(_mainBody.rb.position - _bodies[j].rb.position) + _bodies[j].GetComponent<Celestial>().radius;
                }
                else
                {
                    distance = MathExtentions.FastMagnitude(_mainBody.rb.position - _bodies[j].rb.position);
                }

                if (distance <= _physicsRange)
                {
                    oneInPhysicsRange = true;

                    if (_bodies[j].rb.isKinematic && !_bodies[j].bodyData.forceKinematic)
                    {
                        _bodies[j].Collisions(true);
                    }
                }
                else
                {
                    if (!_bodies[j].rb.isKinematic && !_bodies[j].bodyData.forceKinematic)
                    {
                        _bodies[j].Collisions(false);
                    }
                }
            }

            if (oneInPhysicsRange)
            {
                if (_timeScale > 1)
                {
                    _timeScaleIndex = 1;
                    _timeScale = _timeScales[1];
                }

                if (_mainBody.rb.isKinematic && !mainBody.bodyData.forceKinematic)
                {
                    _mainBody.Collisions(true);
                }
            }
            else
            {
                if (_timeScale > 1)
                {
                    if (!_mainBody.rb.isKinematic && !mainBody.bodyData.forceKinematic)
                    {
                        _mainBody.Collisions(false);
                    }
                }
                else
                {
                    if (_mainBody.rb.isKinematic && !mainBody.bodyData.forceKinematic)
                    {
                        _mainBody.Collisions(true);
                    }
                }
            }
        }

        private void Simulate(float fixedDeltaTime)
        {
            CollisionResolver mainCollisionResolver = null;
            if (_mainBody != null)
            {
                _mainBody.TryGetComponent(out mainCollisionResolver);
            }

            for (int i = 0; i < tightTimeScale; i++)
            {
                _currentTime += fixedDeltaTime;
                bool mainBodyColliding = mainCollisionResolver != null && mainCollisionResolver.IsMainBodyColliding;

                for (int j = 0; j < _bodies.Count; j++)
                {
                    _bodyData[j].acceleration = Acceleration(j, _bodyData[j].position);
                }

                var offsetAcceleration = mainBodyColliding ? Vector3d.zero : _mainBody.bodyData.acceleration;

                for (int k = 0; k < _bodies.Count; k++)
                {
                    _bodyData[k].acceleration -= offsetAcceleration;

                    if (_bodies[k].rb.isKinematic)
                    {
                        if (_bodyData[k].forceKinematic)
                        {
                            var (position, velocity) = Integrate(k, _bodyData[k].acceleration, fixedDeltaTime);
                            _bodyData[k].velocity += velocity;
                            _bodyData[k].position += position;
                        }
                        else
                        {
                            _bodyData[k].velocity += _bodyData[k].acceleration * fixedDeltaTime;
                            _bodyData[k].position += _bodyData[k].velocity * fixedDeltaTime;
                        }

                        _bodies[k].rb.rotation = IntegrateRotatation(_bodies[k].rb.rotation, _bodyData[k].angularVelocity, fixedDeltaTime);
                        _bodies[k].rb.MovePosition((Vector3)_bodyData[k].position);
                    }
                    else
                    {
                        _bodies[k].rb.AddForce((Vector3)_bodyData[k].acceleration, ForceMode.Acceleration);

                        _bodyData[k].position = (Vector3d)(_bodies[k].rb.position + _bodies[k].rb.velocity * fixedDeltaTime);
                        _bodyData[k].velocity = (Vector3d)_bodies[k].rb.velocity;
                        _bodyData[k].angularVelocity = _bodies[k].rb.angularVelocity;
                    }

                    if (_bodies[k].scaledTransform)
                    {
                        _bodies[k].scaledTransform.SetPositionAndRotation((Vector3)_bodies[k].bodyData.position * Constant.INVERSE_SCALE, _bodies[k].rb.rotation);
                    }
                }
            }

            Physics.Simulate(Time.fixedDeltaTime);
        }

        public Vector3d Acceleration(int index, Vector3d position)
        {
            var acceleration = Vector3d.zero;
            for (int i = 0; i < _bodies.Count; i++)
            {
                if (i == index) { continue; }
                if (_bodyData[i].mass < 1e5) { continue; }
                var r1 = _bodyData[i].position - position;
                if (MathExtentions.FastMagnitude((Vector3)r1) == 0) { continue; }
                acceleration += r1.normalized * Constant.G * _bodyData[i].mass / r1.sqrMagnitude;
            }

            //acceleration += (0.5f * _bodyData[index].atmosphereicDesity * (_bodyData[index].airSpeed * _bodyData[index].airSpeed) * _bodyData[index].liftCoefficent 
            //* _bodyData[index].wingCrossSectionalArea) / _bodyData[index].mass

            return acceleration;
        }

        public (Vector3d position, Vector3d velocity) Integrate(int index, Vector3d acceleration, float deltaTime)
        {
            Vector3d Velocity(Vector3d position, float deltaTime)
            {
                return _bodyData[index].velocity + (Acceleration(index, position) * deltaTime);
            }

            Vector3d NextAcceleration(Vector3d acceleration, Vector3d velocity, float deltaTime)
            {
                return acceleration + (Acceleration(index, _bodyData[index].position + (velocity * deltaTime)) * deltaTime);
            }

            Vector3d k1, k2, k3, k4, position, velocity;
            {
                k1 = NextAcceleration(acceleration, _bodyData[index].velocity, 0);
                k2 = NextAcceleration(acceleration, _bodyData[index].velocity + (deltaTime * 0.5f * k1), deltaTime * 0.5f);
                k3 = NextAcceleration(acceleration, _bodyData[index].velocity + (deltaTime * 0.5f * k2), deltaTime * 0.5f);
                k4 = NextAcceleration(acceleration, _bodyData[index].velocity + (deltaTime * -k3), deltaTime);
                velocity = deltaTime * 0.16666666666f * (k1 + (2 * k2) + (2 * k3) + k4);

                k1 = Velocity(_bodyData[index].position, 0);
                k2 = Velocity(_bodyData[index].position + (deltaTime * 0.5f * k1), deltaTime * 0.5f);
                k3 = Velocity(_bodyData[index].position + (deltaTime * 0.5f * k2), deltaTime * 0.5f);
                k4 = Velocity(_bodyData[index].position + (deltaTime * -k3), deltaTime);
                position = deltaTime * 0.16666666666f * (k1 + (2 * k2) + (2 * k3) + k4);

                return (position, velocity);
            }
        }

        private Quaternion IntegrateRotatation(Quaternion rotation, Vector3 angularVelocity, float deltaTime)
        {
            Vector3 deltaRotation = angularVelocity * deltaTime * 0.5f;
            float magnitude = MathExtentions.FastMagnitude(deltaRotation);

            if (magnitude < 1e-6f)
            {
                return rotation;
            }

            Vector3 axis = deltaRotation / magnitude;
            float sinHalfAngle = Mathf.Sin(magnitude);
            float cosHalfAngle = Mathf.Cos(magnitude);

            Quaternion deltaQuaternion = new Quaternion(
                axis.x * sinHalfAngle,
                axis.y * sinHalfAngle,
                axis.z * sinHalfAngle,
                cosHalfAngle
            );

            rotation *= deltaQuaternion;
            return rotation;
        }

        public void TimeScales()
        {
            if (Input.GetKeyDown(KeyCode.X))
            {
                if (_timeScaleIndex != 0)
                {
                    _timeScaleIndex--;
                }
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                if (_timeScaleIndex != _timeScales.Length - 1)
                {
                    if (_timeScale == 1 && oneInPhysicsRange)
                    {
                        return;
                    }
                    _timeScaleIndex++;
                }
            }

            var maxDelta = 1 + Mathf.Abs(_timeScales[_timeScaleIndex] - _timeScale) * 2 * Time.unscaledDeltaTime;
            _timeScale = Mathf.MoveTowards(_timeScale, _timeScales[_timeScaleIndex], maxDelta);
        }

        public void ResetVelocity()
        {
            var offset = _mainBody.bodyData.velocity;

            for (int i = 0; i < _bodies.Count; i++)
            {
                _bodyData[i].velocity -= offset;
                _bodies[i].rb.velocity -= (Vector3)offset;
            }
        }

        public void ResetPosition()
        {
            var offset = _mainBody.bodyData.position;

            for (int i = 0; i < _bodies.Count; i++)
            {
                _bodyData[i].position -= offset;
                _bodies[i].rb.position -= (Vector3)offset;
            }
        }

        public void FindAllBodies()
        {
            _bodies.Clear();
            _bodyData.Clear();
            _bodies = FindObjectsOfType<Body>(false).ToList();

            for (int i = 0; i < _bodies.Count; i++)
            {
                _bodies[i].bodyData.index = i;
                _bodyData.Add(_bodies[i].bodyData);
                _bodies[i].rb = _bodies[i].GetComponent<Rigidbody>();
                _bodies[i].Collisions(false);
                _bodies[i].rb.solverVelocityIterations = Physics.defaultSolverVelocityIterations;
            }

            _mainBody.rb.solverVelocityIterations = 128;
        }

        public void RemoveBody(Body body)
        {
            _bodyData.Remove(body.bodyData);
            _bodies.Remove(body);
        }

        public void AddBody(Body body)
        {
            body.rb = body.GetComponent<Rigidbody>();
            body.Collisions(false);
            _bodies.Add(body);
            _bodyData.Add(body.bodyData);
            body.bodyData.index = _bodies.IndexOf(body);
        }

        public void Save()
        {
            timeScale = 0;

            var copyOfBodyData = new List<BodyData>();

            for (int i = 0; i < bodyData.Count; i++)
            {
                copyOfBodyData.Add(bodyData[i]);
            }

            //private string path => $"{Application.dataPath}/Data/Config.json";

            // Two options. Save and move positions of main objects and spawners, or two make a registry of objects in the world and spawn them in. 

            timeScale = timeScales[timeScaleIndex];
        }

        [ContextMenu("Center")]
        private void ResetToCenter()
        {
            var tempBodies = FindObjectsOfType<Body>(false).ToList();
            var offset = _mainBody.transform.position;
            for (int i = 0; i < tempBodies.Count; i++)
            {
                tempBodies[i].transform.position -= offset;
            }
        }

        public static float ClosestTimeToApproach(Vector3 relitivePosition, Vector3 relitiveVelocity, Vector3 relativeAcceleration, float maxTime)
        {
            float A = Vector3.Dot(relativeAcceleration, relativeAcceleration) / 2f;
            float B = Vector3.Dot(relitiveVelocity, relativeAcceleration) * 3f / 2f;
            float C = Vector3.Dot(relitiveVelocity, relitiveVelocity) + Vector3.Dot(relitivePosition, relativeAcceleration);
            float D = Vector3.Dot(relitivePosition, relitiveVelocity);
            if (A == 0) // Not actually a cubic. Relative acceleration is zero, so return the much simpler linear timeToCPA.
            {
                return Mathf.Clamp(-Vector3.Dot(relitivePosition, relitiveVelocity) / relitiveVelocity.sqrMagnitude, 0f, maxTime);
            }
            float D0 = (B * B) - (3f * A * C);
            float D1 = (2 * B * B * B) - (9f * A * B * C) + (27f * A * A * D);
            float E = (D1 * D1) - (4f * D0 * D0 * D0); // = -27*A^2*discriminant
                                                       // float discriminant = 18f * A * B * C * D - 4f * Mathf.Pow(B, 3f) * D + Mathf.Pow(B, 2f) * Mathf.Pow(C, 2f) - 4f * A * Mathf.Pow(C, 3f) - 27f * Mathf.Pow(A, 2f) * Mathf.Pow(D, 2f);
            if (E > 0)
            { // Single solution (E is positive)
                float F = (D1 + (Mathf.Sign(D1) * Mathf.Sqrt(E))) / 2f;
                float G = Mathf.Sign(F) * Mathf.Pow(Mathf.Abs(F), 1f / 3f);
                float time = -1f / 3f / A * (B + G + (D0 / G));
                return Mathf.Clamp(time, 0f, maxTime);
            }
            else if (E < 0)
            { // Triple solution (E is negative)
                float F_real = D1 / 2f;
                float F_imag = Mathf.Sign(D1) * Mathf.Sqrt(-E) / 2f;
                float F_abs = Mathf.Sqrt((F_real * F_real) + (F_imag * F_imag));
                float F_ang = Mathf.Atan2(F_imag, F_real);
                float G_abs = Mathf.Pow(F_abs, 1f / 3f);
                float G_ang = F_ang / 3f;
                float time = -1f;
                for (int i = 0; i < 3; ++i)
                {
                    float G = G_abs * Mathf.Cos(G_ang + (2f * i * Mathf.PI / 3f));
                    float t = -1f / 3f / A * (B + G + (D0 * G / G_abs / G_abs));
                    if (t > 0f && Mathf.Sign(Vector3.Dot(relitiveVelocity, relitiveVelocity) + Vector3.Dot(relitivePosition, relativeAcceleration) + (3f * t * Vector3.Dot(relitiveVelocity, relativeAcceleration)) + (3f / 2f * t * t * Vector3.Dot(relativeAcceleration, relativeAcceleration))) > 0)
                    { // It's a minimum and in the future.
                        if (time < 0f || t < time) // Update the closest time.
                            time = t;
                    }
                }
                return Mathf.Clamp(time, 0f, maxTime);
            }
            else
            { // Repeated root
                if (Mathf.Abs((B * B) - (2f * A * C)) < 1e-7)
                { // A triple-root.
                    return Mathf.Clamp(-B / 3f / A, 0f, maxTime);
                }
                else
                { // Double root and simple root.
                    return Mathf.Clamp(Mathf.Max(((9f * A * D) - (B * C)) / 2 / ((B * B) - (3f * A * C)), ((4f * A * B * C) - (9f * A * A * D) - (B * B * B)) / A / ((B * B) - (3f * A * C))), 0f, maxTime);
                }
            }
        }

        public static bool NumericalTimeToCollision(Vector3 relPos, Vector3 relVel, Vector3 relAccel, float projectileSpeed, float maxTime, out float interceptTime)
        {
            interceptTime = 0f;
            if (projectileSpeed <= 0f || maxTime < 0.01f) { return false; }

            bool IsFiniteFloat(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
            bool IsFiniteVector(Vector3 value) { return IsFiniteFloat(value.x) && IsFiniteFloat(value.y) && IsFiniteFloat(value.z); }

            if (!IsFiniteVector(relPos) || !IsFiniteVector(relVel) || !IsFiniteVector(relAccel) || !IsFiniteFloat(projectileSpeed) || !IsFiniteFloat(maxTime))
            {
                return false;
            }

            float timeCap = Mathf.Max(0.01f, maxTime);
            float speedSqr = projectileSpeed * projectileSpeed;
            const float eps = 1e-6f;

            if (relAccel.sqrMagnitude < eps)
            {
                float a = Vector3.Dot(relVel, relVel) - speedSqr;
                float b = 2f * Vector3.Dot(relPos, relVel);
                float c = Vector3.Dot(relPos, relPos);

                if (Mathf.Abs(a) < eps)
                {
                    if (Mathf.Abs(b) < eps) { return false; }
                    float t = -c / b;
                    if (t > 0f && t <= timeCap)
                    {
                        interceptTime = t;
                        return true;
                    }
                    return false;
                }

                float discriminant = (b * b) - (4f * a * c);
                if (discriminant < 0f) { return false; }

                float sqrtDisc = Mathf.Sqrt(discriminant);
                float invDenominator = 1f / (2f * a);
                float t1 = (-b - sqrtDisc) * invDenominator;
                float t2 = (-b + sqrtDisc) * invDenominator;
                float best = float.PositiveInfinity;

                if (t1 > 0f && t1 <= timeCap) { best = t1; }
                if (t2 > 0f && t2 <= timeCap) { best = Mathf.Min(best, t2); }
                if (!float.IsInfinity(best))
                {
                    interceptTime = best;
                    return true;
                }

                return false;
            }

            float Evaluate(float t)
            {
                Vector3 r = relPos + (relVel * t) + (0.5f * relAccel * t * t);
                return Vector3.Dot(r, r) - (speedSqr * t * t);
            }

            const int sampleSegments = 16;
            const int refineIterations = 12;
            float step = timeCap / sampleSegments;

            float prevT = 0f;
            float prevF = Evaluate(prevT);
            if (!IsFiniteFloat(prevF)) { return false; }

            bool foundBracket = false;
            float lowT = 0f;
            float highT = 0f;
            float lowF = 0f;
            float highF = 0f;

            for (int i = 1; i <= sampleSegments; i++)
            {
                float t = step * i;
                float f = Evaluate(t);
                if (!IsFiniteFloat(f)) { return false; }

                if (Mathf.Abs(f) < eps && t > 0f)
                {
                    interceptTime = t;
                    return true;
                }

                if ((prevF < 0f && f > 0f) || (prevF > 0f && f < 0f))
                {
                    foundBracket = true;
                    lowT = prevT;
                    highT = t;
                    lowF = prevF;
                    highF = f;
                    break;
                }

                prevT = t;
                prevF = f;
            }

            if (!foundBracket) { return false; }

            for (int i = 0; i < refineIterations; i++)
            {
                float midT = 0.5f * (lowT + highT);
                float midF = Evaluate(midT);
                if (!IsFiniteFloat(midF)) { return false; }

                if (Mathf.Abs(midF) < eps)
                {
                    interceptTime = midT;
                    return interceptTime > 0f && interceptTime <= timeCap;
                }

                if ((lowF < 0f && midF > 0f) || (lowF > 0f && midF < 0f))
                {
                    highT = midT;
                    highF = midF;
                }
                else
                {
                    lowT = midT;
                    lowF = midF;
                }
            }

            interceptTime = 0.5f * (lowT + highT);
            return interceptTime > 0f && interceptTime <= timeCap;
        }

        public static Vector3 PredictPosition(Vector3 position, Vector3 velocity, Vector3 acceleration, float time)
        {
            return position + (velocity * time) + (0.5f * acceleration * Mathf.Pow(time, 2f));
        }
    }
}
