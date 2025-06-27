using UnityEngine;
using System.Collections.Generic;

public class MeshManager : MonoBehaviour
{
    public MeshFilter meshFilter;
    public OctreeSpringFiller springFiller;
    public int maxSubdivisionLevel = 3;
    public bool logSubdividedTriangles = true;

    private Mesh originalMesh;
    private Mesh currentMesh;
    private List<Vector3> vertices;
    private List<int> triangles;
    private List<TriangleData> triangleDataList;

    public Mesh CurrentMesh => currentMesh;
    public List<Vector3> Vertices => vertices;
    public List<int> Triangles => triangles;
    public int TriangleCount => triangleDataList.Count;

    void Awake()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
    }

    public void InitializeMesh(Mesh mesh)
    {
        originalMesh = mesh;
        currentMesh = Instantiate(mesh);
        meshFilter.mesh = currentMesh;

        vertices = new List<Vector3>(currentMesh.vertices);
        triangles = new List<int>(currentMesh.triangles);

        triangleDataList = new List<TriangleData>();
        int triangleCount = triangles.Count / 3;
        for (int i = 0; i < triangleCount; i++)
        {
            triangleDataList.Add(new TriangleData
            {
                originalIndex = i,
                subdivisionLevel = 0,
                canSubdivide = true
            });
        }
    }

    public TriangleData GetTriangleData(int index)
    {
        return triangleDataList[index];
    }

    public void SubdivideTriangles(List<int> triangleIndices, OctreeSpringFiller springFiller)
    {
        Vector3[] oldVertices = vertices.ToArray();
        int[] oldTriangles = triangles.ToArray();
        List<Vector3> newVertices = new List<Vector3>(vertices);

        // Build vertex-to-triangles mapping
        Dictionary<int, List<int>> vertexToTriangles = new Dictionary<int, List<int>>();
        for (int i = 0; i < oldTriangles.Length; i++)
        {
            int vertexIndex = oldTriangles[i];
            if (!vertexToTriangles.ContainsKey(vertexIndex))
            {
                vertexToTriangles[vertexIndex] = new List<int>();
            }
            vertexToTriangles[vertexIndex].Add(i / 3);
        }

        // Build edge-to-triangles mapping
        Dictionary<Edge, List<int>> edgeToTriangles = new Dictionary<Edge, List<int>>();
        for (int i = 0; i < oldTriangles.Length; i += 3)
        {
            int triangleIdx = i / 3;
            int i0 = oldTriangles[i];
            int i1 = oldTriangles[i + 1];
            int i2 = oldTriangles[i + 2];

            AddEdgeToMap(new Edge(i0, i1, oldVertices), triangleIdx, edgeToTriangles);
            AddEdgeToMap(new Edge(i1, i2, oldVertices), triangleIdx, edgeToTriangles);
            AddEdgeToMap(new Edge(i2, i0, oldVertices), triangleIdx, edgeToTriangles);
        }

        // Find all triangles to subdivide using BFS
        HashSet<int> trianglesToSubdivide = new HashSet<int>();
        Queue<int> trianglesToProcess = new Queue<int>(triangleIndices);

        while (trianglesToProcess.Count > 0)
        {
            int currentTri = trianglesToProcess.Dequeue();
            if (trianglesToSubdivide.Contains(currentTri)) continue;

            TriangleData data = triangleDataList[currentTri];
            if (!data.canSubdivide || data.subdivisionLevel >= maxSubdivisionLevel) continue;

            trianglesToSubdivide.Add(currentTri);

            // Get vertices of current triangle
            int baseIdx = currentTri * 3;
            int v0 = oldTriangles[baseIdx];
            int v1 = oldTriangles[baseIdx + 1];
            int v2 = oldTriangles[baseIdx + 2];

            // Find connected triangles
            foreach (int vertexIndex in new[] { v0, v1, v2 })
            {
                if (vertexToTriangles.TryGetValue(vertexIndex, out List<int> connectedTris))
                {
                    foreach (int connectedTri in connectedTris)
                    {
                        if (!trianglesToSubdivide.Contains(connectedTri))
                        {
                            trianglesToProcess.Enqueue(connectedTri);
                        }
                    }
                }
            }
        }

        // Create midpoints for edges
        Dictionary<Edge, int> edgeMidpoints = new Dictionary<Edge, int>();
        foreach (int triIdx in trianglesToSubdivide)
        {
            int i0 = oldTriangles[triIdx * 3];
            int i1 = oldTriangles[triIdx * 3 + 1];
            int i2 = oldTriangles[triIdx * 3 + 2];

            GetMidpoint(i0, i1, oldVertices, newVertices, edgeMidpoints, springFiller);
            GetMidpoint(i1, i2, oldVertices, newVertices, edgeMidpoints, springFiller);
            GetMidpoint(i2, i0, oldVertices, newVertices, edgeMidpoints, springFiller);
        }

        // Rebuild triangles
        List<int> newTriangles = new List<int>();
        List<TriangleData> newTriangleData = new List<TriangleData>();

        for (int i = 0; i < oldTriangles.Length; i += 3)
        {
            int originalTriangleIndex = i / 3;
            TriangleData originalData = triangleDataList[originalTriangleIndex];

            int i0 = oldTriangles[i];
            int i1 = oldTriangles[i + 1];
            int i2 = oldTriangles[i + 2];

            bool has_m01 = edgeMidpoints.TryGetValue(new Edge(i0, i1, oldVertices), out int m01);
            bool has_m12 = edgeMidpoints.TryGetValue(new Edge(i1, i2, oldVertices), out int m12);
            bool has_m20 = edgeMidpoints.TryGetValue(new Edge(i2, i0, oldVertices), out int m20);

            int splitEdgeCount = (has_m01 ? 1 : 0) + (has_m12 ? 1 : 0) + (has_m20 ? 1 : 0);

            switch (splitEdgeCount)
            {
                case 3:
                    int newLevel = originalData.subdivisionLevel + 1;
                    newTriangles.AddRange(new[] { i0, m01, m20 });
                    newTriangles.AddRange(new[] { m01, i1, m12 });
                    newTriangles.AddRange(new[] { m20, m12, i2 });
                    newTriangles.AddRange(new[] { m01, m12, m20 });

                    for (int j = 0; j < 4; j++)
                    {
                        newTriangleData.Add(new TriangleData
                        {
                            originalIndex = originalData.originalIndex,
                            subdivisionLevel = newLevel,
                            canSubdivide = newLevel < maxSubdivisionLevel
                        });
                    }
                    break;

                case 2:
                    if (has_m01 && has_m12)
                    {
                        newTriangles.AddRange(new[] { i0, m01, m12 });
                        newTriangles.AddRange(new[] { i0, m12, i2 });
                        newTriangles.AddRange(new[] { m01, i1, m12 });
                    }
                    else if (has_m12 && has_m20)
                    {
                        newTriangles.AddRange(new[] { i1, m12, m20 });
                        newTriangles.AddRange(new[] { i1, m20, i0 });
                        newTriangles.AddRange(new[] { m12, i2, m20 });
                    }
                    else
                    {
                        newTriangles.AddRange(new[] { i2, m20, m01 });
                        newTriangles.AddRange(new[] { i2, m01, i1 });
                        newTriangles.AddRange(new[] { m20, i0, m01 });
                    }

                    for (int j = 0; j < 3; j++)
                    {
                        newTriangleData.Add(new TriangleData
                        {
                            originalIndex = originalData.originalIndex,
                            subdivisionLevel = originalData.subdivisionLevel,
                            canSubdivide = originalData.canSubdivide
                        });
                    }
                    break;

                case 1:
                    if (has_m01)
                    {
                        newTriangles.AddRange(new[] { i2, i0, m01 });
                        newTriangles.AddRange(new[] { i2, m01, i1 });
                    }
                    else if (has_m12)
                    {
                        newTriangles.AddRange(new[] { i0, i1, m12 });
                        newTriangles.AddRange(new[] { i0, m12, i2 });
                    }
                    else
                    {
                        newTriangles.AddRange(new[] { i1, i2, m20 });
                        newTriangles.AddRange(new[] { i1, m20, i0 });
                    }

                    for (int j = 0; j < 2; j++)
                    {
                        newTriangleData.Add(new TriangleData
                        {
                            originalIndex = originalData.originalIndex,
                            subdivisionLevel = originalData.subdivisionLevel,
                            canSubdivide = originalData.canSubdivide
                        });
                    }
                    break;

                case 0:
                default:
                    newTriangles.AddRange(new[] { i0, i1, i2 });
                    newTriangleData.Add(originalData);
                    break;
            }
        }

        // Update mesh data
        vertices = newVertices;
        triangles = newTriangles;
        triangleDataList = newTriangleData;

        // Update Unity mesh
        currentMesh.Clear();
        currentMesh.vertices = vertices.ToArray();
        currentMesh.triangles = triangles.ToArray();
        currentMesh.RecalculateNormals();
        currentMesh.RecalculateBounds();
        meshFilter.mesh = currentMesh;

        if (logSubdividedTriangles)
        {
            Debug.Log($"Subdivision complete. New vertex count: {vertices.Count}, new triangle count: {triangles.Count / 3}");
        }
    }

    private void AddEdgeToMap(Edge edge, int triangleIdx, Dictionary<Edge, List<int>> edgeToTriangles)
    {
        if (!edgeToTriangles.ContainsKey(edge))
        {
            edgeToTriangles[edge] = new List<int>();
        }
        edgeToTriangles[edge].Add(triangleIdx);
    }

    private int GetMidpoint(int a, int b, Vector3[] verts, List<Vector3> newVerts,
                            Dictionary<Edge, int> midpoints, OctreeSpringFiller springFiller)
    {
        Edge edge = new Edge(a, b, verts);
        if (midpoints.TryGetValue(edge, out int index)) return index;

        Vector3 localMid = (verts[a] + verts[b]) * 0.5f;
        newVerts.Add(localMid);
        int newIndex = newVerts.Count - 1;
        midpoints.Add(edge, newIndex);

        Vector3 worldPos = transform.TransformPoint(localMid);

        // Use centralized vertex addition
        AddVertexAndNotify(localMid, worldPos);

        return newIndex;
    }
    public void AddVertexAndNotify(Vector3 localPosition, Vector3 worldPosition)
    {
        vertices.Add(localPosition);
        currentMesh.vertices = vertices.ToArray();
        currentMesh.RecalculateNormals();

        // Notify OctreeSpringFiller to add spring point
        springFiller.AddSpringPointAtPosition(worldPosition);
        
    }

    // Update vertex positions
    public void UpdateVertexPositions(Vector3[] newPositions)
    {
        vertices = new List<Vector3>(newPositions);
        currentMesh.vertices = newPositions;
        currentMesh.RecalculateNormals();
        currentMesh.RecalculateBounds();
    }

    public struct TriangleData
    {
        public int originalIndex;
        public int subdivisionLevel;
        public bool canSubdivide;
    }

    private struct Edge
    {
        public int v1, v2;
        public Vector3 pos1, pos2;

        public Edge(int a, int b, Vector3[] vertices)
        {
            v1 = Mathf.Min(a, b);
            v2 = Mathf.Max(a, b);
            pos1 = vertices[v1];
            pos2 = vertices[v2];
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Edge)) return false;
            Edge other = (Edge)obj;
            return (pos1 == other.pos1 && pos2 == other.pos2) ||
                   (pos1 == other.pos2 && pos2 == other.pos1);
        }

        public override int GetHashCode()
        {
            return pos1.GetHashCode() ^ pos2.GetHashCode();
        }
    }
}