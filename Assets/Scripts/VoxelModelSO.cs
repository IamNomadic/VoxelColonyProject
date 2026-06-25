using UnityEngine;

[CreateAssetMenu(fileName = "NewVoxelModel", menuName = "Voxel/Voxel Model")]
public class VoxelModelSO : ScriptableObject
{
    [Header("Data Source")]
    public TextAsset modelJson;

    // --- RUNTIME DATA ---
    public int resolution = 8;
    private Color32[] cachedVoxels;

    public Color32 GetVoxel(int x, int y, int z)
    {
        if (cachedVoxels == null) LoadModel();

        // Safety for different resolutions
        if (x < 0 || x >= resolution || y < 0 || y >= resolution || z < 0 || z >= resolution)
            return new Color32(0, 0, 0, 0);

        // Dynamic Indexing: x + (y * Width) + (z * Width * Height)
        int index = x + (y * resolution) + (z * resolution * resolution);

        if (index < 0 || index >= cachedVoxels.Length) return new Color32(0, 0, 0, 0);

        return cachedVoxels[index];
    }

    private void LoadModel()
    {
        if (modelJson == null)
        {
            cachedVoxels = new Color32[512];
            resolution = 8;
            return;
        }

        MicroModelData data = JsonUtility.FromJson<MicroModelData>(modelJson.text);

        if (data != null && data.voxels != null)
        {
            cachedVoxels = data.voxels;

            // --- CRITICAL FIX: Resolution Detection ---
            if (data.resolution > 0)
            {
                resolution = data.resolution;
            }
            else
            {
                // Fallback: Calculate resolution from array length
                // 512 = 8^3, 4096 = 16^3, 32768 = 32^3
                int len = cachedVoxels.Length;
                if (len == 32768) resolution = 32;
                else if (len == 4096) resolution = 16;
                else resolution = 8;
            }
        }
    }
}

[System.Serializable]
public class MicroModelData
{
    public int resolution = 8;
    public Color32[] voxels;
}