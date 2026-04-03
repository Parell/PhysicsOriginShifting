using UnityEngine;

namespace PhysicsFloatingOrigin
{
    // Orbit-style camera that stays local to the active target and avoids clipping through geometry.
    [DefaultExecutionOrder(1)]
    public class LocalCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset;
        [SerializeField] private Vector3 sensitivity = new Vector3(1, 1, 60);
        [SerializeField] private float scrollSpeed;
        [SerializeField] private Vector2 minMaxScrollSpeed;
        [SerializeField] private float wantedDistance;
        public float currentDistance;
        [SerializeField] private float minPadding;
        [SerializeField] private Vector2 minMaxDistance;
        [SerializeField] private LayerMask pullLayerMask;
        private Transform lastTarget;

        private void Update()
        {
            if (target != null)
            {
                UpdateCamera();
            }

            if (lastTarget != target)
            {
                // Reset orbit distance when the focus target changes.
                float diameter = 0;
                minMaxDistance.x = diameter;
                //wantedDistance = diameter + minPadding;
                currentDistance = wantedDistance;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            lastTarget = target;
        }

        private void UpdateCamera()
        {
            // Mouse X/Y rotates the orbit, scroll wheel changes the follow distance.
            Vector3 targetLookInput = new Vector3(
            Input.GetAxisRaw("Mouse Y") * -sensitivity.y,
            Input.GetAxisRaw("Mouse X") * sensitivity.x,
            0); //Input.GetAxisRaw("Rotate Z") * sensitivity.z * Time.unscaledDeltaTime);
            float scrollInput = Input.GetKey(KeyCode.LeftAlt) ? 0 : Input.GetAxis("Mouse ScrollWheel");

            transform.Rotate(targetLookInput);
            scrollSpeed = Mathf.Clamp(currentDistance - minMaxDistance.x + minMaxScrollSpeed.x, minMaxScrollSpeed.x, minMaxScrollSpeed.y);
            wantedDistance = Mathf.Clamp(wantedDistance - (scrollInput * scrollSpeed), minMaxDistance.x, minMaxDistance.y);
            currentDistance = Mathf.MoveTowards(currentDistance, wantedDistance, Time.unscaledDeltaTime * scrollSpeed * 2);
            Vector3 position = target.position + transform.TransformVector(offset) + (transform.rotation * new Vector3(0, 0, -currentDistance));

            RaycastHit hit;
            if (Physics.Linecast(target.position, position, out hit, pullLayerMask))
            {
                // Pull the camera forward to the collision point instead of allowing penetration.
                position = new Vector3(hit.point.x + hit.normal.x * 0.5f, hit.point.y + hit.normal.y * 0.5f, hit.point.z + hit.normal.z * 0.5f);
            }

            transform.position = position;
        }
    }
}
