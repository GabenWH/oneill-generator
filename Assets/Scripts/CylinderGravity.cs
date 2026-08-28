using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CylinderGravity : MonoBehaviour
{
    public ONeillWorld world;

    public bool applyCoriolis = true;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
    }

    private void Start()
    {
        if (world == null)
            world = FindObjectOfType<ONeillWorld>();
    }

    private void FixedUpdate()
    {
        if (world == null)
            return;

        Vector3 acceleration;

        if (applyCoriolis)
        {
            acceleration = world.GetRotatingFrameAcceleration(
                transform.position,
                rb.velocity
            );
        }
        else
        {
            acceleration =
                world.GetCentrifugalAcceleration(transform.position);
        }

        rb.AddForce(acceleration, ForceMode.Acceleration);
    }
}