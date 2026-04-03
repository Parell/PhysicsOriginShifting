using System.Collections;
using UnityEngine;

namespace UnityPhysicsFloatingOrigin
{
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
                //MathExtentions.FastMagnitude(Body.FindBounds(target).extents);
                float diameter = 0;
                minMaxDistance.x = diameter;
                wantedDistance = diameter + minPadding;
                currentDistance = wantedDistance;
            }

            lastTarget = target;
        }

        private void UpdateCamera()
        {
            Vector3 targetLookInput = new Vector3(
            Input.GetAxisRaw("Mouse Y") * sensitivity.y,
            Input.GetAxisRaw("Mouse X") * sensitivity.x,
            Input.GetAxisRaw("Rotate Z") * sensitivity.z * Time.unscaledDeltaTime);
            float scrollInput = Input.GetKey(KeyCode.LeftAlt) ? 0 : Input.GetAxis("Mouse ScrollWheel");

            transform.Rotate(targetLookInput);
            scrollSpeed = Mathf.Clamp(currentDistance - minMaxDistance.x + minMaxScrollSpeed.x, minMaxScrollSpeed.x, minMaxScrollSpeed.y);
            wantedDistance = Mathf.Clamp(wantedDistance - (scrollInput * scrollSpeed), minMaxDistance.x, minMaxDistance.y);
            currentDistance = Mathf.MoveTowards(currentDistance, wantedDistance, Time.unscaledDeltaTime * scrollSpeed * 2);
            Vector3 position = target.position + transform.TransformVector(offset) + (transform.rotation * new Vector3(0, 0, -currentDistance));

            RaycastHit hit;
            if (Physics.Linecast(target.position, position, out hit, pullLayerMask))
            {
                position = new Vector3(hit.point.x + hit.normal.x * 0.5f, hit.point.y + hit.normal.y * 0.5f, hit.point.z + hit.normal.z * 0.5f);
            }

            transform.position = position;
        }
    }
}