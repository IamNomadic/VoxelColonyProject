using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Width/Length of chunk in blocks. Must match VoxelWorld settings.")]
    public const int CHUNK_SIZE = 16;

    [Tooltip("Maximum height limit for this chunk.")]
    public const int CHUNK_HEIGHT = 128;

    // --- INTERNAL DATA ---
    private BlockData[,,] blocks = new BlockData[CHUNK_SIZE, CHUNK_HEIGHT, CHUNK_SIZE];
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private bool isDirty = false;
    public Vector2Int chunkCoord;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
    }

    // --- API ---

    public void SetBlock(int localX, int localY, int localZ, BlockData data)
    {
        if (IsOutOfBounds(localX, localY, localZ)) return;
        blocks[localX, localY, localZ] = data;
        isDirty = true;
    }

    public BlockData GetBlock(int localX, int localY, int localZ)
    {
        if (IsOutOfBounds(localX, localY, localZ)) return null;
        return blocks[localX, localY, localZ];
    }

    private bool IsOutOfBounds(int x, int y, int z)
    {
        return x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE;
    }

    // --- MESH GENERATION ---

    public void RegenerateMesh()
    {
        // (No logic changes here, just cleaned up spacing)
        if (!isDirty) return;

        Dictionary<Material, List<Vector3>> vertsByMat = new Dictionary<Material, List<Vector3>>();
        Dictionary<Material, List<int>> trisByMat = new Dictionary<Material, List<int>>();
        Dictionary<Material, List<Vector2>> uvsByMat = new Dictionary<Material, List<Vector2>>();
        Dictionary<Material, List<Color>> colorsByMat = new Dictionary<Material, List<Color>>();

        for (int x = 0; x < CHUNK_SIZE; x++)
        {
            for (int y = 0; y < CHUNK_HEIGHT; y++)
            {
                for (int z = 0; z < CHUNK_SIZE; z++)
                {
                    BlockData block = blocks[x, y, z];
                    if (block == null) continue;

                    Material mat = block.blockMaterial;
                    if (mat == null) continue;

                    if (!vertsByMat.ContainsKey(mat))
                    {
                        vertsByMat[mat] = new List<Vector3>();
                        trisByMat[mat] = new List<int>();
                        uvsByMat[mat] = new List<Vector2>();
                        colorsByMat[mat] = new List<Color>();
                    }

                    Vector3 pos = new Vector3(x, y, z);

                    // Face Checking
                    if (IsTransparent(x + 1, y, z)) BuildFace(ChunkSide.Right, pos, block, vertsByMat[mat], trisByMat[mat], uvsByMat[mat], colorsByMat[mat]);
                    if (IsTransparent(x - 1, y, z)) BuildFace(ChunkSide.Left, pos, block, vertsByMat[mat], trisByMat[mat], uvsByMat[mat], colorsByMat[mat]);
                    if (IsTransparent(x, y + 1, z)) BuildFace(ChunkSide.Top, pos, block, vertsByMat[mat], trisByMat[mat], uvsByMat[mat], colorsByMat[mat]);
                    if (IsTransparent(x, y - 1, z)) BuildFace(ChunkSide.Bottom, pos, block, vertsByMat[mat], trisByMat[mat], uvsByMat[mat], colorsByMat[mat]);
                    if (IsTransparent(x, y, z + 1)) BuildFace(ChunkSide.Front, pos, block, vertsByMat[mat], trisByMat[mat], uvsByMat[mat], colorsByMat[mat]);
                    if (IsTransparent(x, y, z - 1)) BuildFace(ChunkSide.Back, pos, block, vertsByMat[mat], trisByMat[mat], uvsByMat[mat], colorsByMat[mat]);
                }
            }
        }

        ApplyMesh(vertsByMat, trisByMat, uvsByMat, colorsByMat);
        isDirty = false;
    }

    // (Remaining Mesh Building logic is hidden for brevity as it remains identical to previous working versions)
    // ... IsTransparent, BuildFace, ApplyMesh ...

    bool IsTransparent(int x, int y, int z)
    {
        if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return true;
        return blocks[x, y, z] == null;
    }

    void BuildFace(ChunkSide side, Vector3 pos, BlockData block, List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Color> cols)
    {
        int startIndex = verts.Count;
        switch (side)
        {
            case ChunkSide.Top: verts.Add(pos + new Vector3(0, 1, 0)); verts.Add(pos + new Vector3(0, 1, 1)); verts.Add(pos + new Vector3(1, 1, 1)); verts.Add(pos + new Vector3(1, 1, 0)); break;
            case ChunkSide.Bottom: verts.Add(pos + new Vector3(0, 0, 0)); verts.Add(pos + new Vector3(1, 0, 0)); verts.Add(pos + new Vector3(1, 0, 1)); verts.Add(pos + new Vector3(0, 0, 1)); break;
            case ChunkSide.Left: verts.Add(pos + new Vector3(0, 0, 1)); verts.Add(pos + new Vector3(0, 1, 1)); verts.Add(pos + new Vector3(0, 1, 0)); verts.Add(pos + new Vector3(0, 0, 0)); break;
            case ChunkSide.Right: verts.Add(pos + new Vector3(1, 0, 0)); verts.Add(pos + new Vector3(1, 1, 0)); verts.Add(pos + new Vector3(1, 1, 1)); verts.Add(pos + new Vector3(1, 0, 1)); break;
            case ChunkSide.Front: verts.Add(pos + new Vector3(1, 0, 1)); verts.Add(pos + new Vector3(1, 1, 1)); verts.Add(pos + new Vector3(0, 1, 1)); verts.Add(pos + new Vector3(0, 0, 1)); break;
            case ChunkSide.Back: verts.Add(pos + new Vector3(0, 0, 0)); verts.Add(pos + new Vector3(0, 1, 0)); verts.Add(pos + new Vector3(1, 1, 0)); verts.Add(pos + new Vector3(1, 0, 0)); break;
        }
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
        for (int i = 0; i < 4; i++) cols.Add(block.blockColor);
        tris.Add(startIndex + 0); tris.Add(startIndex + 1); tris.Add(startIndex + 2);
        tris.Add(startIndex + 0); tris.Add(startIndex + 2); tris.Add(startIndex + 3);
    }

    void ApplyMesh(Dictionary<Material, List<Vector3>> verts, Dictionary<Material, List<int>> tris, Dictionary<Material, List<Vector2>> uvs, Dictionary<Material, List<Color>> cols)
    {
        Mesh mesh = new Mesh();
        mesh.name = "ChunkMesh";
        List<Vector3> allVerts = new List<Vector3>();
        List<Vector2> allUvs = new List<Vector2>();
        List<Color> allColors = new List<Color>();
        List<Material> allMats = new List<Material>();
        mesh.subMeshCount = verts.Count;
        int subMeshIndex = 0;
        foreach (var kvp in verts)
        {
            Material mat = kvp.Key;
            allMats.Add(mat);
            int vertOffset = allVerts.Count;
            List<int> subTris = tris[mat];
            for (int i = 0; i < subTris.Count; i++) subTris[i] += vertOffset;
            allVerts.AddRange(kvp.Value);
            allUvs.AddRange(uvs[mat]);
            allColors.AddRange(cols[mat]);
        }
        mesh.SetVertices(allVerts);
        mesh.SetUVs(0, allUvs);
        mesh.SetColors(allColors);
        int currentTriIndex = 0;
        foreach (var kvp in verts)
        {
            mesh.SetTriangles(tris[kvp.Key], currentTriIndex);
            currentTriIndex++;
        }
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
        meshRenderer.sharedMaterials = allMats.ToArray();
        meshCollider.sharedMesh = mesh;
    }

    enum ChunkSide { Top, Bottom, Left, Right, Front, Back }
}