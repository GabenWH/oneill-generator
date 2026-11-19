using UnityEngine;

/// <summary>The nearest eligible physical surface in the cylinder's outward radial direction.</summary>
public static class GroundEffectSurface
{
    public static bool TryMeasure(
        ONeillWorld world, Vector3 origin, float range, RaycastHit[] buffer,
        out Vector3 down, out RaycastHit nearest)
    {
        down = Vector3.zero;
        nearest = default;
        if (world == null || range <= 0f) return false;

        Vector3 radial = Vector3.ProjectOnPlane(origin - world.transform.position, world.transform.forward);
        if (radial.sqrMagnitude < .001f) return false;
        down = radial.normalized;

        int count = Physics.RaycastNonAlloc(origin, down, buffer, range,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == buffer.Length)
        {
            // A full non-alloc query may omit the closest eligible hit.
            buffer = Physics.RaycastAll(origin, down, range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            count = buffer.Length;
        }

        float distance = range;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = buffer[i];
            if (hit.collider == null || hit.distance >= distance) continue;
            if (hit.collider.GetComponentInParent<CylinderChunk>() == null &&
                hit.collider.GetComponentInParent<CylinderWindowChunk>() == null) continue;

            // The nearer water collider wins over terrain underneath it.
            // A global radial cutoff would wrongly discard glass or dry land.
            distance = hit.distance;
            nearest = hit;
        }
        return nearest.collider != null;
    }

    public static float Acceleration(float distance, float outwardSpeed, float range, float lift, float damping)
    {
        if (range <= 0f || distance >= range) return 0f;
        float proximity = Mathf.Clamp01(1f - distance / range);
        return Mathf.Max(0f, lift * proximity * proximity + damping * outwardSpeed * proximity);
    }
}
