using System.Collections.Generic;
using UnityEngine;

public class VoxelWorld : MonoBehaviour
{
    [Header("Biome Settings")]
    [Tooltip("Lower number = Larger Biomes")]
    public float biomeScale = 0.01f;
    public VoxelBiomeSO[] biomes;    // Ensure these are ordered logically (e.g., Flat -> Hilly -> Mountain)

    [Header("World Settings")]
    public int worldSizeChunksX = 5;
    public int worldSizeChunksZ = 5;
    public Material defaultMaterial;
    public BlockData bedrockBlock;

    // Dictionary to find chunks quickly
    private Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();

    void Start()
    {
        GenerateWorld();
    }

    void Update()
    {
        // Debug Regenerate (Right Ctrl + R)
        if (Input.GetKey(KeyCode.RightControl) && Input.GetKeyDown(KeyCode.R))
            RegenerateWorld();
    }

    // --- INTERACTION API ---

    public void ModifyBlock(Vector3 worldPos, BlockData newBlock)
    {
        int x = Mathf.FloorToInt(worldPos.x);
        int y = Mathf.FloorToInt(worldPos.y);
        int z = Mathf.FloorToInt(worldPos.z);

        int cx = x / Chunk.CHUNK_SIZE;
        int cz = z / Chunk.CHUNK_SIZE;
        int lx = x % Chunk.CHUNK_SIZE;
        int lz = z % Chunk.CHUNK_SIZE;

        if (lx < 0) { lx += Chunk.CHUNK_SIZE; cx--; }
        if (lz < 0) { lz += Chunk.CHUNK_SIZE; cz--; }

        if (chunks.TryGetValue(new Vector2Int(cx, cz), out Chunk chunk))
        {
            chunk.SetBlock(lx, y, lz, newBlock);
            chunk.RegenerateMesh();

            // Border Updates
            if (lx == 0) UpdateChunkMesh(cx - 1, cz);
            if (lx == Chunk.CHUNK_SIZE - 1) UpdateChunkMesh(cx + 1, cz);
            if (lz == 0) UpdateChunkMesh(cx, cz - 1);
            if (lz == Chunk.CHUNK_SIZE - 1) UpdateChunkMesh(cx, cz + 1);
        }
    }

    public BlockData GetBlock(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x);
        int y = Mathf.FloorToInt(worldPos.y);
        int z = Mathf.FloorToInt(worldPos.z);

        int cx = x / Chunk.CHUNK_SIZE;
        int cz = z / Chunk.CHUNK_SIZE;
        int lx = x % Chunk.CHUNK_SIZE;
        int lz = z % Chunk.CHUNK_SIZE;

        if (lx < 0) { lx += Chunk.CHUNK_SIZE; cx--; }
        if (lz < 0) { lz += Chunk.CHUNK_SIZE; cz--; }

        if (chunks.TryGetValue(new Vector2Int(cx, cz), out Chunk chunk))
            return chunk.GetBlock(lx, y, lz);

        return null;
    }

    void UpdateChunkMesh(int cx, int cz)
    {
        if (chunks.TryGetValue(new Vector2Int(cx, cz), out Chunk c)) c.RegenerateMesh();
    }

    // --- GENERATION LOGIC ---

    public void RegenerateWorld()
    {
        ClearWorld();
        GenerateWorld();
    }

    void ClearWorld()
    {
        foreach (var chunk in chunks.Values)
        {
            if (chunk != null) Destroy(chunk.gameObject);
        }
        chunks.Clear();
    }

    void GenerateWorld()
    {
        // 1. Initialize Chunks
        for (int cx = 0; cx < worldSizeChunksX; cx++)
        {
            for (int cz = 0; cz < worldSizeChunksZ; cz++)
            {
                CreateChunk(cx, cz);
            }
        }

        // 2. Populate Data with Blended Biomes
        int totalWidth = worldSizeChunksX * Chunk.CHUNK_SIZE;
        int totalDepth = worldSizeChunksZ * Chunk.CHUNK_SIZE;

        for (int x = 0; x < totalWidth; x++)
        {
            for (int z = 0; z < totalDepth; z++)
            {
                // A. Calculate Blended Height
                // We get the height by blending the two closest biomes
                float finalHeightFloat = GetBlendedHeight(x, z, out VoxelBiomeSO dominantBiome);
                int finalHeight = Mathf.RoundToInt(finalHeightFloat);

                // B. Fill the Column
                for (int y = 0; y <= finalHeight; y++)
                {
                    BlockData blockToPlace = null;

                    // Use the Dominant Biome for block types (Grass vs Sand)
                    // (We don't blend blocks, only height, to avoid checkerboard patterns)
                    if (y == finalHeight)
                        blockToPlace = dominantBiome.surfaceBlock;
                    else if (y > finalHeight - 4)
                        blockToPlace = dominantBiome.subSurfaceBlock;
                    else
                        blockToPlace = bedrockBlock;

                    if (blockToPlace != null)
                        SetBlockDataOnly(x, y, z, blockToPlace);
                }
            }
        }

        // 3. Build Meshes
        foreach (var chunk in chunks.Values)
        {
            chunk.RegenerateMesh();
        }
    }

    // --- BIOME BLENDING LOGIC ---

    float GetBlendedHeight(int x, int z, out VoxelBiomeSO dominantBiome)
    {
        if (biomes == null || biomes.Length == 0)
        {
            dominantBiome = null;
            return 1;
        }

        // 1. Get raw noise (0.0 to 1.0)
        float noise = Mathf.PerlinNoise((x + 5000) * biomeScale, (z + 5000) * biomeScale);

        // 2. Map noise to our array range (e.g. if we have 3 biomes, range is 0.0 to 2.0)
        float mappedNoise = noise * (biomes.Length - 1);

        // 3. Find neighbors
        int indexA = Mathf.FloorToInt(mappedNoise);
        int indexB = Mathf.CeilToInt(mappedNoise);

        // Clamp just in case
        indexA = Mathf.Clamp(indexA, 0, biomes.Length - 1);
        indexB = Mathf.Clamp(indexB, 0, biomes.Length - 1);

        // 4. Calculate Blend Factor (t)
        // If mappedNoise is 1.5, t is 0.5.
        float t = mappedNoise - indexA;

        // 5. Get Heights for BOTH biomes
        float heightA = CalculateBiomeHeight(x, z, biomes[indexA]);
        float heightB = CalculateBiomeHeight(x, z, biomes[indexB]);

        // 6. Output Dominant Biome (for block color)
        // If t < 0.5, we are closer to Biome A.
        dominantBiome = (t < 0.5f) ? biomes[indexA] : biomes[indexB];

        // 7. Blend!
        // SmoothStep makes the transition curved (S-shape) rather than linear
        return Mathf.Lerp(heightA, heightB, Mathf.SmoothStep(0f, 1f, t));
    }

    float CalculateBiomeHeight(int x, int z, VoxelBiomeSO biome)
    {
        // Pure height calculation for a single biome
        float noise = Mathf.PerlinNoise(x * biome.terrainScale, z * biome.terrainScale);
        return biome.baseHeight + (noise * biome.terrainAmplitude);
    }

    void CreateChunk(int x, int z)
    {
        GameObject go = new GameObject($"Chunk_{x}_{z}");
        go.transform.parent = this.transform;
        go.transform.position = new Vector3(x * Chunk.CHUNK_SIZE, 0, z * Chunk.CHUNK_SIZE);

        Chunk c = go.AddComponent<Chunk>();
        c.chunkCoord = new Vector2Int(x, z);
        chunks.Add(new Vector2Int(x, z), c);
    }

    void SetBlockDataOnly(int x, int y, int z, BlockData data)
    {
        if (data == null) return;
        if (data.blockMaterial == null && defaultMaterial != null) data.blockMaterial = defaultMaterial;

        int cx = x / Chunk.CHUNK_SIZE;
        int cz = z / Chunk.CHUNK_SIZE;
        int lx = x % Chunk.CHUNK_SIZE;
        int lz = z % Chunk.CHUNK_SIZE;

        if (chunks.TryGetValue(new Vector2Int(cx, cz), out Chunk chunk))
        {
            chunk.SetBlock(lx, y, lz, data);
        }
    }
}