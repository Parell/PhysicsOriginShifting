using UnityEngine;

namespace PhysicsFloatingOrigin
{
    // Free-fly camera used for inspecting the scene without binding to a vessel.
    public class FreeCamera : MonoBehaviour
    {
        public float movementSpeed = 10f;
        public float fastMovementSpeed = 100f;
        public float freeLookSensitivity = 3f;
        public float zoomSensitivity = 10f;
        public float fastZoomSensitivity = 50f;
        private bool looking = false;

        void Update()
        {
            // Hold shift for faster translation and scroll adjustment.
            var fastMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var movementSpeed = fastMode ? this.fastMovementSpeed : this.movementSpeed;

            // WASD + QE + PageUp/PageDown cover the six movement directions.
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                transform.position = transform.position + (-transform.right * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                transform.position = transform.position + (transform.right * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                transform.position = transform.position + (transform.forward * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                transform.position = transform.position + (-transform.forward * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.Q))
            {
                transform.position = transform.position + (-transform.up * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.E))
            {
                transform.position = transform.position + (transform.up * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.PageUp))
            {
                transform.position = transform.position + (Vector3.up * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.PageDown))
            {
                transform.position = transform.position + (-Vector3.up * movementSpeed * Time.deltaTime);
            }

            if (looking)
            {
                float newRotationX = transform.localEulerAngles.y + Input.GetAxis("Mouse X") * freeLookSensitivity;
                float newRotationY = transform.localEulerAngles.x + -Input.GetAxis("Mouse Y") * freeLookSensitivity;
                transform.localEulerAngles = new Vector3(newRotationY, newRotationX, 0f);
            }

            float axis = Input.GetAxis("Mouse ScrollWheel");
            if (axis != 0)
            {
                var zoomSensitivity = fastMode ? this.fastZoomSensitivity : this.zoomSensitivity;
                transform.position = transform.position + transform.forward * axis * zoomSensitivity;
            }

            if (Input.GetKeyDown(KeyCode.Mouse1))
            {
                // Right mouse toggles mouse-look mode.
                StartLooking();
            }
            else if (Input.GetKeyUp(KeyCode.Mouse1))
            {
                StopLooking();
            }
        }

        void OnDisable()
        {
            StopLooking();
        }

        public void StartLooking()
        {
            looking = true;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        public void StopLooking()
        {
            looking = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
