using UnityEngine;

public class LandChunk : CylinderChunk
{
    protected override float InnerRadius(ONeillWorld world, float angle, float z)
        => world.radius - world.SampleTerrainHeight(angle, z);
    protected override float OuterRadius(ONeillWorld world) => world.OuterRadius;
    protected override Color SurfaceColor(ONeillWorld world, float angle, float z)
        => new Color(Mathf.InverseLerp(-world.terrainAmplitude, world.terrainAmplitude,
            world.SampleTerrainHeight(angle, z)), 0f, 0f, 1f);
    protected override Vector3 SurfaceNormal(ONeillWorld world, float angle, float z)
    {
        const float step = .0001f;
        Vector3 before = Point(world, angle - step, z), after = Point(world, angle + step, z);
        Vector3 back = Point(world, angle, z - 1f), front = Point(world, angle, z + 1f);
        return Vector3.Cross(front - back, after - before).normalized;
    }
    private Vector3 Point(ONeillWorld world, float angle, float z)
    {
        float radius = InnerRadius(world, angle, z);
        return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
    }
}
