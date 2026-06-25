using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewStructureGroup", menuName = "Voxel/Structure/Structure Group")]
public class StructureGroupSO : ScriptableObject
{
    [Header("Patch Settings")]
    [Tooltip("Enable to make these structures spawn in clusters.")]
    public bool usePatchGeneration = true;

    [Tooltip("Scale of the noise. Lower = Larger forests.")]
    public float patchScale = 0.05f;

    [Tooltip("Threshold for spawning. Lower (0.3) = More forest. Higher (0.7) = More clearing.")]
    [Range(0.0f, 1.0f)]
    public float patchThreshold = 0.5f;

    [Header("Structure Variants")]
    [Tooltip("The list of structures that belong to this group.")]
    public List<StructureEntry> structures;

    [System.Serializable]
    public class StructureEntry
    {
        public StructureDataSO structure;
        [Tooltip("Relative weight. Higher = spawns more often.")]
        public float weight = 1.0f;
    }

    // Helper: Pick a random structure based on weight
    public StructureDataSO GetRandomStructure()
    {
        if (structures == null || structures.Count == 0) return null;

        float totalWeight = 0;
        foreach (var s in structures) totalWeight += s.weight;

        float roll = Random.Range(0, totalWeight);
        float current = 0;

        foreach (var s in structures)
        {
            current += s.weight;
            if (roll <= current) return s.structure;
        }
        return structures[0].structure;
    }
}