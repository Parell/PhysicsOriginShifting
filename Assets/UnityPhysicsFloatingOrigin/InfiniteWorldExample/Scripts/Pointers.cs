using UnityEngine;

namespace PhysicsFloatingOrigin
{
    // Draws the direction indicators around the active body relative to a reference frame.
    public class Pointers : MonoBehaviour
    {
        [SerializeField] private Transform forwardArrow;
        [SerializeField] private Transform progradeArrow;
        [SerializeField] private Transform retrogradeArrow;
        [SerializeField] private Body referenceFrame;
        [SerializeField] private float distance;

        private Body body;

        private void Start()
        {
            // The example uses the current main body as the anchor for these pointers.
            body = PhysicsManager.mainBody;
        }

        private void Update()
        {
            if (referenceFrame == null) { return; }

            // Use relative velocity so prograde/retrograde reflect the current frame of reference.
            var velocity = (Vector3)(body.bodyData.velocity - referenceFrame.bodyData.velocity);

            var offset = body.transform.position;
            var position = body.transform.forward * distance + offset;
            forwardArrow.SetPositionAndRotation(position, Quaternion.LookRotation(body.transform.forward));

            // Hide the velocity pointers at low speed to avoid noisy visual feedback.
            if (MathExtentions.FastMagnitude(velocity) < 1f)
            {
                progradeArrow.gameObject.SetActive(false);
                retrogradeArrow.gameObject.SetActive(false);
            }
            else
            {
                progradeArrow.gameObject.SetActive(true);
                retrogradeArrow.gameObject.SetActive(true);

                velocity = velocity.normalized;
                position = velocity * distance + offset;
                progradeArrow.SetPositionAndRotation(position, Quaternion.LookRotation(velocity));
                position = -velocity * distance + offset;
                retrogradeArrow.SetPositionAndRotation(position, Quaternion.LookRotation(velocity));
            }
        }
    }
}
