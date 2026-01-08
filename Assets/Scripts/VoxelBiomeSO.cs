using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewVoxelBiome", menuName = "Voxel/Biome")]
public class VoxelBiomeSO : ScriptableObject
{
    [Header("1. Visual Identity")]
    public string biomeName;
    public Color debugColor = Color.white;

    [Header("2. Generation Logic")]
    [Min(1)] public int targetSize = 20;
    [Range(0f, 1f)] public float optimalTemperature = 0.5f;
    [Range(0f, 1f)] public float temperatureTolerance = 0.2f;

    [Header("3. Block Palette")]
    public BlockData surfaceBlock;
    public BlockData subSurfaceBlock;

    [Header("4. Terrain Shape")]
    public int baseHeight = 10;
    public int terrainAmplitude = 10;
    public float terrainScale = 0.05f;

    [Header("5. Decorations (Groups)")]
    [Tooltip("List of structure groups (e.g. 'Oak Forest', 'Rock Scatter').")]
    public List<StructureGroupSO> structureGroups;
}