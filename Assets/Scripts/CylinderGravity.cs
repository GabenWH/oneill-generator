using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CylinderGravity : MonoBehaviour
{
    public ONeillWorld world;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Unity's normal downward gravity makes no sense
        // inside the cylinder.
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

        Vector3 acceleration =
            world.GetRotatingFrameAcceleration(
                rb.position,
                rb.velocity
            );

        rb.AddForce(
            acceleration,
            ForceMode.Acceleration
        );
    }
}