﻿using System.Collections.Generic;
using UnityEngine;

public class MeshDeformer : MonoBehaviour
{
    public OctreeSpringFiller springFiller;
    public MeshFilter meshFilter;
    public int maxSubdivisionLevel = 3;
    public float influenceRadius = 1.0f;
    public bool showWeights = false;
    public bool logSubdividedTriangles = true;

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
        springFiller = GetComponent<OctreeSpringFiller>();

        InitializeDeformationMesh();
        BuildInfluenceMapping();
    }


    void InitializeDeformationMesh()
    {
        workingMesh = originalMesh;

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

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseClick();
        }
    }

    private void HandleMouseClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (FindClosestTriangle(ray, out int triangleIndex, out Vector3 hitPoint))
        {
            Vector3 localPoint = transform.InverseTransformPoint(hitPoint);
            SubdivideSingleTriangle(triangleIndex, localPoint);
        }
    }

    private bool FindClosestTriangle(Ray ray, out int closestTriangleIndex, out Vector3 hitPoint)
    {
        closestTriangleIndex = -1;
        hitPoint = Vector3.zero;
        float closestDistance = Mathf.Infinity;

        Matrix4x4 localToWorld = transform.localToWorldMatrix;
        Vector3[] vertices = workingMesh.vertices;
        int[] triangles = workingMesh.triangles;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = localToWorld.MultiplyPoint3x4(vertices[triangles[i]]);
            Vector3 v1 = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 1]]);
            Vector3 v2 = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 2]]);

            if (RayIntersectsTriangle(ray, v0, v1, v2, out Vector3 intersectPoint))
            {
                float distance = Vector3.Distance(ray.origin, intersectPoint);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTriangleIndex = i / 3;
                    hitPoint = intersectPoint;
                }
            }
        }

        return closestTriangleIndex != -1;
    }

    private bool RayIntersectsTriangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, out Vector3 intersectPoint)
    {
        intersectPoint = Vector3.zero;
        Vector3 e1 = v1 - v0;
        Vector3 e2 = v2 - v0;
        Vector3 h = Vector3.Cross(ray.direction, e2);
        float a = Vector3.Dot(e1, h);

        if (a > -Mathf.Epsilon && a < Mathf.Epsilon)
            return false;

        float f = 1.0f / a;
        Vector3 s = ray.origin - v0;
        float u = f * Vector3.Dot(s, h);

        if (u < 0.0 || u > 1.0)
            return false;

        Vector3 q = Vector3.Cross(s, e1);
        float v = f * Vector3.Dot(ray.direction, q);

        if (v < 0.0 || u + v > 1.0)
            return false;

        float t = f * Vector3.Dot(e2, q);
        if (t > Mathf.Epsilon)
        {
            intersectPoint = ray.origin + ray.direction * t;
            return true;
        }

        return false;
    }

    private void SubdivideSingleTriangle(int triangleIndex, Vector3 localPoint)
    {
        TriangleData data = triangleDataList[triangleIndex];
        if (!data.canSubdivide || data.subdivisionLevel >= maxSubdivisionLevel)
            return;

        if (logSubdividedTriangles)
        {
            Debug.Log($"Subdividing triangle {triangleIndex} at position {localPoint}");
        }

        SubdivideSelectedTriangles(new List<int> { triangleIndex });
        BuildInfluenceMapping();

    }

    public void HandleCollisionPoints(List<Vector3> collisionPoints)
    {
        foreach (Vector3 point in collisionPoints)
        {
            Vector3 localPoint = transform.InverseTransformPoint(point);
            FindAndSubdivideAffectedTriangles(localPoint);
        }


        NotifyMeshChanged();
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

    private void SubdivideSelectedTriangles(List<int> triangleIndices)
    {
        Vector3[] oldVertices = workingMesh.vertices;
        int[] oldTriangles = workingMesh.triangles;
        List<Vector3> newVertices = new List<Vector3>(oldVertices);

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

        // Build edge-to-triangles mapping with position-aware edges
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

            // Get all vertices of this triangle
            int baseIdx = currentTri * 3;
            int v0 = oldTriangles[baseIdx];
            int v1 = oldTriangles[baseIdx + 1];
            int v2 = oldTriangles[baseIdx + 2];

            // Find all triangles sharing these vertices (including on other faces)
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

        // Create midpoints for all edges of triangles to be subdivided
        Dictionary<Edge, int> edgeMidpoints = new Dictionary<Edge, int>();
        foreach (int triIdx in trianglesToSubdivide)
        {
            int i0 = oldTriangles[triIdx * 3];
            int i1 = oldTriangles[triIdx * 3 + 1];
            int i2 = oldTriangles[triIdx * 3 + 2];

            GetMidpoint(i0, i1, oldVertices, newVertices, edgeMidpoints);
            GetMidpoint(i1, i2, oldVertices, newVertices, edgeMidpoints);
            GetMidpoint(i2, i0, oldVertices, newVertices, edgeMidpoints);
        }

        // Rebuild all triangles
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

        // Update the mesh
        workingMesh.Clear();
        workingMesh.vertices = newVertices.ToArray();
        workingMesh.triangles = newTriangles.ToArray();
        workingMesh.RecalculateNormals();
        workingMesh.RecalculateBounds();

        triangleDataList = newTriangleData;
        baseVertices = workingMesh.vertices;
        currentVertices = baseVertices.Clone() as Vector3[];
        NotifyMeshChanged();

        if (logSubdividedTriangles)
        {
            Debug.Log($"Subdivision complete. New vertex count: {newVertices.Count}, new triangle count: {newTriangles.Count / 3}");
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

    private int GetMidpoint(int a, int b, Vector3[] verts, List<Vector3> newVerts, Dictionary<Edge, int> midpoints)
    {
        Edge edge = new Edge(a, b, verts);
        if (midpoints.TryGetValue(edge, out int index)) return index;

        Vector3 mid = (verts[a] + verts[b]) * 0.5f;
        newVerts.Add(mid);
        int newIndex = newVerts.Count - 1;
        midpoints.Add(edge, newIndex);

        Vector3 worldPos = transform.TransformPoint(mid);
        springFiller.AddSpringPointAtPosition(worldPos);

        return newIndex;
    }
    void BuildInfluenceMapping()
    {
        vertexInfluences = new List<WeightedInfluence>[baseVertices.Length];

        for (int i = 0; i < baseVertices.Length; i++)
        {
            vertexInfluences[i] = new List<WeightedInfluence>();
            Vector3 vertexWorld = transform.TransformPoint(baseVertices[i]);

            foreach (SpringPoint sp in springFiller.allSpringPoints)
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


        NotifyMeshChanged();
    }
    public void NotifyMeshChanged()
    {
        if (springFiller != null)
        {

        }
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
        public SpringPoint springPoint;
        public float weight;
    }


}