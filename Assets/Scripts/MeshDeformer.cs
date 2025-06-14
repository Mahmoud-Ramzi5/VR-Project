using UnityEngine;
using System.Collections.Generic;

public class MeshDeformer : MonoBehaviour
{
    public OctreeSpringFillerTest springFiller;
    public MeshFilter meshFilter;
    public int maxSubdivisionLevel = 3;
    public float influenceRadius = 1.0f;
    public bool showWeights = false;
    public bool logSubdividedTriangles = true; // New flag for logging

    private Mesh originalMesh;
    private Mesh workingMesh;
    private List<WeightedInfluence>[] vertexInfluences;
    private Vector3[] baseVertices;
    private Vector3[] currentVertices;

    private struct TriangleData
    {
        public int originalIndex;
        public int subdivisionLevel;
        public bool canSubdivide;
    }
    private List<TriangleData> triangleDataList;

    void Start()
    {
        if (!meshFilter) meshFilter = GetComponent<MeshFilter>();
        originalMesh = meshFilter.mesh;
        springFiller = GetComponent<OctreeSpringFillerTest>();

        InitializeDeformationMesh();
        BuildInfluenceMapping();
    }

    void InitializeDeformationMesh()
    {
        workingMesh = Instantiate(originalMesh);
        meshFilter.mesh = workingMesh;

        baseVertices = workingMesh.vertices;
        currentVertices = baseVertices.Clone() as Vector3[];

        triangleDataList = new List<TriangleData>();
        int triangleCount = workingMesh.triangles.Length / 3;
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

    public void HandleCollisionPoints(List<Vector3> collisionPoints)
    {
        foreach (Vector3 point in collisionPoints)
        {
            Vector3 localPoint = transform.InverseTransformPoint(point);
            FindAndSubdivideAffectedTriangles(localPoint);
        }

        workingMesh.vertices = currentVertices;
        workingMesh.RecalculateNormals();
        workingMesh.RecalculateBounds();
    }

    private void FindAndSubdivideAffectedTriangles(Vector3 localPoint)
    {
        Vector3[] vertices = workingMesh.vertices;
        int[] triangles = workingMesh.triangles;
        List<int> trianglesToSubdivide = new List<int>();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int triangleIndex = i / 3;
            TriangleData data = triangleDataList[triangleIndex];

            if (!data.canSubdivide || data.subdivisionLevel >= maxSubdivisionLevel)
                continue;

            Vector3 v1 = vertices[triangles[i]];
            Vector3 v2 = vertices[triangles[i + 1]];
            Vector3 v3 = vertices[triangles[i + 2]];

            if (IsPointNearTriangle(localPoint, v1, v2, v3))
            {
                trianglesToSubdivide.Add(triangleIndex);
            }
        }

        if (trianglesToSubdivide.Count > 0)
        {
            if (logSubdividedTriangles)
            {
                Debug.Log($"Subdividing {trianglesToSubdivide.Count} triangles at indices: " +
                         string.Join(", ", trianglesToSubdivide));
            }

            SubdivideSelectedTriangles(trianglesToSubdivide);
            BuildInfluenceMapping();
        }
    }

    private bool IsPointNearTriangle(Vector3 point, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        Vector3 centroid = (v1 + v2 + v3) / 3f;
        float distance = Vector3.Distance(point, centroid);
        return distance < influenceRadius;
    }

    private void SubdivideSelectedTriangles(List<int> triangleIndices)
    {
        Vector3[] oldVertices = workingMesh.vertices;
        int[] oldTriangles = workingMesh.triangles;
        List<Vector3> newVertices = new List<Vector3>(oldVertices);
        List<int> newTriangles = new List<int>();
        Dictionary<Edge, int> edgeMidpoints = new Dictionary<Edge, int>();

        List<TriangleData> newTriangleData = new List<TriangleData>();

        for (int i = 0; i < oldTriangles.Length; i += 3)
        {
            int originalTriangleIndex = i / 3;
            TriangleData originalData = triangleDataList[originalTriangleIndex];

            if (triangleIndices.Contains(originalTriangleIndex))
            {
                originalData.canSubdivide = false;
                triangleDataList[originalTriangleIndex] = originalData;

                int newLevel = originalData.subdivisionLevel + 1;
                int i0 = oldTriangles[i];
                int i1 = oldTriangles[i + 1];
                int i2 = oldTriangles[i + 2];

                int m01 = GetMidpoint(i0, i1, oldVertices, newVertices, edgeMidpoints);
                int m12 = GetMidpoint(i1, i2, oldVertices, newVertices, edgeMidpoints);
                int m20 = GetMidpoint(i2, i0, oldVertices, newVertices, edgeMidpoints);

                // Store new triangle indices for logging
                List<int> newTriangleIndices = new List<int>();
                int baseIndex = newTriangles.Count / 3;

                newTriangles.AddRange(new[] { i0, m01, m20 });
                newTriangles.AddRange(new[] { m01, i1, m12 });
                newTriangles.AddRange(new[] { m20, m12, i2 });
                newTriangles.AddRange(new[] { m01, m12, m20 });

                if (logSubdividedTriangles)
                {
                    Debug.Log($"Subdivided triangle {originalTriangleIndex} into new triangles at indices: " +
                             $"{baseIndex}, {baseIndex + 1}, {baseIndex + 2}, {baseIndex + 3}");
                }

                for (int j = 0; j < 4; j++)
                {
                    newTriangleData.Add(new TriangleData
                    {
                        originalIndex = originalData.originalIndex,
                        subdivisionLevel = newLevel,
                        canSubdivide = newLevel < maxSubdivisionLevel
                    });
                }
            }
            else
            {
                newTriangles.AddRange(new[] {
                    oldTriangles[i],
                    oldTriangles[i + 1],
                    oldTriangles[i + 2]
                });
                newTriangleData.Add(originalData);
            }
        }

        workingMesh.vertices = newVertices.ToArray();
        workingMesh.triangles = newTriangles.ToArray();
        workingMesh.RecalculateNormals();
        workingMesh.RecalculateBounds();

        triangleDataList = newTriangleData;
        baseVertices = workingMesh.vertices;
        currentVertices = baseVertices.Clone() as Vector3[];
    }

    void BuildInfluenceMapping()
    {
        vertexInfluences = new List<WeightedInfluence>[baseVertices.Length];

        for (int i = 0; i < baseVertices.Length; i++)
        {
            vertexInfluences[i] = new List<WeightedInfluence>();
            Vector3 vertexWorld = transform.TransformPoint(baseVertices[i]);

            foreach (SpringPointTest sp in springFiller.allSpringPointsTest)
            {
                float distance = Vector3.Distance(vertexWorld, sp.position);
                if (distance < influenceRadius)
                {
                    float weight = Mathf.Exp(-distance * distance);
                    vertexInfluences[i].Add(new WeightedInfluence
                    {
                        springPoint = sp,
                        weight = weight
                    });
                }
            }
        }
    }

    void LateUpdate()
    {
        UpdateDeformation();
    }

    void UpdateDeformation()
    {
        for (int i = 0; i < currentVertices.Length; i++)
        {
            Vector3 newPos = Vector3.zero;
            float totalWeight = 0f;

            foreach (var inf in vertexInfluences[i])
            {
                Vector3 displacement = inf.springPoint.position - inf.springPoint.initialPosition;

                newPos += displacement * inf.weight;
                totalWeight += inf.weight;
            }

            currentVertices[i] = totalWeight > 0.01f ?
                baseVertices[i] + newPos / totalWeight :
                baseVertices[i];
        }

        workingMesh.vertices = currentVertices;
        workingMesh.RecalculateNormals();
        workingMesh.RecalculateBounds();
    }

    private struct Edge
    {
        public int v1, v2;
        public Edge(int a, int b) { v1 = Mathf.Min(a, b); v2 = Mathf.Max(a, b); }
    }

    private int GetMidpoint(int a, int b, Vector3[] verts, List<Vector3> newVerts, Dictionary<Edge, int> midpoints)
    {
        Edge edge = new Edge(a, b);
        if (midpoints.TryGetValue(edge, out int index)) return index;

        Vector3 mid = (verts[a] + verts[b]) * 0.5f;
        newVerts.Add(mid);
        int newIndex = newVerts.Count - 1;
        midpoints.Add(edge, newIndex);
        return newIndex;
    }

    void OnDrawGizmosSelected()
    {
        if (!showWeights || vertexInfluences == null) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < currentVertices.Length; i++)
        {
            Vector3 worldPos = transform.TransformPoint(currentVertices[i]);
            foreach (var inf in vertexInfluences[i])
            {
                Gizmos.DrawLine(worldPos, inf.springPoint.position);
            }
        }
    }

    private struct WeightedInfluence
    {
        public SpringPointTest springPoint;
        public float weight;
    }
}