using UnityEngine;
using System.Collections.Generic;

namespace UnityPhysicsFloatingOrigin
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private Body explosionFXPrefab;
        [SerializeField] private Body impactFXPrefab;
        [Space]
        public Body firedFrom;
        [SerializeField] private Body body;
        [SerializeField] private LayerMask rayHitLayers = -1;
        [SerializeField] private float timeToLive = 5f;
        [SerializeField] private Transform head;
        [SerializeField] private float lengthToHead = 1f;
        [Space]
        [SerializeField] private int damage = 1;
        [SerializeField] private int impactPower;
        [SerializeField] private bool bounce;
        [SerializeField] private bool isThick;
        [SerializeField] private float diameter = 1f;
#if UNITY_EDITOR
        [Space]
        [SerializeField] private bool showDebugVisuals;
#endif

        private HashSet<Rigidbody> ignoredRigidbodies = new HashSet<Rigidbody>();
        private HashSet<Collider> ignoredColliders = new HashSet<Collider>();
        private static RaycastHit[] raycastHits = new RaycastHit[32];
        public float MaxFlightTime => timeToLive;
        public float secondsSinceFired { get; private set; }
        public bool isFired { get; private set; }

        private float initalSecondsSinceFired;

        public void Reset()
        {
            secondsSinceFired = initalSecondsSinceFired;
            ignoredRigidbodies.Clear();
            ignoredColliders.Clear();
        }

        private void Awake()
        {
            initalSecondsSinceFired = secondsSinceFired;

            if (body == null)
            {
                Debug.LogError(name + ": Bullet requires an assigned Body");
            }
        }

        private void FixedUpdate()
        {
            UpdateProjectile(Time.fixedDeltaTime);
        }

        public void Fire(Vector3 position, Quaternion rotation, Vector3 inheritedVelocity, float muzzleVelocity, float deviation)
        {
            body.bodyData.position = (Vector3d)position;
            body.rb.position = position;

            Vector3 deviationAngle = Vector3.zero;
            deviationAngle.x = Random.Range(-deviation, deviation);
            deviationAngle.y = Random.Range(-deviation, deviation);
            Quaternion deviationRotation = Quaternion.Euler(deviationAngle);
            Quaternion launchRotation = rotation * deviationRotation;
            Vector3 launchForward = launchRotation * Vector3.forward;

            body.rb.rotation = launchRotation;
            transform.rotation = launchRotation;

            Vector3 launchVelocity = (launchForward * muzzleVelocity) + inheritedVelocity;
            body.bodyData.velocity = (Vector3d)launchVelocity;
            body.rb.velocity = launchVelocity;
            body.bodyData.angularVelocity = Vector3.zero;
            body.rb.angularVelocity = Vector3.zero;

            isFired = true;
        }

        public void AddIgnoredRigidbody(Rigidbody rigidbody)
        {
            if (rigidbody != null)
            {
                ignoredRigidbodies.Add(rigidbody);
            }
        }

        public void AddIgnoredRigidbodies(IEnumerable<Rigidbody> rigidbodies)
        {
            foreach (var rigidbody in rigidbodies)
            {
                ignoredRigidbodies.Add(rigidbody);
            }
        }

        public void AddIgnoredCollider(Collider collider)
        {
            if (collider != null)
            {
                ignoredColliders.Add(collider);
            }
        }

        public void AddIgnoredColliders(IEnumerable<Collider> colliders)
        {
            foreach (var collider in colliders)
            {
                ignoredColliders.Add(collider);
            }
        }

        public void DestroyFromCollision(Vector3 impactedPosition, Quaternion impactRotation)
        {
            if (impactFXPrefab != null)
            {
                Body particleBody;
                SpawnEffect(impactFXPrefab.gameObject, out particleBody);

                particleBody.GetComponent<ParticleSystem>().Play();
            }

            if (explosionFXPrefab != null)
            {
                SpawnEffect(explosionFXPrefab.gameObject, out _);
            }

            void SpawnEffect(GameObject effect, out Body particleBody)
            {
                particleBody = PoolManager.Allocate(null, effect, impactedPosition, impactRotation).GetComponent<Body>();
                particleBody.bodyData.position = (Vector3d)impactedPosition;
                particleBody.bodyData.velocity = body.bodyData.velocity;
                PhysicsManager.Instance.AddBody(particleBody);
            }

            DestroySilently();
        }

        public void DestroyFromCollision()
        {
            DestroyFromCollision(body.rb.position, Quaternion.LookRotation(body.rb.velocity.normalized));
        }

        public void DestroySilently()
        {
            PhysicsManager.Instance.RemoveBody(body);
            PoolManager.Deallocate(gameObject);
        }

        private void UpdateProjectile(float deltaTime)
        {
            secondsSinceFired += deltaTime;

            if (secondsSinceFired > timeToLive)
            {
                DestroySilently();
            }
            else
            {
                var (hitSomething, hitInfo) = RunHitDetection(head.position, body.rb.velocity, deltaTime);

                if (hitSomething)
                {
                    if (bounce && Vector3.Angle(hitInfo.normal, -body.rb.velocity.normalized) > 45)
                    {
                        var bounceVelocity = Vector3.Reflect(body.rb.velocity, hitInfo.normal);
                        body.bodyData.velocity = (Vector3d)bounceVelocity * 0.5f;
                        transform.rotation = Quaternion.LookRotation(bounceVelocity);

                        HandleImpactDamage(hitInfo);
                    }
                    else
                    {
                        HandleImpactDamage(hitInfo);

                        if (hitInfo.point == Vector3.zero)
                        {
                            Debug.Log("Hmmmm");
                        }

                        DestroyFromCollision(hitInfo.point, Quaternion.LookRotation(body.rb.velocity.normalized));
                    }
                }
            }
        }

        private void HandleImpactDamage(RaycastHit hitInfo)
        {
            // Add force at position on the body script
            // if (impactPower > 0)
            // {
            //     hitInfo.rigidbody.AddForceAtPosition(MathExtentions.FastNorimalize(body.rb.velocity) * impactPower * 1000, hitInfo.point, ForceMode.Impulse);
            // }

            //IDamageable hit;
            // bool test = hitInfo.collider.TryGetComponent(out hit);
            // if (!test)
            // {
            //     test = hitInfo.transform.TryGetComponent(out hit);
            // }

            // if (test)
            // {
            //     hit.Damage(damage);
            // }
        }

        private (bool hitSomething, RaycastHit hitInfo) RunHitDetection(Vector3 position, Vector3 velocity, float deltaTime)
        {
            return isThick
                ? RunThickHitDetection(position, velocity, deltaTime)
                : RunRayHitDetection(position, velocity, deltaTime);
        }

        private (bool hitSomething, RaycastHit hitInfo) RunThickHitDetection(Vector3 position, Vector3 velocity, float deltaTime)
        {
            // For thick bullets, first do collision detection only on things considered targets.
            int hitCount = Physics.SphereCastNonAlloc(
                origin: position,
                direction: velocity.normalized,
                radius: diameter * .5f,
                maxDistance: lengthToHead + MathExtentions.FastMagnitude(velocity) * deltaTime,
                layerMask: rayHitLayers,
                results: raycastHits);

            return GetClosestValidHit(raycastHits, hitCount);
        }

        private (bool hitSomething, RaycastHit hitInfo) RunRayHitDetection(Vector3 position, Vector3 velocity, float deltaTime)
        {
            int hitCount = Physics.RaycastNonAlloc(
                origin: position,
                direction: velocity.normalized,
                maxDistance: lengthToHead + MathExtentions.FastMagnitude(velocity) * deltaTime,
                layerMask: rayHitLayers,
                results: raycastHits);

            return GetClosestValidHit(raycastHits, hitCount);
        }

        private (bool hitSomething, RaycastHit closestHit) GetClosestValidHit(RaycastHit[] listOfHits, int hitCount)
        {
            if (hitCount == 0)
            {
                return (false, new RaycastHit());
            }

            RaycastHit closestHit = new RaycastHit();
            float closestDistance = float.MaxValue;
            bool hitSomething = false;

            // if (IsHitAllowed(listOfHits[0]))
            // {
            //     closestHit = listOfHits[0];
            //     closestDistance = listOfHits[0].distance;
            //     hitSomething = true;
            // }

            for (int i = 0; i < hitCount; ++i)
            {
                if (IsHitAllowed(listOfHits[i]))
                {
                    if (listOfHits[i].distance < closestDistance)
                    {
                        closestDistance = listOfHits[i].distance;
                        closestHit = listOfHits[i];
                        hitSomething = true;
                    }
                }
            }

            return (hitSomething, closestHit);
        }

        private bool IsHitAllowed(RaycastHit hit)
        {
            bool isHitAllowed = true;

            var hitRigidbody = hit.rigidbody;
            if (hitRigidbody != null && ignoredRigidbodies.Contains(hitRigidbody))
            {
                isHitAllowed = false;
            }
            else if (ignoredColliders.Contains(hit.collider))
            {
                isHitAllowed = false;
            }

            return isHitAllowed;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showDebugVisuals) { return; }

            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.DrawLine(Vector3.right, Vector3.left);
            Gizmos.DrawLine(Vector3.up, Vector3.down);
            Gizmos.DrawLine(Vector3.zero, transform.forward * lengthToHead);

            var bulletHead = new Vector3(0f, 0f, lengthToHead);
            Gizmos.DrawLine(bulletHead + Vector3.right, bulletHead + Vector3.right);
            Gizmos.DrawLine(bulletHead + Vector3.up, bulletHead + Vector3.down);

            Gizmos.matrix = Matrix4x4.identity;

            var velocity = Vector3.zero;
            if (Application.isPlaying)
            {
                velocity = body.rb.velocity * Time.fixedDeltaTime;
            }

            if (isThick)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position - velocity, transform.position);
                Gizmos.DrawWireSphere(head.position, diameter * .5f);

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position + velocity, transform.position);
                Gizmos.DrawWireSphere(head.position + velocity, diameter * .5f);
            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position - velocity, transform.position);

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position + velocity, transform.position);
            }
        }
#endif
    }
}
