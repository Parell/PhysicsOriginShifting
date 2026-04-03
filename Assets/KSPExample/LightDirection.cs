using UnityEngine;

namespace UnityPhysicsFloatingOrigin
{
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
            Vector3 targetPoint = localCamera.position - transform.position;
            transform.rotation = Quaternion.LookRotation(targetPoint, Vector3.up);
            //mainLight.intensity = Mathf.Clamp(2.18f * Mathf.Exp(-9.763E-7f * MathExtentions.FastMagnitude(targetPoint)), 0.2f, 2);
        }
    }
}
