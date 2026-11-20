using UnityEngine;

/// <summary>A non-colliding sea surface, generated only over land bands.</summary>
public class CylinderWaterChunk : CylinderChunk
{
    protected override bool HasOuterSurface => false;
    protected override bool HasCollider => false;
    protected override float InnerRadius(ONeillWorld world, float angle, float z) => world.WaterRadius;
    protected override float OuterRadius(ONeillWorld world) => world.WaterRadius;
    protected override Color SurfaceColor(ONeillWorld world, float angle, float z)
        => new Color(world.SampleTerrainHeight(angle, z), world.seaLevel, 0f, 1f);
}
