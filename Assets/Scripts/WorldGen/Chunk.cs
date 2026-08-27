using UnityEngine;
using System.Collections.Generic;

public class MeshData
{
    public List<Vector3> vertices = new List<Vector3>();
    public List<int> triangles = new List<int>();
    public List<Vector2> uvs = new List<Vector2>();
    public List<Color> colors = new List<Color>();

    public void Clear() { vertices.Clear(); triangles.Clear(); uvs.Clear(); colors.Clear(); }
}

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    public const int CHUNK_SIZE = 16;
    public const int CHUNK_HEIGHT = 128;

    private byte[] blocks = new byte[CHUNK_SIZE * CHUNK_HEIGHT * CHUNK_SIZE];
    private byte[] fluidLevels = new byte[CHUNK_SIZE * CHUNK_HEIGHT * CHUNK_SIZE];
    private short[] waterBodyIDs = new short[CHUNK_SIZE * CHUNK_HEIGHT * CHUNK_SIZE];

    private MeshData terrainMesh = new MeshData();       // Layer 1: Solid
    private MeshData transparentMesh = new MeshData();   // Layer 2: Cutout (Leaves/Cacti)
    private MeshData liquidMesh = new MeshData();        // Layer 3: Translucent (Water)

    private MeshFilter terrainFilter;
    private MeshCollider terrainCollider;

    private GameObject transparentObj;
    private MeshFilter transparentFilter;
    private MeshRenderer transparentRenderer;
    private MeshCollider transparentCollider; // Fix: Collider for transparent layer

    private GameObject liquidObj;
    private MeshFilter liquidFilter;
    private MeshRenderer liquidRenderer;

    public Vector2Int chunkCoord;
    public bool isModified = false;

    public class ChunkSaveData
    {
        public byte[] blocks;
        public byte[] fluidLevels;
        public short[] waterBodyIDs;
    }

    public ChunkSaveData GetSaveData()
    {
        return new ChunkSaveData { blocks = (byte[])blocks.Clone(), fluidLevels = (byte[])fluidLevels.Clone(), waterBodyIDs = (short[])waterBodyIDs.Clone() };
    }

    public void LoadSaveData(ChunkSaveData data)
    {
        blocks = (byte[])data.blocks.Clone(); fluidLevels = (byte[])data.fluidLevels.Clone(); waterBodyIDs = (short[])data.waterBodyIDs.Clone(); isModified = true;
    }

    void Awake()
    {
        // 1. Solid Layer setup
        terrainFilter = GetComponent<MeshFilter>();
        terrainCollider = GetComponent<MeshCollider>();
        if (GetComponent<MeshRenderer>() == null) gameObject.AddComponent<MeshRenderer>();

        // 2. Transparent Layer setup
        transparentObj = new GameObject("TransparentLayer");
        transparentObj.transform.parent = transform;
        transparentObj.transform.localPosition = Vector3.zero;
        transparentFilter = transparentObj.AddComponent<MeshFilter>();
        transparentRenderer = transparentObj.AddComponent<MeshRenderer>();
        transparentCollider = transparentObj.AddComponent<MeshCollider>();

        // 3. Liquid Layer setup
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
            // Using sharedMaterial for better performance in a voxel game
            GetComponent<MeshRenderer>().sharedMaterial = BlockManager.Instance.worldMaterial;
            transparentRenderer.sharedMaterial = BlockManager.Instance.transparentMaterial != null ? BlockManager.Instance.transparentMaterial : BlockManager.Instance.worldMaterial;
            liquidRenderer.sharedMaterial = BlockManager.Instance.worldMaterial;
        }
    }

    int GetIndex(int x, int y, int z) { return x + (z << 4) + (y << 8); }

    public byte GetBlockID(int x, int y, int z) { if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return 0; return blocks[GetIndex(x, y, z)]; }
    public BlockData GetBlock(int x, int y, int z) { return BlockManager.Instance.GetBlockData(GetBlockID(x, y, z)); }
    public byte GetFluidLevel(int x, int y, int z) { if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return 0; return fluidLevels[GetIndex(x, y, z)]; }
    public void SetFluidLevel(int x, int y, int z, byte level) { if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return; fluidLevels[GetIndex(x, y, z)] = level; isModified = true; }
    public short GetBodyID(int x, int y, int z) { if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return -1; return waterBodyIDs[GetIndex(x, y, z)]; }
    public void SetBodyID(int x, int y, int z, short id) { if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return; waterBodyIDs[GetIndex(x, y, z)] = id; isModified = true; }
    public void WakeNeighbors(int x, int y, int z) { LiquidSimulator.Instance?.WakeUpArea(this, x, y, z); }

    public void SetBlock(int x, int y, int z, BlockData block, bool isSimulation = false)
    {
        if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return;

        int index = GetIndex(x, y, z);
        byte prevID = blocks[index];
        byte newID = BlockManager.Instance.GetBlockId(block);

        if (block != null && newID == 0) return;

        if (prevID != 0)
        {
            BlockData prevData = BlockManager.Instance.GetBlockData(prevID);
            if (prevData != null && prevData.isWaterSource) LiquidSimulator.Instance?.RemoveSource(this, x, y, z);
        }

        blocks[index] = newID; waterBodyIDs[index] = -1; isModified = true;

        if (!isSimulation) { if (block == null) fluidLevels[index] = 0; else if (block.isLiquid) fluidLevels[index] = 255; else fluidLevels[index] = 255; }
        else { if (block == null || block.isLiquid) fluidLevels[index] = 0; }

        if (!isSimulation && block != null && block.isWaterSource) LiquidSimulator.Instance?.AddSource(this, x, y, z);
        if (prevID != newID && (LiquidSimulator.Instance == null || !LiquidSimulator.Instance.isRunningUpdate)) WakeNeighbors(x, y, z);
    }

    public void RegenerateMesh()
    {
        terrainMesh.Clear();
        transparentMesh.Clear();
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

                    if (block.voxelModel != null && !block.isLiquid)
                    {
                        AddMicroMesh(terrainMesh, pos, block.voxelModel, block);
                        continue;
                    }

                    // Assign to proper render layer
                    MeshData targetMesh;
                    if (block.isLiquid) targetMesh = liquidMesh;
                    else if (block.isTransparent) targetMesh = transparentMesh;
                    else targetMesh = terrainMesh;

                    Vector3 bMin = block.boundsMin;
                    Vector3 bMax = block.boundsMax;

                    if (block.isLiquid)
                    {
                        bool hasLiquidAbove = false;
                        if (y < CHUNK_HEIGHT - 1)
                        {
                            byte aboveID = blocks[GetIndex(x, y + 1, z)];
                            BlockData aboveData = mgr.GetBlockData(aboveID);
                            if (aboveData != null && aboveData.isLiquid) hasLiquidAbove = true;
                        }
                        bMax.y = hasLiquidAbove ? 1.0f : (float)fluidLevels[GetIndex(x, y, z)] / 255f;
                        bMin.y = 0f;
                    }

                    if (bMax.y - bMin.y <= 0.01f) continue;

                    if (ShouldDrawFace(x, y + 1, z, bMin, bMax, Vector3.up, block)) AddFace(targetMesh, pos, Vector3.up, block, bMin, bMax);
                    if (ShouldDrawFace(x, y - 1, z, bMin, bMax, Vector3.down, block)) AddFace(targetMesh, pos, Vector3.down, block, bMin, bMax);
                    if (ShouldDrawFace(x - 1, y, z, bMin, bMax, Vector3.left, block)) AddFace(targetMesh, pos, Vector3.left, block, bMin, bMax);
                    if (ShouldDrawFace(x + 1, y, z, bMin, bMax, Vector3.right, block)) AddFace(targetMesh, pos, Vector3.right, block, bMin, bMax);
                    if (ShouldDrawFace(x, y, z + 1, bMin, bMax, Vector3.forward, block)) AddFace(targetMesh, pos, Vector3.forward, block, bMin, bMax);
                    if (ShouldDrawFace(x, y, z - 1, bMin, bMax, Vector3.back, block)) AddFace(targetMesh, pos, Vector3.back, block, bMin, bMax);
                }
            }
        }
        UpdateMeshes();
    }

    void AddMicroMesh(MeshData target, Vector3 blockPos, VoxelModelSO model, BlockData block)
    {
        int res = model.resolution;
        if (res <= 0) res = 8;
        float scale = 1.0f / (float)res;

        for (int mx = 0; mx < res; mx++)
        {
            for (int my = 0; my < res; my++)
            {
                for (int mz = 0; mz < res; mz++)
                {
                    Color32 c = model.GetVoxel(mx, my, mz);
                    if (c.a == 0) continue;
                    Vector3 microPos = blockPos + new Vector3(mx * scale, my * scale, mz * scale);
                    if (ShouldDrawMicroFace(model, mx, my + 1, mz, res)) AddMicroFace(target, microPos, Vector3.up, scale, c, block);
                    if (ShouldDrawMicroFace(model, mx, my - 1, mz, res)) AddMicroFace(target, microPos, Vector3.down, scale, c, block);
                    if (ShouldDrawMicroFace(model, mx - 1, my, mz, res)) AddMicroFace(target, microPos, Vector3.left, scale, c, block);
                    if (ShouldDrawMicroFace(model, mx + 1, my, mz, res)) AddMicroFace(target, microPos, Vector3.right, scale, c, block);
                    if (ShouldDrawMicroFace(model, mx, my, mz + 1, res)) AddMicroFace(target, microPos, Vector3.forward, scale, c, block);
                    if (ShouldDrawMicroFace(model, mx, my, mz - 1, res)) AddMicroFace(target, microPos, Vector3.back, scale, c, block);
                }
            }
        }
    }

    bool ShouldDrawMicroFace(VoxelModelSO model, int x, int y, int z, int res) { if (x < 0 || x >= res || y < 0 || y >= res || z < 0 || z >= res) return true; return model.GetVoxel(x, y, z).a == 0; }

    void AddMicroFace(MeshData target, Vector3 pos, Vector3 dir, float scale, Color32 color, BlockData block)
    {
        Vector3 tl = Vector3.zero, tr = Vector3.zero, bl = Vector3.zero, br = Vector3.zero;
        if (dir == Vector3.up) { tl = pos + new Vector3(0, scale, 1 * scale); tr = pos + new Vector3(1 * scale, scale, 1 * scale); bl = pos + new Vector3(0, scale, 0); br = pos + new Vector3(1 * scale, scale, 0); }
        else if (dir == Vector3.down) { tl = pos + new Vector3(0, 0, 0); tr = pos + new Vector3(1 * scale, 0, 0); bl = pos + new Vector3(0, 0, 1 * scale); br = pos + new Vector3(1 * scale, 0, 1 * scale); }
        else if (dir == Vector3.forward) { tl = pos + new Vector3(0, 1 * scale, 1 * scale); tr = pos + new Vector3(1 * scale, 1 * scale, 1 * scale); bl = pos + new Vector3(0, 0, 1 * scale); br = pos + new Vector3(1 * scale, 0, 1 * scale); }
        else if (dir == Vector3.back) { tl = pos + new Vector3(1 * scale, 1 * scale, 0); tr = pos + new Vector3(0, 1 * scale, 0); bl = pos + new Vector3(1 * scale, 0, 0); br = pos + new Vector3(0, 0, 0); }
        else if (dir == Vector3.right) { tl = pos + new Vector3(1 * scale, 1 * scale, 1 * scale); tr = pos + new Vector3(1 * scale, 1 * scale, 0); bl = pos + new Vector3(1 * scale, 0, 1 * scale); br = pos + new Vector3(1 * scale, 0, 0); }
        else { tl = pos + new Vector3(0, 1 * scale, 0); tr = pos + new Vector3(0, 1 * scale, 1 * scale); bl = pos + new Vector3(0, 0, 0); br = pos + new Vector3(0, 0, 1 * scale); }

        int vCount = target.vertices.Count;
        target.vertices.Add(tl); target.vertices.Add(tr); target.vertices.Add(bl); target.vertices.Add(br);

        if (dir == Vector3.up || dir == Vector3.down) { target.triangles.Add(vCount); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 3); }
        else { target.triangles.Add(vCount); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 3); }

        target.colors.Add(color); target.colors.Add(color); target.colors.Add(color); target.colors.Add(color);
        float uvSz = 1f / BlockManager.Instance.atlasGridSize; Vector2 gridUV = block.GetUV(dir); Vector2 uv00 = new Vector2(gridUV.x * uvSz, gridUV.y * uvSz);
        target.uvs.Add(uv00); target.uvs.Add(uv00); target.uvs.Add(uv00); target.uvs.Add(uv00);
    }

    bool ShouldDrawFace(int x, int y, int z, Vector3 bMin, Vector3 bMax, Vector3 faceDir, BlockData myBlock)
    {
        // 1. Inset Check: If the face is shaped inside the cell boundary, it NEVER touches a neighbor! 
        if (faceDir == Vector3.up && bMax.y < 1.0f) return true;
        if (faceDir == Vector3.down && bMin.y > 0.0f) return true;
        if (faceDir == Vector3.right && bMax.x < 1.0f) return true;
        if (faceDir == Vector3.left && bMin.x > 0.0f) return true;
        if (faceDir == Vector3.forward && bMax.z < 1.0f) return true;
        if (faceDir == Vector3.back && bMin.z > 0.0f) return true;

        if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return true;

        int index = GetIndex(x, y, z);
        byte id = blocks[index];
        if (id == 0) return true;

        BlockData neighbor = BlockManager.Instance.GetBlockData(id);

        if (myBlock.isLiquid)
        {
            if (neighbor.isLiquid)
            {
                float nHeight = (float)fluidLevels[index] / 255f;
                if (nHeight >= bMax.y - 0.01f) return false;
                return true;
            }
            if (neighbor.isTransparent) return true;
        }
        else
        {
            if (neighbor.isLiquid) return true;
            if (neighbor.isTransparent)
            {
                if (myBlock == neighbor && myBlock.cullSameTransparent) { /* Allow bounds check below to cull it */ }
                else return true;
            }
            if (neighbor.voxelModel != null) return true;
        }

        // 2. Neighbor Precision Check: Does the solid neighbor completely cover this face?
        Vector3 nMin = neighbor.boundsMin;
        Vector3 nMax = neighbor.boundsMax;

        if (faceDir == Vector3.up || faceDir == Vector3.down)
        {
            if (nMin.x <= bMin.x && nMax.x >= bMax.x && nMin.z <= bMin.z && nMax.z >= bMax.z) return false;
        }
        else if (faceDir == Vector3.forward || faceDir == Vector3.back)
        {
            if (nMin.x <= bMin.x && nMax.x >= bMax.x && nMin.y <= bMin.y && nMax.y >= bMax.y) return false;
        }
        else if (faceDir == Vector3.right || faceDir == Vector3.left)
        {
            if (nMin.z <= bMin.z && nMax.z >= bMax.z && nMin.y <= bMin.y && nMax.y >= bMax.y) return false;
        }

        return true;
    }

    void AddFace(MeshData target, Vector3 pos, Vector3 dir, BlockData block, Vector3 bMin, Vector3 bMax)
    {
        Vector3 tl = Vector3.zero, tr = Vector3.zero, bl = Vector3.zero, br = Vector3.zero;

        if (dir == Vector3.up) { tl = pos + new Vector3(bMin.x, bMax.y, bMax.z); tr = pos + new Vector3(bMax.x, bMax.y, bMax.z); bl = pos + new Vector3(bMin.x, bMax.y, bMin.z); br = pos + new Vector3(bMax.x, bMax.y, bMin.z); }
        else if (dir == Vector3.down) { tl = pos + new Vector3(bMin.x, bMin.y, bMin.z); tr = pos + new Vector3(bMax.x, bMin.y, bMin.z); bl = pos + new Vector3(bMin.x, bMin.y, bMax.z); br = pos + new Vector3(bMax.x, bMin.y, bMax.z); }
        else if (dir == Vector3.forward) { tl = pos + new Vector3(bMin.x, bMax.y, bMax.z); tr = pos + new Vector3(bMax.x, bMax.y, bMax.z); bl = pos + new Vector3(bMin.x, bMin.y, bMax.z); br = pos + new Vector3(bMax.x, bMin.y, bMax.z); }
        else if (dir == Vector3.back) { tl = pos + new Vector3(bMax.x, bMax.y, bMin.z); tr = pos + new Vector3(bMin.x, bMax.y, bMin.z); bl = pos + new Vector3(bMax.x, bMin.y, bMin.z); br = pos + new Vector3(bMin.x, bMin.y, bMin.z); }
        else if (dir == Vector3.right) { tl = pos + new Vector3(bMax.x, bMax.y, bMax.z); tr = pos + new Vector3(bMax.x, bMax.y, bMin.z); bl = pos + new Vector3(bMax.x, bMin.y, bMax.z); br = pos + new Vector3(bMax.x, bMin.y, bMin.z); }
        else { tl = pos + new Vector3(bMin.x, bMax.y, bMin.z); tr = pos + new Vector3(bMin.x, bMax.y, bMax.z); bl = pos + new Vector3(bMin.x, bMin.y, bMin.z); br = pos + new Vector3(bMin.x, bMin.y, bMax.z); }

        int vCount = target.vertices.Count;
        target.vertices.Add(tl); target.vertices.Add(tr); target.vertices.Add(bl); target.vertices.Add(br);

        if (dir == Vector3.up || dir == Vector3.down) { target.triangles.Add(vCount); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 3); }
        else { target.triangles.Add(vCount); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 1); target.triangles.Add(vCount + 2); target.triangles.Add(vCount + 3); }

        Color c = block.tintWithBlockColor ? block.blockColor : Color.white;
        if (c.a == 0) c.a = 1f;
        target.colors.Add(c); target.colors.Add(c); target.colors.Add(c); target.colors.Add(c);

        // --- PRECISE MAPPING to prevent squishing ---
        float uvSz = 1f / BlockManager.Instance.atlasGridSize;
        Vector2 gridUV = block.GetUV(dir);
        Vector2 baseUV = new Vector2(gridUV.x * uvSz, gridUV.y * uvSz);

        Vector2 uvTL, uvTR, uvBL, uvBR;

        if (dir == Vector3.up) { uvTL = new Vector2(bMin.x, bMax.z); uvTR = new Vector2(bMax.x, bMax.z); uvBL = new Vector2(bMin.x, bMin.z); uvBR = new Vector2(bMax.x, bMin.z); }
        else if (dir == Vector3.down) { uvTL = new Vector2(bMin.x, 1f - bMin.z); uvTR = new Vector2(bMax.x, 1f - bMin.z); uvBL = new Vector2(bMin.x, 1f - bMax.z); uvBR = new Vector2(bMax.x, 1f - bMax.z); }
        else if (dir == Vector3.forward) { uvTL = new Vector2(bMin.x, bMax.y); uvTR = new Vector2(bMax.x, bMax.y); uvBL = new Vector2(bMin.x, bMin.y); uvBR = new Vector2(bMax.x, bMin.y); }
        else if (dir == Vector3.back) { uvTL = new Vector2(1f - bMax.x, bMax.y); uvTR = new Vector2(1f - bMin.x, bMax.y); uvBL = new Vector2(1f - bMax.x, bMin.y); uvBR = new Vector2(1f - bMin.x, bMin.y); }
        else if (dir == Vector3.right) { uvTL = new Vector2(1f - bMax.z, bMax.y); uvTR = new Vector2(1f - bMin.z, bMax.y); uvBL = new Vector2(1f - bMax.z, bMin.y); uvBR = new Vector2(1f - bMin.z, bMin.y); }
        else { uvTL = new Vector2(bMin.z, bMax.y); uvTR = new Vector2(bMax.z, bMax.y); uvBL = new Vector2(bMin.z, bMin.y); uvBR = new Vector2(bMax.z, bMin.y); }

        target.uvs.Add(new Vector2(baseUV.x + uvTL.x * uvSz, baseUV.y + uvTL.y * uvSz));
        target.uvs.Add(new Vector2(baseUV.x + uvTR.x * uvSz, baseUV.y + uvTR.y * uvSz));
        target.uvs.Add(new Vector2(baseUV.x + uvBL.x * uvSz, baseUV.y + uvBL.y * uvSz));
        target.uvs.Add(new Vector2(baseUV.x + uvBR.x * uvSz, baseUV.y + uvBR.y * uvSz));
    }

    public void UpdateMeshes()
    {
        // 1. Solid Terrain
        Mesh tMesh = terrainFilter.sharedMesh;
        if (tMesh == null) { tMesh = new Mesh(); terrainFilter.sharedMesh = tMesh; }
        tMesh.Clear(); tMesh.SetVertices(terrainMesh.vertices); tMesh.SetTriangles(terrainMesh.triangles, 0); tMesh.SetUVs(0, terrainMesh.uvs); tMesh.SetColors(terrainMesh.colors); tMesh.RecalculateNormals();
        if (tMesh.vertexCount > 0) { terrainCollider.sharedMesh = null; terrainCollider.sharedMesh = tMesh; } else terrainCollider.sharedMesh = null;

        // 2. Transparent / Cutout Terrain
        Mesh transpMesh = transparentFilter.sharedMesh;
        if (transpMesh == null) { transpMesh = new Mesh(); transparentFilter.sharedMesh = transpMesh; }
        transpMesh.Clear(); transpMesh.SetVertices(transparentMesh.vertices); transpMesh.SetTriangles(transparentMesh.triangles, 0); transpMesh.SetUVs(0, transparentMesh.uvs); transpMesh.SetColors(transparentMesh.colors); transpMesh.RecalculateNormals();

        // Fix: Apply Physics Collision to Transparent Mesh Layer!
        if (transpMesh.vertexCount > 0) { transparentCollider.sharedMesh = null; transparentCollider.sharedMesh = transpMesh; } else transparentCollider.sharedMesh = null;

        // 3. Liquid
        Mesh lMesh = liquidFilter.sharedMesh;
        if (lMesh == null) { lMesh = new Mesh(); liquidFilter.sharedMesh = lMesh; }
        lMesh.Clear(); lMesh.SetVertices(liquidMesh.vertices); lMesh.SetTriangles(liquidMesh.triangles, 0); lMesh.SetUVs(0, liquidMesh.uvs); lMesh.SetColors(liquidMesh.colors); lMesh.RecalculateNormals();
    }
}