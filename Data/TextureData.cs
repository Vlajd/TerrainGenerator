using UnityEngine;
using System.Linq;

[CreateAssetMenu()]
public class TextureData : UpdatableData
{
    const int TEXTURESIZE = 512;
    const TextureFormat TEXTUREFORMAT = TextureFormat.RGB565;

    public Layer[] layers;

    private float m_savedMinHeight;
    private float m_savedMaxHeight;
    
    public void ApplyToMaterial(Material material)
    {
        material.SetInt("layerCount", layers.Length);
        material.SetColorArray("baseColors", layers.Select(x => x.tint).ToArray());
        material.SetFloatArray("baseStartHeights", layers.Select(x => x.startHeight).ToArray());
        material.SetFloatArray("baseBlends", layers.Select(x => x.blendStrength).ToArray());
        material.SetFloatArray("baseColorStrengths", layers.Select(x => x.tintStrength).ToArray());
        material.SetFloatArray("baseTextureScales", layers.Select(x => x.textureScale).ToArray());
        Texture2DArray textureArray = GenerateTextureArray(layers.Select(x => x.texure).ToArray());
        material.SetTexture("baseTextures", textureArray);

        UpdateMeshHeights(material, m_savedMinHeight, m_savedMaxHeight);
    }

    public void UpdateMeshHeights(Material material, float minHeight, float maxHeight)
    {
        m_savedMinHeight = minHeight;
        m_savedMaxHeight = maxHeight;

        material.SetFloat("minHeight", minHeight);
        material.SetFloat("maxHeight", maxHeight);
    }
    
    Texture2DArray GenerateTextureArray(Texture2D[] textures)
    {
        Texture2DArray textureArray = new Texture2DArray(
            TEXTURESIZE, TEXTURESIZE,
            textures.Length,
            TEXTUREFORMAT, true
        );

        for (int i = 0; i < textures.Length; i++)
            textureArray.SetPixels(textures[i].GetPixels(), i);
        
        textureArray.Apply();
        return textureArray;
    }

    [System.Serializable]
    public class Layer
    {
        public string name;
        public Texture2D texure;
        public Color tint;
        [Range(0,1)] public float tintStrength;
        [Range(0,1)] public float startHeight;
        [Range(0,1)] public float blendStrength;
        public float textureScale;
    }
}
