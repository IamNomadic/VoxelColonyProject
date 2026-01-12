using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class VoxelStructure
{
    public string structureName;
    public List<VoxelBlockEntry> blocks = new List<VoxelBlockEntry>();
}

[System.Serializable]
public class VoxelBlockEntry
{
    public int x, y, z;
    public string blockName;

    public VoxelBlockEntry(int x, int y, int z, string name)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.blockName = name;
    }
}