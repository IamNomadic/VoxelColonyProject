using UnityEngine;

[CreateAssetMenu(fileName = "PerlinNoise", menuName = "Voxel/Noise/Perlin")]
public class PerlinNoiseSO : INoiseGenerator
{
    public float frequency = 0.1f;
    public int octaves = 1;
    public float lacunarity = 2f;
    public float persistence = 0.5f;
    public int seed = 0;

    public override float GetNoise(float x, float z)
    {
        float amplitude = 1f;
        float frequencyLocal = frequency;
        float sum = 0f;
        float max = 0f;

        for (int i = 0; i < Mathf.Max(1, octaves); i++)
        {
            float nx = (x + seed * 100f) * frequencyLocal;
            float nz = (z + seed * 100f) * frequencyLocal;
            float v = Mathf.PerlinNoise(nx, nz) * 2f - 1f;
            sum += v * amplitude;
            max += amplitude;
            amplitude *= persistence;
            frequencyLocal *= lacunarity;
        }

        if (max == 0f) return 0f;
        return Mathf.Clamp(sum / max, -1f, 1f);
    }
}
