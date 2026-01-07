using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class VoxelWorld : MonoBehaviour
{
    // --- INSPECTOR SETTINGS ---

    [Header("1. World Configuration")]
    [Tooltip("How many chunks wide (X axis) the world is.")]
    [Min(1)]
    public int worldSizeChunksX = 20;

    [Tooltip("How many chunks deep (Z axis) the world is.")]
    [Min(1)]
    public int worldSizeChunksZ = 20;

    [Tooltip("The material applied to all chunk meshes.")]
    public Material defaultMaterial;

    [Tooltip("The block used for the absolute bottom layer of the map.")]
    public BlockData bedrockBlock;

    [Space(10)]
    [Header("2. Biome Strategy")]
    [Tooltip("List of all available biomes. Order does not matter, but keeping them organized helps.")]
    public VoxelBiomeSO[] biomes;

    [Tooltip("Controls the zoom level of the invisible Temperature Map. \nLow (0.01) = Huge climate zones. \nHigh (0.1) = Chaos.")]
    public float temperatureScale = 0.05f;

    [Space(10)]
    [Header("3. Edge Blending & Warping")]
    [Tooltip("Distorts the biome borders so they aren't perfect squares. \n0 = Straight Grid Lines. \n15 = Nice Diagonal/Jagged Edges.")]
    public float warpStrength = 15f;

    [Tooltip("The frequency of the border distortion. \n0.02 = Long smooth waves. \n0.1 = Sharp jittery edges.")]
    public float warpScale = 0.03f;

    // --- INTERNAL DATA ---
    // Hidden from Inspector to prevent lag/clutter
    private int[,] chunkBiomeMap;
    private Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
    private float tempOffset;
    private float warpOffset;

    // --- UNITY LIFECYCLE ---

    void Start()
    {
        // Randomize seeds on start
        tempOffset = Random.Range(0f, 10000f);
        warpOffset = Random.Range(0f, 10000f);
        GenerateWorld();
    }

    void Update()
    {
        // Debug Hotkey
        if (Input.GetKey(KeyCode.RightControl) && Input.GetKeyDown(KeyCode.R))
        {
            tempOffset = Random.Range(0f, 10000f);
            warpOffset = Random.Range(0f, 10000f);
            RegenerateWorld();
        }
    }

    // --- API & HELPERS ---

    /// <summary>
    /// Updates a specific block in the world and rebuilds the mesh. 
    /// Handles chunk borders automatically.
    /// </summary>
    public void ModifyBlock(Vector3 worldPos, BlockData newBlock)
    {
        int x = Mathf.FloorToInt(worldPos.x);
        int y = Mathf.FloorToInt(worldPos.y);
        int z = Mathf.FloorToInt(worldPos.z);

        // Convert world coordinate to Chunk + Local coordinate
        int cx = x / Chunk.CHUNK_SIZE;
        int cz = z / Chunk.CHUNK_SIZE;
        int lx = x % Chunk.CHUNK_SIZE;
        int lz = z % Chunk.CHUNK_SIZE;

        // Negative coordinate handling
        if (lx < 0) { lx += Chunk.CHUNK_SIZE; cx--; }
        if (lz < 0) { lz += Chunk.CHUNK_SIZE; cz--; }

        if (chunks.TryGetValue(new Vector2Int(cx, cz), out Chunk chunk))
        {
            chunk.SetBlock(lx, y, lz, newBlock);
            chunk.RegenerateMesh();

            // If on edge, update neighbor to hide/show faces
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

    // --- GENERATION PIPELINE ---

    public void RegenerateWorld()
    {
        ClearWorld();
        GenerateWorld();
    }

    void ClearWorld()
    {
        foreach (var chunk in chunks.Values)
            if (chunk != null) Destroy(chunk.gameObject);
        chunks.Clear();
    }

    void GenerateWorld()
    {
        // Step 1: Strategy (Biome Map)
        GenerateBiomeMap();

        int totalWidth = worldSizeChunksX * Chunk.CHUNK_SIZE;
        int totalDepth = worldSizeChunksZ * Chunk.CHUNK_SIZE;

        // Step 2: Execution (Chunk Building)
        for (int cx = 0; cx < worldSizeChunksX; cx++)
        {
            for (int cz = 0; cz < worldSizeChunksZ; cz++)
            {
                CreateChunk(cx, cz);

                for (int lx = 0; lx < Chunk.CHUNK_SIZE; lx++)
                {
                    for (int lz = 0; lz < Chunk.CHUNK_SIZE; lz++)
                    {
                        int worldX = cx * Chunk.CHUNK_SIZE + lx;
                        int worldZ = cz * Chunk.CHUNK_SIZE + lz;

                        // -- DOMAIN WARPING --
                        // Distort the "Sample Coordinate" to create wavy/jagged borders
                        float warpX = (Mathf.PerlinNoise((worldX + warpOffset) * warpScale, (worldZ + warpOffset) * warpScale) * 2f - 1f) * warpStrength;
                        float warpZ = (Mathf.PerlinNoise((worldZ + warpOffset) * warpScale, (worldX + warpOffset) * warpScale) * 2f - 1f) * warpStrength;

                        float sampleX = worldX + warpX;
                        float sampleZ = worldZ + warpZ;

                        // Use warped coord for HEIGHT (Smooth slope matches jagged border)
                        float height = GetBilinearSmoothedHeight(sampleX, sampleZ);

                        // Use warped coord for BIOME SELECTION (Jagged borders)
                        int biomeCX = Mathf.FloorToInt(sampleX / Chunk.CHUNK_SIZE);
                        int biomeCZ = Mathf.FloorToInt(sampleZ / Chunk.CHUNK_SIZE);

                        biomeCX = Mathf.Clamp(biomeCX, 0, worldSizeChunksX - 1);
                        biomeCZ = Mathf.Clamp(biomeCZ, 0, worldSizeChunksZ - 1);

                        VoxelBiomeSO biome = biomes[chunkBiomeMap[biomeCX, biomeCZ]];

                        // Fill Column
                        int finalHeight = Mathf.RoundToInt(height);
                        for (int y = 0; y <= finalHeight; y++)
                        {
                            BlockData block = null;
                            if (y == finalHeight) block = biome.surfaceBlock; // Top
                            else if (y > finalHeight - 4) block = biome.subSurfaceBlock; // Middle
                            else block = bedrockBlock; // Bottom

                            if (block != null)
                                SetBlockDataOnly(worldX, y, worldZ, block);
                        }
                    }
                }
            }
        }

        // Step 3: Meshing
        foreach (var chunk in chunks.Values) chunk.RegenerateMesh();
    }

    // --- HEIGHT BLENDING (Bilinear Interpolation) ---

    float GetBilinearSmoothedHeight(float sampleX, float sampleZ)
    {
        // Normalize to "Chunk Space" (0, 1, 2...)
        float u = (sampleX / (float)Chunk.CHUNK_SIZE) - 0.5f;
        float v = (sampleZ / (float)Chunk.CHUNK_SIZE) - 0.5f;

        int x0 = Mathf.FloorToInt(u);
        int z0 = Mathf.FloorToInt(v);
        int x1 = x0 + 1;
        int z1 = z0 + 1;

        float s = u - x0;
        float t = v - z0;

        // Clamp to world
        x0 = Mathf.Clamp(x0, 0, worldSizeChunksX - 1);
        x1 = Mathf.Clamp(x1, 0, worldSizeChunksX - 1);
        z0 = Mathf.Clamp(z0, 0, worldSizeChunksZ - 1);
        z1 = Mathf.Clamp(z1, 0, worldSizeChunksZ - 1);

        // Sample 4 corners
        float h00 = CalculateBiomeHeight(sampleX, sampleZ, biomes[chunkBiomeMap[x0, z0]]);
        float h10 = CalculateBiomeHeight(sampleX, sampleZ, biomes[chunkBiomeMap[x1, z0]]);
        float h01 = CalculateBiomeHeight(sampleX, sampleZ, biomes[chunkBiomeMap[x0, z1]]);
        float h11 = CalculateBiomeHeight(sampleX, sampleZ, biomes[chunkBiomeMap[x1, z1]]);

        // Interpolate
        float bottomLerp = Mathf.Lerp(h00, h10, s);
        float topLerp = Mathf.Lerp(h01, h11, s);
        return Mathf.Lerp(bottomLerp, topLerp, t);
    }

    float CalculateBiomeHeight(float x, float z, VoxelBiomeSO biome)
    {
        return biome.baseHeight +
               (Mathf.PerlinNoise(x * biome.terrainScale, z * biome.terrainScale) * biome.terrainAmplitude);
    }

    // --- MAP GENERATION (Hybrid Region Growing) ---

    void GenerateBiomeMap()
    {
        chunkBiomeMap = new int[worldSizeChunksX, worldSizeChunksZ];
        // Initialize as -1 (Empty)
        for (int x = 0; x < worldSizeChunksX; x++)
            for (int z = 0; z < worldSizeChunksZ; z++)
                chunkBiomeMap[x, z] = -1;

        int safetyLoop = 0;
        while (HasEmptySpots() && safetyLoop < 10000)
        {
            safetyLoop++;
            Vector2Int seed = GetRandomEmptyChunk();
            if (seed.x == -1) break;

            // Pick biome based on temperature at this seed
            float temp = Mathf.PerlinNoise((seed.x + tempOffset) * temperatureScale, (seed.y + tempOffset) * temperatureScale);
            int biomeIndex = PickBiomeForTemperature(temp);
            VoxelBiomeSO selectedBiome = biomes[biomeIndex];

            // Flood fill
            GrowBiome(seed, biomeIndex, selectedBiome.targetSize);
        }
    }

    void GrowBiome(Vector2Int start, int biomeIndex, int targetSize)
    {
        Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        frontier.Enqueue(start);
        chunkBiomeMap[start.x, start.y] = biomeIndex;
        int currentSize = 1;

        while (frontier.Count > 0 && currentSize < targetSize)
        {
            Vector2Int current = frontier.Dequeue();
            Vector2Int[] neighbors = {
                current + Vector2Int.up, current + Vector2Int.down,
                current + Vector2Int.left, current + Vector2Int.right
            };
            Shuffle(neighbors);

            foreach (var n in neighbors)
            {
                if (n.x < 0 || n.x >= worldSizeChunksX || n.y < 0 || n.y >= worldSizeChunksZ) continue;
                if (chunkBiomeMap[n.x, n.y] == -1)
                {
                    chunkBiomeMap[n.x, n.y] = biomeIndex;
                    frontier.Enqueue(n);
                    currentSize++;
                    if (currentSize >= targetSize) return;
                }
            }
        }
    }

    int PickBiomeForTemperature(float temp)
    {
        List<int> candidates = new List<int>();
        for (int i = 0; i < biomes.Length; i++)
        {
            float min = biomes[i].optimalTemperature - biomes[i].temperatureTolerance;
            float max = biomes[i].optimalTemperature + biomes[i].temperatureTolerance;
            if (temp >= min && temp <= max) candidates.Add(i);
        }
        if (candidates.Count == 0) return 0; // Default to first biome
        return candidates[Random.Range(0, candidates.Count)];
    }

    // --- HELPERS ---

    bool HasEmptySpots()
    {
        foreach (int id in chunkBiomeMap) if (id == -1) return true;
        return false;
    }

    Vector2Int GetRandomEmptyChunk()
    {
        // Fast random check
        for (int i = 0; i < 50; i++)
        {
            int x = Random.Range(0, worldSizeChunksX);
            int z = Random.Range(0, worldSizeChunksZ);
            if (chunkBiomeMap[x, z] == -1) return new Vector2Int(x, z);
        }
        // Slow fallback check
        for (int x = 0; x < worldSizeChunksX; x++)
            for (int z = 0; z < worldSizeChunksZ; z++)
                if (chunkBiomeMap[x, z] == -1) return new Vector2Int(x, z);
        return new Vector2Int(-1, -1);
    }

    void Shuffle<T>(T[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            int r = Random.Range(i, array.Length);
            T temp = array[r];
            array[r] = array[i];
            array[i] = temp;
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

    void SetBlockDataOnly(int x, int y, int z, BlockData data)
    {
        if (data == null) return;
        int cx = x / Chunk.CHUNK_SIZE;
        int cz = z / Chunk.CHUNK_SIZE;
        int lx = x % Chunk.CHUNK_SIZE;
        int lz = z % Chunk.CHUNK_SIZE;
        if (chunks.TryGetValue(new Vector2Int(cx, cz), out Chunk chunk))
            chunk.SetBlock(lx, y, lz, data);
    }
}