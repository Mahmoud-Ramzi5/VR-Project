using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static UnityEditor.Searcher.SearcherWindow.Alignment;


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
        targetMesh.RecalculateBounds();

        meshBounds = targetMesh.bounds;
        meshVertices = targetMesh.vertices;
        meshTriangles = targetMesh.triangles;

        FillObjectWithSpringPoints();
    }

    void FixedUpdate()
    {
        foreach (SpringPoint point in allSpringPoints)
        {
            if (!point.isFixed)
            {
                point.ConstrainToMesh(targetMesh, transform);
            }
        }
    }

    //public void FillObjectWithSpringPoints()
    //{
    //    ClearExistingPoints();

    //    // Create world-space bounds
    //    Bounds localBounds = meshBounds;
    //    Debug.Log($"localBounds {localBounds}");

    //    Vector3 worldCenter = transform.TransformPoint(localBounds.center);
    //    Vector3 worldSize = Vector3.Scale(localBounds.size, transform.lossyScale);
    //    Bounds worldBounds = new Bounds(worldCenter, worldSize);
    //    Debug.Log($"worldBounds {worldBounds}");

    //    rootNode = new OctreeNode(worldBounds, localBounds);
    //    int total_nodes = BuildOctree(rootNode);
    //    Debug.Log($"total_nodes {total_nodes}");

    //    CreateSpringPoints();
    //    CreateSpringConnections();

    //    Debug.Log($"Created {allSpringPoints.Count} spring points");
    //}
    public void FillObjectWithSpringPoints()
    {
        ClearExistingPoints();

        // Recalculate accurate world-space bounds
        if (meshVertices.Length <= 0) {
            Debug.Log("Vertices Error");
            return;
        }

        // TransformPoint converts the local mesh vertice dependent on the transform
        // position, scale and orientation into a global position
        Vector3 min = transform.TransformPoint(meshVertices[0]);
        Vector3 max = min;


        // Iterate through all vertices
        // except first one
        for (var i = 1; i < meshVertices.Length; i++)
        {
            var V = transform.TransformPoint(meshVertices[i]);

            // Go through X,Y and Z of the Vector3
            for (var n = 0; n < 3; n++)
            {
                max = Vector3.Max(V, max);
                min = Vector3.Min(V, min);
            }
        }

        Bounds worldBounds = new Bounds();
        worldBounds.SetMinMax(min, max);
        Debug.Log($"World Bounds: {worldBounds}");
        rootNode = new OctreeNode(worldBounds, meshBounds);

        // Build and fill the octree
        int total_nodes = BuildOctree(rootNode);
        Debug.Log($"Octree Nodes: {total_nodes}");

        CreateSpringPoints();
        CreateSpringConnections();

        Debug.Log($"Created {allSpringPoints.Count} spring points.");
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

        if (node.isDivided || node.Divide(minNodeSize))
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
                        if (IsPointInsideMesh(worldPos))
                        {
                            allPointPositions.Add(worldPos);
                            node.pointsPositions.Add(worldPos);
                        }
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
                    if (IsPointInsideMesh(worldPos))
                    {
                        allPointPositions.Add(worldPos);
                        node.pointsPositions.Add(worldPos);
                    }
                }
                result = true;
            }
        }

        return result;
    }


    void CreateSpringPoints()
    {
        foreach (var node in GetAllLeafNodes(rootNode))
        {
            foreach (var worldPos in node.pointsPositions)
            {
                SpringPoint point;
                if (springPointPrefab != null)
                {
                    //point = Instantiate(springPointPrefab, worldPos, Quaternion.identity);
                    //point.transform.SetParent(transform);

                    // Instantiate as child and use localPosition
                    point = Instantiate(springPointPrefab, transform);
                    point.transform.localPosition = transform.InverseTransformPoint(worldPos);
                }
                else
                {
                    // Create fallback GameObject with SpringPoint if prefab is missing
                    GameObject fallbackGO = new GameObject("SpringPoint_Fallback");
                    fallbackGO.transform.parent = transform;
                    fallbackGO.transform.localPosition = transform.InverseTransformPoint(worldPos);

                    point = fallbackGO.AddComponent<SpringPoint>();
                }

                point.radius = particleSpacing * 0.5f;
                point.connections = new List<Connection>();
                point.name = $"Point_{worldPos.x}_{worldPos.y}_{worldPos.z}";
                point.isFixed = IsCornerPoint(transform.InverseTransformPoint(worldPos));

                // for debuging
                point.isFixed = true;

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

        // 1. Fast bounding box check - if outside, definitely outside mesh
        if (!meshBounds.Contains(localPoint))
            return false;

        // 2. Use multiple ray directions to increase reliability
        Vector3[] baseDirections = {
        Vector3.left,
        Vector3.right,
        Vector3.forward,
        Vector3.back,
        Vector3.up,
        Vector3.down
        };

        float originOffset = 1e-6f; // Small offset
        float jitterAmount = 0.001f; // Small angle jitter
        Vector3[] testDirections = new Vector3[baseDirections.Length];
        for (int i = 0; i < baseDirections.Length; i++)
        {
            // Create small random jitter vector
            Vector3 jitter = new Vector3(
                Random.Range(-jitterAmount, jitterAmount),
                Random.Range(-jitterAmount, jitterAmount),
                Random.Range(-jitterAmount, jitterAmount)
            );

            // Add jitter and normalize to keep direction unit length
            // reduces the edges or vertices where the ray barely grazes the mesh
            testDirections[i] = (baseDirections[i] + jitter).normalized;
        }

        // If *any* direction ray test says point is inside (odd intersections), we say inside
        foreach (Vector3 direction in testDirections)
        {
            // Nudge the ray origin a bit forward, to avoid self-intersections
            Vector3 rayOrigin = localPoint + direction * originOffset;

            // Check intersections
            int intersections = CountRayIntersections(rayOrigin, direction);
            if (intersections % 2 == 1) // odd = inside
                return true; // point is inside mesh
        }

        return false; // All tests say outside
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

        float epsilon = 1e-6f;
        // If determinant is near zero, ray is parallel
        if (Mathf.Abs(det) < epsilon) // Ray parallel to triangle plane
            return false;

        float invDet = 1.0f / det;
        Vector3 t = origin - v1;
        float u = Vector3.Dot(t, p) * invDet;
        if (u < 0f || u > 1f)
            return false;

        Vector3 q = Vector3.Cross(t, e1);
        float v = Vector3.Dot(direction, q) * invDet;
        if (v < 0f || u + v > 1f)
            return false;

        float dist = Vector3.Dot(e2, q) * invDet;
        return dist > epsilon;
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
