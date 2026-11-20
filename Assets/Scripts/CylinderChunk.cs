using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[Flags]
public enum ChunkEdges
{
    None = 0, AngularStart = 1, AngularEnd = 2,
    LengthStart = 4, LengthEnd = 8, All = 15
}

/// <summary>Shared curved surface, outer shell, boundary walls and mesh ownership.</summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public abstract class CylinderChunk : MonoBehaviour
{
    private Mesh generatedMesh;
    protected virtual bool HasOuterSurface => true;
    protected virtual bool HasCollider => true;
    protected abstract float InnerRadius(ONeillWorld world, float angle, float z);
    protected abstract float OuterRadius(ONeillWorld world);
    protected virtual Color SurfaceColor(ONeillWorld world, float angle, float z) => Color.white;
    protected virtual Vector3 SurfaceNormal(ONeillWorld world, float angle, float z)
        => new Vector3(-Mathf.Cos(angle), -Mathf.Sin(angle), 0f);

    public void Generate(ONeillWorld world, int angularIndex, int longitudinalIndex,
        int angularChunkCount, int longitudinalChunkCount, int angularResolution,
        int longitudinalResolution, ChunkEdges edges = ChunkEdges.All)
    {
        if (world == null) throw new ArgumentNullException(nameof(world));
        if (angularChunkCount < 1 || longitudinalChunkCount < 1 || angularResolution < 1 || longitudinalResolution < 1)
            throw new ArgumentOutOfRangeException(nameof(angularResolution), "Chunk counts and resolutions must be positive.");
        if (angularIndex < 0 || angularIndex >= angularChunkCount || longitudinalIndex < 0 || longitudinalIndex >= longitudinalChunkCount)
            throw new ArgumentOutOfRangeException(nameof(angularIndex));

        int row = angularResolution + 1;
        int surfaceCount = row * (longitudinalResolution + 1);
        var vertices = new List<Vector3>(surfaceCount * 2);
        var normals = new List<Vector3>(surfaceCount * 2);
        var colors = new List<Color>(surfaceCount * 2);
        var uvs = new List<Vector2>(surfaceCount * 2);
        var triangles = new List<int>(angularResolution * longitudinalResolution * 12);
        for (int surface = 0; surface < (HasOuterSurface ? 2 : 1); surface++)
        {
            for (int z = 0; z <= longitudinalResolution; z++)
            {
                // Global grid coordinates give adjacent chunks exactly the same samples.
                float worldZ = world.length * ((longitudinalIndex * longitudinalResolution + z) /
                    (float)(longitudinalChunkCount * longitudinalResolution) - .5f);
                for (int a = 0; a <= angularResolution; a++)
                {
                    int globalA = (angularIndex * angularResolution + a) % (angularChunkCount * angularResolution);
                    float angle = globalA * (Mathf.PI * 2f / (angularChunkCount * angularResolution));
                    float radius = surface == 0 ? InnerRadius(world, angle, worldZ) : OuterRadius(world);
                    Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    vertices.Add(radial * radius + Vector3.forward * worldZ);
                    normals.Add(surface == 0 ? SurfaceNormal(world, angle, worldZ) : radial);
                    colors.Add(surface == 0 ? SurfaceColor(world, angle, worldZ) : new Color(0f, 1f, 0f, 1f));
                    uvs.Add(new Vector2(angle * world.WaterRadius, worldZ));
                }
            }
            int offset = surface * surfaceCount;
            for (int z = 0; z < longitudinalResolution; z++)
                for (int a = 0; a < angularResolution; a++)
                {
                    int i = offset + z * row + a;
                    // Increasing angle crossed with increasing Z points outward.
                    if (surface == 0) AddQuad(triangles, i, i + row, i + row + 1, i + 1);
                    else AddQuad(triangles, i, i + 1, i + row + 1, i + row);
                }
        }
        if (HasOuterSurface)
        {
            for (int z = 0; z < longitudinalResolution; z++)
            {
                int i = z * row;
                if ((edges & ChunkEdges.AngularStart) != 0) Wall(i + row, i, surfaceCount, vertices, normals, colors, uvs, triangles);
                i += angularResolution;
                if ((edges & ChunkEdges.AngularEnd) != 0) Wall(i, i + row, surfaceCount, vertices, normals, colors, uvs, triangles);
            }
            for (int a = 0; a < angularResolution; a++)
            {
                if ((edges & ChunkEdges.LengthStart) != 0) Wall(a, a + 1, surfaceCount, vertices, normals, colors, uvs, triangles);
                int i = longitudinalResolution * row + a;
                if ((edges & ChunkEdges.LengthEnd) != 0) Wall(i + 1, i, surfaceCount, vertices, normals, colors, uvs, triangles);
            }
        }

        ReleaseMesh();
        generatedMesh = new Mesh { name = $"{GetType().Name}_{angularIndex}_{longitudinalIndex}",
            indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        generatedMesh.SetVertices(vertices);
        generatedMesh.SetNormals(normals);
        generatedMesh.SetColors(colors);
        generatedMesh.SetUVs(0, uvs);
        generatedMesh.SetTriangles(triangles, 0);
        generatedMesh.RecalculateTangents();
        generatedMesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = generatedMesh;
        if (HasCollider)
        {
            var collider = GetComponent<MeshCollider>();
            if (collider == null) collider = gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = generatedMesh;
        }
    }

    private static void AddQuad(List<int> indices, int a, int b, int c, int d)
    {
        indices.Add(a); indices.Add(b); indices.Add(c);
        indices.Add(a); indices.Add(c); indices.Add(d);
    }

    private static void Wall(int a, int b, int outerOffset, List<Vector3> vertices,
        List<Vector3> normals, List<Color> colors, List<Vector2> uvs, List<int> indices)
    {
        int start = vertices.Count;
        int[] corners = { a, b, b + outerOffset, a + outerOffset };
        Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[b + outerOffset] - vertices[a]).normalized;
        for (int n = 0; n < 4; n++)
        {
            vertices.Add(vertices[corners[n]]);
            normals.Add(normal);
            colors.Add(new Color(0f, 1f, 0f, 1f));
            uvs.Add(new Vector2(n == 1 || n == 2 ? Vector3.Distance(vertices[a], vertices[b]) : 0f,
                n >= 2 ? Vector3.Distance(vertices[a], vertices[a + outerOffset]) : 0f));
        }
        AddQuad(indices, start, start + 1, start + 2, start + 3);
    }

    private void ReleaseMesh()
    {
        if (generatedMesh == null) return;
        var collider = GetComponent<MeshCollider>();
        if (collider != null) collider.sharedMesh = null;
        GetComponent<MeshFilter>().sharedMesh = null;
        if (Application.isPlaying) Destroy(generatedMesh);
        else DestroyImmediate(generatedMesh);
        generatedMesh = null;
    }

    protected virtual void OnDestroy() { ReleaseMesh(); }
}
