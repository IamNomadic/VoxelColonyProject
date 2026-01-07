using UnityEngine;

[CreateAssetMenu(fileName = "NewVoxelBiome", menuName = "Voxel/Biome")]
public class VoxelBiomeSO : ScriptableObject
{
    [Header("1. Visual Identity")]
    public string biomeName;
    [Tooltip("Color used for debug visualizations (if any).")]
    public Color debugColor = Color.white;

    [Header("2. Generation Logic")]
    [Tooltip("Target size in CHUNKS. \nExample: 5 = Small Cluster, 50 = Massive Continent.")]
    [Min(1)]
    public int targetSize = 20;

    [Tooltip("Ideal climate for this biome. \n0.0 = Ice/Snow \n1.0 = Desert/Fire")]
    [Range(0f, 1f)]
    public float optimalTemperature = 0.5f;

    [Tooltip("Range of temperature this biome accepts. \nExample: 0.2 means it accepts Optimal +/- 0.2")]
    [Range(0f, 1f)]
    public float temperatureTolerance = 0.2f;

    [Header("3. Block Palette")]
    [Tooltip("The block used for the top-most layer (Grass, Sand, Snow).")]
    public BlockData surfaceBlock;

    [Tooltip("The block used for the layer immediately below surface (Dirt, Sandstone).")]
    public BlockData subSurfaceBlock;

    [Header("4. Terrain Shape")]
    [Tooltip("The flat baseline height of this biome.")]
    public int baseHeight = 10;

    [Tooltip("How high the hills/mountains go above the base height.")]
    public int terrainAmplitude = 10;

    [Tooltip("Frequency of hills. \n0.01 = Wide rolling hills. \n0.1 = Spiky jagged noise.")]
    public float terrainScale = 0.05f;
}