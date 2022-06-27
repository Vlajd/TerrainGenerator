using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour
{
    private const float SCALE = 1f;
    // Viewer Move Threshold For Chunk Update
    private const float VMTFCU = 25f;
    // Square VMTFCU
    private const float SQRVMTFCU = VMTFCU * VMTFCU;
    [SerializeField] private LODInfo[] detailLevels;
    [SerializeField] private Transform viewer;
    [SerializeField] private Material mapMaterial;
    [SerializeField] private static Vector2 viewerPosition;

    private static float m_maxViewDistance;
    private int m_chunkSize;
    private int m_chunksVisibleInDistance;
    private Dictionary<Vector2, TerrainChunk> m_terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    private static List<TerrainChunk> m_terrainChunksVisibleLastUpdate = new List<TerrainChunk>();
    private static MapGenerator m_mapGenerator;
    private Vector2 m_previousViewerPosition;

    private void Start()
    {
        m_mapGenerator = FindObjectOfType<MapGenerator>();

        m_maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistanceThreshold;
        m_chunkSize = MapGenerator.MAPCHUNKSIZE - 1;
        m_chunksVisibleInDistance = Mathf.RoundToInt(m_maxViewDistance / m_chunkSize);
        
        UpdateVisibleChunks();
    }
    
    private void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / SCALE;

        if ( !( (m_previousViewerPosition - viewerPosition).sqrMagnitude > SQRVMTFCU) )
            return;
        
        m_previousViewerPosition = viewerPosition;
        UpdateVisibleChunks();
    }
    
    private void UpdateVisibleChunks()
    {
        for (int i = 0; i < m_terrainChunksVisibleLastUpdate.Count; i++)
        {
            m_terrainChunksVisibleLastUpdate[i].SetVisible(false);
        }
        m_terrainChunksVisibleLastUpdate.Clear();


        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / m_chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / m_chunkSize);

        for (int yOffset = -m_chunksVisibleInDistance; yOffset <= m_chunksVisibleInDistance; yOffset++)
        {
            for (int xOffset = -m_chunksVisibleInDistance; xOffset <= m_chunksVisibleInDistance; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
                
                if (m_terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                {
                    m_terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                    
                }
                else
                {
                    m_terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, m_chunkSize, detailLevels, transform, mapMaterial));
                }
            }
        }
    }

    public class TerrainChunk {

        private GameObject m_meshObject;
        private Vector2 m_position;
        private Bounds m_bounds;
        private MeshRenderer m_meshRenderer;
        private MeshFilter m_meshFilter;
        private LODInfo[] m_detailLevels;
        private LODMesh[] m_lodMeshes;
        private MapData m_mapData;
        private bool m_mapDataReceived;
        private int m_previousLODIndex = -1;

        public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, Transform parent, Material material)
        {
            m_detailLevels = detailLevels;

            m_position = coord * size;
            m_bounds = new Bounds(m_position, Vector3.one * size);
            Vector3 positionV3 = new Vector3(m_position.x, 0, m_position.y);

            m_meshObject = new GameObject("TerrainChunk");
            m_meshRenderer = m_meshObject.AddComponent<MeshRenderer>();
            m_meshFilter = m_meshObject.AddComponent<MeshFilter>();
            m_meshRenderer.material = material;

            m_meshObject.transform.position = positionV3 * SCALE;
            m_meshObject.transform.parent = parent;
            m_meshObject.transform.localScale = Vector3.one * SCALE;

            SetVisible(false);

            m_lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i < detailLevels.Length; i++)
            {
                m_lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
            }

            m_mapGenerator.RequestMapData(m_position, OnMapDataReceived);
        }
        
        public void UpdateTerrainChunk()
        {
            if (!m_mapDataReceived)
                return;

            float viewerDistanceFromNearestEdge = Mathf.Sqrt(m_bounds.SqrDistance(viewerPosition));     
            bool visible = viewerDistanceFromNearestEdge <= m_maxViewDistance;
            
            if (visible)
            {
                int lodIndex = 0;
                for (int i = 0; i < m_detailLevels.Length; i++)
                {
                    if (viewerDistanceFromNearestEdge > m_detailLevels[i].visibleDistanceThreshold)
                        lodIndex = i + 1;
                    else
                        break;
                }

                if (lodIndex != m_previousLODIndex)
                {
                    LODMesh lodMesh = m_lodMeshes[lodIndex];
                    if (lodMesh.hasMesh)
                    {
                        m_previousLODIndex = lodIndex;
                        m_meshFilter.mesh = lodMesh.mesh;
                    }
                    else if (!lodMesh.hasRequestedMesh)
                    {
                        lodMesh.RequestMesh(m_mapData);
                    }
                }

                m_terrainChunksVisibleLastUpdate.Add(this);
            }

            SetVisible(visible);
        }
        
        public void SetVisible(bool visible)
        {
            m_meshObject.SetActive(visible);
        }

        public bool IsVisible()
        {
            return m_meshObject.activeSelf;
        }
        
        private void OnMeshDataReceived(MeshData meshData)
        {
            m_meshFilter.mesh = meshData.CreateMesh();
        }

        private void OnMapDataReceived(MapData mapData)
        {
            m_mapData = mapData;
            m_mapDataReceived = true;

            Texture2D texture = TextureGenerator.TextureFromColorMap(mapData.colorMap, MapGenerator.MAPCHUNKSIZE, MapGenerator.MAPCHUNKSIZE);
            m_meshRenderer.material.mainTexture = texture;

            UpdateTerrainChunk();
        }
    }
    

    private class LODMesh
    {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;

        private int m_lod;
        private System.Action m_updateCallback;

        public LODMesh(int lod, System.Action updateCallback)
        {
            m_lod = lod;
            m_updateCallback = updateCallback;
        }

        public void RequestMesh(MapData mapData)
        {
            hasRequestedMesh = true;
            m_mapGenerator.RequestMeshData(mapData, m_lod, OnMeshDataReceived);
        }
        
        private void OnMeshDataReceived(MeshData meshData)
        {
            mesh = meshData.CreateMesh();
            hasMesh = true;
            
            m_updateCallback();
        }
    }
    
    [System.Serializable]
    public struct LODInfo
    {
        public int lod;
        public float visibleDistanceThreshold;
    }
}
