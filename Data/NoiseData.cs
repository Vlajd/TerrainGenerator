using UnityEngine;

[CreateAssetMenu()]
public class NoiseData : UpdatableData
{
    public float noiseScale;
    public int octaves;
    [Range(0f, 1f)] public float persistance;
    public float lacunarity;
    public int seed;
    public Vector2 offset;
    public Noise.NormalizeMode normalizeMode;
    
    private protected override void OnValidate()
    {
        if (lacunarity < 1)
            lacunarity = 1;

        if (octaves < 0)
            octaves = 0;

        base.OnValidate();
    }
}
