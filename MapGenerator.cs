using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode {NoiseMap, Mesh, FalloffMap};

    [SerializeField] private DrawMode drawMode;
    [SerializeField] private Material terrainMaterial;
    
    [Header("Data")]
    public TerrainData terrainData;
    public NoiseData noiseData;
    public TextureData textureData;
    
    [Header("Editor")]
    [SerializeField, Range(0,6)] private int editorLOD;
    public bool autoUpdate;

    private float[,] m_falloffMap;
    private Queue<MapThreadInfo<MapData>> m_mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    private Queue<MapThreadInfo<MeshData>> m_meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();


    public int mapChunkSize {
        get {
            if (terrainData.useFlatShading)
                return 95;
            else
                return 239;
        }
    }

    public void DrawMapInEditor()
    {
        MapData mapData = GenerateMapData(Vector2.zero);

        MapDisplay display = FindObjectOfType<MapDisplay>();

        switch (drawMode)
        {
            case DrawMode.NoiseMap:
                display.DrawTexture(
                    TextureGenerator.TextureFromHeightMap(mapData.heightMap)
                );
                break;
            case DrawMode.Mesh:
                display.DrawMesh(
                    MeshGenerator.GenerateTerrainMesh(
                        mapData.heightMap,
                        terrainData.meshHeightMultiplier,
                        terrainData.meshHeightCurve,
                        editorLOD,
                        terrainData.useFlatShading
                    )
                );
                break;
            case DrawMode.FalloffMap:
                display.DrawTexture(
                    TextureGenerator.TextureFromHeightMap(
                        FalloffGenerator.GenerateFalloffMap(mapChunkSize)
                    )
                );
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


    private void OnValuesUpdated()
    {
        if (!Application.isPlaying)
            DrawMapInEditor();
    }
    
    private void OnTextureValuesUpdated()
    {
        textureData.ApplyToMaterial(terrainMaterial);
    }

    private void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback)
    {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, terrainData.meshHeightMultiplier, terrainData.meshHeightCurve, lod, terrainData.useFlatShading);
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
        int mapChunkSize2 = mapChunkSize + 2;
        float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize2, mapChunkSize2, noiseData.noiseScale, noiseData.octaves, noiseData.persistance, noiseData.lacunarity, noiseData.seed, centre + noiseData.offset, noiseData.normalizeMode);

        if (terrainData.useFalloff)
        {
            if (m_falloffMap == null)
                m_falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize2);

            for (int y = 0; y < mapChunkSize2; y++)
            {
                for (int x = 0; x < mapChunkSize2; x++)
                {
                    if (terrainData.useFalloff)
                    {
                        noiseMap[x,y] = Mathf.Clamp01(noiseMap[x,y] - m_falloffMap[x,y]);
                    }
                }
            }
        }
        
        textureData.UpdateMeshHeights(terrainMaterial, terrainData.minHeight, terrainData.maxHeight);

        return new MapData(noiseMap);
    }

    private void OnValidate()
    {
        if (terrainData != null)
        {
            terrainData.OnValuesUpdated -= OnValuesUpdated;
            terrainData.OnValuesUpdated += OnValuesUpdated;
        }

        if (noiseData != null)
        {
            noiseData.OnValuesUpdated -= OnValuesUpdated;
            noiseData.OnValuesUpdated += OnValuesUpdated;
        }
        if (textureData != null)
        {
            textureData.OnValuesUpdated -= OnTextureValuesUpdated;
            textureData.OnValuesUpdated += OnTextureValuesUpdated;
        }
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


public struct MapData
{
    public readonly float[,] heightMap;

    public MapData(float[,] heightMap)
    {
        this.heightMap = heightMap;
    }
}
