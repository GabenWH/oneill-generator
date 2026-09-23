using System;
using Unity.VisualScripting;
using UnityEngine;

public class LandChunk : CylinderChunk
{

    protected override int GenerateInnerSurface(ONeillWorld world, int angularResolution, int longitudinalResolution, Vector3[] vertices, Color[] colors, Vector2[] uvs, float angleStep, float zStep, float angleStart, float zStart, int v)
    {
        for (int z = 0; z <= longitudinalResolution; z++)
        {

            float worldZ =
                zStart + zStep * z;

            for (int a = 0; a <= angularResolution; a++)
            {

                float angle =
                    angleStart + angleStep * a;

                // Gentle terrain displacement.
                float noise =
                    Mathf.PerlinNoise(
                        angle * 3f + 100f,
                        worldZ * 0.0005f + 100f
                    );

                float terrainHeight =
                    (noise - 0.5f) * 500f;

                // Terrain grows inward from the nominal
                // cylinder radius.
                float r =
                    world.radius - terrainHeight;

                vertices[v] = new Vector3(
                    Mathf.Cos(angle) * r,
                    Mathf.Sin(angle) * r,
                    worldZ
                );

                float heightForShader = Mathf.InverseLerp(-50f, 50f, terrainHeight);
                colors[v] = new Color(heightForShader, 0f, 0f, 1f);

                //I tried to make it simpler, but I just made it harder🥺
                float u = a/(float)angularResolution;
                float uZ = z/(float)longitudinalResolution;
                uvs[v] = new Vector2(
                    u,
                    uZ
                );

                v++;
            }
        }

        return v;
    }
    protected override int GenerateBottom(ONeillWorld world, int angularResolution, int longitudinalResolution, Vector3[] vertices, Color[] colors, Vector2[] uvs, float anglePerChunk, float lengthPerChunk, float angleStart, float zStart, int v)
    {
        return v;
    }
}
