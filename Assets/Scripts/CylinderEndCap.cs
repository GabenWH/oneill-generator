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

        Vector3[] verts = new Vector3[resolution*radialRes+1];
        int[] tris = new int[resolution*3*radialRes];

        verts[0] = Vector3.zero;
    
        for (int i = 0; i < resolution; i++)
        {
            float angle = (i / (float)resolution) * Mathf.PI * 2f;
            for (int j = 0; j < radialRes; j++)
            {
                int index = 1+j * resolution+i;
                float normalizedRadius = j / (float)radialRes;
                float currentRadius = normalizedRadius*radius;
                verts[index] = new Vector3(MathF.Cos(angle) * currentRadius, Mathf.Sin(angle) * currentRadius, -depth * (1f - Mathf.Pow(normalizedRadius, 2f)));
            }

        }

        int t = 0;
        for (int i = 0; i< resolution; i++)
        {
            int current = i+1;
            int next = ((i+1) % resolution) + 1;

            if (facesPositiveZ)
            {
                tris[t++] = 0;
                tris[t++] = current;
                tris[t++] = next;
            }
            else
            {
                tris[t++]=0;
                tris[t++]=next;
                tris[t++]=current;
            }
        }
        mesh.vertices = verts;
        mesh.triangles = tris;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;
    }
}
