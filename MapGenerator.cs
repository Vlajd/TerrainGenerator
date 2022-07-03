using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode {NoiseMap, ColorMap, Mesh, FalloffMap};
    
    [SerializeField] private DrawMode drawMode;
    [SerializeField] private Noise.NormalizeMode normalizeMode;

    [Header("Mesh Generation")]
    [SerializeField] private float meshHeightMultiplier;
    [SerializeField] private AnimationCurve meshHeightCurve;
    [SerializeField, Range(0,6)] private int editorLOD;

    [Header("Noise Generation")]
    [SerializeField] private float noiseScale;
    [SerializeField] private int octaves;
    [SerializeField, Range(0f, 1f)] private float persistance;
    [SerializeField] private float lacunarity;
    [SerializeField] private int seed;
    [SerializeField] private Vector2 offset;
    [SerializeField] private bool useFalloff;

    [SerializeField] private TerrainType[] regions;
    
    [Header("Visualization")]
    public bool autoUpdate;
    
    [HideInInspector] public const int MAPCHUNKSIZE = 239;

    private float[,] m_falloffMap;
    private Queue<MapThreadInfo<MapData>> m_mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    private Queue<MapThreadInfo<MeshData>> m_meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();


    private void Awake()
    {
        m_falloffMap = FalloffGenerator.GenerateFalloffMap(MAPCHUNKSIZE);
    }

    public void DrawMapInEditor()
    {
        MapData mapData = GenerateMapData(Vector2.zero);

        MapDisplay display = FindObjectOfType<MapDisplay>();
        switch (drawMode)
        {
            case DrawMode.NoiseMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
                break;
            case DrawMode.ColorMap:
                display.DrawTexture(TextureGenerator.TextureFromColorMap(mapData.colorMap, MAPCHUNKSIZE, MAPCHUNKSIZE));
                break;
            case DrawMode.Mesh:
                display.DrawMesh(
                    MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, editorLOD),
                    TextureGenerator.TextureFromColorMap(mapData.colorMap, MAPCHUNKSIZE, MAPCHUNKSIZE)
                );
                break;
            case DrawMode.FalloffMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFalloffMap(MAPCHUNKSIZE)));
                break;
        }
    }

    public void RequestMapData(Vector2 centre, Action<MapData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MapDataThread(centre, callback);
        };
    
        new Thread(threadStart).Start();
    }

    public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MeshDataThread(mapData, lod, callback);
        };

        new Thread(threadStart).Start();
    }


    private void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback)
    {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, lod);
        lock (m_meshDataThreadInfoQueue)
        {
            m_meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    private void MapDataThread(Vector2 centre, Action<MapData> callback)
    {
        MapData mapData = GenerateMapData(centre);
        lock (m_mapDataThreadInfoQueue)
        {
            m_mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    private void Update()
    {
        for (int i = 0; i < m_mapDataThreadInfoQueue.Count; i++)
        {
            MapThreadInfo<MapData> threadInfo = m_mapDataThreadInfoQueue.Dequeue();
            threadInfo.callback(threadInfo.parameter);
        }

        for (int i = 0; i < m_meshDataThreadInfoQueue.Count; i++)
        {
            MapThreadInfo<MeshData> threadInfo = m_meshDataThreadInfoQueue.Dequeue();
            threadInfo.callback(threadInfo.parameter);
        }
    }


    private MapData GenerateMapData(Vector2 centre)
    {
        float[,] noiseMap = Noise.GenerateNoiseMap(MAPCHUNKSIZE + 2, MAPCHUNKSIZE + 2, noiseScale, octaves, persistance, lacunarity, seed, centre + offset, normalizeMode);

        Color[] colorMap = new Color[MAPCHUNKSIZE * MAPCHUNKSIZE];
        for (int y = 0; y < MAPCHUNKSIZE; y++)
        {
            for (int x = 0; x < MAPCHUNKSIZE; x++)
            {
                if (useFalloff)
                {
                    noiseMap[x,y] = Mathf.Clamp01(noiseMap[x,y] - m_falloffMap[x,y]);
                }
                float currentHeight = noiseMap[x, y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight >= regions[i].height)
                        colorMap[y * MAPCHUNKSIZE + x] = regions[i].color;
                    else
                        break;
                }
            }
        }

        return new MapData(noiseMap, colorMap);
    }

    private void OnValidate()
    {
        if (lacunarity < 1)
            lacunarity = 1;

        if (octaves < 0)
            octaves = 0;

        m_falloffMap = FalloffGenerator.GenerateFalloffMap(MAPCHUNKSIZE);
    }

    private struct MapThreadInfo<T>
    {
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}


[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color color;
}

public struct MapData
{
    public readonly float[,] heightMap;
    public readonly Color[] colorMap;

    public MapData(float[,] heightMap, Color[] colorMap)
    {
        this.heightMap = heightMap;
        this.colorMap = colorMap;
    }
}
