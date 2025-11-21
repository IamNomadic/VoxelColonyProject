using UnityEngine;

public abstract class INoiseGenerator : ScriptableObject
{
    // Return a noise value in range [-1, 1] for coordinates (x,z)
    public abstract float GetNoise(float x, float z);
}
