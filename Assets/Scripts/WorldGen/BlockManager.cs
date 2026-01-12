using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Needed for sorting

public class BlockManager : MonoBehaviour
{
    public static BlockManager Instance;

    [Header("Configuration")]
    public Material worldMaterial;

    [Header("Debug View")]
    // We make this Read-Only just so you can see what loaded
    public List<BlockData> loadedBlocks = new List<BlockData>();

    // Fast lookup
    private Dictionary<string, byte> nameToId = new Dictionary<string, byte>();
    private BlockData[] idToBlock; // Array for fast ID access

    void Awake()
    {
        Instance = this;
        InitializeRegistry();
    }

    void InitializeRegistry()
    {
        nameToId.Clear();
        loadedBlocks.Clear();

        // 1. Load all BlockData assets from Resources/Blocks
        BlockData[] rawData = Resources.LoadAll<BlockData>("Blocks");

        // 2. Sort them by name to ensure IDs stick (Air -> Dirt -> Stone -> Water)
        // If we don't sort, IDs might scramble randomly, breaking saved worlds.
        List<BlockData> sortedList = rawData.OrderBy(b => b.name).ToList();

        // 3. Initialize Array (Size + 1 because 0 is reserved for Air)
        // Max 255 blocks because we use 'byte'
        idToBlock = new BlockData[256];

        // 4. Register
        byte currentID = 1;
        foreach (var block in sortedList)
        {
            if (currentID == 255)
            {
                Debug.LogError("Too many blocks! Max is 255.");
                break;
            }

            // Store in our lookups
            idToBlock[currentID] = block;
            nameToId.Add(block.name, currentID);
            loadedBlocks.Add(block); // For debug inspector view

            Debug.Log($"Auto-Registered: [ID {currentID}] {block.name}");
            currentID++;
        }
    }

    public BlockData GetBlockData(byte id)
    {
        // 0 is Air
        if (id == 0) return null;
        return idToBlock[id];
    }

    public byte GetBlockId(BlockData data)
    {
        if (data == null) return 0;
        if (nameToId.TryGetValue(data.name, out byte id)) return id;

        Debug.LogWarning($"Block '{data.name}' not registered! Did you put it in Resources/Blocks?");
        return 0;
    }
}