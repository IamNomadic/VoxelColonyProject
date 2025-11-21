using UnityEngine;

[CreateAssetMenu(fileName = "NewBlockData", menuName = "Voxel/Block Data")]
public class BlockData : ScriptableObject
{
    public string blockName;
    public Material blockMaterial;
    public Color blockColor = Color.white;
    public Vector3 blockScale = Vector3.one;

    // Helper to make the dictionary key reliable
    public override int GetHashCode()
    {
        return blockMaterial != null ? blockMaterial.GetHashCode() : blockColor.GetHashCode();
    }
}
