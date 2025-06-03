using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static UnityEngine.ParticleSystem;

public class OctreeSpringFiller : MonoBehaviour
{
    [Header("Filling Settings")]
    public SpringPoint springPointPrefab;
    public float minNodeSize = 0.5f;
    public float particleSpacing = 0.5f;
    public bool visualizeConnections = true;

    [Header("Spring Settings")]
    public float springConstant = 10f;
    public float damperConstant = 0.5f;
    public float connectionRadius = 2f;

    private Mesh targetMesh;
    private Bounds meshBounds;
    private Vector3[] meshVertices;
    private int[] meshTriangles;

    private OctreeNode rootNode;
    private List<Vector3> allPointPositions = new List<Vector3>();
    private List<SpringPoint> allSpringPoints = new List<SpringPoint>();


    void Start()
    {
        targetMesh = GetComponent<MeshFilter>().mesh;

        meshBounds = targetMesh.bounds;
        meshVertices = targetMesh.vertices;
        meshTriangles = targetMesh.triangles;

        FillObjectWithSpringPoints();
    }

    void FixedUpdate()
    {
        foreach (SpringPoint point in allSpringPoints)
        {
            point.HandleBoundaryBox();
        }
    }

    public void FillObjectWithSpringPoints()
    {
        ClearExistingPoints();

        // Create world-space bounds
        Bounds localBounds = meshBounds;

        Vector3 worldCenter = transform.TransformPoint(localBounds.center);
        Vector3 worldSize = Vector3.Scale(localBounds.size, transform.lossyScale);
        Bounds worldBounds = new Bounds(worldCenter, worldSize);
        Debug.Log($"worldBounds {worldBounds}");

        rootNode = new OctreeNode(worldBounds, localBounds);
        int total_nodes = BuildOctree(rootNode);
        Debug.Log($"total_nodes {total_nodes}");

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

    int BuildOctree(OctreeNode node)
    {
        int total_nodes = 0;

        if (node.isDivided)
        {
            total_nodes += node.children.Length;
            foreach (var child in node.children)
            {
                int childCount = BuildOctree(child);
                if (childCount > 0)
                {
                    total_nodes += childCount;
                    total_nodes -= 1;
                }
            }
        }
        if (node.Divide(minNodeSize))
        {
            total_nodes += node.children.Length;
            foreach (var child in node.children)
            {
                int childCount = BuildOctree(child);
                if (childCount > 0)
                {
                    total_nodes += childCount;
                    total_nodes -= 1;
                }
            }
        }
        else
        {
            Debug.Log("PlaceParticlesInNode");
            PlaceParticlesInNode(node);
        }

        return total_nodes;
    }

    void PlaceParticlesInNode(OctreeNode node)
    {
        //Bounds worldBounds = node.worldBounds;
        //Vector3 center = worldBounds.center;

        //if (IsPointInsideMesh(center))
        //    node.pointsPositions.Add(center);

        if (NodeIntersectsMesh(node))
        {
            return;
        }

        Bounds localBounds = node.localBounds;
        int stepsX = Mathf.Max(2, Mathf.FloorToInt(localBounds.size.x / particleSpacing));
        int stepsY = Mathf.Max(2, Mathf.FloorToInt(localBounds.size.y / particleSpacing));
        int stepsZ = Mathf.Max(2, Mathf.FloorToInt(localBounds.size.z / particleSpacing));

        for (int x = 0; x < stepsX; x++)
        {
            for (int y = 0; y < stepsY; y++)
            {
                for (int z = 0; z < stepsZ; z++)
                {
                    // Calculate normalized position in grid (0-1 range)
                    Vector3 normalizedPos = new Vector3(
                        stepsX > 1 ? x / (float)(stepsX - 1) : 0.5f,
                        stepsY > 1 ? y / (float)(stepsY - 1) : 0.5f,
                        stepsZ > 1 ? z / (float)(stepsZ - 1) : 0.5f
                    );

                    // Calculate position in local space (within bounds)
                    Vector3 localPos = new Vector3(
                        Mathf.Lerp(localBounds.min.x, localBounds.max.x, normalizedPos.x),
                        Mathf.Lerp(localBounds.min.y, localBounds.max.y, normalizedPos.y),
                        Mathf.Lerp(localBounds.min.z, localBounds.max.z, normalizedPos.z)
                    );

                    // Convert to world space
                    Vector3 worldPos = transform.TransformPoint(localPos);

                    // Use approximate comparison instead of exact Contains
                    bool alreadyExists = allPointPositions.Any(p =>
                        Vector3.Distance(p, worldPos) < particleSpacing * 0.5f);

                    if (!alreadyExists)
                    {
                        //if (IsPointInsideMesh(worldPos))
                        //{
                        allPointPositions.Add(worldPos);
                        node.pointsPositions.Add(worldPos);
                        //}
                    }
                }
            }
        }
    }

    bool NodeIntersectsMesh(OctreeNode node)
    {
        Bounds localBounds = node.localBounds;

        bool result = false;
        foreach (var vertex in meshVertices)
        {
            if (localBounds.Contains(vertex))
            {
                Vector3 worldPos = transform.TransformPoint(vertex);

                // Use approximate comparison instead of exact Contains
                bool alreadyExists = allPointPositions.Any(p =>
                    Vector3.Distance(p, worldPos) < particleSpacing * 0.5f);

                if (!alreadyExists)
                {
                    //if (IsPointInsideMesh(worldPos))
                    //{
                    allPointPositions.Add(worldPos);
                    node.pointsPositions.Add(worldPos);
                    //}
                }
                result = true;
            }
        }

        return result;
    }


    void CreateSpringPointObjects()
    {
        foreach (var node in GetAllLeafNodes(rootNode))
        {
            foreach (var worldPos in node.pointsPositions)
            {
                SpringPoint point;
                if (springPointPrefab != null)
                {
                    point = Instantiate(springPointPrefab, worldPos, Quaternion.identity);
                    point.name = $"Point_{worldPos.x}_{worldPos.y}_{worldPos.z}";
                }
                else
                {
                    // TODO: handle no prefab
                    point = Instantiate(new SpringPoint(), worldPos, Quaternion.identity);
                }

                point.transform.SetParent(transform);
                point.radius = particleSpacing * 0.5f;

                // Set bounds to match mesh bounds
                //point.boundsMin = transform.TransformPoint(meshBounds.min);
                //point.boundsMax = transform.TransformPoint(meshBounds.max);

                // for debuging
                point.isFixed = true;
                point.connections = new List<Connection>();

                if (true && IsCornerPoint(transform.InverseTransformPoint(worldPos)))
                {
                    point.isFixed = true;
                }

                allSpringPoints.Add(point);
            }
        }
    }

    bool IsCornerPoint(Vector3 localPos)
    {
        Vector3 min = meshBounds.min;
        Vector3 max = meshBounds.max;
        float threshold = particleSpacing * 0.5f;

        return (Vector3.Distance(localPos, min) < threshold ||
               Vector3.Distance(localPos, new Vector3(min.x, min.y, max.z)) < threshold ||
               Vector3.Distance(localPos, new Vector3(min.x, max.y, min.z)) < threshold ||
               Vector3.Distance(localPos, new Vector3(min.x, max.y, max.z)) < threshold ||
               Vector3.Distance(localPos, new Vector3(max.x, min.y, min.z)) < threshold ||
               Vector3.Distance(localPos, new Vector3(max.x, min.y, max.z)) < threshold ||
               Vector3.Distance(localPos, new Vector3(max.x, max.y, min.z)) < threshold ||
               Vector3.Distance(localPos, max) < threshold);
    }

    void CreateSpringConnections()
    {
        // Clear existing connections
        foreach (var point in allSpringPoints)
        {
            point.connections.Clear();
        }

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

    // Check if SpringPoint is in Mesh
    bool IsPointInsideMesh(Vector3 point)
    {
        // Transform point to mesh's local space
        Vector3 localPoint = transform.InverseTransformPoint(point);

        // 1. Fast bounding box check
        if (!meshBounds.Contains(localPoint))
            return true;

        // 2. Use multiple ray directions to increase reliability
        Vector3[] testDirections = {
        Vector3.forward,
        Vector3.left,
        Vector3.right,
        Vector3.up,
        Vector3.down,
        Vector3.back
    };

        int insideCount = 0;

        foreach (Vector3 dir in testDirections)
        {
            int intersections = CountRayIntersections(localPoint, dir);
            if (intersections % 2 == 1) // if odd number of intersections
                insideCount++;
        }

        // Consider inside if majority of tests agree
        return insideCount > testDirections.Length / 2;
    }

    int CountRayIntersections(Vector3 origin, Vector3 direction)
    {
        int count = 0;

        for (int i = 0; i < meshTriangles.Length; i += 3)
        {
            Vector3 v1 = meshVertices[meshTriangles[i]];
            Vector3 v2 = meshVertices[meshTriangles[i + 1]];
            Vector3 v3 = meshVertices[meshTriangles[i + 2]];

            if (RayTriangleIntersection(origin, direction, v1, v2, v3))
                count++;
        }

        return count;
    }

    bool RayTriangleIntersection(Vector3 origin, Vector3 direction,
                                Vector3 v1, Vector3 v2, Vector3 v3)
    {
        Vector3 e1 = v2 - v1;
        Vector3 e2 = v3 - v1;
        Vector3 p = Vector3.Cross(direction, e2);
        float det = Vector3.Dot(e1, p);

        // If determinant is near zero, ray is parallel
        if (det < Mathf.Epsilon && det > -Mathf.Epsilon)
            return false;

        float invDet = 1.0f / det;
        Vector3 t = origin - v1;
        float u = Vector3.Dot(t, p) * invDet;

        if (u < 0 || u > 1)
            return false;

        Vector3 q = Vector3.Cross(t, e1);
        float v = Vector3.Dot(direction, q) * invDet;

        if (v < 0 || u + v > 1)
            return false;

        float dist = Vector3.Dot(e2, q) * invDet;
        return dist > Mathf.Epsilon;
    }
    //

    void OnDrawGizmos()
    {
        if (rootNode != null)
        {
            rootNode.DrawGizmos(Color.yellow, Color.green);
        }
    }
}
