using UnityEngine;

[CreateAssetMenu(fileName = "RandomNoise", menuName = "Voxel/Noise/Random")]
public class RandomNoiseSO : INoiseGenerator
{
    public float scale = 1f;
    public int seed = 0;

    public override float GetNoise(float x, float z)
    {
        int xi = Mathf.FloorToInt(x * scale);
        int zi = Mathf.FloorToInt(z * scale);
        int hash = xi * 73856093 ^ zi * 19349663 ^ seed;
        hash = (hash << 13) ^ hash;
        float v = 1.0f - ((hash * (hash * hash * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824.0f;
        return Mathf.Clamp(v, -1f, 1f);
    }
}
