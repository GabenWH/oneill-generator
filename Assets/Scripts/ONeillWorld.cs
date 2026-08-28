using UnityEngine;

public class ONeillWorld : MonoBehaviour
{
    [Header("Cylinder")]
    public float radius = 4000f;
    public float length = 32000f;

    [Header("Artificial Gravity")]
    [Tooltip("Surface gravity in m/s².")]
    public float surfaceGravity = 9.81f;

    [Header("Rotation")]
    public bool simulateCoriolis = true;

    public float AngularVelocity
    {
        get { return Mathf.Sqrt(surfaceGravity / radius); }
    }

    public float RPM
    {
        get { return AngularVelocity * 60f / (2f * Mathf.PI); }
    }

    public Vector3 GetCentrifugalAcceleration(Vector3 position)
    {
        Vector3 r = position - transform.position;

        // Cylinder axis is Z. Axial displacement contributes
        // nothing to centrifugal acceleration.
        r = Vector3.ProjectOnPlane(r, Vector3.forward);

        Vector3 omega = Vector3.forward * AngularVelocity;

        return -Vector3.Cross(
            omega,
            Vector3.Cross(omega, r)
        );
    }

    public Vector3 GetRotatingFrameAcceleration(
        Vector3 position,
        Vector3 velocity)
    {
        Vector3 centrifugal =
            GetCentrifugalAcceleration(position);

        if (!simulateCoriolis)
            return centrifugal;

        Vector3 omega =
            Vector3.forward * AngularVelocity;

        Vector3 coriolis =
            -2f * Vector3.Cross(omega, velocity);

        return centrifugal + coriolis;
    }

    public float GravityAtRadius(float r)
    {
        return AngularVelocity * AngularVelocity * r;
    }

    public float GravityFraction(Vector3 position)
    {
        Vector3 r = position - transform.position;
        r = Vector3.ProjectOnPlane(r, Vector3.forward);

        return GravityAtRadius(r.magnitude) / surfaceGravity;
    }

    private void Start()
    {
        Debug.Log(
            $"O'Neill Cylinder: Radius {radius:N0} m | " +
            $"Length {length:N0} m | " +
            $"Rotation {RPM:F3} RPM | " +
            $"Period {(60f / RPM):F1} seconds"
        );
    }
}