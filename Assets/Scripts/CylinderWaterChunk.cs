using UnityEngine;

/// <summary>A finite curved water collision surface; rendering is independent of the flight query.</summary>
public sealed class CylinderWaterChunk : CylinderChunk
{
    public float surfaceRadius;

    private void Awake() { MeshString = "Water"; }

    protected override int GenerateInnerSurface(ONeillWorld world, int angularResolution,
        int longitudinalResolution, Vector3[] vertices, Color[] colors, Vector2[] uvs,
        float angleStep, float zStep, float angleStart, float zStart, int v)
    {
        float r = surfaceRadius > 0f ? surfaceRadius : world.radius;
        for (int z = 0; z <= longitudinalResolution; z++)
        for (int a = 0; a <= angularResolution; a++)
        {
            float angle = angleStart + angleStep * a;
            vertices[v] = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, zStart + zStep * z);
            uvs[v] = new Vector2((float)a / angularResolution, (float)z / longitudinalResolution);
            colors[v++] = Color.white;
        }
        return v;
    }

    protected override int GenerateBottom(ONeillWorld world, int angularResolution,
        int longitudinalResolution, Vector3[] vertices, Color[] colors, Vector2[] uvs,
        float angleStep, float zStep, float angleStart, float zStart, int v) => v;
}
