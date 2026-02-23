using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

public class VoxelWorld : MonoBehaviour
{
    [Header("1. Infinite Generation")]
    public Material defaultMaterial;
    public BlockData bedrockBlock;

    [Header("2. Player View Settings")]
    public Transform player;
    [Range(2, 32)] public int viewDistance = 8;
    public int unloadBuffer = 2;

    [Header("3. Biome Strategy (Voronoi)")]
    public VoxelBiomeSO[] biomes;
    [Tooltip("How large the 'Grown' biome patches are (in Chunks).")]
    public int biomeCellSize = 15;
    [Tooltip("Scale of the underlying temperature map.")]
    public float temperatureScale = 0.002f;
    public float biomeOffset = 5000f;

    [Header("4. Generation Noise")]
    public float warpStrength = 15f;
    public float warpScale = 0.03f;
    public float globalScale = 0.005f;
    public int globalAmplitude = 30;
    public int globalHeightOffset = 0;

    [Header("5. Async Performance")]
    public float maxMsPerFrame = 8f;

    [Header("6. Save / Load System")]
    public string saveFileName = "World1";
    public bool loadOnStart = true;

    // --- INTERNAL DATA ---
    private Dictionary<Vector2Int, Chunk> activeChunks = new Dictionary<Vector2Int, Chunk>();

    // Chunk Save Data Dictionary
    private Dictionary<Vector2Int, Chunk.ChunkSaveData> savedChunkData = new Dictionary<Vector2Int, Chunk.ChunkSaveData>();

    // Queues
    private HashSet<Vector2Int> chunksDataQueued = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> chunksMeshQueued = new HashSet<Vector2Int>();
    private List<Vector2Int> creationList = new List<Vector2Int>();
    private List<Chunk> meshingList = new List<Chunk>();

    private float warpNoiseOffset;
    private int seedOffset; // For deterministic biome seeds
    private Vector2Int lastPlayerChunkCoord = new Vector2Int(-999999, -999999);

    public bool IsGenerating { get; private set; }

    void Start()
    {
        if (player == null)
        {
            var pm = FindObjectOfType<PlayerMovement>();
            if (pm != null) player = pm.transform;
            else if (Camera.main != null) player = Camera.main.transform;
        }

        string path = Path.Combine(Application.persistentDataPath, saveFileName + ".save");
        if (loadOnStart && File.Exists(path))
        {
            LoadWorld();
        }
        else
        {
            warpNoiseOffset = UnityEngine.Random.Range(0f, 10000f);
            biomeOffset += UnityEngine.Random.Range(0f, 10000f);
            seedOffset = UnityEngine.Random.Range(0, 100000);
            StartCoroutine(ProcessWorldQueues());
        }
    }

    void Update()
    {
        if (player == null) return;

        Vector3 pPos = player.position;
        int pCx = Mathf.FloorToInt(pPos.x / Chunk.CHUNK_SIZE);
        int pCz = Mathf.FloorToInt(pPos.z / Chunk.CHUNK_SIZE);
        Vector2Int currentChunkCoord = new Vector2Int(pCx, pCz);

        if (currentChunkCoord != lastPlayerChunkCoord)
        {
            lastPlayerChunkCoord = currentChunkCoord;
            UpdateVisibleChunks(currentChunkCoord);
        }

        // Save/Load/Regen Hotkeys
        if (Input.GetKey(KeyCode.RightControl))
        {
            if (Input.GetKeyDown(KeyCode.R)) RegenerateWorld(true);  // True = Wipe everything and randomize
            if (Input.GetKeyDown(KeyCode.S)) SaveWorld();
            if (Input.GetKeyDown(KeyCode.L)) LoadWorld();
        }
    }

    // --- SAVE / LOAD LOGIC ---
    public void SaveWorld()
    {
        string path = Path.Combine(Application.persistentDataPath, saveFileName + ".save");
        using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create)))
        {
            // 1. Save Seeds
            writer.Write(warpNoiseOffset);
            writer.Write(biomeOffset);
            writer.Write(seedOffset);

            // 2. Ensure active modified chunks are in the dictionary before saving
            foreach (var kvp in activeChunks)
            {
                if (kvp.Value != null && kvp.Value.isModified)
                {
                    savedChunkData[kvp.Key] = kvp.Value.GetSaveData();
                }
            }

            // 3. Write chunk count
            writer.Write(savedChunkData.Count);

            // 4. Save Chunk Data
            int arrayLength = Chunk.CHUNK_SIZE * Chunk.CHUNK_HEIGHT * Chunk.CHUNK_SIZE;
            int shortArrayByteLength = arrayLength * 2;

            foreach (var kvp in savedChunkData)
            {
                writer.Write(kvp.Key.x);
                writer.Write(kvp.Key.y);
                writer.Write(kvp.Value.blocks);
                writer.Write(kvp.Value.fluidLevels);

                // Convert short[] to byte[] for fast writing
                byte[] waterBytes = new byte[shortArrayByteLength];
                Buffer.BlockCopy(kvp.Value.waterBodyIDs, 0, waterBytes, 0, shortArrayByteLength);
                writer.Write(waterBytes);
            }
        }
        Debug.Log($"World saved to: {path}");
    }

    public void LoadWorld()
    {
        string path = Path.Combine(Application.persistentDataPath, saveFileName + ".save");
        if (!File.Exists(path))
        {
            Debug.LogWarning("No save file found at " + path);
            return;
        }

        using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
        {
            // 1. Load Seeds
            warpNoiseOffset = reader.ReadSingle();
            biomeOffset = reader.ReadSingle();
            seedOffset = reader.ReadInt32();

            // 2. Load Chunk Data
            savedChunkData.Clear();
            int chunkCount = reader.ReadInt32();

            int arrayLength = Chunk.CHUNK_SIZE * Chunk.CHUNK_HEIGHT * Chunk.CHUNK_SIZE;
            int shortArrayByteLength = arrayLength * 2;

            for (int i = 0; i < chunkCount; i++)
            {
                Vector2Int coord = new Vector2Int(reader.ReadInt32(), reader.ReadInt32());
                Chunk.ChunkSaveData data = new Chunk.ChunkSaveData();

                data.blocks = reader.ReadBytes(arrayLength);
                data.fluidLevels = reader.ReadBytes(arrayLength);

                byte[] waterBytes = reader.ReadBytes(shortArrayByteLength);
                data.waterBodyIDs = new short[arrayLength];
                Buffer.BlockCopy(waterBytes, 0, data.waterBodyIDs, 0, shortArrayByteLength);

                savedChunkData[coord] = data;
            }
        }

        Debug.Log($"World loaded from: {path}");
        RegenerateWorld(false); // Reload with loaded seeds and dictionary, preventing randomized wipes
    }

    // --- CHUNK MANAGEMENT ---
    void UpdateVisibleChunks(Vector2Int center)
    {
        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                Vector2Int coord = new Vector2Int(center.x + x, center.y + z);
                if (!activeChunks.ContainsKey(coord) && !chunksDataQueued.Contains(coord))
                {
                    chunksDataQueued.Add(coord);
                    creationList.Add(coord);
                }
            }
        }

        List<Vector2Int> toRemove = new List<Vector2Int>();
        int unloadDist = viewDistance + unloadBuffer;

        foreach (var kvp in activeChunks)
        {
            int dist = Mathf.Max(Mathf.Abs(kvp.Key.x - center.x), Mathf.Abs(kvp.Key.y - center.y));
            if (dist > unloadDist) toRemove.Add(kvp.Key);
        }

        foreach (var coord in toRemove)
        {
            if (activeChunks.TryGetValue(coord, out Chunk c))
            {
                // Save modified chunks to memory before destroying
                if (c != null && c.isModified)
                {
                    savedChunkData[coord] = c.GetSaveData();
                }

                activeChunks.Remove(coord);
                if (c != null) Destroy(c.gameObject);
                if (chunksDataQueued.Contains(coord)) chunksDataQueued.Remove(coord);
                if (chunksMeshQueued.Contains(coord)) chunksMeshQueued.Remove(coord);
            }
        }
    }

    // --- ASYNC PROCESSOR ---
    IEnumerator ProcessWorldQueues()
    {
        System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();

        while (true)
        {
            IsGenerating = true;
            timer.Restart();

            // PHASE 1: PRIORITIZE
            if (player != null && creationList.Count > 0)
            {
                Vector2Int pCoord = GetChunkCoord(Mathf.FloorToInt(player.position.x), Mathf.FloorToInt(player.position.z));
                creationList.Sort((a, b) => {
                    float distA = (a - pCoord).sqrMagnitude;
                    float distB = (b - pCoord).sqrMagnitude;
                    return distA.CompareTo(distB);
                });
            }

            // PHASE 2: CREATE DATA
            while (creationList.Count > 0 && timer.ElapsedMilliseconds < maxMsPerFrame)
            {
                Vector2Int coord = creationList[0];
                creationList.RemoveAt(0);
                chunksDataQueued.Remove(coord);

                if (!IsChunkInLoadRange(coord)) continue;

                CreateChunkObject(coord.x, coord.y);

                // Load from memory if we have saved modifications, otherwise generate fresh
                if (savedChunkData.TryGetValue(coord, out Chunk.ChunkSaveData data))
                {
                    activeChunks[coord].LoadSaveData(data);
                }
                else
                {
                    GenerateChunkTerrainData(coord.x, coord.y);
                    GenerateStructuresForChunk(coord.x, coord.y);

                    // Reset flag so chunks aren't marked 'modified' just by spawning
                    if (activeChunks.TryGetValue(coord, out Chunk c))
                    {
                        c.isModified = false;
                    }
                }

                if (!chunksMeshQueued.Contains(coord))
                {
                    chunksMeshQueued.Add(coord);
                    meshingList.Add(activeChunks[coord]);
                }
            }

            // PHASE 3: GENERATE MESHES
            if (timer.ElapsedMilliseconds < maxMsPerFrame && meshingList.Count > 0)
            {
                if (player != null)
                {
                    Vector3 pPos = player.position;
                    meshingList.Sort((a, b) =>
                    {
                        if (a == null || b == null) return 0;
                        float distA = (a.transform.position - pPos).sqrMagnitude;
                        float distB = (b.transform.position - pPos).sqrMagnitude;
                        return distA.CompareTo(distB);
                    });
                }

                for (int i = meshingList.Count - 1; i >= 0; i--)
                {
                    if (timer.ElapsedMilliseconds >= maxMsPerFrame) break;

                    Chunk c = meshingList[i];
                    if (c == null) { meshingList.RemoveAt(i); continue; }

                    if (!activeChunks.ContainsKey(c.chunkCoord))
                    {
                        meshingList.RemoveAt(i);
                        continue;
                    }

                    if (AreNeighborsLoaded(c.chunkCoord))
                    {
                        c.RegenerateMesh();
                        chunksMeshQueued.Remove(c.chunkCoord);
                        meshingList.RemoveAt(i);
                    }
                }
            }

            IsGenerating = false;
            yield return null;
        }
    }

    // --- INFINITE VORONOI BIOMES ---
    VoxelBiomeSO GetBiomeVoronoi(int chunkX, int chunkZ)
    {
        int cellScale = Mathf.Max(1, biomeCellSize);
        int cellX = Mathf.FloorToInt((float)chunkX / cellScale);
        int cellZ = Mathf.FloorToInt((float)chunkZ / cellScale);

        float minDist = float.MaxValue;
        Vector2Int bestSeed = Vector2Int.zero;

        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                int cx = cellX + i;
                int cz = cellZ + j;

                UnityEngine.Random.InitState((cx * 8901) + (cz * 2345) + seedOffset);
                int localX = UnityEngine.Random.Range(0, cellScale);
                int localZ = UnityEngine.Random.Range(0, cellScale);

                Vector2Int seedPos = new Vector2Int(cx * cellScale + localX, cz * cellScale + localZ);
                float dist = Vector2Int.Distance(new Vector2Int(chunkX, chunkZ), seedPos);

                if (dist < minDist)
                {
                    minDist = dist;
                    bestSeed = seedPos;
                }
            }
        }

        float noise = Mathf.PerlinNoise((bestSeed.x + biomeOffset) * temperatureScale, (bestSeed.y + biomeOffset) * temperatureScale);
        return PickBiomeForTemperature(noise);
    }

    VoxelBiomeSO PickBiomeForTemperature(float temp)
    {
        VoxelBiomeSO bestBiome = biomes[0];
        float minDiff = 100f;
        List<VoxelBiomeSO> candidates = new List<VoxelBiomeSO>();
        foreach (var b in biomes)
        {
            if (temp >= b.optimalTemperature - b.temperatureTolerance && temp <= b.optimalTemperature + b.temperatureTolerance)
            {
                candidates.Add(b);
            }
        }
        if (candidates.Count > 0) return candidates[0];

        foreach (var b in biomes)
        {
            float diff = Mathf.Abs(temp - b.optimalTemperature);
            if (diff < minDiff) { minDiff = diff; bestBiome = b; }
        }
        return bestBiome;
    }

    // --- TERRAIN GENERATION ---
    void GenerateChunkTerrainData(int cx, int cz)
    {
        if (!activeChunks.TryGetValue(new Vector2Int(cx, cz), out Chunk chunk)) return;

        int startX = cx * Chunk.CHUNK_SIZE;
        int startZ = cz * Chunk.CHUNK_SIZE;
        VoxelBiomeSO chunkBiome = GetBiomeVoronoi(cx, cz);

        for (int x = 0; x < Chunk.CHUNK_SIZE; x++)
        {
            for (int z = 0; z < Chunk.CHUNK_SIZE; z++)
            {
                int worldX = startX + x;
                int worldZ = startZ + z;

                float wX = (Mathf.PerlinNoise((worldX + warpNoiseOffset) * warpScale, (worldZ + warpNoiseOffset) * warpScale) * 2f - 1f) * warpStrength;
                float wZ = (Mathf.PerlinNoise((worldZ + warpNoiseOffset) * warpScale, (worldX + warpNoiseOffset) * warpScale) * 2f - 1f) * warpStrength;
                float sX = worldX + wX;
                float sZ = worldZ + wZ;

                float biomeHeight = GetSmoothInfiniteBiomeHeight(sX, sZ);
                float globalNoise = Mathf.PerlinNoise(sX * globalScale, sZ * globalScale) * globalAmplitude;
                float finalHeightFloat = biomeHeight + globalNoise + globalHeightOffset;

                int finalHeight = Mathf.RoundToInt(finalHeightFloat);

                for (int y = 0; y <= finalHeight; y++)
                {
                    BlockData block = null;
                    if (y == finalHeight) block = chunkBiome.surfaceBlock;
                    else if (y > finalHeight - 4) block = chunkBiome.subSurfaceBlock;
                    else block = bedrockBlock;
                    if (block != null) chunk.SetBlock(x, y, z, block);
                }
            }
        }
    }

    float GetSmoothInfiniteBiomeHeight(float x, float z)
    {
        float u = (x / Chunk.CHUNK_SIZE);
        float v = (z / Chunk.CHUNK_SIZE);

        int x0 = Mathf.FloorToInt(u);
        int z0 = Mathf.FloorToInt(v);

        float s = u - x0;
        float t = v - z0;

        float h00 = GetBiomeHeightAtPos(x0, z0, x, z);
        float h10 = GetBiomeHeightAtPos(x0 + 1, z0, x, z);
        float h01 = GetBiomeHeightAtPos(x0, z0 + 1, x, z);
        float h11 = GetBiomeHeightAtPos(x0 + 1, z0 + 1, x, z);

        return Mathf.Lerp(Mathf.Lerp(h00, h10, s), Mathf.Lerp(h01, h11, s), t);
    }

    float GetBiomeHeightAtPos(int chunkX, int chunkZ, float worldX, float worldZ)
    {
        VoxelBiomeSO b = GetBiomeVoronoi(chunkX, chunkZ);
        return b.baseHeight + (Mathf.PerlinNoise(worldX * b.terrainScale, worldZ * b.terrainScale) * b.terrainAmplitude);
    }

    // --- STRUCTURES ---
    void GenerateStructuresForChunk(int cx, int cz)
    {
        if (!activeChunks.TryGetValue(new Vector2Int(cx, cz), out Chunk chunk)) return;

        VoxelBiomeSO biome = GetBiomeVoronoi(cx, cz);
        if (biome.structureGroups == null || biome.structureGroups.Count == 0) return;

        int startX = cx * Chunk.CHUNK_SIZE;
        int startZ = cz * Chunk.CHUNK_SIZE;

        for (int x = 0; x < Chunk.CHUNK_SIZE; x++)
        {
            for (int z = 0; z < Chunk.CHUNK_SIZE; z++)
            {
                int worldX = startX + x;
                int worldZ = startZ + z;
                foreach (var group in biome.structureGroups)
                {
                    float finalSpawnChance = 1.0f;
                    if (group.usePatchGeneration)
                    {
                        float noiseVal = Mathf.PerlinNoise(
                            (worldX + warpNoiseOffset + group.GetHashCode()) * group.patchScale,
                            (worldZ + warpNoiseOffset + group.GetHashCode()) * group.patchScale
                        );
                        if (noiseVal < group.patchThreshold) finalSpawnChance = 0f;
                        else { float range = 1.0f - group.patchThreshold; finalSpawnChance = (noiseVal - group.patchThreshold) / range; }
                    }
                    if (finalSpawnChance <= 0.001f) continue;
                    StructureDataSO structure = group.GetRandomStructure();
                    if (structure == null) continue;
                    if (UnityEngine.Random.value < (structure.spawnDensity * finalSpawnChance))
                    {
                        int y = GetSurfaceHeightAt(worldX, worldZ);
                        if (y <= 0) continue;
                        BlockData surface = GetBlockDataOnly(worldX, y, worldZ);
                        if (surface == biome.surfaceBlock)
                        {
                            SpawnStructure(worldX, y + 1 + structure.yOffset, worldZ, structure);
                            goto NextBlock;
                        }
                    }
                }
            NextBlock:;
            }
        }
    }

    // --- UTILITIES ---
    bool AreNeighborsLoaded(Vector2Int coord)
    {
        if (!activeChunks.ContainsKey(coord + Vector2Int.up)) return false;
        if (!activeChunks.ContainsKey(coord + Vector2Int.down)) return false;
        if (!activeChunks.ContainsKey(coord + Vector2Int.left)) return false;
        if (!activeChunks.ContainsKey(coord + Vector2Int.right)) return false;
        return true;
    }

    bool IsChunkInLoadRange(Vector2Int coord)
    {
        if (player == null) return false;
        int pCx = Mathf.FloorToInt(player.position.x / Chunk.CHUNK_SIZE);
        int pCz = Mathf.FloorToInt(player.position.z / Chunk.CHUNK_SIZE);
        int dist = Mathf.Max(Mathf.Abs(coord.x - pCx), Mathf.Abs(coord.y - pCz));
        return dist <= (viewDistance + unloadBuffer);
    }

    void CreateChunkObject(int x, int z)
    {
        if (activeChunks.ContainsKey(new Vector2Int(x, z))) return;
        GameObject go = new GameObject($"Chunk_{x}_{z}");
        go.transform.parent = transform;
        go.transform.position = new Vector3(x * Chunk.CHUNK_SIZE, 0, z * Chunk.CHUNK_SIZE);
        Chunk c = go.AddComponent<Chunk>();
        c.chunkCoord = new Vector2Int(x, z);
        activeChunks[new Vector2Int(x, z)] = c;
    }

    void SpawnStructure(int rootX, int rootY, int rootZ, StructureDataSO structureData)
    {
        var blocks = structureData.GetStructure();
        int rotation = UnityEngine.Random.Range(0, 4);
        foreach (var kvp in blocks)
        {
            Vector3Int offset = kvp.Key;
            BlockData block = kvp.Value;
            Vector3Int rotOffset = RotatePoint(offset, rotation);
            SetBlockDataOnly(rootX + rotOffset.x, rootY + rotOffset.y, rootZ + rotOffset.z, block);
        }
    }

    Vector3Int RotatePoint(Vector3Int p, int rotation)
    {
        if (rotation == 0) return p;
        if (rotation == 1) return new Vector3Int(p.z, p.y, -p.x);
        if (rotation == 2) return new Vector3Int(-p.x, p.y, -p.z);
        return new Vector3Int(-p.z, p.y, p.x);
    }

    public void ModifyBlock(Vector3 worldPos, BlockData newBlock)
    {
        int x = Mathf.FloorToInt(worldPos.x); int y = Mathf.FloorToInt(worldPos.y); int z = Mathf.FloorToInt(worldPos.z);
        SetBlockDataOnly(x, y, z, newBlock);
        Vector2Int c = GetChunkCoord(x, z);
        UpdateChunkMesh(c.x, c.y);
        UpdateChunkMesh(c.x + 1, c.y); UpdateChunkMesh(c.x - 1, c.y);
        UpdateChunkMesh(c.x, c.y + 1); UpdateChunkMesh(c.x, c.y - 1);
    }

    void SetBlockDataOnly(int x, int y, int z, BlockData data)
    {
        if (y < 0 || y >= Chunk.CHUNK_HEIGHT) return;
        Vector2Int coord = GetChunkCoord(x, z);
        if (activeChunks.TryGetValue(coord, out Chunk chunk))
        {
            int lx = x - (coord.x * Chunk.CHUNK_SIZE);
            int lz = z - (coord.y * Chunk.CHUNK_SIZE);
            chunk.SetBlock(lx, y, lz, data);
        }
    }

    BlockData GetBlockDataOnly(int x, int y, int z)
    {
        if (y < 0 || y >= Chunk.CHUNK_HEIGHT) return null;
        Vector2Int coord = GetChunkCoord(x, z);
        if (activeChunks.TryGetValue(coord, out Chunk chunk))
        {
            int lx = x - (coord.x * Chunk.CHUNK_SIZE);
            int lz = z - (coord.y * Chunk.CHUNK_SIZE);
            return chunk.GetBlock(lx, y, lz);
        }
        return null;
    }

    public BlockData GetBlock(Vector3 worldPos)
    {
        return GetBlockDataOnly(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y), Mathf.FloorToInt(worldPos.z));
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
        if (activeChunks.TryGetValue(new Vector2Int(cx, cz), out Chunk c)) c.RegenerateMesh();
    }

    public Chunk GetChunkByCoord(int cx, int cz)
    {
        if (activeChunks.TryGetValue(new Vector2Int(cx, cz), out Chunk c)) return c;
        return null;
    }

    Vector2Int GetChunkCoord(int x, int z)
    {
        return new Vector2Int(Mathf.FloorToInt((float)x / Chunk.CHUNK_SIZE), Mathf.FloorToInt((float)z / Chunk.CHUNK_SIZE));
    }

    public void RegenerateWorld(bool generateNewSeeds = true)
    {
        StopAllCoroutines();
        chunksMeshQueued.Clear(); chunksDataQueued.Clear(); creationList.Clear(); meshingList.Clear();

        if (generateNewSeeds)
        {
            savedChunkData.Clear();
            warpNoiseOffset = UnityEngine.Random.Range(0f, 10000f);
            seedOffset = UnityEngine.Random.Range(0, 100000);
        }

        foreach (var c in activeChunks.Values) if (c != null) Destroy(c.gameObject);
        activeChunks.Clear();

        lastPlayerChunkCoord = new Vector2Int(-999999, -999999);
        StartCoroutine(ProcessWorldQueues());
    }
}