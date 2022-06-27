using UnityEngine;

public static class MeshGenerator
{
    public static MeshData GenerateTerrainMesh(float[,] heightMap, float heightMultiplier, AnimationCurve _heightCurve, int levelOfDetail)
    {
        AnimationCurve heightCurve = new AnimationCurve(_heightCurve.keys);

        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);
        float topLeftX = (width - 1) * -.5f;
        float topLeftZ = (height - 1) * .5f;

        // MeshSimplificationIncrement
        int meshSI = (levelOfDetail == 0)? 1: levelOfDetail * 2; 
        int verteciesPerLine = (width - 1) / meshSI + 1;

        MeshData meshData = new MeshData(verteciesPerLine, verteciesPerLine);
        int vertexIndex = 0;
        
        for (int y = 0; y < height; y += meshSI)
        {
            for (int x = 0; x < width; x += meshSI)
            {
                meshData.vertices[vertexIndex] = new Vector3(topLeftX + x, heightCurve.Evaluate(heightMap[x,y]) * heightMultiplier, topLeftZ - y);
                meshData.texCoords[vertexIndex] = new Vector2(x / (float)width, y / (float)height);

                if (x < width - 1 && y < height - 1)
                {
                    meshData.AddTriangle(vertexIndex, vertexIndex + verteciesPerLine + 1, vertexIndex + verteciesPerLine);
                    meshData.AddTriangle(vertexIndex + verteciesPerLine + 1, vertexIndex, vertexIndex + 1);
                }

                vertexIndex++;
            }
        }
        
        return meshData;
    }
}

public class MeshData
{
    public Vector3[] vertices;
    public int[] indices;
    public Vector2[] texCoords;

    private int m_triangleIndex = 0;

    public MeshData(int meshWidth, int meshHeight)
    {
        vertices = new Vector3[meshWidth * meshHeight];
        texCoords = new Vector2[meshWidth * meshHeight];
        indices = new int[(meshWidth - 1) * (meshHeight - 1) * 6];
    }
    
    public void AddTriangle(int a, int b, int c)
    {
        indices[m_triangleIndex]     = a;
        indices[m_triangleIndex + 1] = b;
        indices[m_triangleIndex + 2] = c;
        m_triangleIndex += 3;
    }
    
    public Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = indices;
        mesh.uv = texCoords;
        mesh.normals = CalculateNormals();
        return mesh;
    }

    private Vector3[] CalculateNormals()
    {
        Vector3[] vertexNormals = new Vector3[vertices.Length];
        int triangleCount = indices.Length / 3;

        for (int i = 0; i < triangleCount; i++)
        {
            int normalTriangleIndex =  i * 3;
            int vertexIndexA = indices[normalTriangleIndex];
            int vertexIndexB = indices[normalTriangleIndex + 1];
            int vertexIndexC = indices[normalTriangleIndex + 2];

            Vector3 triangleNormal = SurfaceNormalFromIndices(vertexIndexA, vertexIndexB, vertexIndexC);
            vertexNormals[vertexIndexA] += triangleNormal;
            vertexNormals[vertexIndexB] += triangleNormal;
            vertexNormals[vertexIndexC] += triangleNormal;
        }

        foreach (Vector3 vertexNormal in vertexNormals)
            vertexNormal.Normalize();

        return vertexNormals;
    }

    Vector3 SurfaceNormalFromIndices(int indexA, int indexB, int indexC)
    {
        Vector3 pointA = vertices[indexA];
        Vector3 pointB = vertices[indexB];
        Vector3 pointC = vertices[indexC];

        Vector3 sideAB = pointB - pointA;
        Vector3 sideAC = pointC - pointA;
        return Vector3.Cross(sideAB, sideAC).normalized;
    }
}
