using UnityEngine;

[System.Serializable]
public class WorldLayer
{
    public string name;
    public int height = 1;                  // nominal layer thickness in voxels
    public int baseHeightOffset = 0;        // vertical offset of this layer relative to world base (optional)
    public int maxAdditionalHeight = 0;     // how many voxels noise can add on top of this layer
    public BlockData blockData;             // block used for voxels in this layer
    public INoiseGenerator heightNoise;     // optional noise ScriptableObject to drive height variation
    public float noiseScale = 1f;           // multiplies coordinates before querying noise
    public float noiseAmplitude = 1f;       // multiplies noise result (0..1 typically) into voxel height
}
