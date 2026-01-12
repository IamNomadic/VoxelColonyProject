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
        vertices.Clear(); triangles.Clear(); uvs.Clear(); colors.Clear();
    }
}

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    public const int CHUNK_SIZE = 16;
    public const int CHUNK_HEIGHT = 128;

    private byte[] blocks = new byte[CHUNK_SIZE * CHUNK_HEIGHT * CHUNK_SIZE];
    private byte[] fluidLevels = new byte[CHUNK_SIZE * CHUNK_HEIGHT * CHUNK_SIZE];
    private short[] waterBodyIDs = new short[CHUNK_SIZE * CHUNK_HEIGHT * CHUNK_SIZE];

    private MeshData terrainMesh = new MeshData();
    private MeshData liquidMesh = new MeshData();

    private MeshFilter terrainFilter;
    private MeshCollider terrainCollider;
    private GameObject liquidObj;
    private MeshFilter liquidFilter;
    private MeshRenderer liquidRenderer;

    public Vector2Int chunkCoord;

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

        for (int i = 0; i < waterBodyIDs.Length; i++) waterBodyIDs[i] = -1;
    }

    void Start()
    {
        if (BlockManager.Instance != null)
        {
            GetComponent<MeshRenderer>().material = BlockManager.Instance.worldMaterial;
            liquidRenderer.material = BlockManager.Instance.worldMaterial;
        }
    }

    int GetIndex(int x, int y, int z) { return x + (z << 4) + (y << 8); }

    // --- BLOCK API ---
    public byte GetBlockID(int x, int y, int z)
    {
        if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return 0;
        return blocks[GetIndex(x, y, z)];
    }

    public BlockData GetBlock(int x, int y, int z)
    {
        return BlockManager.Instance.GetBlockData(GetBlockID(x, y, z));
    }

    public void SetBlock(int x, int y, int z, BlockData block)
    {
        if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return;

        int index = GetIndex(x, y, z);
        byte prevID = blocks[index];
        byte newID = BlockManager.Instance.GetBlockId(block);

        // Source Removal
        if (prevID != 0)
        {
            BlockData prevData = BlockManager.Instance.GetBlockData(prevID);
            if (prevData != null && prevData.isWaterSource)
                LiquidSimulator.Instance?.RemoveSource(this, x, y, z);
        }

        blocks[index] = newID;

        // Reset Fluid/Body
        if (block == null) fluidLevels[index] = 0;
        else if (block.isLiquid) fluidLevels[index] = 255;
        else fluidLevels[index] = 255;

        waterBodyIDs[index] = -1;

        if (block != null && block.isWaterSource)
            LiquidSimulator.Instance?.AddSource(this, x, y, z);

        if (prevID != newID && (LiquidSimulator.Instance == null || !LiquidSimulator.Instance.isRunningUpdate))
        {
            WakeNeighbors(x, y, z);
        }
    }

    public byte GetFluidLevel(int x, int y, int z)
    {
        if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return 0;
        return fluidLevels[GetIndex(x, y, z)];
    }
    public void SetFluidLevel(int x, int y, int z, byte level)
    {
        if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return;
        fluidLevels[GetIndex(x, y, z)] = level;
    }
    public short GetBodyID(int x, int y, int z)
    {
        if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return -1;
        return waterBodyIDs[GetIndex(x, y, z)];
    }
    public void SetBodyID(int x, int y, int z, short id)
    {
        if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return;
        waterBodyIDs[GetIndex(x, y, z)] = id;
    }
    public void WakeNeighbors(int x, int y, int z) { LiquidSimulator.Instance?.WakeUpArea(this, x, y, z); }


    // --- MESH GENERATION ---
    public void RegenerateMesh()
    {
        terrainMesh.Clear();
        liquidMesh.Clear();
        BlockManager mgr = BlockManager.Instance;

        for (int x = 0; x < CHUNK_SIZE; x++)
        {
            for (int y = 0; y < CHUNK_HEIGHT; y++)
            {
                for (int z = 0; z < CHUNK_SIZE; z++)
                {
                    byte id = blocks[GetIndex(x, y, z)];
                    if (id == 0) continue;

                    BlockData block = mgr.GetBlockData(id);
                    if (block == null) continue;

                    Vector3 pos = new Vector3(x, y, z);

                    // --- NEW: MICRO-MODEL CHECK ---
                    if (block.voxelModel != null && !block.isLiquid)
                    {
                        AddMicroMesh(terrainMesh, pos, block.voxelModel);
                        continue; // Skip standard meshing for this block
                    }
                    // -----------------------------

                    MeshData targetMesh = block.isLiquid ? liquidMesh : terrainMesh;
                    float h = block.height;

                    if (block.isLiquid)
                    {
                        bool hasLiquidAbove = false;
                        if (y < CHUNK_HEIGHT - 1)
                        {
                            byte aboveID = blocks[GetIndex(x, y + 1, z)];
                            BlockData aboveData = mgr.GetBlockData(aboveID);
                            if (aboveData != null && aboveData.isLiquid) hasLiquidAbove = true;
                        }
                        h = hasLiquidAbove ? 1.0f : (float)fluidLevels[GetIndex(x, y, z)] / 255f;
                    }

                    if (h <= 0.01f) continue;

                    if (ShouldDrawFace(x, y + 1, z, h, block.isLiquid)) AddFace(targetMesh, pos, Vector3.up, block, h);
                    if (ShouldDrawFace(x, y - 1, z, h, block.isLiquid)) AddFace(targetMesh, pos, Vector3.down, block, h);
                    if (ShouldDrawFace(x - 1, y, z, h, block.isLiquid)) AddFace(targetMesh, pos, Vector3.left, block, h);
                    if (ShouldDrawFace(x + 1, y, z, h, block.isLiquid)) AddFace(targetMesh, pos, Vector3.right, block, h);
                    if (ShouldDrawFace(x, y, z + 1, h, block.isLiquid)) AddFace(targetMesh, pos, Vector3.forward, block, h);
                    if (ShouldDrawFace(x, y, z - 1, h, block.isLiquid)) AddFace(targetMesh, pos, Vector3.back, block, h);
                }
            }
        }
        UpdateMeshes();
    }

    // --- NEW: MICRO-MESH LOGIC ---
    void AddMicroMesh(MeshData target, Vector3 blockPos, VoxelModelSO model)
    {
        float scale = 1f / 8f; // 0.125

        for (int mx = 0; mx < 8; mx++)
        {
            for (int my = 0; my < 8; my++)
            {
                for (int mz = 0; mz < 8; mz++)
                {
                    Color32 c = model.GetVoxel(mx, my, mz);
                    if (c.a == 0) continue; // Skip air

                    Vector3 microPos = blockPos + new Vector3(mx * scale, my * scale, mz * scale);

                    // Hidden Face Culling within the micro-model
                    if (ShouldDrawMicroFace(model, mx, my + 1, mz)) AddMicroFace(target, microPos, Vector3.up, scale, c);
                    if (ShouldDrawMicroFace(model, mx, my - 1, mz)) AddMicroFace(target, microPos, Vector3.down, scale, c);
                    if (ShouldDrawMicroFace(model, mx - 1, my, mz)) AddMicroFace(target, microPos, Vector3.left, scale, c);
                    if (ShouldDrawMicroFace(model, mx + 1, my, mz)) AddMicroFace(target, microPos, Vector3.right, scale, c);
                    if (ShouldDrawMicroFace(model, mx, my, mz + 1)) AddMicroFace(target, microPos, Vector3.forward, scale, c);
                    if (ShouldDrawMicroFace(model, mx, my, mz - 1)) AddMicroFace(target, microPos, Vector3.back, scale, c);
                }
            }
        }
    }

    bool ShouldDrawMicroFace(VoxelModelSO model, int x, int y, int z)
    {
        // If edge of 8x8x8, we draw it (we don't check neighbors of the main block for simplicity yet)
        if (x < 0 || x >= 8 || y < 0 || y >= 8 || z < 0 || z >= 8) return true;

        // If neighbor inside model is air, we draw face
        if (model.GetVoxel(x, y, z).a == 0) return true;

        return false;
    }

    void AddMicroFace(MeshData target, Vector3 pos, Vector3 dir, float scale, Color32 color)
    {
        Vector3 tl = Vector3.zero, tr = Vector3.zero, bl = Vector3.zero, br = Vector3.zero;

        // Identical vertex math to AddFace, but scaled
        if (dir == Vector3.up)
        {
            tl = pos + new Vector3(0, scale, 1 * scale); tr = pos + new Vector3(1 * scale, scale, 1 * scale);
            bl = pos + new Vector3(0, scale, 0); br = pos + new Vector3(1 * scale, scale, 0);
        }
        else if (dir == Vector3.down)
        {
            tl = pos + new Vector3(0, 0, 0); tr = pos + new Vector3(1 * scale, 0, 0);
            bl = pos + new Vector3(0, 0, 1 * scale); br = pos + new Vector3(1 * scale, 0, 1 * scale);
        }
        else if (dir == Vector3.forward)
        {
            tl = pos + new Vector3(0, 1 * scale, 1 * scale); tr = pos + new Vector3(1 * scale, 1 * scale, 1 * scale);
            bl = pos + new Vector3(0, 0, 1 * scale); br = pos + new Vector3(1 * scale, 0, 1 * scale);
        }
        else if (dir == Vector3.back)
        {
            tl = pos + new Vector3(1 * scale, 1 * scale, 0); tr = pos + new Vector3(0, 1 * scale, 0);
            bl = pos + new Vector3(1 * scale, 0, 0); br = pos + new Vector3(0, 0, 0);
        }
        else if (dir == Vector3.right)
        {
            tl = pos + new Vector3(1 * scale, 1 * scale, 1 * scale); tr = pos + new Vector3(1 * scale, 1 * scale, 0);
            bl = pos + new Vector3(1 * scale, 0, 1 * scale); br = pos + new Vector3(1 * scale, 0, 0);
        }
        else
        { // Left
            tl = pos + new Vector3(0, 1 * scale, 0); tr = pos + new Vector3(0, 1 * scale, 1 * scale);
            bl = pos + new Vector3(0, 0, 0); br = pos + new Vector3(0, 0, 1 * scale);
        }

        int vCount = target.vertices.Count;
        target.vertices.Add(tl); target.vertices.Add(tr);
        target.vertices.Add(bl); target.vertices.Add(br);

        if (dir == Vector3.up)
        {
            target.triangles.Add(vCount); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 2);
            target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 3);
        }
        else if (dir == Vector3.down)
        {
            target.triangles.Add(vCount); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 1);
            target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 3); target.triangles.Add(vCount + 1);
        }
        else
        {
            target.triangles.Add(vCount); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 1);
            target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 3);
        }

        // Apply Micro-Voxel Color
        target.colors.Add(color); target.colors.Add(color); target.colors.Add(color); target.colors.Add(color);

        // Simple UVs (0-1 corner mapping per micro face)
        target.uvs.Add(new Vector2(0, 1)); target.uvs.Add(new Vector2(1, 1));
        target.uvs.Add(new Vector2(0, 0)); target.uvs.Add(new Vector2(1, 0));
    }

    // --- STANDARD MESHING HELPERS ---
    bool ShouldDrawFace(int x, int y, int z, float myHeight, bool amILiquid)
    {
        if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return true;

        int index = GetIndex(x, y, z);
        byte id = blocks[index];
        if (id == 0) return true;

        BlockData neighbor = BlockManager.Instance.GetBlockData(id);

        if (amILiquid)
        {
            if (neighbor.isLiquid)
            {
                float nHeight = (float)fluidLevels[index] / 255f;
                if (nHeight >= myHeight - 0.01f) return false;
                return true;
            }
            if (!neighbor.isTransparent) return false;
            return true;
        }
        else
        {
            if (neighbor.isLiquid) return true;
            if (neighbor.isTransparent) return true;
            // NEW: If neighbor is a micro-block (voxelModel != null), we should draw our face against it!
            if (neighbor.voxelModel != null) return true;
            if (neighbor.height < 1.0f) return true;
            return false;
        }
    }

    void AddFace(MeshData target, Vector3 pos, Vector3 dir, BlockData block, float h)
    {
        Vector3 tl = Vector3.zero, tr = Vector3.zero, bl = Vector3.zero, br = Vector3.zero;
        if (dir == Vector3.up) { tl = pos + new Vector3(0, h, 1); tr = pos + new Vector3(1, h, 1); bl = pos + new Vector3(0, h, 0); br = pos + new Vector3(1, h, 0); }
        else if (dir == Vector3.down) { tl = pos + new Vector3(0, 0, 0); tr = pos + new Vector3(1, 0, 0); bl = pos + new Vector3(0, 0, 1); br = pos + new Vector3(1, 0, 1); }
        else if (dir == Vector3.forward) { tl = pos + new Vector3(0, h, 1); tr = pos + new Vector3(1, h, 1); bl = pos + new Vector3(0, 0, 1); br = pos + new Vector3(1, 0, 1); }
        else if (dir == Vector3.back) { tl = pos + new Vector3(1, h, 0); tr = pos + new Vector3(0, h, 0); bl = pos + new Vector3(1, 0, 0); br = pos + new Vector3(0, 0, 0); }
        else if (dir == Vector3.right) { tl = pos + new Vector3(1, h, 1); tr = pos + new Vector3(1, h, 0); bl = pos + new Vector3(1, 0, 1); br = pos + new Vector3(1, 0, 0); }
        else { tl = pos + new Vector3(0, h, 0); tr = pos + new Vector3(0, h, 1); bl = pos + new Vector3(0, 0, 0); br = pos + new Vector3(0, 0, 1); }

        int vCount = target.vertices.Count;
        target.vertices.Add(tl); target.vertices.Add(tr); target.vertices.Add(bl); target.vertices.Add(br);

        if (dir == Vector3.up) { target.triangles.Add(vCount); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 3); }
        else if (dir == Vector3.down) { target.triangles.Add(vCount); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 3); target.triangles.Add(vCount + 1); }
        else { target.triangles.Add(vCount); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 3); }

        Color c = block.blockColor;
        if (c.a == 0) c.a = 1f;
        target.colors.Add(c); target.colors.Add(c); target.colors.Add(c); target.colors.Add(c);
        target.uvs.Add(new Vector2(0, 1)); target.uvs.Add(new Vector2(1, 1)); target.uvs.Add(new Vector2(0, 0)); target.uvs.Add(new Vector2(1, 0));
    }

    public void UpdateMeshes()
    {
        Mesh tMesh = terrainFilter.sharedMesh;
        if (tMesh == null) { tMesh = new Mesh(); terrainFilter.sharedMesh = tMesh; }
        tMesh.Clear();
        tMesh.SetVertices(terrainMesh.vertices);
        tMesh.SetTriangles(terrainMesh.triangles, 0);
        tMesh.SetUVs(0, terrainMesh.uvs);
        tMesh.SetColors(terrainMesh.colors);
        tMesh.RecalculateNormals();
        if (tMesh.vertexCount > 0) terrainCollider.sharedMesh = tMesh;
        else terrainCollider.sharedMesh = null;

        Mesh lMesh = liquidFilter.sharedMesh;
        if (lMesh == null) { lMesh = new Mesh(); liquidFilter.sharedMesh = lMesh; }
        lMesh.Clear();
        lMesh.SetVertices(liquidMesh.vertices);
        lMesh.SetTriangles(liquidMesh.triangles, 0);
        lMesh.SetUVs(0, liquidMesh.uvs);
        lMesh.SetColors(liquidMesh.colors);
        lMesh.RecalculateNormals();
    }
}