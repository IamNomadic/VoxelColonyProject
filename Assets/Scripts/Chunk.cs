using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    // Standard chunk size
    public const int CHUNK_SIZE = 16;
    public const int CHUNK_HEIGHT = 128; // Adjust if your world is taller

    // The data: 3D array of blocks
    private BlockData[,,] blocks = new BlockData[CHUNK_SIZE, CHUNK_HEIGHT, CHUNK_SIZE];

    // Mesh data
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    // Tracking if this chunk needs an update
    private bool isDirty = false;

    // Neighbor references for seamless edges (optional, simple version checks self only first)
    public Vector2Int chunkCoord;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
    }

    public void SetBlock(int localX, int localY, int localZ, BlockData data)
    {
        if (localX < 0 || localX >= CHUNK_SIZE ||
            localY < 0 || localY >= CHUNK_HEIGHT ||
            localZ < 0 || localZ >= CHUNK_SIZE)
            return;

        blocks[localX, localY, localZ] = data;
        isDirty = true;
    }

    public BlockData GetBlock(int localX, int localY, int localZ)
    {
        if (localX < 0 || localX >= CHUNK_SIZE ||
            localY < 0 || localY >= CHUNK_HEIGHT ||
            localZ < 0 || localZ >= CHUNK_SIZE)
            return null;

        return blocks[localX, localY, localZ];
    }

    public void RegenerateMesh()
    {
        if (!isDirty) return;

        // We group vertices by Material to support multiple materials in one mesh (Submeshes)
        Dictionary<Material, List<Vector3>> vertsByMat = new Dictionary<Material, List<Vector3>>();
        Dictionary<Material, List<int>> trisByMat = new Dictionary<Material, List<int>>();
        Dictionary<Material, List<Vector2>> uvsByMat = new Dictionary<Material, List<Vector2>>();
        Dictionary<Material, List<Color>> colorsByMat = new Dictionary<Material, List<Color>>();

        // Iterate every block in the chunk
        for (int x = 0; x < CHUNK_SIZE; x++)
        {
            for (int y = 0; y < CHUNK_HEIGHT; y++)
            {
                for (int z = 0; z < CHUNK_SIZE; z++)
                {
                    BlockData block = blocks[x, y, z];
                    if (block == null) continue;

                    // Get or Create lists for this material
                    Material mat = block.blockMaterial;
                    // Fallback for null material (use a default if needed, or skip)
                    if (mat == null) continue;

                    if (!vertsByMat.ContainsKey(mat))
                    {
                        vertsByMat[mat] = new List<Vector3>();
                        trisByMat[mat] = new List<int>();
                        uvsByMat[mat] = new List<Vector2>();
                        colorsByMat[mat] = new List<Color>();
                    }

                    Vector3 pos = new Vector3(x, y, z);

                    // Check all 6 neighbors. If neighbor is empty, draw face.
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

    bool IsTransparent(int x, int y, int z)
    {
        // If out of bounds of this chunk, we currently assume "Empty" to draw the edge face.
        // Ideally, you would ask the VoxelWorld for the neighbor chunk's block.
        if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE) return true;
        return blocks[x, y, z] == null;
    }

    void BuildFace(ChunkSide side, Vector3 pos, BlockData block, List<Vector3> verts, List<int> tris, List<Vector2> uvs, List<Color> cols)
    {
        int startIndex = verts.Count;

        // Add 4 vertices for the face
        switch (side)
        {
            case ChunkSide.Top:
                verts.Add(pos + new Vector3(0, 1, 0)); verts.Add(pos + new Vector3(0, 1, 1));
                verts.Add(pos + new Vector3(1, 1, 1)); verts.Add(pos + new Vector3(1, 1, 0));
                break;
            case ChunkSide.Bottom:
                verts.Add(pos + new Vector3(0, 0, 0)); verts.Add(pos + new Vector3(1, 0, 0));
                verts.Add(pos + new Vector3(1, 0, 1)); verts.Add(pos + new Vector3(0, 0, 1));
                break;
            case ChunkSide.Left:
                verts.Add(pos + new Vector3(0, 0, 1)); verts.Add(pos + new Vector3(0, 1, 1));
                verts.Add(pos + new Vector3(0, 1, 0)); verts.Add(pos + new Vector3(0, 0, 0));
                break;
            case ChunkSide.Right:
                verts.Add(pos + new Vector3(1, 0, 0)); verts.Add(pos + new Vector3(1, 1, 0));
                verts.Add(pos + new Vector3(1, 1, 1)); verts.Add(pos + new Vector3(1, 0, 1));
                break;
            case ChunkSide.Front: // +Z
                verts.Add(pos + new Vector3(1, 0, 1)); verts.Add(pos + new Vector3(1, 1, 1));
                verts.Add(pos + new Vector3(0, 1, 1)); verts.Add(pos + new Vector3(0, 0, 1));
                break;
            case ChunkSide.Back: // -Z
                verts.Add(pos + new Vector3(0, 0, 0)); verts.Add(pos + new Vector3(0, 1, 0));
                verts.Add(pos + new Vector3(1, 1, 0)); verts.Add(pos + new Vector3(1, 0, 0));
                break;
        }

        // Add UVs (Simple 0-1 mapping)
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1));
        uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));

        // Add Colors
        for (int i = 0; i < 4; i++) cols.Add(block.blockColor);

        // Add Triangles
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

        // Combine all dictionary lists into one Mesh with SubMeshes
        foreach (var kvp in verts)
        {
            Material mat = kvp.Key;
            allMats.Add(mat);

            // Offset triangles by current vertex count
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