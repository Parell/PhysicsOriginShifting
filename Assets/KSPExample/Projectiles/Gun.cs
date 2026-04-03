using UnityEngine;
using System.Collections.Generic;

namespace UnityPhysicsFloatingOrigin
{
    public class Barrel
    {
        public float recoilLength = 0.3f;
        public float recoverSpeed = 1f;

        private Transform barrel;
        private Vector3 startLocalPosition;
        private float recoil;

        public Barrel(Transform barrel, float recoilLength, float recoverSpeed)
        {
            this.barrel = barrel;
            this.recoilLength = recoilLength;
            this.recoverSpeed = recoverSpeed;
            startLocalPosition = barrel.localPosition;
        }

        public void FireRecoil()
        {
            recoil = recoilLength;
        }

        public void ResetBarrelOverTime(float deltaTime)
        {
            recoil = Mathf.MoveTowards(recoil, 0f, recoverSpeed * deltaTime);

            if (recoil > 0f)
            {
                barrel.transform.localPosition = startLocalPosition + (Vector3.back * recoil);
            }
        }
    }

    public class Gun : MonoBehaviour
    {
        [Header("Ballistics")]
        public float fireDelay = 0.2f;
        public float muzzleVelocity = 200f;
        public float deviation = 0.1f;

        [Header("Fire Points")]
        public bool isSequentialFiring;
        [SerializeField] private List<Transform> firePoints;

        [Header("Barrel Visuals")]
        public float recoilLength = 0.3f;
        public float recoilRecoverSpeed = 1f;
        [SerializeField] private List<Transform> recoilingBarrels;

        [Header("Firing")]
        public Projectile bulletPrefab;
        [SerializeField] private ParticleSystem muzzleFlashPrefab;
        public bool isFiring;
        public bool ignoreOwnRigidbody = true;

        [Header("Ammo")]
        public bool useAmmo;
        public int maxAmmo = 300;

        private Dictionary<Transform, ParticleSystem> firePointToMuzzleFlash = new Dictionary<Transform, ParticleSystem>();
        private List<Barrel> barrelVisuals = new List<Barrel>();
        private List<Rigidbody> ignoredRigidbodies = new List<Rigidbody>();
        private List<Collider> ignoredColliders = new List<Collider>();

        private float lastShotTime;
        private int firePointIndex;

        public Body body { get; private set; }
        public Vector3 inheritedVelocity { get; set; }

        public bool readyToFire => lastShotTime >= fireDelay && hasAmmo;
        public bool hasAmmo => !useAmmo || (useAmmo && ammoCount > 0);
        public int ammoCount { get; private set; }

        private void Awake()
        {
            body = GetComponentInParent<Body>();
            ReloadAmmo();

            if (firePoints.Count == 0)
            {
                Debug.Log(name + ": needs to assign a fire point to fire");
                RegisterFirePoint(transform);
            }
            else
            {
                foreach (var firePoint in firePoints)
                {
                    RegisterFirePoint(firePoint);
                }
            }

            if (recoilingBarrels.Count > 0)
            {
                foreach (var barrel in recoilingBarrels)
                {
                    RegisterRecoilingBarrel(barrel);
                }
            }
        }

        private void RegisterFirePoint(Transform firePoint)
        {
            if (firePoint == null) { return; }

            if (muzzleFlashPrefab != null)
            {
                var muzzleFlash = Instantiate(muzzleFlashPrefab, firePoint, false);
                firePointToMuzzleFlash.Add(firePoint, muzzleFlash);
            }
        }

        private void RegisterRecoilingBarrel(Transform barrel)
        {
            if (barrel == null) { return; }

            var recoilingBarrel = new Barrel(barrel, recoilLength, recoilRecoverSpeed);
            barrelVisuals.Add(recoilingBarrel);
        }

        private void FixedUpdate()
        {
            inheritedVelocity = body.rb.velocity;

            if (PhysicsManager.timeScaleIndex == 1)
            {
                if (isFiring)
                {
                    AttemptFireShot(inheritedVelocity);
                }

                foreach (var barrel in barrelVisuals)
                {
                    barrel.ResetBarrelOverTime(PhysicsManager.deltaTime);
                }

                lastShotTime += PhysicsManager.deltaTime;
            }
        }

        public void ReloadAmmo()
        {
            ammoCount = maxAmmo;
        }

        public void SetAmmo(int ammo)
        {
            ammoCount = ammo;
        }

        public void AddIgnoredCollider(Collider collider)
        {
            ignoredColliders.Add(collider);
        }

        public void ClearIgnoredColliderList()
        {
            ignoredColliders.Clear();
        }

        public void AddIgnoredRigidbody(Rigidbody rigidbody)
        {
            ignoredRigidbodies.Add(rigidbody);
        }

        public void ClearIgnoredRigidbodies()
        {
            ignoredRigidbodies.Clear();
        }

        public bool FireSingleShot()
        {
            return AttemptFireShot(inheritedVelocity);
        }

        private bool AttemptFireShot(Vector3 inheritedVelocity)
        {
            if (!readyToFire)
            {
                return false;
            }

            if (isSequentialFiring)
            {
                var firePoint = firePoints[firePointIndex % firePoints.Count];
                FireBulletFromFirePoint(firePoint, inheritedVelocity);
                firePointIndex += 1;

                ammoCount -= 1;
            }
            else
            {
                foreach (var firePoint in firePoints)
                {
                    FireBulletFromFirePoint(firePoint, inheritedVelocity);
                    firePointIndex += 1;

                    ammoCount -= 1;
                }
            }

            lastShotTime = 0;
            return true;
        }

        private void FireBulletFromFirePoint(Transform firePoint, Vector3 velocity)
        {
            var bullet = PoolManager.Allocate(null, bulletPrefab.gameObject, firePoint.position, firePoint.rotation).GetComponent<Projectile>();
            bullet.Reset();
            bullet.firedFrom = body;
            var bulletBody = bullet.GetComponent<Body>();
            bulletBody.faction = bullet.firedFrom.faction;
            PhysicsManager.Instance.AddBody(bulletBody);

            bullet.AddIgnoredRigidbody(bulletBody.rb);

            if (ignoreOwnRigidbody)
            {
                bullet.AddIgnoredRigidbody(body.rb);
            }

            if (ignoredRigidbodies.Count > 0)
            {
                bullet.AddIgnoredRigidbodies(ignoredRigidbodies);
            }

            if (ignoredColliders.Count > 0)
            {
                bullet.AddIgnoredColliders(ignoredColliders);
            }

            bullet.Fire(
                position: firePoint.position,
                rotation: firePoint.rotation,
                velocity,
                muzzleVelocity,
                deviation);

            //body.AddForce((float)bulletBody.bodyData.mass * muzzleVelocity * -firePoint.forward);

            if (barrelVisuals.Count > 0)
            {
                barrelVisuals[firePointIndex % barrelVisuals.Count].FireRecoil();

            }

            if (firePointToMuzzleFlash.ContainsKey(firePoint))
            {
                firePointToMuzzleFlash[firePoint].Play();
            }
        }
    }
}
