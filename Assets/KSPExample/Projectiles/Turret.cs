using UnityEngine;

namespace UnityPhysicsFloatingOrigin
{
    public class Turret : MonoBehaviour
    {
        [Header("Rotations")]
        [SerializeField] private Transform aimBase;
        [SerializeField] private Transform barrels;
        [Header("Elevation")]
        public float elevationSpeed = 30f;
        public float maxElevation = 60f;
        public float maxDepression = 5f;
        [Header("Traverse")]
        public float traverseSpeed = 60f;
        [SerializeField] private bool hasLimitedTraverse;
        [Range(0, 179)] public float leftLimit = 120f;
        [Range(0, 179)] public float rightLimit = 120f;
        [Header("Behavior")]
        public bool isIdle;
        public Vector3 aimPosition;
        [SerializeField] private float aimedThreshold = 5f;
        private float limitedTraverseAngle;
        [Header("Debug")]
        public bool drawDebugRay = true;
        public bool drawDebugArcs;

        private float angleToTarget;
        private float elevation;
        private bool hasBarrels;
        public bool isAimed;
        private bool isBaseAtRest;
        private bool isBarrelAtRest;

        private void Awake()
        {
            hasBarrels = barrels != null;
            if (aimBase == null)
            {
                Debug.LogError(name + ": TurretAim requires an assigned TurretBase");
            }
        }

        private void FixedUpdate()
        {
            if (isIdle)
            {
                if (!isBarrelAtRest && !isBaseAtRest)
                {
                    RotateTurretToIdle();
                }
                isAimed = false;
            }
            else
            {
                RotateBaseToFaceTarget(aimPosition);

                if (hasBarrels)
                {
                    RotateBarrelsToFaceTarget(aimPosition);
                }

                angleToTarget = GetTurretAngleToTarget(aimPosition);

                isAimed = angleToTarget < aimedThreshold;

                isBarrelAtRest = false;
                isBaseAtRest = false;
            }
        }

        private float GetTurretAngleToTarget(Vector3 targetPosition)
        {
            float angle = 999;

            if (hasBarrels)
            {
                angle = Vector3.Angle(targetPosition - barrels.position, barrels.forward);
            }
            else
            {
                Vector3 flattenedTarget = Vector3.ProjectOnPlane(
                    targetPosition - aimBase.position,
                    aimBase.up);

                angle = Vector3.Angle(
                    flattenedTarget - aimBase.position,
                    aimBase.forward);
            }

            return angle;
        }

        private void RotateTurretToIdle()
        {
            // Rotate the base to its default position.
            if (hasLimitedTraverse)
            {
                limitedTraverseAngle = Mathf.MoveTowards(
                    limitedTraverseAngle, 0,
                    traverseSpeed * Time.deltaTime);

                if (Mathf.Abs(limitedTraverseAngle) > Mathf.Epsilon)
                {
                    aimBase.localEulerAngles = Vector3.up * limitedTraverseAngle;
                }
                else
                {
                    isBaseAtRest = true;
                }
            }
            else
            {
                aimBase.rotation = Quaternion.RotateTowards(
                    aimBase.rotation,
                    transform.rotation,
                    traverseSpeed * Time.deltaTime);

                isBaseAtRest = Mathf.Abs(aimBase.localEulerAngles.y) < Mathf.Epsilon;
            }

            if (hasBarrels)
            {
                elevation = Mathf.MoveTowards(elevation, 0, elevationSpeed * Time.deltaTime);
                if (Mathf.Abs(elevation) > Mathf.Epsilon)
                {
                    barrels.localEulerAngles = Vector3.right * -elevation;
                }
                else
                {
                    isBarrelAtRest = true;
                }
            }
            else
            {
                isBarrelAtRest = true;
            }
        }

        private void RotateBarrelsToFaceTarget(Vector3 targetPosition)
        {
            Vector3 localTargetPos = aimBase.InverseTransformDirection(targetPosition - barrels.position);
            Vector3 flattenedVecForBarrels = Vector3.ProjectOnPlane(localTargetPos, Vector3.up);

            float targetElevation = Vector3.Angle(flattenedVecForBarrels, localTargetPos);
            targetElevation *= Mathf.Sign(localTargetPos.y);

            targetElevation = Mathf.Clamp(targetElevation, -maxDepression, maxElevation);
            elevation = Mathf.MoveTowards(elevation, targetElevation, elevationSpeed * Time.deltaTime);

            if (Mathf.Abs(elevation) > Mathf.Epsilon)
            {
                barrels.localEulerAngles = Vector3.right * -elevation;
            }

#if UNITY_EDITOR
            if (drawDebugRay)
            {
                Debug.DrawRay(barrels.position, barrels.forward * MathExtentions.FastMagnitude(localTargetPos), Color.red);
            }
#endif
        }

        private void RotateBaseToFaceTarget(Vector3 targetPosition)
        {
            Vector3 turretUp = transform.up;

            Vector3 vecToTarget = targetPosition - aimBase.position;
            Vector3 flattenedVecForBase = Vector3.ProjectOnPlane(vecToTarget, turretUp);

            if (hasLimitedTraverse)
            {
                Vector3 turretForward = transform.forward;
                float targetTraverse = Vector3.SignedAngle(turretForward, flattenedVecForBase, turretUp);

                targetTraverse = Mathf.Clamp(targetTraverse, -leftLimit, rightLimit);
                limitedTraverseAngle = Mathf.MoveTowards(
                    limitedTraverseAngle,
                    targetTraverse,
                    traverseSpeed * Time.deltaTime);

                if (Mathf.Abs(limitedTraverseAngle) > Mathf.Epsilon)
                {
                    aimBase.localEulerAngles = Vector3.up * limitedTraverseAngle;
                }
            }
            else
            {
                aimBase.rotation = Quaternion.RotateTowards(
                    Quaternion.LookRotation(aimBase.forward, turretUp),
                    Quaternion.LookRotation(flattenedVecForBase, turretUp),
                    traverseSpeed * Time.deltaTime);
            }

#if UNITY_EDITOR
            if (drawDebugRay && !hasBarrels)
            {
                Debug.DrawRay(aimBase.position,
                    aimBase.forward * MathExtentions.FastMagnitude(flattenedVecForBase),
                    Color.red);
            }
#endif
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawDebugArcs)
            {
                return;
            }

            if (aimBase != null)
            {
                const float kArcSize = 10f;
                Color colorTraverse = new Color(1f, 0.5f, 0.5f, 0.1f);
                Color colorElevation = new Color(0.5f, 1f, 0.5f, 0.1f);
                Color colorDepression = new Color(0.5f, 0.5f, 1f, 0.1f);

                Transform arcRoot = barrels != null ? barrels : aimBase;

                // Red traverse arc
                UnityEditor.Handles.color = colorTraverse;
                if (hasLimitedTraverse)
                {
                    UnityEditor.Handles.DrawSolidArc(
                        arcRoot.position, aimBase.up,
                        transform.forward, rightLimit,
                        kArcSize);
                    UnityEditor.Handles.DrawSolidArc(
                        arcRoot.position, aimBase.up,
                        transform.forward, -leftLimit,
                        kArcSize);
                }
                else
                {
                    UnityEditor.Handles.DrawSolidArc(
                        arcRoot.position, aimBase.up,
                        transform.forward, 360f,
                        kArcSize);
                }

                if (barrels != null)
                {
                    // Green elevation arc
                    UnityEditor.Handles.color = colorElevation;
                    UnityEditor.Handles.DrawSolidArc(
                        barrels.position, barrels.right,
                        aimBase.forward, -maxElevation,
                        kArcSize);

                    // Blue depression arc
                    UnityEditor.Handles.color = colorDepression;
                    UnityEditor.Handles.DrawSolidArc(
                        barrels.position, barrels.right,
                        aimBase.forward, maxDepression,
                        kArcSize);
                }
            }
        }
#endif
    }
}