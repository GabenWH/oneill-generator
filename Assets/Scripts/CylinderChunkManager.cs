using System;
using UnityEngine;

public class CylinderChunkManager : MonoBehaviour
{
    public ONeillWorld world;

    [Header("Chunking")]
    public int angularChunks = 24;
    public int longitudinalChunks = 16;

    [Header("Mesh Resolution")]
    public int angularResolution = 12;
    public int longitudinalResolution = 12;

    [Header("Materials")]
    public Material landMaterial;
    public Material glassMaterial;

    private void Start()
    {
        if (world == null)
            world = FindObjectOfType<ONeillWorld>();

        GenerateWorld();
    }

    public void GenerateWorld()
    {
        for (int z = 0; z < longitudinalChunks; z++)
        {
            for (int a = 0; a < angularChunks; a++)
            {
                // Every fourth strip is left open,
                // approximating O'Neill window bands.
                if (a % 4 == 3)
                {
                    GenerateWindow(a, z);
                    continue;
                }

                GameObject chunk =
                    new GameObject(
                        $"Chunk_{a}_{z}"
                    );

                chunk.transform.SetParent(
                    transform,
                    false
                );

                MeshRenderer renderer =
                    chunk.AddComponent<MeshRenderer>();

                MeshFilter filter =
                    chunk.AddComponent<MeshFilter>();

                MeshCollider collider =
                    chunk.AddComponent<MeshCollider>();

                CylinderChunk cylinderChunk =
                    chunk.AddComponent<CylinderChunk>();

                renderer.material =
                    landMaterial;

                cylinderChunk.Generate(
                    world,
                    a,
                    z,
                    angularChunks,
                    longitudinalChunks,
                    angularResolution,
                    longitudinalResolution
                );
            }
        }
    }

    private void GenerateWindow(int a, int z)
    {
        GameObject chunk =
    new GameObject(
        $"GlassChunk_{a}_{z}"
    );

        chunk.transform.SetParent(
            transform,
            false
        );

        MeshRenderer renderer =
            chunk.AddComponent<MeshRenderer>();

        MeshFilter filter =
            chunk.AddComponent<MeshFilter>();

        MeshCollider collider =
            chunk.AddComponent<MeshCollider>();

        CylinderWindowChunk cylinderChunk =
            chunk.AddComponent<CylinderWindowChunk>();

        renderer.material =
            glassMaterial;

        cylinderChunk.Generate(
            world,
            a,
            z,
            angularChunks,
            longitudinalChunks,
            angularResolution,
            longitudinalResolution
        );
    }
}