using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class CylinderEndCap : MonoBehaviour
{
    // Start is called before the first frame update


    public void Generate(float radius, int radialRes, int resolution, float depth, bool facesPositiveZ)
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        Mesh mesh = new Mesh();
        mesh.name = "CylinderEndCap";

        Vector3[] verts = new Vector3[resolution * radialRes + 1];
        int triCount = resolution + (radialRes - 1) * resolution * 2;
        int[] tris = new int[triCount * 3];

        float zDirection = facesPositiveZ ? 1f : -1f;
        verts[0] = new Vector3(0f, 0f, zDirection * depth);

        for (int i = 0; i < resolution; i++)
        {
            float angle = (i / (float)resolution) * Mathf.PI * 2f;
            for (int j = 0; j < radialRes; j++)
            {
                int index = 1 + j * resolution + i;
                float normalizedRadius = (j + 1) / (float)radialRes;
                float currentRadius = normalizedRadius * radius;
                verts[index] = new Vector3(MathF.Cos(angle) * currentRadius, Mathf.Sin(angle) * currentRadius, zDirection*depth * (1f - Mathf.Pow(normalizedRadius, 2f)));
            }

        }

        int t = 0;
        for (int i = 0; i < resolution; i++)
        {
            int current = 1 + i;
            int next = 1 + ((i + 1) % resolution);

            tris[t++] = 0;
            tris[t++] = facesPositiveZ ? current : next;
            tris[t++] = facesPositiveZ ? next : current;
        }

        for (int j = 0; j < radialRes - 1; j++)
        {
            for (int i = 0; i < resolution; i++)
            {
                int current = i + j * resolution + 1;
                int next =
                    1 + j * resolution +
                    ((i + 1) % resolution);

                int outerCurrent =
                    1 + (j + 1) * resolution + i;

                int outerNext =
                    1 + (j + 1) * resolution +
                    ((i + 1) % resolution);

                if (facesPositiveZ)
                {
                    tris[t++] = current;
                    tris[t++] = outerCurrent;
                    tris[t++] = next;

                    tris[t++] = next;
                    tris[t++] = outerCurrent;
                    tris[t++] = outerNext;
                }
                else
                {

                    tris[t++] = current;
                    tris[t++] = next;
                    tris[t++] = outerCurrent;

                    tris[t++] = next;
                    tris[t++] = outerNext;
                    tris[t++] = outerCurrent;
                }
            }
        }

        mesh.vertices = verts;
        mesh.triangles = tris;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;
    }
}
