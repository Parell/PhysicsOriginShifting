using UnityEngine;

namespace PhysicsFloatingOrigin
{
    // Keeps the scene light pointed at the active local camera.
    public class LightDirection : MonoBehaviour
    {
        [SerializeField] private Transform localCamera;
        private Light mainLight;

        private void Start()
        {
            mainLight = GetComponent<Light>();
        }

        private void Update()
        {
            // The light follows the camera so the example keeps consistent view lighting.
            Vector3 targetPoint = localCamera.position - transform.position;
            transform.rotation = Quaternion.LookRotation(targetPoint, Vector3.up);
        }
    }
}
