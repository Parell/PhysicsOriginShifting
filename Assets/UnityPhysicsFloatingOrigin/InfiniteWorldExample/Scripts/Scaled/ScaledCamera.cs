using UnityEngine;

namespace UnityPhysicsFloatingOrigin
{
    public class ScaledCamera : MonoBehaviour
    {
        [SerializeField] private Camera localCamera;
        [SerializeField] private float unscaledFarClipPlane = 1e10f;
        [SerializeField] private float nearClipPlane = 1f;
        private Camera scaledCamera;

        private void Start()
        {
            scaledCamera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            scaledCamera.nearClipPlane = localCamera.farClipPlane * nearClipPlane * Constant.INVERSE_SCALE;
            scaledCamera.farClipPlane = unscaledFarClipPlane * Constant.INVERSE_SCALE;

            transform.SetPositionAndRotation(localCamera.transform.position * Constant.INVERSE_SCALE, localCamera.transform.rotation);
        }
    }
}
