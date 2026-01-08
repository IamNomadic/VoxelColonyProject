using UnityEngine;

[CreateAssetMenu(fileName = "NewBlockData", menuName = "Voxel/Block Data")]
public class BlockData : ScriptableObject
{
    [Header("Identity")]
    public string blockName;
    public bool isWaterSource = false; // Add this!
    [Header("Original Settings")]
    public Material blockMaterial;
    public Color blockColor = Color.white;
    public Vector3 blockScale = Vector3.one;
    [Header("Physics / Gameplay")]
    public bool isLiquid = false; // Add this line!
    [Header("Shape Settings (New)")]
    [Range(0.1f, 1.0f)]
    [Tooltip("1.0 = Full Block. 0.5 = Slab. 0.25 = Snow Layer.")]
    public float height = 1.0f;

    [Tooltip("If true, neighbors will draw faces against this (e.g. Glass, Leaves, Slabs).")]
    public bool isTransparent = false;

    [Header("Texture Coordinates (Optional)")]
    // If you use a Texture Atlas, set these. If using simple Colors, leave as (0,0).
    public Vector2 topUV;
    public Vector2 sideUV;
    public Vector2 bottomUV;

    // Helper to make the dictionary key reliable (Original Logic + Height check)
    public override int GetHashCode()
    {
        int hash = blockMaterial != null ? blockMaterial.GetHashCode() : blockColor.GetHashCode();
        // We include height in the hash so a Slab is treated as different from a Full Block
        return hash * 23 + height.GetHashCode();
    }
}