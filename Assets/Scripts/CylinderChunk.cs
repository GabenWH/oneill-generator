using System;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public abstract class CylinderChunk : MonoBehaviour
{
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    protected String MeshString = "Cylinder";

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
            $"{MeshString}_{angularIndex}_{longitudinalIndex}";

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
        float angleStep = anglePerChunk / angularResolution;
        float zStep = lengthPerChunk / longitudinalResolution;

        int v = 0;
        v = GenerateInnerSurface(world, angularResolution, longitudinalResolution, vertices, colors, uvs, angleStep, zStep, angleStart, zStart, v);

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
        v = 0;
        Vector3[] bottomVerts =
            new Vector3[vertsAround * vertsLong];
        Color[] bottomColors =
            new Color[bottomVerts.Length];

        Vector2[] bottomUVs =
            new Vector2[bottomVerts.Length];
        int[] bottomTriangles =
            new int[
                angularResolution *
                longitudinalResolution *
                6
            ];




        v = GenerateBottom(world, angularResolution, longitudinalResolution, bottomVerts, bottomColors, bottomUVs, angleStep, zStep, angleStart, zStart, v);
        if (v > 0)
        {
            for (int z = 0; z < longitudinalResolution; z++)
            {
                for (int a = 0; a < angularResolution; a++)
                {
                    int i =
                        z * vertsAround + a;

                    int nextRow =
                        i + vertsAround;

                    // Winding faces inward so the cylinder is visible from inside.
                    bottomTriangles[t++] = i;
                    bottomTriangles[t++] = i + 1;
                    bottomTriangles[t++] = nextRow;

                    bottomTriangles[t++] = i + 1;
                    bottomTriangles[t++] = nextRow + 1;
                    bottomTriangles[t++] = nextRow;
                }
            }

        }

    }
    protected abstract int GenerateInnerSurface(ONeillWorld world, int angularResolution, int longitudinalResolution, Vector3[] vertices, Color[] colors, Vector2[] uvs, float angleStep, float zStep, float angleStart, float zStart, int v);
    //Generate inner verts
    //Return next vertex int
    protected abstract int GenerateBottom(ONeillWorld world, int angularResolution, int longitudinalResolution, Vector3[] vertices, Color[] colors, Vector2[] uvs, float angleStep, float zStep, float angleStart, float zStart, int v);
    //Generate outer verts
    //return next vertex int
}
