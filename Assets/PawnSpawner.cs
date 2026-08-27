using UnityEngine;

public class PawnSpawner : MonoBehaviour
{
    public static PawnSpawner Instance;

    [Header("Dependencies")]
    [Tooltip("The prefab that contains the Pawn.cs component.")]
    public GameObject pawnPrefab;

    [Header("Initial Spawning")]
    public int initialSpawnCount = 5;
    [Tooltip("How far around the player to scatter the initial pawns.")]
    public float spawnRadius = 20f;
    [Tooltip("Delay in seconds before spawning, to allow the VoxelWorld to generate terrain.")]
    public float initialSpawnDelay = 3.0f;

    private VoxelWorld world;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        world = FindObjectOfType<VoxelWorld>();

        if (pawnPrefab != null && initialSpawnCount > 0)
        {
            Invoke(nameof(SpawnInitialPawns), initialSpawnDelay);
        }
        else
        {
            Debug.LogWarning("PawnSpawner: No pawn prefab assigned or count is 0.");
        }
    }

    private void Update()
    {
        // Debug Hotkey: Press 'P' to spawn a pawn right in front of you
        if (Input.GetKeyDown(KeyCode.P))
        {
            Transform cam = Camera.main.transform;
            SpawnPawnAt(cam.position + cam.forward * 3f);
        }
    }

    public void SpawnInitialPawns()
    {
        if (pawnPrefab == null || world == null) return;

        Transform center = Camera.main != null ? Camera.main.transform : transform;

        for (int i = 0; i < initialSpawnCount; i++)
        {
            // Pick a random X/Z offset
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 targetPos = center.position + new Vector3(randomCircle.x, 0, randomCircle.y);

            SpawnPawnAt(targetPos);
        }

        Debug.Log($"[PawnSpawner] Spawned {initialSpawnCount} initial pawns.");
    }

    /// <summary>
    /// Spawns a pawn at the given X/Z coordinate. 
    /// Automatically drops them from the sky so they snap to the voxel surface.
    /// </summary>
    public Pawn SpawnPawnAt(Vector3 position)
    {
        if (pawnPrefab == null) return null;

        // Spawn at the very top of the world so Pawn.SnapToGround() can trace downwards to find the floor
        Vector3 safeSpawnPos = new Vector3(position.x, Chunk.CHUNK_HEIGHT - 1, position.z);

        GameObject newPawnObj = Instantiate(pawnPrefab, safeSpawnPos, Quaternion.identity);
        newPawnObj.name = $"Pawn_{Random.Range(1000, 9999)}";

        Pawn pawnScript = newPawnObj.GetComponent<Pawn>();
        return pawnScript;
    }
}