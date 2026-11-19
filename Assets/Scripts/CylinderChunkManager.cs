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
    public Color lowColor;
    public Color midColor;
    public Color highColor;
    public Material glassMaterial;
    public bool includeGroundEffectTestWater;
    public Material groundEffectTestWaterMaterial;

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

                LandChunk cylinderChunk =
                    chunk.AddComponent<LandChunk>();

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
        if (includeGroundEffectTestWater) GenerateGroundEffectTestWater();

        GameObject endCapA = new GameObject("EndCap_A");
        endCapA.transform.SetParent(transform,false);

        GameObject endCapB = new GameObject("EndCap_B");
        endCapB.transform.SetParent(transform,false);

        endCapA.AddComponent<MeshFilter>();
        endCapA.AddComponent<MeshRenderer>().material = glassMaterial;

        endCapB.AddComponent<MeshFilter>();
        endCapB.AddComponent<MeshRenderer>().material = glassMaterial;

        CylinderEndCap capA = endCapA.AddComponent<CylinderEndCap>();
        CylinderEndCap capB = endCapB.AddComponent<CylinderEndCap>();

        endCapA.transform.localPosition = new Vector3(0f,0f,world.length * 0.5f);
        endCapB.transform.localPosition = new Vector3(0f,0f,world.length * -0.5f);

        capA.Generate(world.radius,16,64,100f,false);
        capB.Generate(world.radius,16,64,100f,true);




    }

    private void GenerateGroundEffectTestWater()
    {
        // A small optional flight-test surface, not a world-wide water layer.
        // Use one fine slice and put it in front of the existing terrain relief.
        const int slices = 256;
        if (world.radius <= 252f) return;
        var patch = new GameObject("Water (ground-effect test patch)");
        patch.transform.SetParent(transform, false);
        var water = patch.AddComponent<CylinderWaterChunk>();
        water.surfaceRadius = world.radius - 252f;
        var renderer = patch.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = groundEffectTestWaterMaterial != null ? groundEffectTestWaterMaterial : glassMaterial;
        water.Generate(world, 0, slices / 2, slices, slices, 12, 4);
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

        CylinderWindowChunk cylinderWindowChunk =
            chunk.AddComponent<CylinderWindowChunk>();

        renderer.material =
            glassMaterial;

        cylinderWindowChunk.Generate(
            world,
            a,
            z,
            angularChunks,
            longitudinalChunks,
            angularResolution,
            1
        );
    }
}