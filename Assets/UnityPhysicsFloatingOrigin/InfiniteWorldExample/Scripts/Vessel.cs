using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PhysicsFloatingOrigin
{
    // Handles player-facing vessel input and delegates actual motion to Mover.
    public class Vessel : MonoBehaviour
    {
        public enum WeaponKind { Kinetic, Missile }

        public bool controlable = true;
        public bool control;

        [FormerlySerializedAs("weapons")]
        [SerializeField] private WeaponData[] weaponsData;
        [HideInInspector] public Body body;

        private Mover mover;
        private Transform cameraTransform;
        private LocalCamera localCamera;

        private float tapSpeed = 0.5f;
        public bool retrograde;
        private float lastTapTime;
        private bool isHolding;
        private float holdTimer;
        private float holdTime = 0.5f;

        private void Awake()
        {
            cameraTransform = Camera.main.transform;
            if (!body) { body = GetComponent<Body>(); }
            if (!mover) { mover = GetComponent<Mover>(); }
        }

        private void Update()
        {
            if (!controlable) { return; }

            if (control) { MovementInputs(); return; }
        }

        private void MovementInputs()
        {
            if (!mover) { return; }

            // Right mouse locks attitude to the current camera direction.
            if (Input.GetKey(KeyCode.Mouse1))
            {
                Transform steeringTransform = localCamera ? localCamera.transform : cameraTransform;
                if (steeringTransform)
                {
                    Vector3 forward = steeringTransform.forward;
                    if (forward.sqrMagnitude >= 1e-8f)
                    {
                        mover.targetRotation = Quaternion.LookRotation(forward, steeringTransform.up);
                        if (PhysicsManager.timeScaleIndex != 1) { PhysicsManager.timeScaleIndex = 1; PhysicsManager.timeScale = PhysicsManager.timeScales[1]; }
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                // Hook for missile launch or other action bindings.
            }

            // Double-tap W/S toggles retrograde mode without extra UI.
            bool w = Input.GetKey(KeyCode.W);
            bool s = Input.GetKey(KeyCode.S);
            if (w) { mover.translation.z = 1; if (Input.GetKeyDown(KeyCode.W)) { if ((Time.time - lastTapTime) < tapSpeed) { retrograde = false; } lastTapTime = Time.time; } }
            if (s) { mover.translation.z = -1; if (Input.GetKeyDown(KeyCode.S)) { if ((Time.time - lastTapTime) < tapSpeed) { retrograde = true; } lastTapTime = Time.time; } }
            if ((!w && !s) || (w && s)) { mover.translation.z = 0; }

            bool d = Input.GetKey(KeyCode.D);
            bool a = Input.GetKey(KeyCode.A);
            mover.translation.x = d == a ? 0 : (d ? 1 : -1);

            bool r = Input.GetKey(KeyCode.R);
            bool f = Input.GetKey(KeyCode.F);
            mover.translation.y = r == f ? 0 : (r ? 1 : -1);
        }

        [System.Serializable]
        private class WeaponData
        {
            public Body target = null;
            public Gun gun = null;
            public Turret turret = null;
            public bool disabled;
            public WeaponKind kind = WeaponKind.Kinetic;
            public float range = 1000f;
        }
    }
}
