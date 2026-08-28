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
        get
        {
            // a = omega² r
            return Mathf.Sqrt(surfaceGravity / radius);
        }
    }

    public float RPM
    {
        get
        {
            return AngularVelocity * 60f / (2f * Mathf.PI);
        }
    }

    /// <summary>
    /// Returns centrifugal acceleration at the supplied position.
    /// Cylinder axis is world Z.
    /// </summary>
    public Vector3 GetCentrifugalAcceleration(Vector3 worldPosition)
    {
        Vector3 local = worldPosition - transform.position;

        Vector3 radial = new Vector3(local.x, local.y, 0f);

        if (radial.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        float r = radial.magnitude;

        return radial.normalized * AngularVelocity * AngularVelocity * r;
    }

    /// <summary>
    /// Acceleration in a rotating reference frame.
    /// Includes centrifugal acceleration and optionally Coriolis acceleration.
    /// </summary>
    public Vector3 GetRotatingFrameAcceleration(
        Vector3 worldPosition,
        Vector3 worldVelocity)
    {
        Vector3 acceleration = GetCentrifugalAcceleration(worldPosition);

        if (simulateCoriolis)
        {
            Vector3 omega = Vector3.forward * AngularVelocity;

            // Coriolis:
            // a = -2 omega x v
            acceleration += -2f * Vector3.Cross(omega, worldVelocity);
        }

        return acceleration;
    }

    public float GravityAtRadius(float r)
    {
        return AngularVelocity * AngularVelocity * r;
    }

    public float GravityFraction(Vector3 worldPosition)
    {
        Vector3 local = worldPosition - transform.position;
        float r = new Vector2(local.x, local.y).magnitude;

        return GravityAtRadius(r) / surfaceGravity;
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