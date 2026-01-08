using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewBlockData", menuName = "Voxel/Structure/Imported Structure")]
public class StructureDataSO : ScriptableObject
{
    [Header("Data Source")]
    public TextAsset structureJson;

    [Header("Translation Palette")]
    public List<BlockData> blockPalette;

    [Header("Spawn Rules")]
    public int yOffset = 0;

    [Range(0f, 0.1f)]
    public float spawnDensity = 0.01f;

    [Tooltip("How much space to reserve around this structure (prevents overlapping).")]
    public int spawnRadius = 3;

    // --- NEW: PATCH SETTINGS ---
    [Header("Patch Generation")]
    [Tooltip("If true, structures spawn in clumps/forests based on noise.")]
    public bool usePatchGeneration = false;

    [Tooltip("Low value (0.01) = Huge Forests. High value (0.2) = Small clumps.")]
    public float patchScale = 0.05f;

    [Tooltip("Values > 0.5 mean patches are rarer. Values < 0.5 mean patches are common.")]
    [Range(0.1f, 0.9f)]
    public float patchThreshold = 0.5f;

    // --- CACHE ---
    private Dictionary<Vector3Int, BlockData> cachedStructure;

    public Dictionary<Vector3Int, BlockData> GetStructure()
    {
        if (cachedStructure != null) return cachedStructure;
        if (structureJson == null) return new Dictionary<Vector3Int, BlockData>();

        cachedStructure = new Dictionary<Vector3Int, BlockData>();

        VoxelStructure data = JsonUtility.FromJson<VoxelStructure>(structureJson.text);

        Dictionary<string, BlockData> paletteLookup = new Dictionary<string, BlockData>();
        foreach (var b in blockPalette)
        {
            if (b != null) paletteLookup[b.blockName] = b;
        }

        foreach (var entry in data.blocks)
        {
            if (paletteLookup.TryGetValue(entry.blockName, out BlockData block))
            {
                cachedStructure[new Vector3Int(entry.x, entry.y, entry.z)] = block;
            }
        }

        return cachedStructure;
    }
}