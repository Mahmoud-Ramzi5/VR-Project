using UnityEngine;
using System.Collections.Generic;

public class MeshDeformer : MonoBehaviour
{
    public OctreeSpringFiller springFiller;
    public MeshFilter meshFilter;
    public int subdivisionLevel = 1;
    public float influenceRadius = 1.0f;
    public bool showWeights = false;

    private Mesh originalMesh;
    private Mesh subdividedMesh;
    private List<WeightedInfluence>[] vertexInfluences;
    private Vector3[] baseVertices;
    private Vector3[] currentVertices;

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
        subdividedMesh = Instantiate(originalMesh);
        for (int i = 0; i < subdivisionLevel; i++)
        {
            subdividedMesh = SubdivideMesh(subdividedMesh);
        }
        meshFilter.mesh = subdividedMesh;

        baseVertices = subdividedMesh.vertices;
        currentVertices = baseVertices.Clone() as Vector3[];
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

            if (totalWeight > 0.01f)
            {
                currentVertices[i] = baseVertices[i] + newPos / totalWeight;
            }
            else
            {
                currentVertices[i] = baseVertices[i];
            }
        }

        subdividedMesh.vertices = currentVertices;
        subdividedMesh.RecalculateNormals();
        subdividedMesh.RecalculateBounds();
    }

    private struct Edge
    {
        public int v1, v2;
        public Edge(int a, int b)
        {
            v1 = Mathf.Min(a, b);
            v2 = Mathf.Max(a, b);
        }
    }

    Mesh SubdivideMesh(Mesh mesh)
    {
        Vector3[] oldVertices = mesh.vertices;
        int[] oldTriangles = mesh.triangles;
        List<Vector3> newVertices = new List<Vector3>(oldVertices);
        List<int> newTriangles = new List<int>();
        Dictionary<Edge, int> edgeMidpoints = new Dictionary<Edge, int>();

        int triangleCount = oldTriangles.Length / 3;
        for (int i = 0; i < triangleCount; i++)
        {
            int i0 = oldTriangles[i * 3];
            int i1 = oldTriangles[i * 3 + 1];
            int i2 = oldTriangles[i * 3 + 2];

            int m01 = GetMidpoint(i0, i1, oldVertices, newVertices, edgeMidpoints);
            int m12 = GetMidpoint(i1, i2, oldVertices, newVertices, edgeMidpoints);
            int m20 = GetMidpoint(i2, i0, oldVertices, newVertices, edgeMidpoints);

            newTriangles.AddRange(new[] { i0, m01, m20 });
            newTriangles.AddRange(new[] { m01, i1, m12 });
            newTriangles.AddRange(new[] { m20, m12, i2 });
            newTriangles.AddRange(new[] { m01, m12, m20 });
        }

        Mesh newMesh = new Mesh
        {
            vertices = newVertices.ToArray(),
            triangles = newTriangles.ToArray(),
            normals = mesh.normals,
            uv = mesh.uv
        };
        newMesh.RecalculateNormals();
        return newMesh;
    }

    int GetMidpoint(int a, int b, Vector3[] verts, List<Vector3> newVerts, Dictionary<Edge, int> midpoints)
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
        public SpringPoint springPoint;
        public float weight;
    }
}