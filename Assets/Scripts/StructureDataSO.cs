using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Voxel/Structure/Imported Structure")]
public class StructureDataSO : ScriptableObject
{
    [Header("Data Source")]
    [Tooltip("Drag your .json file here (Must be inside Assets folder).")]
    public TextAsset structureJson;

    [Header("Translation Palette")]
    [Tooltip("List ALL blocks used in this structure so we can look them up by name.")]
    public List<BlockData> blockPalette;

    [Header("Spawn Rules")]
    [Tooltip("Move the structure down? (e.g. -1 to bury roots).")]
    public int yOffset = 0;

    [Tooltip("Chance to spawn in valid biome (0.01 = 1% chance per block).")]
    [Range(0f, 0.1f)]
    public float spawnDensity = 0.01f;

    // --- CACHE ---
    private Dictionary<Vector3Int, BlockData> cachedStructure;

    public Dictionary<Vector3Int, BlockData> GetStructure()
    {
        if (cachedStructure != null) return cachedStructure;
        if (structureJson == null) return new Dictionary<Vector3Int, BlockData>();

        cachedStructure = new Dictionary<Vector3Int, BlockData>();

        // 1. Parse JSON
        VoxelStructure data = JsonUtility.FromJson<VoxelStructure>(structureJson.text);

        // 2. Build Lookup Dictionary for Palette
        Dictionary<string, BlockData> paletteLookup = new Dictionary<string, BlockData>();
        foreach (var b in blockPalette)
        {
            if (b != null) paletteLookup[b.blockName] = b;
        }

        // 3. Convert
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