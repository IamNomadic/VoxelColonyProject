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

    [Tooltip("Time in seconds to break the block in Survival Mode. -1 = indestructible. Auto-defaults to 1 if set to 0.")]
    public float durability = 1.0f;

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

    // --- NEW: Sanitize data when loaded ---
    private void OnEnable()
    {
        // Fixes old data that was created before 'durability' existed (which defaults to 0)
        if (durability == 0f) durability = 1.0f;
    }

    private void OnValidate()
    {
        // Prevents you from accidentally typing 0 in the Unity Inspector
        if (durability == 0f) durability = 1.0f;
    }

    public override int GetHashCode()
    {
        int hash = blockMaterial != null ? blockMaterial.GetHashCode() : blockColor.GetHashCode();
        // Include model in identity check so chunks regenerate if model changes
        if (voxelModel != null) hash ^= voxelModel.GetHashCode();
        return hash * 23 + height.GetHashCode();
    }
}