using System.Collections.Generic;
using UnityEngine;

/// <summary>Dense nearby water with inexpensive expanding strips out to the simulation boundary.</summary>
public static class CylinderWaterSurface
{
    public static Mesh BuildDetailMesh(float size)
    {
        const int resolution = 128;
        float core = Mathf.Min(64f, size);
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int z = 0; z <= resolution; z++)
            for (int x = 0; x <= resolution; x++)
            {
                int i = vertices.Count;
                vertices.Add(new Vector3((x / (float)resolution - .5f) * core, 0, (z / (float)resolution - .5f) * core));
                if (x == resolution || z == resolution) continue;
                Quad(triangles, i, i + resolution + 1, i + resolution + 2, i + 1);
            }
        float inner = core * .5f;
        while (inner < size * .5f - .01f)
        {
            float outer = Mathf.Min(size * .5f, inner * 1.5f);
            for (int side = 0; side < 4; side++)
            {
                int start = vertices.Count;
                for (int i = 0; i <= resolution; i++)
                {
                    vertices.Add(Edge(side, i / (float)resolution, inner));
                    vertices.Add(Edge(side, i / (float)resolution, outer));
                    if (i == resolution) continue;
                    int a = start + i * 2;
                    Quad(triangles, a, a + 2, a + 3, a + 1);
                }
            }
            inner = outer;
        }
        var mesh = new Mesh { name = "Cylinder water clipmap" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector3 Edge(int side, float t, float extent)
    {
        float value = Mathf.Lerp(-extent, extent, t);
        switch (side)
        {
            case 0: return new Vector3(value, 0, -extent);
            case 1: return new Vector3(extent, 0, value);
            case 2: return new Vector3(-value, 0, extent);
            default: return new Vector3(-extent, 0, -value);
        }
    }
    private static void Quad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }
}
