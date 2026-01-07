using System.Collections.Generic;
using UnityEngine;

public class VoxelWorld : MonoBehaviour
{
    // --- INSPECTOR SETTINGS ---
    [Header("1. World Configuration")]
    [Min(1)] public int worldSizeChunksX = 20;
    [Min(1)] public int worldSizeChunksZ = 20;
    public Material defaultMaterial;
    public BlockData bedrockBlock;

    [Header("2. Biome Strategy")]
    public VoxelBiomeSO[] biomes;
    public float temperatureScale = 0.05f;

    [Header("3. Edge Blending & Warping")]
    public float warpStrength = 15f;
    public float warpScale = 0.03f;

    [Header("4. Global Terrain Shape")]
    public float globalScale = 0.005f;
    public int globalAmplitude = 30;
    public int globalHeightOffset = 0;

    // --- INTERNAL DATA ---
    private int[,] chunkBiomeMap;
    private Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
    private float tempOffset;
    private float warpOffset;

    void Start()
    {
        tempOffset = Random.Range(0f, 10000f);
        warpOffset = Random.Range(0f, 10000f);
        GenerateWorld();
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.RightControl) && Input.GetKeyDown(KeyCode.R))
        {
            tempOffset = Random.Range(0f, 10000f);
            warpOffset = Random.Range(0f, 10000f);
            RegenerateWorld();
        }
    }

    // --- API (Interaction) ---
    public void ModifyBlock(Vector3 worldPos, BlockData newBlock)
    {
        int x = Mathf.FloorToInt(worldPos.x);
        int y = Mathf.FloorToInt(worldPos.y);
        int z = Mathf.FloorToInt(worldPos.z);

        SetBlockDataOnly(x, y, z, newBlock);

        Vector2Int coord = GetChunkCoord(x, z);
        int cx = coord.x;
        int cz = coord.y;

        int lx = x - (cx * Chunk.CHUNK_SIZE);
        int lz = z - (cz * Chunk.CHUNK_SIZE);

        UpdateChunkMesh(cx, cz);

        if (lx == 0) UpdateChunkMesh(cx - 1, cz);
        if (lx == Chunk.CHUNK_SIZE - 1) UpdateChunkMesh(cx + 1, cz);
        if (lz == 0) UpdateChunkMesh(cx, cz - 1);
        if (lz == Chunk.CHUNK_SIZE - 1) UpdateChunkMesh(cx, cz + 1);
    }

    public BlockData GetBlock(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x);
        int y = Mathf.FloorToInt(worldPos.y);
        int z = Mathf.FloorToInt(worldPos.z);
        return GetBlockDataOnly(x, y, z);
    }

    // --- GENERATION PIPELINE ---
    public void RegenerateWorld()
    {
        ClearWorld();
        GenerateWorld();
    }

    void ClearWorld()
    {
        foreach (var chunk in chunks.Values) if (chunk != null) Destroy(chunk.gameObject);
        chunks.Clear();
    }

    void GenerateWorld()
    {
        GenerateBiomeMap();

        for (int cx = 0; cx < worldSizeChunksX; cx++)
            for (int cz = 0; cz < worldSizeChunksZ; cz++)
                CreateChunk(cx, cz);

        int totalWidth = worldSizeChunksX * Chunk.CHUNK_SIZE;
        int totalDepth = worldSizeChunksZ * Chunk.CHUNK_SIZE;

        for (int x = 0; x < totalWidth; x++)
        {
            for (int z = 0; z < totalDepth; z++)
            {
                float warpX = (Mathf.PerlinNoise((x + warpOffset) * warpScale, (z + warpOffset) * warpScale) * 2f - 1f) * warpStrength;
                float warpZ = (Mathf.PerlinNoise((z + warpOffset) * warpScale, (x + warpOffset) * warpScale) * 2f - 1f) * warpStrength;

                float sampleX = x + warpX;
                float sampleZ = z + warpZ;

                float biomeHeight = GetBilinearSmoothedHeight(sampleX, sampleZ);
                float globalNoise = Mathf.PerlinNoise(sampleX * globalScale, sampleZ * globalScale) * globalAmplitude;

                float finalHeightFloat = biomeHeight + globalNoise + globalHeightOffset;

                int biomeCX = Mathf.Clamp(Mathf.FloorToInt(sampleX / Chunk.CHUNK_SIZE), 0, worldSizeChunksX - 1);
                int biomeCZ = Mathf.Clamp(Mathf.FloorToInt(sampleZ / Chunk.CHUNK_SIZE), 0, worldSizeChunksZ - 1);
                VoxelBiomeSO biome = biomes[chunkBiomeMap[biomeCX, biomeCZ]];

                int finalHeight = Mathf.RoundToInt(finalHeightFloat);
                for (int y = 0; y <= finalHeight; y++)
                {
                    BlockData block = null;
                    if (y == finalHeight) block = biome.surfaceBlock;
                    else if (y > finalHeight - 4) block = biome.subSurfaceBlock;
                    else block = bedrockBlock;

                    if (block != null) SetBlockDataOnly(x, y, z, block);
                }
            }
        }

        Pass_GenerateStructures(totalWidth, totalDepth);

        foreach (var chunk in chunks.Values) chunk.RegenerateMesh();
    }

    // --- DECORATION SYSTEM ---
    void Pass_GenerateStructures(int width, int depth)
    {
        Random.InitState(System.DateTime.Now.Millisecond);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                float warpX = (Mathf.PerlinNoise((x + warpOffset) * warpScale, (z + warpOffset) * warpScale) * 2f - 1f) * warpStrength;
                float warpZ = (Mathf.PerlinNoise((z + warpOffset) * warpScale, (x + warpOffset) * warpScale) * 2f - 1f) * warpStrength;

                int biomeCX = Mathf.Clamp(Mathf.FloorToInt((x + warpX) / Chunk.CHUNK_SIZE), 0, worldSizeChunksX - 1);
                int biomeCZ = Mathf.Clamp(Mathf.FloorToInt((z + warpZ) / Chunk.CHUNK_SIZE), 0, worldSizeChunksZ - 1);

                VoxelBiomeSO biome = biomes[chunkBiomeMap[biomeCX, biomeCZ]];

                if (biome.structures == null || biome.structures.Count == 0) continue;

                foreach (var structure in biome.structures)
                {
                    if (Random.value < structure.spawnDensity)
                    {
                        int surfaceY = GetSurfaceHeightAt(x, z);
                        BlockData ground = GetBlockDataOnly(x, surfaceY, z);
                        if (ground == biome.surfaceBlock)
                        {
                            SpawnStructure(x, surfaceY + 1 + structure.yOffset, z, structure);
                            break;
                        }
                    }
                }
            }
        }
    }

    void SpawnStructure(int rootX, int rootY, int rootZ, StructureDataSO structureData)
    {
        var blocks = structureData.GetStructure();

        // 1. Pick a Random Rotation (0, 1, 2, or 3)
        // 0 = 0 deg, 1 = 90 deg, 2 = 180 deg, 3 = 270 deg
        int rotation = Random.Range(0, 4);

        foreach (var kvp in blocks)
        {
            Vector3Int offset = kvp.Key;
            BlockData block = kvp.Value;

            // 2. Rotate the offset
            Vector3Int rotatedOffset = RotatePoint(offset, rotation);

            int finalX = rootX + rotatedOffset.x;
            int finalY = rootY + rotatedOffset.y;
            int finalZ = rootZ + rotatedOffset.z;

            SetBlockDataOnly(finalX, finalY, finalZ, block);

            // Foundation Fix (Extends structure down)
            if (offset.y == 0)
            {
                int checkY = finalY - 1;
                int safety = 0;
                while (GetBlockDataOnly(finalX, checkY, finalZ) == null && checkY > 0 && safety < 10)
                {
                    SetBlockDataOnly(finalX, checkY, finalZ, block);
                    checkY--;
                    safety++;
                }
            }
        }
    }

    // --- ROTATION HELPER ---
    Vector3Int RotatePoint(Vector3Int p, int rotation)
    {
        // Simple 2D rotation on X/Z plane
        if (rotation == 0) return p;                                // 0 degrees
        if (rotation == 1) return new Vector3Int(p.z, p.y, -p.x);   // 90 degrees
        if (rotation == 2) return new Vector3Int(-p.x, p.y, -p.z);  // 180 degrees
        return new Vector3Int(-p.z, p.y, p.x);                      // 270 degrees
    }

    // --- MATH HELPERS ---
    Vector2Int GetChunkCoord(int x, int z)
    {
        int cx = Mathf.FloorToInt((float)x / Chunk.CHUNK_SIZE);
        int cz = Mathf.FloorToInt((float)z / Chunk.CHUNK_SIZE);
        return new Vector2Int(cx, cz);
    }

    void SetBlockDataOnly(int x, int y, int z, BlockData data)
    {
        if (y < 0 || y >= Chunk.CHUNK_HEIGHT) return;

        Vector2Int coord = GetChunkCoord(x, z);
        int cx = coord.x;
        int cz = coord.y;
        int lx = x - (cx * Chunk.CHUNK_SIZE);
        int lz = z - (cz * Chunk.CHUNK_SIZE);

        if (cx >= 0 && cx < worldSizeChunksX && cz >= 0 && cz < worldSizeChunksZ)
        {
            if (chunks.TryGetValue(coord, out Chunk chunk))
                chunk.SetBlock(lx, y, lz, data);
        }
    }

    BlockData GetBlockDataOnly(int x, int y, int z)
    {
        if (y < 0 || y >= Chunk.CHUNK_HEIGHT) return null;

        Vector2Int coord = GetChunkCoord(x, z);
        int cx = coord.x;
        int cz = coord.y;
        int lx = x - (cx * Chunk.CHUNK_SIZE);
        int lz = z - (cz * Chunk.CHUNK_SIZE);

        if (cx >= 0 && cx < worldSizeChunksX && cz >= 0 && cz < worldSizeChunksZ)
        {
            if (chunks.TryGetValue(coord, out Chunk chunk))
                return chunk.GetBlock(lx, y, lz);
        }
        return null;
    }

    int GetSurfaceHeightAt(int x, int z)
    {
        for (int y = Chunk.CHUNK_HEIGHT - 1; y > 0; y--)
        {
            if (GetBlockDataOnly(x, y, z) != null) return y;
        }
        return 0;
    }

    void UpdateChunkMesh(int cx, int cz)
    {
        if (chunks.TryGetValue(new Vector2Int(cx, cz), out Chunk c)) c.RegenerateMesh();
    }

    // --- BIOME LOGIC ---
    float GetBilinearSmoothedHeight(float sampleX, float sampleZ)
    {
        float u = (sampleX / (float)Chunk.CHUNK_SIZE) - 0.5f;
        float v = (sampleZ / (float)Chunk.CHUNK_SIZE) - 0.5f;
        int x0 = Mathf.FloorToInt(u); int z0 = Mathf.FloorToInt(v);
        int x1 = x0 + 1; int z1 = z0 + 1;
        float s = u - x0; float t = v - z0;
        x0 = Mathf.Clamp(x0, 0, worldSizeChunksX - 1); x1 = Mathf.Clamp(x1, 0, worldSizeChunksX - 1);
        z0 = Mathf.Clamp(z0, 0, worldSizeChunksZ - 1); z1 = Mathf.Clamp(z1, 0, worldSizeChunksZ - 1);
        float h00 = CalculateBiomeHeight(sampleX, sampleZ, biomes[chunkBiomeMap[x0, z0]]);
        float h10 = CalculateBiomeHeight(sampleX, sampleZ, biomes[chunkBiomeMap[x1, z0]]);
        float h01 = CalculateBiomeHeight(sampleX, sampleZ, biomes[chunkBiomeMap[x0, z1]]);
        float h11 = CalculateBiomeHeight(sampleX, sampleZ, biomes[chunkBiomeMap[x1, z1]]);
        return Mathf.Lerp(Mathf.Lerp(h00, h10, s), Mathf.Lerp(h01, h11, s), t);
    }
    float CalculateBiomeHeight(float x, float z, VoxelBiomeSO biome)
    {
        return biome.baseHeight + (Mathf.PerlinNoise(x * biome.terrainScale, z * biome.terrainScale) * biome.terrainAmplitude);
    }
    void GenerateBiomeMap()
    {
        chunkBiomeMap = new int[worldSizeChunksX, worldSizeChunksZ];
        for (int x = 0; x < worldSizeChunksX; x++) for (int z = 0; z < worldSizeChunksZ; z++) chunkBiomeMap[x, z] = -1;
        int safety = 0;
        while (HasEmptySpots() && safety++ < 10000)
        {
            Vector2Int seed = GetRandomEmptyChunk(); if (seed.x == -1) break;
            float temp = Mathf.PerlinNoise((seed.x + tempOffset) * temperatureScale, (seed.y + tempOffset) * temperatureScale);
            int bIndex = PickBiomeForTemperature(temp);
            GrowBiome(seed, bIndex, biomes[bIndex].targetSize);
        }
    }
    void GrowBiome(Vector2Int start, int biomeIndex, int targetSize)
    {
        Queue<Vector2Int> q = new Queue<Vector2Int>(); q.Enqueue(start); chunkBiomeMap[start.x, start.y] = biomeIndex;
        int size = 1;
        while (q.Count > 0 && size < targetSize)
        {
            Vector2Int c = q.Dequeue();
            Vector2Int[] n = { c + Vector2Int.up, c + Vector2Int.down, c + Vector2Int.left, c + Vector2Int.right };
            Shuffle(n);
            foreach (var next in n)
            {
                if (next.x < 0 || next.x >= worldSizeChunksX || next.y < 0 || next.y >= worldSizeChunksZ) continue;
                if (chunkBiomeMap[next.x, next.y] == -1) { chunkBiomeMap[next.x, next.y] = biomeIndex; q.Enqueue(next); size++; }
            }
        }
    }
    int PickBiomeForTemperature(float temp)
    {
        List<int> c = new List<int>();
        for (int i = 0; i < biomes.Length; i++)
        {
            if (temp >= biomes[i].optimalTemperature - biomes[i].temperatureTolerance && temp <= biomes[i].optimalTemperature + biomes[i].temperatureTolerance) c.Add(i);
        }
        return c.Count == 0 ? 0 : c[Random.Range(0, c.Count)];
    }
    bool HasEmptySpots() { foreach (int i in chunkBiomeMap) if (i == -1) return true; return false; }
    Vector2Int GetRandomEmptyChunk()
    {
        for (int i = 0; i < 50; i++) { int x = Random.Range(0, worldSizeChunksX); int z = Random.Range(0, worldSizeChunksZ); if (chunkBiomeMap[x, z] == -1) return new Vector2Int(x, z); }
        for (int x = 0; x < worldSizeChunksX; x++) for (int z = 0; z < worldSizeChunksZ; z++) if (chunkBiomeMap[x, z] == -1) return new Vector2Int(x, z);
        return new Vector2Int(-1, -1);
    }
    void Shuffle<T>(T[] a) { for (int i = 0; i < a.Length; i++) { int r = Random.Range(i, a.Length); T t = a[r]; a[r] = a[i]; a[i] = t; } }
    void CreateChunk(int x, int z)
    {
        GameObject go = new GameObject($"Chunk_{x}_{z}"); go.transform.parent = transform; go.transform.position = new Vector3(x * Chunk.CHUNK_SIZE, 0, z * Chunk.CHUNK_SIZE);
        Chunk c = go.AddComponent<Chunk>(); c.chunkCoord = new Vector2Int(x, z); chunks[new Vector2Int(x, z)] = c;
    }
}