using UnityEngine;
using System.Collections.Generic;

public class MeshSpringFiller : MonoBehaviour
{
    public SpringPoint springPointPrefab;
    public Mesh sourceMesh;
    public float pointSpacing = 0.5f;
    public float connectionRadius = 1.0f;
    public float springConstant = 20f;
    public float damperConstant = 1.0f;

    [Header("Surface Options")]
    public bool generateSurfacePoints = true;
    public float surfacePointDensity = 2f;

    [Header("Constraints")]
    public bool fixSurfacePoints = false;

    private List<SpringPoint> points = new List<SpringPoint>();
    private Bounds meshBounds;

    void Start()
    {
        if (sourceMesh == null)
        {
            // Try to get mesh from MeshFilter if not assigned
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf != null) sourceMesh = mf.sharedMesh;

            if (sourceMesh == null)
            {
                Debug.LogError("No source mesh assigned!");
                return;
            }
        }

        FillMeshWithPoints();
        CreateConnections();
    }

    void FillMeshWithPoints()
    {
        ClearExistingPoints();

        // Get accurate bounds (in world space)
        meshBounds = GetWorldSpaceBounds();

        GenerateVolumePoints();

        if (generateSurfacePoints)
        {
            GenerateSurfacePoints();
        }

        Debug.Log($"Generated {points.Count} points");
    }

    Bounds GetWorldSpaceBounds()
    {
        if (sourceMesh == null) return new Bounds();

        // Get local bounds and transform to world space
        Bounds bounds = sourceMesh.bounds;
        Vector3 center = transform.TransformPoint(bounds.center);
        Vector3 size = transform.TransformVector(bounds.size);

        return new Bounds(center, size);
    }

    void ClearExistingPoints()
    {
        foreach (var point in points)
        {
            if (point != null) Destroy(point.gameObject);
        }
        points.Clear();
    }

    void GenerateVolumePoints()
    {
        // Calculate how many points we need in each dimension
        int xCount = Mathf.CeilToInt(meshBounds.size.x / pointSpacing);
        int yCount = Mathf.CeilToInt(meshBounds.size.y / pointSpacing);
        int zCount = Mathf.CeilToInt(meshBounds.size.z / pointSpacing);

        // Adjust starting position to center the grid
        Vector3 startPos = meshBounds.min + new Vector3(
            (meshBounds.size.x - (xCount - 1) * pointSpacing) / 2,
            (meshBounds.size.y - (yCount - 1) * pointSpacing) / 2,
            (meshBounds.size.z - (zCount - 1) * pointSpacing) / 2
        );

        // Generate points in grid pattern
        for (int x = 0; x < xCount; x++)
        {
            for (int y = 0; y < yCount; y++)
            {
                for (int z = 0; z < zCount; z++)
                {
                    Vector3 pointPos = startPos + new Vector3(
                        x * pointSpacing,
                        y * pointSpacing,
                        z * pointSpacing
                    );

                    // For a cube, we can use simple bounds check
                    if (meshBounds.Contains(pointPos))
                    {
                        CreateSpringPoint(pointPos, false);
                    }
                }
            }
        }
    }

    void GenerateSurfacePoints()
    {
        if (sourceMesh == null) return;

        float surfaceSpacing = pointSpacing / surfacePointDensity;
        Vector3[] vertices = sourceMesh.vertices;
        int[] triangles = sourceMesh.triangles;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v1 = transform.TransformPoint(vertices[triangles[i]]);
            Vector3 v2 = transform.TransformPoint(vertices[triangles[i + 1]]);
            Vector3 v3 = transform.TransformPoint(vertices[triangles[i + 2]]);

            Vector3 edge1 = v2 - v1;
            Vector3 edge2 = v3 - v1;
            float area = Vector3.Cross(edge1, edge2).magnitude / 2f;
            int pointsToGenerate = Mathf.Max(1, Mathf.CeilToInt(area / (surfaceSpacing * surfaceSpacing)));

            for (int j = 0; j < pointsToGenerate; j++)
            {
                float u = Random.Range(0f, 1f);
                float v = Random.Range(0f, 1f);
                if (u + v > 1f)
                {
                    u = 1 - u;
                    v = 1 - v;
                }

                Vector3 pointPos = v1 + u * edge1 + v * edge2;

                // Ensure point is on the surface (not inside)
                if (IsPointOnSurface(pointPos))
                {
                    CreateSpringPoint(pointPos, fixSurfacePoints);
                }
            }
        }
    }

    bool IsPointOnSurface(Vector3 point)
    {
        // Simple check - point should be very close to mesh bounds
        Vector3 localPoint = transform.InverseTransformPoint(point);
        Bounds localBounds = sourceMesh.bounds;

        return Mathf.Approximately(localPoint.x, localBounds.min.x) ||
               Mathf.Approximately(localPoint.x, localBounds.max.x) ||
               Mathf.Approximately(localPoint.y, localBounds.min.y) ||
               Mathf.Approximately(localPoint.y, localBounds.max.y) ||
               Mathf.Approximately(localPoint.z, localBounds.min.z) ||
               Mathf.Approximately(localPoint.z, localBounds.max.z);
    }

    void CreateSpringPoint(Vector3 position, bool isFixed)
    {
        SpringPoint newPoint = Instantiate(springPointPrefab, position, Quaternion.identity);
        newPoint.transform.SetParent(transform);
        newPoint.isFixed = isFixed;
        points.Add(newPoint);
    }

    void CreateConnections()
    {
        // Simple O(n^2) connection for small point counts
        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                float distance = Vector3.Distance(
                    points[i].transform.position,
                    points[j].transform.position
                );

                if (distance <= connectionRadius && !IsConnected(points[i], points[j]))
                {
                    ConnectPoints(points[i], points[j], distance);
                }
            }
        }
    }

    bool IsConnected(SpringPoint a, SpringPoint b)
    {
        foreach (Connection conn in a.connections)
        {
            if (conn.point == b) return true;
        }
        return false;
    }

    void ConnectPoints(SpringPoint a, SpringPoint b, float distance)
    {
        Connection c1 = new Connection(b, distance, springConstant, damperConstant);
        a.connections.Add(c1);

        Connection c2 = new Connection(a, distance, springConstant, damperConstant);
        b.connections.Add(c2);
    }

    void OnDrawGizmosSelected()
    {
        if (sourceMesh != null)
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(sourceMesh.bounds.center, sourceMesh.bounds.size);
        }
    }
}