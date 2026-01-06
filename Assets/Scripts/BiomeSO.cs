using UnityEngine;

[CreateAssetMenu(fileName = "NewVoxelBiome", menuName = "Voxel/Biome")]
public class VoxelBiomeSO : ScriptableObject
{
    [Header("Visuals")]
    public string biomeName;
    public Color debugColor = Color.green; // For debug views

    [Header("Blocks")]
    public BlockData surfaceBlock; // Top layer (Grass, Sand, Snow)
    public BlockData subSurfaceBlock; // Below top (Dirt, Sandstone)

    [Header("Terrain Shape")]
    public int baseHeight = 10;      // The floor level of this biome
    public float terrainScale = 0.05f; // How wide the hills are
    public int terrainAmplitude = 10;  // How tall the hills are
}