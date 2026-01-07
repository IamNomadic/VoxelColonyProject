using UnityEngine;
using System.Collections.Generic;

// --- HELPER CLASS ---
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
    // --- SETTINGS ---
    public const int CHUNK_SIZE = 16;
    public const int CHUNK_HEIGHT = 128;

    // --- INTERNAL DATA ---
    public Vector2Int chunkCoord;
    private BlockData[,,] blocks = new BlockData[CHUNK_SIZE, CHUNK_HEIGHT, CHUNK_SIZE];
    private MeshData meshData = new MeshData();

    // PERFORMANCE FIX: Cache the world reference!
    private VoxelWorld world;

    // --- INITIALIZATION ---
    void Awake()
    {
        if (GetComponent<MeshFilter>() == null) gameObject.AddComponent<MeshFilter>();
        if (GetComponent<MeshRenderer>() == null) gameObject.AddComponent<MeshRenderer>();
        if (GetComponent<MeshCollider>() == null) gameObject.AddComponent<MeshCollider>();
    }

    void Start()
    {
        // 1. CACHE THE WORLD HERE (The Speed Fix)
        world = FindObjectOfType<VoxelWorld>();

        if (world != null && world.defaultMaterial != null)
        {
            GetComponent<MeshRenderer>().material = world.defaultMaterial;
        }
    }

    // --- DATA ACCESS ---
    public BlockData GetBlock(int x, int y, int z)
    {
        // Optimization: Check local bounds first to avoid method calls
        if (x >= 0 && x < CHUNK_SIZE && y >= 0 && y < CHUNK_HEIGHT && z >= 0 && z < CHUNK_SIZE)
        {
            return blocks[x, y, z];
        }

        // Optimization: Use cached World reference instead of finding it every time
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
            blocks[x, y, z] = block;
        }
    }

    // --- MESH GENERATION ---
    public void RegenerateMesh()
    {
        meshData.Clear();

        // Loop Logic Optimized:
        // We access the array directly for the center block to skip the safety check overhead
        for (int x = 0; x < CHUNK_SIZE; x++)
        {
            for (int y = 0; y < CHUNK_HEIGHT; y++)
            {
                for (int z = 0; z < CHUNK_SIZE; z++)
                {
                    BlockData block = blocks[x, y, z]; // Direct Access (Fast)
                    if (block == null) continue;

                    Vector3 pos = new Vector3(x, y, z);

                    // Check neighbors
                    if (ShouldDrawFace(x, y + 1, z, block.height))
                        AddFace(pos, Vector3.up, block, block.height);

                    if (ShouldDrawFace(x, y - 1, z, block.height))
                        AddFace(pos, Vector3.down, block, block.height);

                    if (ShouldDrawFace(x - 1, y, z, block.height))
                        AddFace(pos, Vector3.left, block, block.height);

                    if (ShouldDrawFace(x + 1, y, z, block.height))
                        AddFace(pos, Vector3.right, block, block.height);

                    if (ShouldDrawFace(x, y, z + 1, block.height))
                        AddFace(pos, Vector3.forward, block, block.height);

                    if (ShouldDrawFace(x, y, z - 1, block.height))
                        AddFace(pos, Vector3.back, block, block.height);
                }
            }
        }

        UpdateMesh();
    }

    bool ShouldDrawFace(int x, int y, int z, float myHeight)
    {
        // We still use the safe GetBlock here because neighbors might be out of bounds
        BlockData neighbor = GetBlock(x, y, z);

        if (neighbor == null) return true;
        if (neighbor.isTransparent) return true;
        if (neighbor.height < 1.0f) return true;
        return false;
    }

    void AddFace(Vector3 pos, Vector3 dir, BlockData block, float h)
    {
        Vector3 tl, tr, bl, br;

        // Vertices (Fixed Winding)
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

        int vCount = meshData.vertices.Count;
        meshData.vertices.Add(tl); meshData.vertices.Add(tr);
        meshData.vertices.Add(bl); meshData.vertices.Add(br);

        // Triangles (Fixed Winding Logic)
        if (dir == Vector3.up)
        {
            // Top Face: Clockwise
            meshData.triangles.Add(vCount);
            meshData.triangles.Add(vCount + 1);
            meshData.triangles.Add(vCount + 2);

            meshData.triangles.Add(vCount + 2);
            meshData.triangles.Add(vCount + 1);
            meshData.triangles.Add(vCount + 3);
        }
        else
        {
            // Side/Bottom Faces: Flipped for Outward Facing
            meshData.triangles.Add(vCount);
            meshData.triangles.Add(vCount + 2);
            meshData.triangles.Add(vCount + 1);

            meshData.triangles.Add(vCount + 1);
            meshData.triangles.Add(vCount + 2);
            meshData.triangles.Add(vCount + 3);
        }

        // Colors
        Color c = block.blockColor;
        if (c.a == 0) c.a = 1f;

        meshData.colors.Add(c); meshData.colors.Add(c);
        meshData.colors.Add(c); meshData.colors.Add(c);

        // UVs
        Vector2 texturePos = Vector2.zero;
        if (dir == Vector3.up) texturePos = block.topUV;
        else if (dir == Vector3.down) texturePos = block.bottomUV;
        else texturePos = block.sideUV;

        if (texturePos != Vector2.zero)
        {
            float tileWidth = 0.1f;
            float tileHeight = 0.1f;
            float currentHeight = (dir == Vector3.up || dir == Vector3.down) ? 1.0f : h;

            Vector2 uvBL = texturePos;
            Vector2 uvTL = texturePos + new Vector2(0, tileHeight * currentHeight);
            Vector2 uvBR = texturePos + new Vector2(tileWidth, 0);
            Vector2 uvTR = texturePos + new Vector2(tileWidth, tileHeight * currentHeight);

            meshData.uvs.Add(uvTL); meshData.uvs.Add(uvTR);
            meshData.uvs.Add(uvBL); meshData.uvs.Add(uvBR);
        }
        else
        {
            meshData.uvs.Add(new Vector2(0, 1));
            meshData.uvs.Add(new Vector2(1, 1));
            meshData.uvs.Add(new Vector2(0, 0));
            meshData.uvs.Add(new Vector2(1, 0));
        }
    }

    void UpdateMesh()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        MeshCollider collider = GetComponent<MeshCollider>();

        Mesh mesh = filter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            filter.sharedMesh = mesh;
        }

        mesh.Clear();
        mesh.vertices = meshData.vertices.ToArray();
        mesh.triangles = meshData.triangles.ToArray();
        mesh.uv = meshData.uvs.ToArray();
        mesh.colors = meshData.colors.ToArray();

        mesh.RecalculateNormals();

        if (mesh.vertexCount > 0)
        {
            collider.sharedMesh = mesh;
        }
        else
        {
            collider.sharedMesh = null;
        }
    }
}