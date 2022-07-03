using UnityEngine;

public static class MeshGenerator
{
    public static MeshData GenerateTerrainMesh(float[,] heightMap, float heightMultiplier, AnimationCurve _heightCurve, int levelOfDetail)
    {
        AnimationCurve heightCurve = new AnimationCurve(_heightCurve.keys);

        // MeshSimplificationIncrement
        int meshSI = (levelOfDetail == 0)? 1: levelOfDetail * 2; 

        int borderedSize = heightMap.GetLength(0);
        int meshSize = borderedSize - 2 * meshSI;
        int meshSizeUnsimplified = borderedSize - 2;

        float topLeftX = (meshSizeUnsimplified - 1) * -.5f;
        float topLeftZ = (meshSizeUnsimplified - 1) * .5f;

        int verteciesPerLine = (meshSize - 1) / meshSI + 1;

        MeshData meshData = new MeshData(verteciesPerLine);

        int[,] vertexIndicesMap = new int[borderedSize,borderedSize];
        int meshVertexIndex = 0;
        int borderVertexIndex = -1;

        for (int y = 0; y < borderedSize; y += meshSI)
        {
            for (int x = 0; x < borderedSize; x += meshSI)
            {
                bool isBorderVertex = y == 0 || y == borderedSize - 1 || x == 0 || x == borderedSize - 1;
                
                if (isBorderVertex)
                {
                    vertexIndicesMap[x,y] = borderVertexIndex;
                    borderVertexIndex--;
                }
                else
                {
                    vertexIndicesMap[x,y] = meshVertexIndex;
                    meshVertexIndex++;
                }
            }
        }
        
        for (int y = 0; y < borderedSize; y += meshSI)
        {
            for (int x = 0; x < borderedSize; x += meshSI)
            {
                int vertexIndex = vertexIndicesMap[x,y];

                Vector2 percent = new Vector2((x - meshSI) / (float)meshSize, (y - meshSI) / (float)meshSize);
                
                float height = heightCurve.Evaluate(heightMap[x,y]) * heightMultiplier;
                Vector3 vertexPosition = new Vector3(topLeftX + percent.x * meshSizeUnsimplified, height, topLeftZ - percent.y * meshSizeUnsimplified);
                
                meshData.AddVertex(vertexPosition, percent, vertexIndex);

                if (x < borderedSize - 1 && y < borderedSize - 1)
                {
                    int a = vertexIndicesMap[x, y];
                    int b = vertexIndicesMap[x + meshSI, y];
                    int c = vertexIndicesMap[x, y + meshSI];
                    int d = vertexIndicesMap[x + meshSI, y + meshSI];
                    meshData.AddTriangle(a,d,c);
                    meshData.AddTriangle(d,a,b);
                }

                vertexIndex++;
            }
        }
        
        meshData.BakeNormals();

        return meshData;
    }
}

public class MeshData
{
    private Vector3[] m_vertices;
    private int[] m_indices;
    private Vector2[] m_texCoords;
    private Vector3[] m_bakedNormals;
    
    private Vector3[] m_borderVertices;
    private int[] m_borderIndices;

    private int m_triangleIndex = 0;
    private int m_borderTriangleIndex = 0;

    public MeshData(int verteciesPerLine)
    {
        int doubleVerteciesPerLine = verteciesPerLine * verteciesPerLine;
        m_vertices = new Vector3[doubleVerteciesPerLine];
        m_texCoords = new Vector2[doubleVerteciesPerLine];
        m_indices = new int[(verteciesPerLine - 1) * (verteciesPerLine - 1) * 6];

        m_borderVertices = new Vector3[verteciesPerLine * 4 + 4];
        m_borderIndices = new int[24 * verteciesPerLine];
    }

    public void AddVertex(Vector3 vertexPosition, Vector2 texCoord, int vertexIndex)
    {
        if (vertexIndex < 0)
        {
            m_borderVertices[-vertexIndex - 1] = vertexPosition;
        }
        else
        {
            m_vertices[vertexIndex] = vertexPosition;
            m_texCoords[vertexIndex] = texCoord;
        }
    }
    
    public void AddTriangle(int a, int b, int c)
    {
        if (a < 0 || b < 0 || c < 0)
        {
            m_borderIndices[m_borderTriangleIndex]     = a;
            m_borderIndices[m_borderTriangleIndex + 1] = b;
            m_borderIndices[m_borderTriangleIndex + 2] = c;
            m_borderTriangleIndex += 3;
        }
        else
        {
            m_indices[m_triangleIndex]     = a;
            m_indices[m_triangleIndex + 1] = b;
            m_indices[m_triangleIndex + 2] = c;
            m_triangleIndex += 3;
        }
    }
    
    public Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = m_vertices;
        mesh.triangles = m_indices;
        mesh.uv = m_texCoords;
        mesh.normals = m_bakedNormals;
        return mesh;
    }

    public void BakeNormals()
    {
        m_bakedNormals = CalculateNormals();
    }

    private Vector3[] CalculateNormals()
    {
        Vector3[] vertexNormals = new Vector3[m_vertices.Length];

        int triangleCount = m_indices.Length / 3;
        for (int i = 0; i < triangleCount; i++)
        {
            int normalTriangleIndex =  i * 3;
            int vertexIndexA = m_indices[normalTriangleIndex];
            int vertexIndexB = m_indices[normalTriangleIndex + 1];
            int vertexIndexC = m_indices[normalTriangleIndex + 2];

            Vector3 triangleNormal = SurfaceNormalFromIndices(vertexIndexA, vertexIndexB, vertexIndexC);
            vertexNormals[vertexIndexA] += triangleNormal;
            vertexNormals[vertexIndexB] += triangleNormal;
            vertexNormals[vertexIndexC] += triangleNormal;
        }

        int borderTriangleCount = m_borderIndices.Length / 3;
        for (int i = 0; i < borderTriangleCount; i++)
        {
            int normalTriangleIndex =  i * 3;
            int vertexIndexA = m_borderIndices[normalTriangleIndex];
            int vertexIndexB = m_borderIndices[normalTriangleIndex + 1];
            int vertexIndexC = m_borderIndices[normalTriangleIndex + 2];

            Vector3 triangleNormal = SurfaceNormalFromIndices(vertexIndexA, vertexIndexB, vertexIndexC);
            if (vertexIndexA >= 0)
                vertexNormals[vertexIndexA] += triangleNormal;
            if (vertexIndexB >= 0)
                vertexNormals[vertexIndexB] += triangleNormal;
            if (vertexIndexC >= 0)
                vertexNormals[vertexIndexC] += triangleNormal;
        }

        foreach (Vector3 vertexNormal in vertexNormals)
            vertexNormal.Normalize();

        return vertexNormals;
    }

    private Vector3 SurfaceNormalFromIndices(int indexA, int indexB, int indexC)
    {
        Vector3 pointA = (indexA < 0)?m_borderVertices[-indexA - 1] : m_vertices[indexA];
        Vector3 pointB = (indexB < 0)?m_borderVertices[-indexB - 1] : m_vertices[indexB];
        Vector3 pointC = (indexC < 0)?m_borderVertices[-indexC - 1] : m_vertices[indexC];

        Vector3 sideAB = pointB - pointA;
        Vector3 sideAC = pointC - pointA;
        return Vector3.Cross(sideAB, sideAC).normalized;
    }
}
