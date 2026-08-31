using System;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class CylinderWindowChunk : MonoBehaviour
{
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    float glassThickness = 20f;

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
            $"WindowCylinder_{angularIndex}_{longitudinalIndex}";

        int vertsAround = angularResolution + 1;
        int vertsLong = longitudinalResolution + 1;


        int vertsPerSurface = vertsAround * vertsLong;
        Vector3[] vertices =
            new Vector3[vertsPerSurface * 2];

        Vector2[] uvs =
            new Vector2[vertices.Length];

        int[] triangles =
            new int[
                angularResolution *
                longitudinalResolution *
                6 * 2
            ];


        //a fuck ton of math I forgot to comment idk take highschool trig again and then explain it to me I was high when I wrote this :P
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

                // Giving the window inner verts
                //basically I take the inside of the oneill and then use the radius and angle to calculate their x,y,z relative to the cylinder
                float innerRadius =
                    world.radius;

                vertices[v] = new Vector3(
                    Mathf.Cos(angle) * innerRadius,
                    Mathf.Sin(angle) * innerRadius,
                    worldZ
                );

                //now the outter
                float outerRadius = world.radius + glassThickness;
                vertices[v + vertsPerSurface] = new Vector3(
                    Mathf.Cos(angle) * outerRadius,
                    Mathf.Sin(angle) * outerRadius,
                    worldZ
                );

                uvs[v] = new Vector2(
                    ta,
                    tz
                );
                uvs[v + vertsPerSurface] = uvs[v];
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

                // I'm gonna be real, I don't really know how I figured this out, probably googled it...
                triangles[t++] = i;
                triangles[t++] = nextRow;
                triangles[t++] = i + 1;

                triangles[t++] = i + 1;
                triangles[t++] = nextRow;
                triangles[t++] = nextRow + 1;


                //we take the vertex surfaces from the inside but because they're on the outsied we reverse the winding because they're outside verts not inside verts, I'm very smart
                int outerI = i + vertsPerSurface;
                int outerNextRow = nextRow + vertsPerSurface;

                triangles[t++] = outerI;
                triangles[t++] = outerI + 1;
                triangles[t++] = outerNextRow;

                triangles[t++] = outerI + 1;
                triangles[t++] = outerNextRow + 1;
                triangles[t++] = outerNextRow;
                //see, order is a little crooked from the previous one, thats because its on the outside. Explaining this though is like rotating an apple in your brain, you have to be trans to get it.
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;

        //I love unity sometimes.
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;
    }
}
