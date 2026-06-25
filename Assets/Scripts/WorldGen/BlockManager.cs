using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BlockManager : MonoBehaviour
{
    public static BlockManager Instance;

    [Header("Configuration")]
    public Material worldMaterial;

    [Header("Debug View")]
    public List<BlockData> loadedBlocks = new List<BlockData>();

    // Stores all 171 runtime blocks
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
        GenerateColors();     // 5 Shades x 32 Colors = 160 Blocks
        GenerateGrayscale();  // 11 Steps (100% to 0%) = 11 Blocks

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
        // 1. Define the 5 Brightness Levels requested
        float[] brightnessLevels = { 1.0f, 0.75f, 0.40f, 0.20f, 0.05f };
        string[] levelNames = { "100", "75", "40", "20", "05" };

        // 2. Loop Shades FIRST (Outer Loop)
        // This ensures the List order is: [All Brights], then [All 75%], then [All 40%]...
        // This makes the UI render them in rows of depreciating brightness.
        for (int s = 0; s < brightnessLevels.Length; s++)
        {
            float val = brightnessLevels[s];
            string suffix = levelNames[s];

            for (int i = 0; i < 32; i++)
            {
                BlockData b = CreateBaseBlock($"Runtime_Color_{i}_{suffix}", $"Color_{i:00}_{suffix}%");

                // Rainbow Spectrum (Hue 0-1)
                float hue = (float)i / 32f;
                // Saturation 0.85 for vibrant colors
                b.blockColor = Color.HSVToRGB(hue, 0.85f, val);

                colorPalette.Add(b);
            }
        }
    }

    void GenerateGrayscale()
    {
        // 11 Steps: 100%, 90%, 80% ... 10%, 0%
        for (int i = 0; i <= 10; i++)
        {
            // Calculate Value (1.0 down to 0.0)
            float val = 1.0f - (i * 0.1f);
            int percentage = Mathf.RoundToInt(val * 100f);

            BlockData b = CreateBaseBlock($"Runtime_Gray_{percentage}", $"Gray_{percentage}%");

            // Saturation 0 = Grayscale
            b.blockColor = Color.HSVToRGB(0f, 0f, val);

            colorPalette.Add(b);
        }

        Debug.Log($"Generated {colorPalette.Count} Palette Blocks.");
    }

    BlockData CreateBaseBlock(string name, string uiName)
    {
        BlockData b = ScriptableObject.CreateInstance<BlockData>();
        b.name = name;
        b.blockName = uiName;

        b.blockMaterial = worldMaterial;
        b.height = 1.0f;
        b.isLiquid = false;
        b.durability = 1.0f; // NEW: Give runtime blocks a default break time
        b.isTransparent = false;
        b.topUV = Vector2.zero;
        b.sideUV = Vector2.zero;
        b.bottomUV = Vector2.zero;
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