using UnityEngine;

public class VoxelTickManager : MonoBehaviour
{
    public static VoxelTickManager Instance;

    public VoxelWorld world;

    [Header("Tick Settings")]
    [Tooltip("Number of random blocks ticked per active chunk every tick cycle. Higher = faster growth.")]
    public int randomTicksPerChunk = 3;
    [Tooltip("How often (in simulation seconds) the tick cycle runs.")]
    public float tickRate = 1.0f;

    private float timer;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (world == null) world = FindObjectOfType<VoxelWorld>();
    }

    void Update()
    {
        if (world == null || world.IsGenerating) return;

        // Ensure SimulationClock exists before using it to avoid startup order errors
        if (SimulationClock.Instance == null) return;

        // Use SimulationDeltaTime instead of standard Time.deltaTime
        // When the game is paused, this adds 0, naturally freezing the tick cycle!
        timer += SimulationClock.Instance.SimulationDeltaTime;

        if (timer >= tickRate)
        {
            timer = 0f;
            DoRandomTicks();
        }
    }

    void DoRandomTicks()
    {
        if (world.ActiveChunks == null) return;

        foreach (var chunk in world.ActiveChunks)
        {
            if (chunk == null) continue;

            for (int i = 0; i < randomTicksPerChunk; i++)
            {
                // Pick a random block coordinate within the chunk
                int x = Random.Range(0, Chunk.CHUNK_SIZE);
                int y = Random.Range(0, Chunk.CHUNK_HEIGHT);
                int z = Random.Range(0, Chunk.CHUNK_SIZE);

                byte id = chunk.GetBlockID(x, y, z);
                if (id == 0) continue;

                BlockData block = BlockManager.Instance.GetBlockData(id);
                if (block != null && block.isGrowable)
                {
                    AttemptGrowth(chunk, x, y, z, block);
                }
            }
        }
    }

    void AttemptGrowth(Chunk chunk, int lx, int ly, int lz, BlockData plant)
    {
        // Convert to absolute world coordinates
        int wx = chunk.chunkCoord.x * Chunk.CHUNK_SIZE + lx;
        int wy = ly;
        int wz = chunk.chunkCoord.y * Chunk.CHUNK_SIZE + lz;

        // Roll probability
        if (Random.value > plant.growthChance) return;

        // 1. Structure Growth (Sapling -> Tree)
        if (plant.growsIntoStructure != null)
        {
            if (plant.growsOn != null)
            {
                BlockData soil = world.GetBlock(new Vector3(wx, wy - 1, wz));
                if (soil != plant.growsOn) return;
            }

            world.ModifyBlock(new Vector3(wx, wy, wz), null);
            world.SpawnStructureRuntime(wx, wy + plant.growsIntoStructure.yOffset, wz, plant.growsIntoStructure);
            return;
        }

        // 2. Stack Growth (Cactus, Sugarcane -> Vertical stacking)
        if (plant.maxGrowHeight > 0)
        {
            int currentY = wy;
            int plantHeight = 1;
            bool validSoil = false;

            while (currentY > 0)
            {
                BlockData below = world.GetBlock(new Vector3(wx, currentY - 1, wz));
                if (below == plant)
                {
                    plantHeight++;
                    currentY--;
                }
                else
                {
                    if (plant.growsOn != null) validSoil = (below == plant.growsOn);
                    else validSoil = true;
                    break;
                }
            }

            if (!validSoil) return;

            currentY = wy;
            while (currentY < Chunk.CHUNK_HEIGHT - 1)
            {
                BlockData above = world.GetBlock(new Vector3(wx, currentY + 1, wz));
                if (above == plant)
                {
                    plantHeight++;
                    currentY++;
                }
                else
                {
                    break;
                }
            }

            if (plantHeight < plant.maxGrowHeight)
            {
                BlockData aboveFinal = world.GetBlock(new Vector3(wx, currentY + 1, wz));
                if (aboveFinal == null)
                {
                    world.ModifyBlock(new Vector3(wx, currentY + 1, wz), plant);
                }
            }
        }
    }
}