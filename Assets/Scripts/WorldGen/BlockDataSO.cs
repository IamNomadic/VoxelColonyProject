using UnityEngine;

[CreateAssetMenu(fileName = "NewBlockData", menuName = "Voxel/Block Data")]
public class BlockData : ScriptableObject
{
    [Header("Identity")]
    public string blockName;
    public bool isWaterSource = false;

    [Header("Original Settings")]
    public Material blockMaterial;
    public Color blockColor = Color.white;
    public Vector3 blockScale = Vector3.one;

    [Header("Physics / Gameplay")]
    public bool isLiquid = false;

    [Header("Shape Settings")]
    [Range(0.1f, 1.0f)]
    public float height = 1.0f;
    public bool isTransparent = false;

    [Header("Micro-Model (New)")]
    [Tooltip("If assigned, the chunk will render this 8x8x8 model instead of a simple cube.")]
    public VoxelModelSO voxelModel;

    [Header("Texture Coordinates")]
    public Vector2 topUV;
    public Vector2 sideUV;
    public Vector2 bottomUV;

    public override int GetHashCode()
    {
        int hash = blockMaterial != null ? blockMaterial.GetHashCode() : blockColor.GetHashCode();
        // Include model in identity check so chunks regenerate if model changes
        if (voxelModel != null) hash ^= voxelModel.GetHashCode();
        return hash * 23 + height.GetHashCode();
    }
}