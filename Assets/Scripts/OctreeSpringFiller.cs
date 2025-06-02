using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class OctreeSpringFiller : MonoBehaviour
{
    [Header("Filling Settings")]
    public GameObject targetObject;
    public GameObject springPointPrefab;
    private MeshFilter targetMeshFilter;
    private MeshRenderer targetMeshRenderer;
    public float minNodeSize = 0.5f;
    public float particleSpacing = 0.3f;
    public bool visualizeConnections = true;

    [Header("Spring Settings")]
    public float springConstant = 10f;
    public float damperConstant = 0.5f;
    public float connectionRangeMultiplier = 1.1f;
    private Mesh targetMesh;
    private Vector3[] meshVertices;
    private int[] meshTriangles;

    private OctreeNode rootNode;
    private List<SpringPoint> allSpringPoints = new List<SpringPoint>();

    private class OctreeNode
    {
        public Bounds bounds;
        public OctreeNode[] children;
        public List<Vector3> particlePositions = new List<Vector3>();
        public bool isDivided;

        public OctreeNode(Bounds nodeBounds)
        {
            bounds = nodeBounds;
        }

        public bool Divide(float minSize)
        {
            if (bounds.size.x <= minSize) return false;

            float quarter = bounds.size.x / 4f;
            float childSize = bounds.size.x / 2f;
            Vector3 childExtents = new Vector3(childSize, childSize, childSize);

            children = new OctreeNode[8];
            for (int i = 0; i < 8; i++)
            {
                Vector3 center = bounds.center;
                center.x += (i & 1) == 0 ? -quarter : quarter;
                center.y += (i & 2) == 0 ? -quarter : quarter;
                center.z += (i & 4) == 0 ? -quarter : quarter;

                children[i] = new OctreeNode(new Bounds(center, childExtents));
            }

            isDivided = true;
            return true;
        }
    }

    void Start()
    {
        if (targetObject == null)
        {
            Debug.LogError("No target object assigned!");
            return;
        }
        targetMeshFilter = targetObject.GetComponent<MeshFilter>();
        targetMeshRenderer = targetObject.GetComponent<MeshRenderer>();

        targetMesh = targetMeshFilter.sharedMesh;
        meshVertices = targetMesh.vertices;
        meshTriangles = targetMesh.triangles;

        FillObjectWithSpringPoints();
    }

    public void FillObjectWithSpringPoints()
    {
        // Clear existing particles
        ClearExistingPoints();

        // Create root octree node based on mesh bounds
        //Bounds meshBounds = targetObject.GetComponent<Renderer>().bounds;
        Bounds meshBounds = targetMesh.bounds;
        // Transform bounds to world space
        Vector3 worldMin = targetMeshFilter.transform.TransformPoint(meshBounds.min);
        Vector3 worldMax = targetMeshFilter.transform.TransformPoint(meshBounds.max);
        meshBounds = new Bounds();
        meshBounds.SetMinMax(worldMin, worldMax);

        //meshBounds.center = targetMeshFilter.transform.TransformPoint(meshBounds.center);
        //meshBounds.extents = targetMeshFilter.transform.TransformVector(meshBounds.extents);
        
        rootNode = new OctreeNode(meshBounds);

        // Build octree and place particles
        BuildOctree(rootNode);

        // Create all spring point GameObjects
        CreateSpringPointObjects();

        // Establish spring connections
        CreateSpringConnections();

        Debug.Log($"Created {allSpringPoints.Count} spring points");
    }

    void ClearExistingPoints()
    {
        foreach (var point in allSpringPoints)
        {
            if (point != null && point.gameObject != null)
            {
                Destroy(point.gameObject);
            }
        }
        allSpringPoints.Clear();
    }

    void BuildOctree(OctreeNode node)
    {
        if (!NodeIntersectsMesh(node)) return;

        bool canDivide = node.Divide(minNodeSize);

        if (canDivide)
        {
            foreach (var child in node.children)
            {
                BuildOctree(child);
            }
        }
        else
        {
            PlaceParticlesInNode(node);
        }
    }

    bool NodeIntersectsMesh(OctreeNode node)
    {
        //// Fast bounds check first
        //if (!targetObject.GetComponent<Renderer>().bounds.Intersects(node.bounds))
        //    return false;

        //// More precise check using collider
        //return Physics.CheckBox(node.bounds.center, node.bounds.extents, Quaternion.identity);
        
        // Transform node bounds to mesh local space
        Bounds localBounds = new Bounds(
            targetMeshFilter.transform.InverseTransformPoint(node.bounds.center),
            targetMeshFilter.transform.InverseTransformVector(node.bounds.size));

        // Check if any mesh vertex is inside this node
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
                        z * particleSpacing);

                    if (IsPointInsideMesh(point))
                    {
                        node.particlePositions.Add(point);
                    }
                }
            }
        }
    }

    void CreateSpringPointObjects()
    {
        foreach (var node in GetAllLeafNodes(rootNode))
        {
            foreach (var pos in node.particlePositions)
            {
                GameObject pointObj;

                if (springPointPrefab != null)
                {
                    pointObj = Instantiate(springPointPrefab, pos, Quaternion.identity);
                }
                else
                {
                    pointObj = new GameObject("SpringPoint");
                    pointObj.transform.position = pos;
                }

                // Add and configure SpringPoint component
                SpringPoint springPoint = pointObj.GetComponent<SpringPoint>();
                if (springPoint == null)
                {
                    springPoint = pointObj.AddComponent<SpringPoint>();
                }

                springPoint.radius = particleSpacing * 0.5f;
                springPoint.connections = new List<Connection>();

                // Parent to this object for organization
                pointObj.transform.parent = transform;

                allSpringPoints.Add(springPoint);
            }
        }
    }

    void CreateSpringConnections()
    {
        float connectionRange = particleSpacing * connectionRangeMultiplier;
        float connectionRangeSqr = connectionRange * connectionRange;

        // Spatial partitioning for faster neighbor finding
        Dictionary<Vector3Int, List<SpringPoint>> spatialGrid = new Dictionary<Vector3Int, List<SpringPoint>>();

        // First pass: populate spatial grid
        foreach (var point in allSpringPoints)
        {
            Vector3Int gridPos = new Vector3Int(
                Mathf.FloorToInt(point.transform.position.x / particleSpacing),
                Mathf.FloorToInt(point.transform.position.y / particleSpacing),
                Mathf.FloorToInt(point.transform.position.z / particleSpacing));

            if (!spatialGrid.ContainsKey(gridPos))
            {
                spatialGrid[gridPos] = new List<SpringPoint>();
            }
            spatialGrid[gridPos].Add(point);
        }

        // Second pass: find neighbors
        foreach (var point in allSpringPoints)
        {
            Vector3Int gridPos = new Vector3Int(
                Mathf.FloorToInt(point.transform.position.x / particleSpacing),
                Mathf.FloorToInt(point.transform.position.y / particleSpacing),
                Mathf.FloorToInt(point.transform.position.z / particleSpacing));

            // Check adjacent grid cells (3x3x3 area)
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        Vector3Int neighborGridPos = gridPos + new Vector3Int(x, y, z);
                        if (spatialGrid.TryGetValue(neighborGridPos, out var potentialNeighbors))
                        {
                            foreach (var neighbor in potentialNeighbors)
                            {
                                if (point == neighbor) continue;

                                float sqrDist = (point.transform.position - neighbor.transform.position).sqrMagnitude;
                                if (sqrDist <= connectionRangeSqr)
                                {
                                    float distance = Mathf.Sqrt(sqrDist);
                                    if (!point.connections.Any(c => c.point == neighbor))
                                    {
                                        point.connections.Add(new Connection(
                                        neighbor,
                                        distance,
                                        springConstant,
                                        damperConstant
                                    ));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Add visualization if enabled
            if (visualizeConnections)
            {
                AddConnectionVisualization(point);
            }
        }
    }

    void AddConnectionVisualization(SpringPoint point)
    {
        LineRenderer lr = point.gameObject.AddComponent<LineRenderer>();
        lr.startWidth = 0.02f;
        lr.endWidth = 0.02f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.white;
        lr.endColor = Color.white;

        lr.positionCount = point.connections.Count * 2;
        int idx = 0;
        foreach (var conn in point.connections)
        {
            lr.SetPosition(idx++, point.transform.position);
            lr.SetPosition(idx++, conn.point.transform.position);
        }

    }

    List<OctreeNode> GetAllLeafNodes(OctreeNode node)
    {
        List<OctreeNode> leaves = new List<OctreeNode>();
        if (!node.isDivided)
        {
            if (node.particlePositions.Count > 0)
                leaves.Add(node);
        }
        else
        {
            foreach (var child in node.children)
            {
                leaves.AddRange(GetAllLeafNodes(child));
            }
        }
        return leaves;
    }

    bool IsPointInsideMesh(Vector3 point)
    {
        // Transform point to mesh local space
        Vector3 localPoint = targetMeshFilter.transform.InverseTransformPoint(point);

        // Fast bounds check
        if (!targetMesh.bounds.Contains(localPoint))
            return false;

        // Ray-casting method without physics
        int intersections = 0;
        Vector3 rayDirection = new Vector3(0.0001f, 1f, 0.0001f).normalized; // Slightly off-axis
        Vector3 rayOrigin = localPoint - rayDirection * 100f;

        // Check all triangles
        for (int i = 0; i < meshTriangles.Length; i += 3)
        {
            Vector3 v0 = meshVertices[meshTriangles[i]];
            Vector3 v1 = meshVertices[meshTriangles[i + 1]];
            Vector3 v2 = meshVertices[meshTriangles[i + 2]];

            if (RayIntersectsTriangle(rayOrigin, rayDirection, v0, v1, v2))
                intersections++;
        }

        return intersections % 2 == 1; // Odd number of intersections means inside
    }

    bool RayIntersectsTriangle(Vector3 rayOrigin, Vector3 rayDirection, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        Vector3 h = Vector3.Cross(rayDirection, edge2);
        float a = Vector3.Dot(edge1, h);

        if (a > -0.00001f && a < 0.00001f)
            return false;

        float f = 1.0f / a;
        Vector3 s = rayOrigin - v0;
        float u = f * Vector3.Dot(s, h);

        if (u < 0.0f || u > 1.0f)
            return false;

        Vector3 q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(rayDirection, q);

        if (v < 0.0f || u + v > 1.0f)
            return false;

        float t = f * Vector3.Dot(edge2, q);
        return t > 0.00001f;
    }




    void OnDrawGizmosSelected()
    {
        if (rootNode != null)
        {
            DrawNodeGizmos(rootNode);
        }
    }

    void DrawNodeGizmos(OctreeNode node)
    {
        Gizmos.color = node.isDivided ? Color.green : (node.particlePositions.Count > 0 ? Color.yellow : Color.red);
        Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);

        if (node.isDivided)
        {
            foreach (var child in node.children)
            {
                DrawNodeGizmos(child);
            }
        }
    }
}