using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PawnSpawner : MonoBehaviour
{
    [System.Serializable]
    public class BiomePawnEntry
    {
        public string name;
        public GameObject prefab;
        [Range(0f, 10f)]
        public float density = 1.0f;
        public int minHeight = 5;
    }

    [Header("Configuration")]
    [Tooltip("Keep trying to spawn periodically until all pawns are placed.")]
    public bool retryUntilFull = true;

    [Header("Population List")]
    public List<BiomePawnEntry> populationList;

    private VoxelWorld world;
    private Dictionary<string, int> spawnCounts = new Dictionary<string, int>();

    void Start()
    {
        world = FindObjectOfType<VoxelWorld>();
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        // Initial delay to let the first chunks generate
        yield return new WaitForSeconds(1.0f);

        bool stillSpawning = true;

        while (stillSpawning)
        {
            stillSpawning = false;

            // Calculate limits
            int worldWidth = world.worldSizeChunksX * Chunk.CHUNK_SIZE;
            int worldDepth = world.worldSizeChunksZ * Chunk.CHUNK_SIZE;
            int totalChunks = world.worldSizeChunksX * world.worldSizeChunksZ;

            foreach (var entry in populationList)
            {
                if (entry.prefab == null) continue;

                // How many SHOULD exist?
                int targetCount = Mathf.RoundToInt(totalChunks * entry.density);

                // How many HAVE we spawned?
                if (!spawnCounts.ContainsKey(entry.name)) spawnCounts[entry.name] = 0;
                int currentCount = spawnCounts[entry.name];

                if (currentCount < targetCount)
                {
                    // Try to spawn batch
                    for (int i = 0; i < 5; i++) // Try 5 times per frame per species
                    {
                        if (currentCount >= targetCount) break;

                        if (TrySpawnSafe(entry, worldWidth, worldDepth))
                        {
                            currentCount++;
                            spawnCounts[entry.name] = currentCount;
                        }
                    }
                    // If we still need more, keep the loop running
                    if (currentCount < targetCount) stillSpawning = true;
                }
            }

            // Wait one frame before trying again
            yield return null;
        }

        Debug.Log("[PawnSpawner] All populations reached target density.");
    }

    bool TrySpawnSafe(BiomePawnEntry entry, int maxX, int maxZ)
    {
        int x = Random.Range(2, maxX - 2);
        int z = Random.Range(2, maxZ - 2);

        // 1. DATA CHECK: Ask the Block Data where the surface SHOULD be
        int dataHeight = GetDataSurfaceHeight(x, z);

        // If data says it's too low (water/void), abort early
        if (dataHeight <= entry.minHeight) return false;

        // 2. PHYSICS CHECK: Raycast to see where the Mesh actually is
        Vector3 skyPos = new Vector3(x + 0.5f, 250f, z + 0.5f);

        if (Physics.Raycast(skyPos, Vector3.down, out RaycastHit hit, 300f))
        {
            // 3. INTEGRITY CHECK (The Fix)
            // If the physics hit is way below the data height, the chunk isn't loaded yet.
            // Example: Data says Y=50, Raycast hits Y=0 (Bedrock). Difference is 50.
            if (Mathf.Abs(hit.point.y - dataHeight) > 2.0f)
            {
                return false; // Mesh not ready, try again later
            }

            // 4. SPAWN HIGH
            // Spawn 2 units ABOVE the hit point to ensure we don't clip into the floor.
            // The pawn's gravity will handle the landing.
            Vector3 spawnPos = hit.point + Vector3.up * 2.0f;

            Instantiate(entry.prefab, spawnPos, Quaternion.identity);
            return true;
        }

        return false;
    }

    // Helper: Finds the highest solid block in the DATA array
    int GetDataSurfaceHeight(int x, int z)
    {
        for (int y = Chunk.CHUNK_HEIGHT - 1; y > 0; y--)
        {
            if (world.GetBlock(new Vector3(x, y, z)) != null)
            {
                // Verify space above is clear (optional safety)
                if (world.GetBlock(new Vector3(x, y + 1, z)) == null)
                    return y;
            }
        }
        return 0;
    }
}