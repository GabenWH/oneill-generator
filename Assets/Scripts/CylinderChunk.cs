using System;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class CylinderChunk : MonoBehaviour
{
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    public void Generate(
        ONeillWorld world,
        int angularIndex,
        int longitudinalIndex,
        int angularChunkCount,
        int longitudinalChunkCount,
        int angularResolution,
        int longitudinalResolution)
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();

        Mesh mesh = new Mesh();
        mesh.name =
            $"Cylinder_{angularIndex}_{longitudinalIndex}";

        int vertsAround = angularResolution + 1;
        int vertsLong = longitudinalResolution + 1;
        Vector3[] vertices =
            new Vector3[vertsAround * vertsLong];
        Color[] colors = new Color[vertices.Length];
        Vector2[] uvs =
            new Vector2[vertices.Length];

        int[] triangles =
            new int[
                angularResolution *
                longitudinalResolution *
                6
            ];

        float anglePerChunk =
            Mathf.PI * 2f / angularChunkCount;

        float lengthPerChunk =
            world.length / longitudinalChunkCount;

        float angleStart =
            angularIndex * anglePerChunk;

        float zStart =
            -world.length * 0.5f +
            longitudinalIndex * lengthPerChunk;

        int v = 0;

        for (int z = 0; z <= longitudinalResolution; z++)
        {
            float tz =
                z / (float)longitudinalResolution;

            float worldZ =
                zStart + tz * lengthPerChunk;

            for (int a = 0; a <= angularResolution; a++)
            {
                float ta =
                    a / (float)angularResolution;

                float angle =
                    angleStart + ta * anglePerChunk;

                // Gentle terrain displacement.
                float noise =
                    Mathf.PerlinNoise(
                        angle * 3f + 100f,
                        worldZ * 0.0005f + 100f
                    );

                float terrainHeight =
                    (noise - 0.5f) * 100f;

                // Terrain grows inward from the nominal
                // cylinder radius.
                float r =
                    world.radius - terrainHeight;

                vertices[v] = new Vector3(
                    Mathf.Cos(angle) * r,
                    Mathf.Sin(angle) * r,
                    worldZ
                );

                float heightForShader = Mathf.InverseLerp(-50f,50f,terrainHeight);
                colors[v] = new Color(heightForShader,0f,0f,1f);

                uvs[v] = new Vector2(
                    ta,
                    tz
                );

                v++;
            }
        }

        int t = 0;

        for (int z = 0; z < longitudinalResolution; z++)
        {
            for (int a = 0; a < angularResolution; a++)
            {
                int i =
                    z * vertsAround + a;

                int nextRow =
                    i + vertsAround;

                // Winding faces inward so the cylinder is visible from inside.
                triangles[t++] = i;
                triangles[t++] = nextRow;
                triangles[t++] = i + 1;

                triangles[t++] = i + 1;
                triangles[t++] = nextRow;
                triangles[t++] = nextRow + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.colors = colors;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;
    }
}
