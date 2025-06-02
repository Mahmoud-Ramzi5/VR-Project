using UnityEngine;
using System.Collections.Generic;

public class VoxelSpringFiller : MonoBehaviour
{
    [Header("Filling Settings")]
    public GameObject targetObject;
    public GameObject springPointPrefab;
    public float particleSpacing = 0.3f;
    public bool visualizeConnections = true;

    [Header("Spring Settings")]
    public float springConstant = 5f;
    public float damperConstant = 0.5f;
    public float connectionRangeMultiplier = 1.1f;

    private MeshFilter targetMeshFilter;
    private Mesh targetMesh;
    private Vector3[] meshVertices;
    private int[] meshTriangles;

    private List<SpringPoint> allSpringPoints = new List<SpringPoint>();
    private Dictionary<Vector3Int, SpringPoint> spatialGrid = new Dictionary<Vector3Int, SpringPoint>();

    void Start()
    {
        if (targetObject == null)
        {
            Debug.LogError("No target object assigned!");
            return;
        }

        targetMeshFilter = targetObject.GetComponent<MeshFilter>();
        targetMesh = targetMeshFilter.sharedMesh;
        meshVertices = targetMesh.vertices;
        meshTriangles = targetMesh.triangles;

        FillVolume();
        ConnectNeighbors();

        Debug.Log($"Created {allSpringPoints.Count} spring points.");
    }

    void FillVolume()
    {
        Bounds bounds = targetMesh.bounds;
        bounds.center = targetMeshFilter.transform.TransformPoint(bounds.center);
        bounds.extents = targetMeshFilter.transform.TransformVector(bounds.extents);

        Vector3 origin = bounds.min;
        Vector3Int gridSize = new Vector3Int(
            Mathf.CeilToInt(bounds.size.x / particleSpacing),
            Mathf.CeilToInt(bounds.size.y / particleSpacing),
            Mathf.CeilToInt(bounds.size.z / particleSpacing)
        );

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                for (int z = 0; z < gridSize.z; z++)
                {
                    Vector3 worldPos = origin + new Vector3(x, y, z) * particleSpacing;

                    if (IsPointInsideMesh(worldPos))
                    {
                        CreateSpringParticle(worldPos);
                    }
                }
            }
        }
    }

    void CreateSpringParticle(Vector3 position)
    {
        GameObject pointObj = springPointPrefab ? Instantiate(springPointPrefab, position, Quaternion.identity) : new GameObject("SpringPoint");
        pointObj.transform.position = position;
        pointObj.transform.parent = transform;

        SpringPoint sp = pointObj.GetComponent<SpringPoint>() ?? pointObj.AddComponent<SpringPoint>();
        sp.radius = particleSpacing * 0.5f;
        sp.connections = new List<Connection>();

        Vector3Int gridPos = new Vector3Int(
            Mathf.FloorToInt(position.x / particleSpacing),
            Mathf.FloorToInt(position.y / particleSpacing),
            Mathf.FloorToInt(position.z / particleSpacing));

        spatialGrid[gridPos] = sp;
        allSpringPoints.Add(sp);
    }

    void ConnectNeighbors()
    {
        Vector3Int[] offsets =
        {
            new Vector3Int(1,0,0), new Vector3Int(-1,0,0),
            new Vector3Int(0,1,0), new Vector3Int(0,-1,0),
            new Vector3Int(0,0,1), new Vector3Int(0,0,-1)
        };

        foreach (var kvp in spatialGrid)
        {
            Vector3Int pos = kvp.Key;
            SpringPoint sp = kvp.Value;

            foreach (var offset in offsets)
            {
                Vector3Int neighborPos = pos + offset;
                if (spatialGrid.TryGetValue(neighborPos, out var neighbor))
                {
                    float restLength = Vector3.Distance(sp.transform.position, neighbor.transform.position);
                    sp.connections.Add(new Connection(neighbor, restLength, springConstant, damperConstant));
                }
            }

            if (visualizeConnections)
                AddConnectionVisualization(sp);
        }
    }

    bool IsPointInsideMesh(Vector3 point)
    {
        Vector3 localPoint = targetMeshFilter.transform.InverseTransformPoint(point);
        if (!targetMesh.bounds.Contains(localPoint))
            return false;

        int intersections = 0;
        Vector3 dir = new Vector3(1, 0.3f, 0.2f).normalized;
        Vector3 rayOrigin = localPoint - dir * 100f;

        for (int i = 0; i < meshTriangles.Length; i += 3)
        {
            Vector3 v0 = meshVertices[meshTriangles[i]];
            Vector3 v1 = meshVertices[meshTriangles[i + 1]];
            Vector3 v2 = meshVertices[meshTriangles[i + 2]];

            if (RayIntersectsTriangle(rayOrigin, dir, v0, v1, v2))
                intersections++;
        }

        return (intersections % 2) == 1;
    }

    bool RayIntersectsTriangle(Vector3 rayOrigin, Vector3 rayDir, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        Vector3 h = Vector3.Cross(rayDir, edge2);
        float a = Vector3.Dot(edge1, h);

        if (a > -0.0001f && a < 0.0001f)
            return false;

        float f = 1.0f / a;
        Vector3 s = rayOrigin - v0;
        float u = f * Vector3.Dot(s, h);
        if (u < 0.0f || u > 1.0f) return false;

        Vector3 q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(rayDir, q);
        if (v < 0.0f || u + v > 1.0f) return false;

        float t = f * Vector3.Dot(edge2, q);
        return t > 0.0001f;
    }

    void AddConnectionVisualization(SpringPoint point)
    {
        var lr = point.gameObject.AddComponent<LineRenderer>();
        lr.positionCount = point.connections.Count * 2;
        lr.startWidth = 0.02f;
        lr.endWidth = 0.02f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.white;
        lr.endColor = Color.white;

        int idx = 0;
        foreach (var c in point.connections)
        {
            lr.SetPosition(idx++, point.transform.position);
            lr.SetPosition(idx++, c.point.transform.position);
        }
    }
}
