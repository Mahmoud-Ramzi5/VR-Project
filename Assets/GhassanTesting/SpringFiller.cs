using UnityEngine;
using System.Collections.Generic;

public class SpringFiller : MonoBehaviour
{
    //[Header("Filling Settings")]
    //public GameObject targetObject;
    //public GameObject springPointPrefab;
    //public float spacing = 0.3f;
    //public float margin = 0.01f;

    //[Header("Spring Settings")]
    //public float springConstant = 20f;
    //public float damperConstant = 1f;
    //public float connectionRangeMultiplier = 1.1f;
    //public bool visualizeConnections = false;

    //private Mesh targetMesh;
    //private MeshFilter meshFilter;
    //private Vector3[] vertices;
    //private int[] triangles;
    //private List<SpringPoint> allPoints = new List<SpringPoint>();

    //void Start()
    //{
    //    if (!targetObject)
    //    {
    //        Debug.LogError("Assign a target object.");
    //        return;
    //    }

    //    meshFilter = targetObject.GetComponent<MeshFilter>();
    //    if (!meshFilter)
    //    {
    //        Debug.LogError("Missing MeshFilter.");
    //        return;
    //    }

    //    targetMesh = meshFilter.sharedMesh;
    //    vertices = targetMesh.vertices;
    //    triangles = targetMesh.triangles;

    //    FillMesh();
    //}

    //void FillMesh()
    //{
    //    Bounds bounds = targetMesh.bounds;
    //    Vector3 worldMin = meshFilter.transform.TransformPoint(bounds.min);
    //    Vector3 worldMax = meshFilter.transform.TransformPoint(bounds.max);

    //    Vector3 size = worldMax - worldMin;
    //    int countX = Mathf.CeilToInt(size.x / spacing);
    //    int countY = Mathf.CeilToInt(size.y / spacing);
    //    int countZ = Mathf.CeilToInt(size.z / spacing);

    //    Dictionary<Vector3Int, SpringPoint> grid = new Dictionary<Vector3Int, SpringPoint>();

    //    // Create points inside the mesh
    //    for (int x = 0; x <= countX; x++)
    //        for (int y = 0; y <= countY; y++)
    //            for (int z = 0; z <= countZ; z++)
    //            {
    //                Vector3 worldPos = worldMin + new Vector3(x * spacing, y * spacing, z * spacing);
    //                if (IsInsideMesh(worldPos))
    //                {
    //                    GameObject go = springPointPrefab ? Instantiate(springPointPrefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
    //                    go.transform.position = worldPos;
    //                    go.transform.localScale = Vector3.one * spacing * 0.5f;
    //                    go.transform.parent = this.transform;

    //                    SpringPoint sp = go.GetComponent<SpringPoint>() ?? go.AddComponent<SpringPoint>();
    //                    sp.radius = spacing * 0.5f;
    //                    sp.mass = 1f;
    //                    sp.connections = new List<Connection>();

    //                    Vector3Int key = new Vector3Int(x, y, z);
    //                    grid[key] = sp;
    //                    allPoints.Add(sp);
    //                }
    //            }

    //    // Connect neighbors
    //    Vector3Int[] directions = new Vector3Int[]
    //    {
    //        new Vector3Int(1, 0, 0), new Vector3Int(0, 1, 0),
    //        new Vector3Int(0, 0, 1), new Vector3Int(1, 1, 0),
    //        new Vector3Int(1, 0, 1), new Vector3Int(0, 1, 1),
    //        new Vector3Int(1, 1, 1), new Vector3Int(-1, 0, 0),
    //        new Vector3Int(0, -1, 0), new Vector3Int(0, 0, -1),
    //        new Vector3Int(-1, -1, 0), new Vector3Int(-1, 0, -1),
    //        new Vector3Int(0, -1, -1), new Vector3Int(-1, -1, -1)
    //    };

    //    foreach (var kvp in grid)
    //    {
    //        var originKey = kvp.Key;
    //        var originPoint = kvp.Value;

    //        foreach (var dir in directions)
    //        {
    //            Vector3Int neighborKey = originKey + dir;
    //            if (grid.TryGetValue(neighborKey, out SpringPoint neighbor))
    //            {
    //                float restLength = Vector3.Distance(originPoint.transform.position, neighbor.transform.position);
    //                originPoint.connections.Add(new Connection(neighbor, restLength, springConstant, damperConstant));
    //            }
    //        }

    //        if (visualizeConnections)
    //        {
    //            AddLineRenderer(originPoint);
    //        }
    //    }

    //    Debug.Log($"Created {allPoints.Count} spring points.");
    //}

    //bool IsInsideMesh(Vector3 worldPoint)
    //{
    //    Vector3 localPoint = meshFilter.transform.InverseTransformPoint(worldPoint);
    //    if (!targetMesh.bounds.Contains(localPoint)) return false;

    //    int hits = 0;
    //    Vector3 dir = new Vector3(0.577f, 0.577f, 0.577f); // diagonal ray
    //    Vector3 origin = localPoint - dir * 100f;

    //    for (int i = 0; i < triangles.Length; i += 3)
    //    {
    //        Vector3 v0 = vertices[triangles[i]];
    //        Vector3 v1 = vertices[triangles[i + 1]];
    //        Vector3 v2 = vertices[triangles[i + 2]];

    //        if (RayTriangleIntersect(origin, dir, v0, v1, v2))
    //            hits++;
    //    }

    //    return hits % 2 == 1;
    //}

    //bool RayTriangleIntersect(Vector3 rayOrigin, Vector3 rayDir, Vector3 v0, Vector3 v1, Vector3 v2)
    //{
    //    Vector3 edge1 = v1 - v0;
    //    Vector3 edge2 = v2 - v0;
    //    Vector3 h = Vector3.Cross(rayDir, edge2);
    //    float a = Vector3.Dot(edge1, h);
    //    if (a > -0.0001f && a < 0.0001f) return false;

    //    float f = 1.0f / a;
    //    Vector3 s = rayOrigin - v0;
    //    float u = f * Vector3.Dot(s, h);
    //    if (u < 0.0f || u > 1.0f) return false;

    //    Vector3 q = Vector3.Cross(s, edge1);
    //    float v = f * Vector3.Dot(rayDir, q);
    //    if (v < 0.0f || u + v > 1.0f) return false;

    //    float t = f * Vector3.Dot(edge2, q);
    //    return t > 0.0001f;
    //}

    //void AddLineRenderer(SpringPoint p)
    //{
    //    LineRenderer lr = p.gameObject.AddComponent<LineRenderer>();
    //    lr.positionCount = 2;
    //    lr.startWidth = 0.01f;
    //    lr.endWidth = 0.01f;
    //    lr.material = new Material(Shader.Find("Sprites/Default"));
    //    lr.startColor = Color.white;
    //    lr.endColor = Color.white;

    //    if (p.connections.Count > 0)
    //    {
    //        lr.SetPosition(0, p.transform.position);
    //        lr.SetPosition(1, p.connections[0].point.transform.position);
    //    }
    //}
}
