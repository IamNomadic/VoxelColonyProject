using UnityEngine;

[CreateAssetMenu(fileName = "NewWorldPattern", menuName = "Voxel/World Pattern")]
public class WorldPattern : ScriptableObject
{
    public WorldLayer[] layers;
    public int width = 10;   // X size of world
    public int depth = 10;   // Z size of world
}
