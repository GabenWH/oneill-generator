using UnityEngine;

/// <summary>Glass shares the shell builder; the manager excludes walls between glass neighbors.</summary>
public class CylinderWindowChunk : CylinderChunk
{
    protected override float InnerRadius(ONeillWorld world, float angle, float z) => world.radius;
    protected override float OuterRadius(ONeillWorld world) => world.radius + world.glassThickness;
}
