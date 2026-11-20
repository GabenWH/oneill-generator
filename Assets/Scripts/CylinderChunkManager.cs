using UnityEngine;
using UnityEngine.Rendering;

public class CylinderChunkManager : MonoBehaviour
{
    public ONeillWorld world;
    [Header("Chunking")]
    [Min(6)] public int angularChunks = 24;
    [Min(1)] public int longitudinalChunks = 16;
    [Min(1)] public int landStripes = 3;
    [Header("Mesh Resolution")]
    [Min(1)] public int angularResolution = 12;
    [Min(1)] public int longitudinalResolution = 12;
    [Header("Materials")]
    public Material landMaterial;
    public Material glassMaterial;
    public Material waterMaterial;
    public Color lowColor;
    public Color midColor;
    public Color highColor;
    private GameObject generatedRoot;

    private void Start()
    {
        if (generatedRoot == null) GenerateWorld();
    }

    public bool IsLand(int angularIndex)
    {
        int index = ((angularIndex % angularChunks) + angularChunks) % angularChunks;
        return (index * (landStripes * 2) / angularChunks) % 2 == 0;
    }

    public ChunkEdges GetExposedEdges(int a, int z)
    {
        ChunkEdges edges = ChunkEdges.None;
        if (IsLand(a - 1) != IsLand(a)) edges |= ChunkEdges.AngularStart;
        if (IsLand(a + 1) != IsLand(a)) edges |= ChunkEdges.AngularEnd;
        if (z == 0) edges |= ChunkEdges.LengthStart;
        if (z == longitudinalChunks - 1) edges |= ChunkEdges.LengthEnd;
        return edges;
    }

    [ContextMenu("Regenerate World")]
    public void GenerateWorld()
    {
        if (world == null) world = FindObjectOfType<ONeillWorld>();
        if (world == null) { Debug.LogError("Cylinder chunk manager needs a world.", this); return; }
        landStripes = Mathf.Max(1, landStripes);
        int sectors = landStripes * 2;
        angularChunks = Mathf.Max(sectors, Mathf.CeilToInt(angularChunks / (float)sectors) * sectors);
        longitudinalChunks = Mathf.Max(1, longitudinalChunks);
        angularResolution = Mathf.Max(1, angularResolution);
        longitudinalResolution = Mathf.Max(1, longitudinalResolution);
        if (generatedRoot != null)
        {
            generatedRoot.SetActive(false);
            if (Application.isPlaying) Destroy(generatedRoot);
            else DestroyImmediate(generatedRoot);
        }
        generatedRoot = new GameObject("Generated Cylinder");
        generatedRoot.transform.SetParent(transform, false);
        for (int z = 0; z < longitudinalChunks; z++)
            for (int a = 0; a < angularChunks; a++)
            {
                bool land = IsLand(a);
                CylinderChunk chunk = land ? CreateChunk<LandChunk>($"Land_{a}_{z}", landMaterial)
                    : (CylinderChunk)CreateChunk<CylinderWindowChunk>($"Glass_{a}_{z}", glassMaterial);
                chunk.Generate(world, a, z, angularChunks, longitudinalChunks,
                    angularResolution, longitudinalResolution, GetExposedEdges(a, z));
                if (land && waterMaterial != null)
                {
                    var water = CreateChunk<CylinderWaterChunk>($"Water_{a}_{z}", waterMaterial);
                    water.Generate(world, a, z, angularChunks, longitudinalChunks,
                        angularResolution, longitudinalResolution, ChunkEdges.None);
                }
            }
        CreateEndCap("EndCap_A", world.length * .5f, false);
        CreateEndCap("EndCap_B", -world.length * .5f, true);
    }

    private T CreateChunk<T>(string name, Material material) where T : CylinderChunk
    {
        var go = new GameObject(name);
        go.transform.SetParent(generatedRoot.transform, false);
        var chunk = go.AddComponent<T>();
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        if (chunk is CylinderWindowChunk || chunk is CylinderWaterChunk)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = chunk is CylinderWaterChunk;
        }
        return chunk;
    }

    private void CreateEndCap(string name, float z, bool facesPositiveZ)
    {
        var go = new GameObject(name);
        go.transform.SetParent(generatedRoot.transform, false);
        go.transform.localPosition = Vector3.forward * z;
        var cap = go.AddComponent<CylinderEndCap>();
        go.GetComponent<MeshRenderer>().sharedMaterial = glassMaterial;
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        cap.Generate(world.radius, 16, angularChunks * angularResolution, 100f, facesPositiveZ);
    }
}
