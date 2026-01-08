using UnityEngine;
using System.Collections.Generic;

public class LiquidSimulator : MonoBehaviour
{
    public static LiquidSimulator Instance;

    [Header("Settings")]
    public float flowSpeed = 15f;
    public byte minLiquidLevel = 5;
    public byte dropletThreshold = 20;

    [Header("Testing")]
    [Tooltip("Drag your Water Block Data here so the tap knows what to spawn.")]
    public BlockData waterReference;

    [Header("Smart Flow")]
    [Range(0, 8)] public int scanRadius = 5;

    [Header("Fatigue")]
    public int maxMovesBeforeDeath = 25;

    public int maxUpdatesPerFrame = 2000;

    public bool isRunningUpdate = false;

    // --- DATA TYPES ---
    class LiquidNode { public Chunk c; public int x, y, z; public int moves; }
    class SourceNode { public Chunk c; public int x, y, z; } // New: For Taps

    // --- LISTS ---
    private List<LiquidNode> activeNodes = new List<LiquidNode>();
    private HashSet<string> activeUnique = new HashSet<string>();
    private List<LiquidNode> inboxNodes = new List<LiquidNode>();
    private HashSet<string> inboxUnique = new HashSet<string>();
    private HashSet<Chunk> dirtyChunks = new HashSet<Chunk>();

    // List of active taps
    private List<SourceNode> waterSources = new List<SourceNode>();

    private float timer;
    private VoxelWorld world;

    private Vector3Int[] neighborOffsets = new Vector3Int[]
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
    };

    void Awake() { Instance = this; }
    void Start() { world = FindObjectOfType<VoxelWorld>(); }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer > (1f / flowSpeed))
        {
            isRunningUpdate = true; // LOCK
            SimulateStep();
            isRunningUpdate = false; // UNLOCK
            timer = 0;
        }
    }

    // --- SOURCE BLOCK API ---
    public void AddSource(Chunk c, int x, int y, int z)
    {
        waterSources.Add(new SourceNode { c = c, x = x, y = y, z = z });
    }

    public void RemoveSource(Chunk c, int x, int y, int z)
    {
        waterSources.RemoveAll(n => n.c == c && n.x == x && n.y == y && n.z == z);
    }

    void ProcessSources()
    {
        if (waterReference == null) return;

        for (int i = waterSources.Count - 1; i >= 0; i--)
        {
            var s = waterSources[i];

            // Safety check: Chunk destroyed?
            if (s.c == null) { waterSources.RemoveAt(i); continue; }

            // Target block is the one directly BELOW the tap
            int targetY = s.y - 1;
            if (targetY >= 0)
            {
                BlockData below = s.c.GetBlock(s.x, targetY, s.z);
                byte levelBelow = s.c.GetFluidLevel(s.x, targetY, s.z);

                // If Empty OR (Water AND Not Full) -> FILL IT
                if (below == null || (below.isLiquid && levelBelow < 255))
                {
                    // If air, place the block
                    if (below == null) s.c.SetBlock(s.x, targetY, s.z, waterReference);

                    // Fill to max
                    s.c.SetFluidLevel(s.x, targetY, s.z, 255);

                    // Force Wake Up so it flows immediately
                    WakeUpArea(s.c, s.x, targetY, s.z);

                    // Mark chunk for mesh rebuild
                    if (!dirtyChunks.Contains(s.c)) dirtyChunks.Add(s.c);
                }
            }
        }
    }

    // --- STANDARD API ---
    public void WakeUpArea(Chunk chunk, int x, int y, int z)
    {
        AddToInbox(chunk, x, y, z, 0);
        AddToInbox(chunk, x + 1, y, z, 0);
        AddToInbox(chunk, x - 1, y, z, 0);
        AddToInbox(chunk, x, y + 1, z, 0);
        AddToInbox(chunk, x, y - 1, z, 0);
        AddToInbox(chunk, x, y, z + 1, 0);
        AddToInbox(chunk, x, y, z - 1, 0);
    }

    public void AddToSimulation(Chunk c, int x, int y, int z)
    {
        AddToInbox(c, x, y, z, 0);
    }

    void AddToInbox(Chunk c, int x, int y, int z, int moves)
    {
        if (GetNeighborBlock(c, x, y, z, out Chunk targetC, out int tx, out int ty, out int tz))
        {
            string key = $"{targetC.GetInstanceID()}_{tx}_{ty}_{tz}";
            if (!inboxUnique.Contains(key))
            {
                inboxUnique.Add(key);
                inboxNodes.Add(new LiquidNode { c = targetC, x = tx, y = ty, z = tz, moves = moves });
            }
        }
    }

    void SimulateStep()
    {
        // 1. RUN TAPS (This spawns new water)
        ProcessSources();

        // 2. MERGE INBOX
        if (inboxNodes.Count > 0)
        {
            foreach (var node in inboxNodes)
            {
                string key = $"{node.c.GetInstanceID()}_{node.x}_{node.y}_{node.z}";
                if (!activeUnique.Contains(key))
                {
                    activeUnique.Add(key);
                    activeNodes.Add(node);
                }
            }
            inboxNodes.Clear();
            inboxUnique.Clear();
        }

        if (activeNodes.Count == 0) return;

        int updates = 0;
        dirtyChunks.Clear();

        List<LiquidNode> nextPass = new List<LiquidNode>();
        HashSet<string> nextUnique = new HashSet<string>();

        // 3. PROCESS FLOW
        foreach (var node in activeNodes)
        {
            if (updates > maxUpdatesPerFrame)
            {
                AddNodeDirect(node.c, node.x, node.y, node.z, node.moves, nextPass, nextUnique);
                continue;
            }

            if (node.c == null) continue;

            bool changed = Flow(node.c, node.x, node.y, node.z, node.moves, nextPass, nextUnique);
            if (changed)
            {
                updates++;
                if (!dirtyChunks.Contains(node.c)) dirtyChunks.Add(node.c);
            }
        }

        foreach (var c in dirtyChunks) if (c != null) c.RegenerateMesh();

        activeNodes = nextPass;
        activeUnique = nextUnique;
    }

    bool Flow(Chunk chunk, int x, int y, int z, int currentMoves, List<LiquidNode> nextList, HashSet<string> nextSet)
    {
        byte myLevel = chunk.GetFluidLevel(x, y, z);
        BlockData block = chunk.GetBlock(x, y, z);

        if (block == null || !block.isLiquid || myLevel <= 0) return false;

        bool changed = false;
        bool stillActive = false;
        bool flowedThisFrame = false;

        // FATIGUE CHECK
        if (myLevel < dropletThreshold && currentMoves > maxMovesBeforeDeath)
        {
            chunk.SetBlock(x, y, z, null);
            chunk.SetFluidLevel(x, y, z, 0);
            WakeNeighborsInternal(chunk, x, y, z, nextList, nextSet);
            return true;
        }

        // SLEEP CHECK
        if (myLevel == 255 && y < Chunk.CHUNK_HEIGHT - 1)
        {
            BlockData above = chunk.GetBlock(x, y + 1, z);
            if (above != null && above.isLiquid) return false;
        }

        // 1. GRAVITY
        if (y > 0)
        {
            BlockData below = chunk.GetBlock(x, y - 1, z);
            byte levelBelow = (below == null) ? (byte)0 : chunk.GetFluidLevel(x, y - 1, z);
            int spaceBelow = (below == null || below.isLiquid) ? (255 - levelBelow) : 0;
            if (below != null && !below.isLiquid) spaceBelow = 0;

            if (spaceBelow > 0)
            {
                byte moveAmount = (byte)Mathf.Min(myLevel, spaceBelow);
                if (moveAmount > 0)
                {
                    if (below == null) chunk.SetBlock(x, y - 1, z, block);

                    chunk.SetFluidLevel(x, y - 1, z, (byte)(levelBelow + moveAmount));
                    myLevel -= moveAmount;
                    chunk.SetFluidLevel(x, y, z, myLevel);

                    AddNodeDirect(chunk, x, y - 1, z, 0, nextList, nextSet);
                    if (!dirtyChunks.Contains(chunk)) dirtyChunks.Add(chunk);

                    WakeNeighborsInternal(chunk, x, y, z, nextList, nextSet);

                    changed = true;
                    stillActive = true;
                    flowedThisFrame = true;
                }
            }
        }

        // 2. SIDEWAYS
        if (myLevel > minLiquidLevel)
        {
            int prioritizedIndex = -1;
            if (scanRadius > 0) prioritizedIndex = ScanForElevationDrop(chunk, x, y, z);

            ShuffleOffsets();

            List<Vector3Int> checkOrder = new List<Vector3Int>();
            Vector3Int smartVector = Vector3Int.zero;

            if (prioritizedIndex != -1)
            {
                if (prioritizedIndex == 0) smartVector = new Vector3Int(1, 0, 0);
                if (prioritizedIndex == 1) smartVector = new Vector3Int(-1, 0, 0);
                if (prioritizedIndex == 2) smartVector = new Vector3Int(0, 0, 1);
                if (prioritizedIndex == 3) smartVector = new Vector3Int(0, 0, -1);
                checkOrder.Add(smartVector);
            }

            foreach (var off in neighborOffsets) if (off != smartVector) checkOrder.Add(off);

            bool isDroplet = myLevel < dropletThreshold;

            foreach (Vector3Int offset in checkOrder)
            {
                if (myLevel <= minLiquidLevel) break;

                if (GetNeighborBlock(chunk, x + offset.x, y, z + offset.z, out Chunk nChunk, out int nX, out int nY, out int nZ))
                {
                    BlockData nBlock = nChunk.GetBlock(nX, nY, nZ);
                    if (nBlock == null || nBlock.isLiquid)
                    {
                        byte nLevel = (nBlock == null) ? (byte)0 : nChunk.GetFluidLevel(nX, nY, nZ);

                        if (myLevel > nLevel)
                        {
                            byte amountToGive = 0;

                            if (isDroplet) amountToGive = myLevel;
                            else
                            {
                                int total = myLevel + nLevel;
                                int average = total / 2;
                                int remainder = total % 2;
                                amountToGive = (byte)(myLevel - (average + remainder));
                            }

                            if (amountToGive > 0)
                            {
                                if (nBlock == null) nChunk.SetBlock(nX, nY, nZ, block);

                                nChunk.SetFluidLevel(nX, nY, nZ, (byte)(nLevel + amountToGive));
                                myLevel -= amountToGive;
                                chunk.SetFluidLevel(x, y, z, myLevel);

                                AddNodeDirect(nChunk, nX, nY, nZ, currentMoves + 1, nextList, nextSet);
                                if (!dirtyChunks.Contains(nChunk)) dirtyChunks.Add(nChunk);

                                WakeNeighborsInternal(chunk, x, y, z, nextList, nextSet);

                                changed = true;
                                stillActive = true;
                                flowedThisFrame = true;

                                if (myLevel == 0) break;
                            }
                        }
                    }
                }
            }
        }

        // STAGNATION KILLER
        if (!flowedThisFrame && myLevel < dropletThreshold)
        {
            currentMoves += 5;
            stillActive = true;
        }

        // 3. FINAL DISSIPATE CHECK
        if (chunk.GetFluidLevel(x, y, z) <= minLiquidLevel)
        {
            chunk.SetBlock(x, y, z, null);
            chunk.SetFluidLevel(x, y, z, 0);
            WakeNeighborsInternal(chunk, x, y, z, nextList, nextSet);

            changed = true;
            stillActive = false;
        }

        if (stillActive || changed)
        {
            AddNodeDirect(chunk, x, y, z, currentMoves, nextList, nextSet);
        }

        return changed;
    }

    void WakeNeighborsInternal(Chunk c, int x, int y, int z, List<LiquidNode> list, HashSet<string> set)
    {
        AddNodeDirect(c, x + 1, y, z, 0, list, set);
        AddNodeDirect(c, x - 1, y, z, 0, list, set);
        AddNodeDirect(c, x, y + 1, z, 0, list, set);
        AddNodeDirect(c, x, y - 1, z, 0, list, set);
        AddNodeDirect(c, x, y, z + 1, 0, list, set);
        AddNodeDirect(c, x, y, z - 1, 0, list, set);
    }

    int ScanForElevationDrop(Chunk originChunk, int startX, int startY, int startZ)
    {
        int bestDir = -1;
        float shortestDistSq = 9999f;

        for (int x = -scanRadius; x <= scanRadius; x++)
        {
            for (int z = -scanRadius; z <= scanRadius; z++)
            {
                if (x == 0 && z == 0) continue;
                if ((x * x + z * z) > (scanRadius * scanRadius)) continue;

                int checkX = startX + x;
                int checkZ = startZ + z;
                bool isHole = false;

                if (checkX >= 0 && checkX < Chunk.CHUNK_SIZE && checkZ >= 0 && checkZ < Chunk.CHUNK_SIZE)
                {
                    if (startY > 0)
                    {
                        BlockData b = originChunk.GetBlock(checkX, startY - 1, checkZ);
                        if (b == null) isHole = true;
                        else if (b.isLiquid && originChunk.GetFluidLevel(checkX, startY - 1, checkZ) < 200) isHole = true;
                    }
                }
                else
                {
                    if (GetNeighborBlock(originChunk, checkX, startY - 1, checkZ, out Chunk nC, out int nX, out int nY, out int nZ))
                    {
                        BlockData b = nC.GetBlock(nX, nY, nZ);
                        if (b == null) isHole = true;
                        else if (b.isLiquid && nC.GetFluidLevel(nX, nY, nZ) < 200) isHole = true;
                    }
                }

                if (isHole)
                {
                    float distSq = x * x + z * z;
                    if (distSq < shortestDistSq)
                    {
                        shortestDistSq = distSq;
                        if (Mathf.Abs(x) > Mathf.Abs(z)) bestDir = (x > 0) ? 0 : 1;
                        else bestDir = (z > 0) ? 2 : 3;
                    }
                }
            }
        }
        return bestDir;
    }

    void ShuffleOffsets()
    {
        for (int i = 0; i < neighborOffsets.Length; i++)
        {
            int rnd = Random.Range(i, neighborOffsets.Length);
            Vector3Int temp = neighborOffsets[rnd];
            neighborOffsets[rnd] = neighborOffsets[i];
            neighborOffsets[i] = temp;
        }
    }

    void AddNodeDirect(Chunk c, int x, int y, int z, int moves, List<LiquidNode> list, HashSet<string> set)
    {
        if (GetNeighborBlock(c, x, y, z, out Chunk targetC, out int tx, out int ty, out int tz))
        {
            string key = $"{targetC.GetInstanceID()}_{tx}_{ty}_{tz}";
            if (!set.Contains(key))
            {
                set.Add(key);
                list.Add(new LiquidNode { c = targetC, x = tx, y = ty, z = tz, moves = moves });
            }
        }
    }

    bool GetNeighborBlock(Chunk currentChunk, int x, int y, int z, out Chunk targetChunk, out int targetX, out int targetY, out int targetZ)
    {
        targetChunk = currentChunk;
        targetX = x; targetY = y; targetZ = z;

        if (y < 0 || y >= Chunk.CHUNK_HEIGHT) return false;
        if (x >= 0 && x < Chunk.CHUNK_SIZE && z >= 0 && z < Chunk.CHUNK_SIZE) return true;

        Vector2Int currentCoord = currentChunk.chunkCoord;
        int nextCX = currentCoord.x;
        int nextCZ = currentCoord.y;

        if (x < 0) { nextCX--; targetX = Chunk.CHUNK_SIZE - 1; }
        else if (x >= Chunk.CHUNK_SIZE) { nextCX++; targetX = 0; }

        if (z < 0) { nextCZ--; targetZ = Chunk.CHUNK_SIZE - 1; }
        else if (z >= Chunk.CHUNK_SIZE) { nextCZ++; targetZ = 0; }

        if (world != null)
        {
            targetChunk = world.GetChunkByCoord(nextCX, nextCZ);
            return targetChunk != null;
        }
        return false;
    }
}