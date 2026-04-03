using UnityEngine;

public class ExampleBody : MonoBehaviour
{
    public Rigidbody rb;
    [SerializeField] private Vector3 initalValocity;
    [SerializeField] private float acceleration;

    public Vector3 InitialVelocity => initalValocity;
    public float Acceleration => acceleration;
    public Vector3 AccelerationVector => Vector3.forward * acceleration;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.AddForce(initalValocity, ForceMode.VelocityChange);
    }

    private void FixedUpdate()
    {
        rb.AddForce(Vector3.one.normalized * acceleration, ForceMode.Acceleration);
    }
}
