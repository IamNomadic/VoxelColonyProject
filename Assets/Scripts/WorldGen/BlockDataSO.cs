using UnityEngine;

// Define the tool types available in your game
public enum ToolType
{
    None,
    Pickaxe,
    Axe,
    Shovel
}

// Define the texture layout styles
public enum TextureLayout
{
    Single,    // 1 texture applied to all 6 sides
    Pillar,    // Top, Bottom, and 1 uniform wrapping side texture
    Unique     // 6 independently assigned textures
}

[CreateAssetMenu(fileName = "NewBlockData", menuName = "Voxel/Block Data")]
public class BlockData : ScriptableObject
{
    [Header("Identity")]
    public string blockName;
    public bool isWaterSource = false;

    [Header("Original Settings")]
    public Material blockMaterial;
    public Color blockColor = Color.white;
    [Tooltip("If true, the blockColor will tint the texture. Disable this for fully textured blocks.")]
    public bool tintWithBlockColor = true;
    public Vector3 blockScale = Vector3.one;

    [Header("Physics / Gameplay")]
    public bool isLiquid = false;
    public int maxToolUses = 0; // 0 means it's a stackable block. > 0 means it's a tool!
    [Tooltip("Time in seconds to break the block in Survival Mode. -1 = indestructible. Auto-defaults to 1 if set to 0.")]
    public float durability = 0.5f;

    [Header("Tool Settings (If this item IS a tool)")]
    public ToolType toolType = ToolType.None;
    [Tooltip("How much faster this tool breaks its preferred blocks (e.g. 2 = twice as fast)")]
    public float toolSpeedMultiplier = 2.0f;

    [Header("Mining Settings (If this item IS a block)")]
    [Tooltip("The tool type preferred to mine this block quickly.")]
    public ToolType preferredTool = ToolType.None;
    [Tooltip("If true, the block will ONLY drop as an item if mined with the preferred tool.")]
    public bool requiresPreferredToolToDrop = false;

    [Header("Shape Settings")]
    [Tooltip("The minimum bounds (Bottom-Left-Back). Standard is 0,0,0.")]
    public Vector3 boundsMin = Vector3.zero;
    [Tooltip("The maximum bounds (Top-Right-Forward). Standard is 1,1,1. (e.g. A slab is 1, 0.5, 1)")]
    public Vector3 boundsMax = Vector3.one;

    [Header("Transparency Settings")]
    [Tooltip("Is this block see-through? (Requires a Cutout/Transparent Material)")]
    public bool isTransparent = false;
    [Tooltip("If true, placing this transparent block next to another of the SAME type will hide the faces between them (e.g. Glass). If false, it renders inner faces (e.g. Cactus).")]
    public bool cullSameTransparent = false;

    [Header("Micro-Model (New)")]
    [Tooltip("If assigned, the chunk will render this 8x8x8 model instead of a simple cube.")]
    public VoxelModelSO voxelModel;

    [Header("Texture & UV Mapping")]
    public TextureLayout textureLayout = TextureLayout.Single;

    [Header("Grid Coordinates (X, Y)")]
    [Tooltip("Enter the grid column and row. Bottom-Left is X:0, Y:0. (1, 0 is the white square!)")]
    public Vector2 mainUV = new Vector2(1, 0);

    [Header("UVs: Pillar Top & Bottom")]
    public Vector2 topUV = new Vector2(1, 0);
    public Vector2 bottomUV = new Vector2(1, 0);

    [Header("UVs: Unique Additional Sides")]
    public Vector2 frontUV = new Vector2(1, 0);
    public Vector2 backUV = new Vector2(1, 0);
    public Vector2 leftUV = new Vector2(1, 0);
    public Vector2 rightUV = new Vector2(1, 0);

    [Header("Growth Settings")]
    [Tooltip("Can this block grow over time?")]
    public bool isGrowable = false;
    [Range(0f, 1f)]
    [Tooltip("Chance to grow every time it receives a random tick.")]
    public float growthChance = 0.3f;
    [Tooltip("The block required under the root (e.g. Sand for Cactus, Grass for Sapling). Leave empty for any block.")]
    public BlockData growsOn;
    [Tooltip("For stacking plants like Cactus. The maximum blocks tall it can become.")]
    public int maxGrowHeight = 0;
    [Tooltip("For Saplings. If set, growing will replace this block with this structure.")]
    public StructureDataSO growsIntoStructure;

    private void OnEnable()
    {
        if (durability == 0f) durability = 1.0f;
    }

    private void OnValidate()
    {
        if (durability == 0f) durability = 1.0f;
    }

    public Vector2 GetUV(Vector3 faceDir)
    {
        if (textureLayout == TextureLayout.Single)
            return mainUV;

        if (textureLayout == TextureLayout.Pillar)
        {
            if (faceDir == Vector3.up) return topUV;
            if (faceDir == Vector3.down) return bottomUV;
            return mainUV; // Sides
        }

        // Unique
        if (faceDir == Vector3.up) return topUV;
        if (faceDir == Vector3.down) return bottomUV;
        if (faceDir == Vector3.forward) return frontUV;
        if (faceDir == Vector3.back) return backUV;
        if (faceDir == Vector3.left) return leftUV;
        if (faceDir == Vector3.right) return rightUV;

        return mainUV;
    }

    public override int GetHashCode()
    {
        int hash = blockMaterial != null ? blockMaterial.GetHashCode() : blockColor.GetHashCode();
        if (voxelModel != null) hash ^= voxelModel.GetHashCode();
        return hash * 23 + boundsMax.GetHashCode() ^ boundsMin.GetHashCode();
    }
}