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
        public float density = 0.5f; // Density per Chunk
        public int minHeight = 5;
        [HideInInspector] public List<GameObject> activeInstances = new List<GameObject>();
    }

    [Header("Configuration")]
    public float spawnCheckInterval = 1.0f;
    public float despawnDistanceBuffer = 32f; // Distance beyond view range to despawn

    [Header("Population List")]
    public List<BiomePawnEntry> populationList;

    private VoxelWorld world;
    private Transform player;

    void Start()
    {
        world = FindObjectOfType<VoxelWorld>();

        // Find player to spawn around
        var pm = FindObjectOfType<PlayerMovement>();
        if (pm != null) player = pm.transform;
        else if (Camera.main != null) player = Camera.main.transform;

        StartCoroutine(PopulationLoop());
    }

    // NEW: Clears all current pawns in the world
    public void ClearAllPawns()
    {
        foreach (var entry in populationList)
        {
            foreach (var pawn in entry.activeInstances)
            {
                if (pawn != null) Destroy(pawn);
            }
            entry.activeInstances.Clear();
        }
    }

    IEnumerator PopulationLoop()
    {
        // Wait until world has started generating
        while (world.IsGenerating) yield return new WaitForSeconds(0.5f);
        yield return new WaitForSeconds(1.0f);

        while (true)
        {
            if (player == null)
            {
                yield return null;
                continue;
            }

            // 1. DESPAWN STEP (Cleanup old pawns)
            CleanupDistantPawns();

            // 2. SPAWN STEP
            // Calculate how many loaded chunks we effectively have
            // (Assuming a square area around player based on view distance)
            int viewDist = world.viewDistance;
            int loadedChunkCount = (viewDist * 2 + 1) * (viewDist * 2 + 1);

            foreach (var entry in populationList)
            {
                if (entry.prefab == null) continue;

                // Target count is based on currently visible area, not total world size
                int targetCount = Mathf.CeilToInt(loadedChunkCount * entry.density);

                // Clean nulls from list (in case they were destroyed externally)
                entry.activeInstances.RemoveAll(x => x == null);

                if (entry.activeInstances.Count < targetCount)
                {
                    // Attempt to spawn a batch
                    int attempts = 5;
                    while (attempts > 0 && entry.activeInstances.Count < targetCount)
                    {
                        if (TrySpawnAroundPlayer(entry))
                        {
                            // Success
                        }
                        attempts--;
                    }
                }
            }

            yield return new WaitForSeconds(spawnCheckInterval);
        }
    }

    void CleanupDistantPawns()
    {
        float maxDist = (world.viewDistance * Chunk.CHUNK_SIZE) + despawnDistanceBuffer;
        float maxDistSqr = maxDist * maxDist;
        Vector3 playerPos = player.position;

        foreach (var entry in populationList)
        {
            for (int i = entry.activeInstances.Count - 1; i >= 0; i--)
            {
                GameObject pawn = entry.activeInstances[i];
                if (pawn == null) continue;

                if ((pawn.transform.position - playerPos).sqrMagnitude > maxDistSqr)
                {
                    Destroy(pawn);
                    entry.activeInstances.RemoveAt(i);
                }
            }
        }
    }

    bool TrySpawnAroundPlayer(BiomePawnEntry entry)
    {
        // Pick a random spot within View Distance
        int radius = world.viewDistance * Chunk.CHUNK_SIZE;
        Vector3 playerPos = player.position;

        int rX = Mathf.RoundToInt(playerPos.x + Random.Range(-radius, radius));
        int rZ = Mathf.RoundToInt(playerPos.z + Random.Range(-radius, radius));

        // 1. DATA CHECK: Ask the Block Data where the surface SHOULD be
        BlockData surfaceBlock = world.GetBlock(new Vector3(rX, 0, rZ)); // Just checking if chunk exists
        if (surfaceBlock == null) return false; // Chunk not loaded yet

        int dataHeight = GetDataSurfaceHeight(rX, rZ);
        if (dataHeight <= entry.minHeight) return false; // Too low/water

        // 2. PHYSICS CHECK: Raycast to see if the Mesh is ready
        // We start high up to ensure we hit the terrain
        Vector3 skyPos = new Vector3(rX + 0.5f, 250f, rZ + 0.5f);

        if (Physics.Raycast(skyPos, Vector3.down, out RaycastHit hit, 300f))
        {
            // Integrity Check: Is the mesh roughly where the data says it is?
            // If the mesh hasn't generated yet, the raycast might hit nothing or something else.
            if (Mathf.Abs(hit.point.y - dataHeight) > 3.0f)
            {
                return false; // Mesh not synced with data yet
            }

            // 3. SPAWN
            Vector3 spawnPos = hit.point + Vector3.up * 1.0f;
            GameObject newPawn = Instantiate(entry.prefab, spawnPos, Quaternion.identity);

            // Keep hierarchy clean
            newPawn.transform.parent = this.transform;

            entry.activeInstances.Add(newPawn);
            return true;
        }

        return false;
    }

    // Helper: Finds the highest solid block in the DATA array
    int GetDataSurfaceHeight(int x, int z)
    {
        // We iterate down from max height to find the first solid block
        for (int y = Chunk.CHUNK_HEIGHT - 1; y > 0; y--)
        {
            if (world.GetBlock(new Vector3(x, y, z)) != null)
            {
                // Ensure space above is empty
                if (world.GetBlock(new Vector3(x, y + 1, z)) == null)
                    return y;
            }
        }
        return 0;
    }
}