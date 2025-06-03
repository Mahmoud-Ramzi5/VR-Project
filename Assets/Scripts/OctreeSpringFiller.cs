using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class OctreeSpringFiller : MonoBehaviour
{
    [Header("Filling Settings")]
    public GameObject targetObject;
    public GameObject springPointPrefab;
    public float minNodeSize = 0.5f;
    public float particleSpacing = 2f;
    public bool visualizeConnections = true;

    [Header("Spring Settings")]
    public float springConstant = 10f;
    public float damperConstant = 0.5f;
    public float connectionRadius = 2f;

    private MeshFilter targetMeshFilter;
    private Mesh targetMesh;
    private Vector3[] meshVertices;
    private int[] meshTriangles;

    private OctreeNode rootNode;
    private List<SpringPoint> allSpringPoints = new List<SpringPoint>();

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

        FillObjectWithSpringPoints();
    }

    public void FillObjectWithSpringPoints()
    {
        ClearExistingPoints();

        // Create world-space bounds
        Bounds localBounds = targetMesh.bounds;
        Vector3 worldCenter = targetMeshFilter.transform.TransformPoint(localBounds.center);
        Vector3 worldSize = Vector3.Scale(localBounds.size, targetMeshFilter.transform.lossyScale);
        Bounds worldBounds = new Bounds(worldCenter, worldSize);

        rootNode = new OctreeNode(worldBounds);
        BuildOctree(rootNode);
        CreateSpringPointObjects();
        CreateSpringConnections();

        Debug.Log($"Created {allSpringPoints.Count} spring points");
    }

    void ClearExistingPoints()
    {
        foreach (var point in allSpringPoints)
        {
            if (point != null && point.gameObject != null)
                Destroy(point.gameObject);
        }
        allSpringPoints.Clear();
    }

    void BuildOctree(OctreeNode node)
    {
        if (!NodeIntersectsMesh(node)) return;

        if (node.Divide(minNodeSize))
        {
            foreach (var child in node.children)
                BuildOctree(child);
        }
        else
        {
            PlaceParticlesInNode(node);
        }
    }

    bool NodeIntersectsMesh(OctreeNode node)
    {
        Bounds localBounds = new Bounds(
            targetMeshFilter.transform.InverseTransformPoint(node.bounds.center),
            targetMeshFilter.transform.InverseTransformVector(node.bounds.size)
            );

        foreach (var vertex in meshVertices)
        {
            if (localBounds.Contains(vertex))
                return true;
        }

        return false;
    }
    void PlaceParticlesInNode(OctreeNode node)
    {
        Vector3 start = node.bounds.min;
        Vector3 end = node.bounds.max;

        int stepsX = Mathf.CeilToInt(node.bounds.size.x / particleSpacing);
        int stepsY = Mathf.CeilToInt(node.bounds.size.y / particleSpacing);
        int stepsZ = Mathf.CeilToInt(node.bounds.size.z / particleSpacing);

        for (int x = 0; x <= stepsX; x++)
        {
            for (int y = 0; y <= stepsY; y++)
            {
                for (int z = 0; z <= stepsZ; z++)
                {
                    Vector3 point = start + new Vector3(
                        x * particleSpacing,
                        y * particleSpacing,
                        z * particleSpacing
                        );

                    //if (point.x <= end.x && point.y <= end.y && point.z <= end.z)
                    //{
                    if (IsPointInsideMesh(point))
                        node.pointsPositions.Add(point);
                    //}
                }
            }
        }
    }

    void CreateSpringPointObjects()
    {
        foreach (var node in GetAllLeafNodes(rootNode))
        {
            foreach (var pos in node.pointsPositions)
            {
                GameObject pointObj;
                if (springPointPrefab != null)
                {
                    pointObj = Instantiate(springPointPrefab, pos, Quaternion.identity);
                    pointObj.name = $"Point_{pos.x}_{pos.y}_{pos.z}";
                }
                else
                {
                    pointObj = new GameObject("SpringPoint");
                }

                pointObj.transform.position = pos;
                pointObj.transform.parent = transform;

                SpringPoint springPoint = pointObj.GetComponent<SpringPoint>() ?? pointObj.AddComponent<SpringPoint>();
                springPoint.radius = particleSpacing * 0.5f;
                springPoint.connections = new List<Connection>();

                allSpringPoints.Add(springPoint);
            }
        }
    }

    void CreateSpringConnections()
    {        
        // For each point, find nearby points and create connections
        for (int i = 0; i < allSpringPoints.Count; i++)
        {
            SpringPoint currentPoint = allSpringPoints[i];

            for (int j = i + 1; j < allSpringPoints.Count; j++)
            {
                SpringPoint otherPoint = allSpringPoints[j];
                float distance = Vector3.Distance(currentPoint.transform.position, otherPoint.transform.position);

                // Connect if within radius and not already connected
                if (distance <= connectionRadius * particleSpacing && !IsConnected(currentPoint, otherPoint))
                {
                    ConnectPoints(currentPoint, otherPoint, distance);
                }
            }
        }
    }
    bool IsConnected(SpringPoint a, SpringPoint b)
    {
        // Check if a is already connected to b
        foreach (var conn in a.connections)
        {
            if (conn.point == b) return true;
        }
        return false;
    }
    void ConnectPoints(SpringPoint a, SpringPoint b, float distance)
    {
        // Clamp rest length to reasonable values
        float restLength = Mathf.Clamp(distance, 0.5f, 3f);

        Connection c1 = new Connection(b, restLength, springConstant, damperConstant);
        a.connections.Add(c1);

        Connection c2 = new Connection(a, restLength, springConstant, damperConstant);
        b.connections.Add(c2);
    }

    List<OctreeNode> GetAllLeafNodes(OctreeNode node)
    {
        List<OctreeNode> leaves = new();
        if (!node.isDivided)
        {
            if (node.pointsPositions.Count > 0)
                leaves.Add(node);
        }
        else
        {
            foreach (var child in node.children)
                leaves.AddRange(GetAllLeafNodes(child));
        }
        return leaves;
    }

    bool IsPointInsideMesh(Vector3 worldPoint)
    {
        Vector3 localPoint = targetMeshFilter.transform.InverseTransformPoint(worldPoint);

        if (!targetMesh.bounds.Contains(localPoint))
            return false;

        int intersections = 0;
        Vector3 rayDir = new Vector3(0.0001f, 1f, 0.0001f).normalized;
        Vector3 rayOrigin = localPoint - rayDir * 100f;

        for (int i = 0; i < meshTriangles.Length; i += 3)
        {
            Vector3 v0 = meshVertices[meshTriangles[i]];
            Vector3 v1 = meshVertices[meshTriangles[i + 1]];
            Vector3 v2 = meshVertices[meshTriangles[i + 2]];

            if (RayIntersectsTriangle(rayOrigin, rayDir, v0, v1, v2))
                intersections++;
        }

        return intersections % 2 == 1;
    }

    bool RayIntersectsTriangle(Vector3 rayOrigin, Vector3 rayDir, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        Vector3 h = Vector3.Cross(rayDir, edge2);
        float a = Vector3.Dot(edge1, h);

        if (Mathf.Abs(a) < 0.000001f)
            return false;

        float f = 1.0f / a;
        Vector3 s = rayOrigin - v0;
        float u = f * Vector3.Dot(s, h);
        if (u < 0.0f || u > 1.0f)
            return false;

        Vector3 q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(rayDir, q);
        if (v < 0.0f || u + v > 1.0f)
            return false;

        float t = f * Vector3.Dot(edge2, q);
        return t > 0.00001f;
    }

    void OnDrawGizmosSelected()
    {
        if (rootNode != null)
            DrawNodeGizmos(rootNode);
    }

    void DrawNodeGizmos(OctreeNode node)
    {
        Gizmos.color = node.isDivided ? Color.green : (node.pointsPositions.Count > 0 ? Color.yellow : Color.red);
        Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);

        if (node.isDivided)
        {
            foreach (var child in node.children)
                DrawNodeGizmos(child);
        }
    }
}
