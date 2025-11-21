using System.Collections.Generic;
using UnityEngine;

public class VoxelWorld : MonoBehaviour
{
    [Header("World Settings")]
    public WorldPattern worldPattern;
    public int worldSizeChunksX = 10;
    public int worldSizeChunksZ = 10;

    [Header("References")]
    public Material defaultMaterial;

    // Dictionary to find chunks quickly by coordinate
    private Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();

    void Start()
    {
        if (worldPattern != null) GenerateWorld();
    }

    void Update()
    {
        // Debug Regenerate
        bool rightCtrl = Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (rightCtrl && shift && Input.GetKeyDown(KeyCode.R)) RegenerateWorld();
    }

    // --- INTERACTION API ---

    /// <summary>
    /// modifies a block at a specific world position and updates the relevant chunks.
    /// </summary>
    public void ModifyBlock(Vector3 worldPos, BlockData newBlock)
    {
        int x = Mathf.FloorToInt(worldPos.x);
        int y = Mathf.FloorToInt(worldPos.y);
        int z = Mathf.FloorToInt(worldPos.z);

        // 1. Determine which chunk this belongs to
        int cx = x / Chunk.CHUNK_SIZE;
        int cz = z / Chunk.CHUNK_SIZE;
        int lx = x % Chunk.CHUNK_SIZE;
        int lz = z % Chunk.CHUNK_SIZE;

        // Handle negative coordinates correctly if your world expands negative
        if (lx < 0) { lx += Chunk.CHUNK_SIZE; cx--; }
        if (lz < 0) { lz += Chunk.CHUNK_SIZE; cz--; }

        if (chunks.TryGetValue(new Vector2Int(cx, cz), out Chunk chunk))
        {
            // 2. Update the data
            chunk.SetBlock(lx, y, lz, newBlock);

            // 3. Update this chunk's mesh
            chunk.RegenerateMesh();

            // 4. Check Borders: If we modified a block on the edge, we must update the neighbor
            //    because the neighbor might have a face hidden by this block (or needs to hide one).

            if (lx == 0) UpdateChunkMesh(cx - 1, cz);
            if (lx == Chunk.CHUNK_SIZE - 1) UpdateChunkMesh(cx + 1, cz);
            if (lz == 0) UpdateChunkMesh(cx, cz - 1);
            if (lz == Chunk.CHUNK_SIZE - 1) UpdateChunkMesh(cx, cz + 1);
        }
    }

    void UpdateChunkMesh(int cx, int cz)
    {
        if (chunks.TryGetValue(new Vector2Int(cx, cz), out Chunk c))
        {
            c.RegenerateMesh();
        }
    }

    // --- GENERATION LOGIC ---

    public void RegenerateWorld()
    {
        ClearWorld();
        if (worldPattern != null) GenerateWorld();
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

        // 2. Populate Data
        int totalWidth = worldSizeChunksX * Chunk.CHUNK_SIZE;
        int totalDepth = worldSizeChunksZ * Chunk.CHUNK_SIZE;
        int currentBase = 0;

        foreach (WorldLayer layer in worldPattern.layers)
        {
            for (int x = 0; x < totalWidth; x++)
            {
                for (int z = 0; z < totalDepth; z++)
                {
                    int extra = 0;
                    if (layer.heightNoise != null && layer.maxAdditionalHeight > 0)
                    {
                        float nx = x * layer.noiseScale;
                        float nz = z * layer.noiseScale;
                        float n = layer.heightNoise.GetNoise(nx, nz);
                        extra = Mathf.RoundToInt(((n + 1f) * 0.5f * layer.noiseAmplitude) * layer.maxAdditionalHeight);
                    }

                    int layerTop = currentBase + layer.height + extra;

                    for (int y = currentBase; y < layerTop; y++)
                    {
                        SetBlockDataOnly(x, y, z, layer.blockData);
                    }
                }
            }
            currentBase += layer.height;
        }

        // 3. Build Meshes
        foreach (var chunk in chunks.Values)
        {
            chunk.RegenerateMesh();
        }
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

    // Helper for initial generation (doesn't trigger mesh rebuild)
    void SetBlockDataOnly(int x, int y, int z, BlockData data)
    {
        if (data == null) return;
        // Ensure material default
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