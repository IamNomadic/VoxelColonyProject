using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BlockManager : MonoBehaviour
{
    public static BlockManager Instance;

    [Header("Configuration")]
    [Tooltip("Material for solid blocks (Dirt, Stone). Keep surface type Opaque.")]
    public Material worldMaterial;

    [Tooltip("Material for leaves/cacti. Duplicate your worldMaterial, check 'Alpha Clipping', and assign it here.")]
    public Material transparentMaterial;

    [Tooltip("How many textures fit across the atlas? (1 = single texture, 2 = 2x2 grid, 16 = 16x16 grid)")]
    public int atlasGridSize = 2;

    [Header("Debug View")]
    public List<BlockData> loadedBlocks = new List<BlockData>();

    // Stores all runtime blocks
    [HideInInspector] public List<BlockData> colorPalette = new List<BlockData>();

    private Dictionary<string, byte> nameToId = new Dictionary<string, byte>();
    private BlockData[] idToBlock;

    void Awake()
    {
        Instance = this;
        InitializeRegistry();
    }

    void InitializeRegistry()
    {
        nameToId.Clear();
        loadedBlocks.Clear();
        colorPalette.Clear();

        // 1. Load Standard Blocks
        BlockData[] rawData = Resources.LoadAll<BlockData>("Blocks");
        List<BlockData> allBlocks = rawData.OrderBy(b => b.name).ToList();

        // 2. Generate Runtime Palettes
        GenerateColors();
        GenerateGrayscale();

        // Add them to the main list
        allBlocks.AddRange(colorPalette);

        // 3. Register IDs
        idToBlock = new BlockData[256];
        byte currentID = 1;

        foreach (var block in allBlocks)
        {
            if (currentID == 255)
            {
                Debug.LogError("Max Block ID limit reached (255)! Some blocks were skipped.");
                break;
            }

            idToBlock[currentID] = block;
            nameToId[block.name] = currentID;
            loadedBlocks.Add(block);

            currentID++;
        }
    }

    void GenerateColors()
    {
        float[] brightnessLevels = { 1.0f, 0.75f, 0.40f, 0.20f, 0.05f };
        string[] levelNames = { "100", "75", "40", "20", "05" };

        for (int s = 0; s < brightnessLevels.Length; s++)
        {
            float val = brightnessLevels[s];
            string suffix = levelNames[s];

            for (int i = 0; i < 32; i++)
            {
                BlockData b = CreateBaseBlock($"Runtime_Color_{i}_{suffix}", $"Color_{i:00}_{suffix}%");
                float hue = (float)i / 32f;
                b.blockColor = Color.HSVToRGB(hue, 0.85f, val);
                colorPalette.Add(b);
            }
        }
    }

    void GenerateGrayscale()
    {
        for (int i = 0; i <= 10; i++)
        {
            float val = 1.0f - (i * 0.1f);
            int percentage = Mathf.RoundToInt(val * 100f);

            BlockData b = CreateBaseBlock($"Runtime_Gray_{percentage}", $"Gray_{percentage}%");
            b.blockColor = Color.HSVToRGB(0f, 0f, val);
            colorPalette.Add(b);
        }
    }

    BlockData CreateBaseBlock(string name, string uiName)
    {
        BlockData b = ScriptableObject.CreateInstance<BlockData>();
        b.name = name;
        b.blockName = uiName;

        b.blockMaterial = worldMaterial;

        // Use custom bounds instead of height
        b.boundsMin = Vector3.zero;
        b.boundsMax = Vector3.one;

        b.isLiquid = false;
        b.durability = 1.0f;
        b.isTransparent = false;

        b.tintWithBlockColor = true;
        b.textureLayout = TextureLayout.Single;
        b.mainUV = new Vector2(1, 0);

        return b;
    }

    public BlockData GetBlockData(byte id) { return (id == 0) ? null : idToBlock[id]; }

    public byte GetBlockId(BlockData data)
    {
        if (data == null) return 0;
        if (nameToId.TryGetValue(data.name, out byte id)) return id;
        return 0;
    }
}