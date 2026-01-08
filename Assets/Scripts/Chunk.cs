using UnityEngine;
using System.Collections.Generic;

public class MeshData
{
    public List<Vector3> vertices = new List<Vector3>();
    public List<int> triangles = new List<int>();
    public List<Vector2> uvs = new List<Vector2>();
    public List<Color> colors = new List<Color>();

    public void Clear()
    {
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();
        colors.Clear();
    }
}

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    public const int CHUNK_SIZE = 16;
    public const int CHUNK_HEIGHT = 128;
    public const byte MAX_LIQUID = 255;

    public Vector2Int chunkCoord;

    private BlockData[,,] blocks = new BlockData[CHUNK_SIZE, CHUNK_HEIGHT, CHUNK_SIZE];
    private byte[,,] fluidLevels = new byte[CHUNK_SIZE, CHUNK_HEIGHT, CHUNK_SIZE];

    // Two separate meshes
    private MeshData terrainMesh = new MeshData();
    private MeshData liquidMesh = new MeshData();

    private MeshFilter terrainFilter;
    private MeshCollider terrainCollider;
    private GameObject liquidObj;
    private MeshFilter liquidFilter;
    private MeshRenderer liquidRenderer;

    private VoxelWorld world;

    void Awake()
    {
        terrainFilter = GetComponent<MeshFilter>();
        terrainCollider = GetComponent<MeshCollider>();
        if (GetComponent<MeshRenderer>() == null) gameObject.AddComponent<MeshRenderer>();

        liquidObj = new GameObject("LiquidLayer");
        liquidObj.transform.parent = transform;
        liquidObj.transform.localPosition = Vector3.zero;

        liquidFilter = liquidObj.AddComponent<MeshFilter>();
        liquidRenderer = liquidObj.AddComponent<MeshRenderer>();
    }

    void Start()
    {
        world = FindObjectOfType<VoxelWorld>();
        if (world != null)
        {
            GetComponent<MeshRenderer>().material = world.defaultMaterial;
            liquidRenderer.material = world.defaultMaterial;
        }
    }

    // --- LIQUID API ---
    public byte GetFluidLevel(int x, int y, int z)
    {
        if (x >= 0 && x < CHUNK_SIZE && y >= 0 && y < CHUNK_HEIGHT && z >= 0 && z < CHUNK_SIZE)
            return fluidLevels[x, y, z];
        return 0;
    }

    public void SetFluidLevel(int x, int y, int z, byte level)
    {
        if (x >= 0 && x < CHUNK_SIZE && y >= 0 && y < CHUNK_HEIGHT && z >= 0 && z < CHUNK_SIZE)
            fluidLevels[x, y, z] = level;
    }

    public BlockData GetBlock(int x, int y, int z)
    {
        if (x >= 0 && x < CHUNK_SIZE && y >= 0 && y < CHUNK_HEIGHT && z >= 0 && z < CHUNK_SIZE)
            return blocks[x, y, z];

        if (world != null)
        {
            Vector3 worldPos = transform.position + new Vector3(x, y, z);
            return world.GetBlock(worldPos);
        }
        return null;
    }

    public void SetBlock(int x, int y, int z, BlockData block)
    {
        if (x >= 0 && x < CHUNK_SIZE && y >= 0 && y < CHUNK_HEIGHT && z >= 0 && z < CHUNK_SIZE)
        {
            BlockData prev = blocks[x, y, z];

            // 1. Deregister old source if we are breaking a tap
            if (prev != null && prev.isWaterSource)
            {
                LiquidSimulator.Instance?.RemoveSource(this, x, y, z);
            }

            blocks[x, y, z] = block;

            // ... (Your existing fluid initialization logic) ...
            if (block == null) fluidLevels[x, y, z] = 0;
            else if (block.isLiquid) fluidLevels[x, y, z] = MAX_LIQUID;
            else fluidLevels[x, y, z] = MAX_LIQUID;

            // 2. Register new source if we are placing a tap
            if (block != null && block.isWaterSource)
            {
                LiquidSimulator.Instance?.AddSource(this, x, y, z);
            }

            // ... (Your existing WakeNeighbors logic) ...
            if (prev != block && (LiquidSimulator.Instance == null || !LiquidSimulator.Instance.isRunningUpdate))
            {
                WakeNeighbors(x, y, z);
            }
        }
    }

    public void WakeNeighbors(int x, int y, int z)
    {
        LiquidSimulator.Instance?.WakeUpArea(this, x, y, z);
    }

    // --- MESH GENERATION ---
    public void RegenerateMesh()
    {
        terrainMesh.Clear();
        liquidMesh.Clear();

        for (int x = 0; x < CHUNK_SIZE; x++)
        {
            for (int y = 0; y < CHUNK_HEIGHT; y++)
            {
                for (int z = 0; z < CHUNK_SIZE; z++)
                {
                    BlockData block = blocks[x, y, z];
                    if (block == null) continue;

                    MeshData targetMesh = block.isLiquid ? liquidMesh : terrainMesh;

                    float h;
                    if (block.isLiquid)
                    {
                        // Check if block ABOVE is also liquid for seamless vertical flow
                        BlockData above = GetBlock(x, y + 1, z);
                        if (above != null && above.isLiquid)
                        {
                            h = 1.0f;
                        }
                        else
                        {
                            h = (float)fluidLevels[x, y, z] / MAX_LIQUID;
                        }
                    }
                    else
                    {
                        h = block.height;
                    }

                    if (h <= 0.01f) continue;

                    Vector3 pos = new Vector3(x, y, z);

                    // Pass amILiquid flag to ShouldDrawFace
                    if (ShouldDrawFace(x, y + 1, z, h, block.isLiquid))
                        AddFace(targetMesh, pos, Vector3.up, block, h);

                    if (ShouldDrawFace(x, y - 1, z, h, block.isLiquid))
                        AddFace(targetMesh, pos, Vector3.down, block, h);

                    if (ShouldDrawFace(x - 1, y, z, h, block.isLiquid))
                        AddFace(targetMesh, pos, Vector3.left, block, h);

                    if (ShouldDrawFace(x + 1, y, z, h, block.isLiquid))
                        AddFace(targetMesh, pos, Vector3.right, block, h);

                    if (ShouldDrawFace(x, y, z + 1, h, block.isLiquid))
                        AddFace(targetMesh, pos, Vector3.forward, block, h);

                    if (ShouldDrawFace(x, y, z - 1, h, block.isLiquid))
                        AddFace(targetMesh, pos, Vector3.back, block, h);
                }
            }
        }

        UpdateMeshes();
    }

    bool ShouldDrawFace(int x, int y, int z, float myHeight, bool amILiquid)
    {
        BlockData neighbor = GetBlock(x, y, z);
        if (neighbor == null) return true;

        // --- LOGIC IF I AM WATER ---
        if (amILiquid)
        {
            if (neighbor.isLiquid)
            {
                float nHeight = (float)GetFluidLevel(x, y, z) / MAX_LIQUID;
                // Only draw if the neighbor is significantly lower
                if (nHeight >= myHeight - 0.01f) return false;
                return true;
            }
            if (!neighbor.isTransparent) return false;
            return true;
        }

        // --- LOGIC IF I AM SOLID ---
        else
        {
            // Always draw solid faces against liquid
            if (neighbor.isLiquid) return true;
            if (neighbor.isTransparent) return true;
            if (neighbor.height < 1.0f) return true;
            return false;
        }
    }

    void AddFace(MeshData target, Vector3 pos, Vector3 dir, BlockData block, float h)
    {
        Vector3 tl, tr, bl, br;

        // Vertex Definitions (Unchanged)
        if (dir == Vector3.up)
        {
            tl = pos + new Vector3(0, h, 1); tr = pos + new Vector3(1, h, 1);
            bl = pos + new Vector3(0, h, 0); br = pos + new Vector3(1, h, 0);
        }
        else if (dir == Vector3.down)
        {
            tl = pos + new Vector3(0, 0, 0); tr = pos + new Vector3(1, 0, 0);
            bl = pos + new Vector3(0, 0, 1); br = pos + new Vector3(1, 0, 1);
        }
        else if (dir == Vector3.forward)
        {
            tl = pos + new Vector3(0, h, 1); tr = pos + new Vector3(1, h, 1);
            bl = pos + new Vector3(0, 0, 1); br = pos + new Vector3(1, 0, 1);
        }
        else if (dir == Vector3.back)
        {
            tl = pos + new Vector3(1, h, 0); tr = pos + new Vector3(0, h, 0);
            bl = pos + new Vector3(1, 0, 0); br = pos + new Vector3(0, 0, 0);
        }
        else if (dir == Vector3.right)
        {
            tl = pos + new Vector3(1, h, 1); tr = pos + new Vector3(1, h, 0);
            bl = pos + new Vector3(1, 0, 1); br = pos + new Vector3(1, 0, 0);
        }
        else
        { // Left
            tl = pos + new Vector3(0, h, 0); tr = pos + new Vector3(0, h, 1);
            bl = pos + new Vector3(0, 0, 0); br = pos + new Vector3(0, 0, 1);
        }

        int vCount = target.vertices.Count;
        target.vertices.Add(tl); target.vertices.Add(tr);
        target.vertices.Add(bl); target.vertices.Add(br);

        // --- TRIANGLE WINDING FIXED ---
        if (dir == Vector3.up)
        {
            // Clockwise (Standard)
            target.triangles.Add(vCount); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 2);
            target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 3);
        }
        else if (dir == Vector3.down)
        {
            // Clockwise (0->1->2 produces Down Normal for these specific verts)
            // PREVIOUS BUG WAS HERE (0->2->1)
            target.triangles.Add(vCount); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 2);
            target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 3);
        }
        else
        {
            // Sides (Clockwise relative to face normal)
            target.triangles.Add(vCount); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 1);
            target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 3);
        }

        Color c = block.blockColor;
        if (c.a == 0) c.a = 1f;
        target.colors.Add(c); target.colors.Add(c); target.colors.Add(c); target.colors.Add(c);

        target.uvs.Add(new Vector2(0, 1)); target.uvs.Add(new Vector2(1, 1));
        target.uvs.Add(new Vector2(0, 0)); target.uvs.Add(new Vector2(1, 0));
    }
    public void UpdateMeshes()
    {
        Mesh tMesh = terrainFilter.sharedMesh;
        if (tMesh == null) { tMesh = new Mesh(); terrainFilter.sharedMesh = tMesh; }
        tMesh.Clear();
        tMesh.vertices = terrainMesh.vertices.ToArray();
        tMesh.triangles = terrainMesh.triangles.ToArray();
        tMesh.uv = terrainMesh.uvs.ToArray();
        tMesh.colors = terrainMesh.colors.ToArray();
        tMesh.RecalculateNormals();
        if (tMesh.vertexCount > 0) terrainCollider.sharedMesh = tMesh;
        else terrainCollider.sharedMesh = null;

        Mesh lMesh = liquidFilter.sharedMesh;
        if (lMesh == null) { lMesh = new Mesh(); liquidFilter.sharedMesh = lMesh; }
        lMesh.Clear();
        lMesh.vertices = liquidMesh.vertices.ToArray();
        lMesh.triangles = liquidMesh.triangles.ToArray();
        lMesh.uv = liquidMesh.uvs.ToArray();
        lMesh.colors = liquidMesh.colors.ToArray();
        lMesh.RecalculateNormals();
    }
}
