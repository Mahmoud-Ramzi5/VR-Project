using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Linq;


public class OctreeSpringFillerTest : MonoBehaviour
{
    [Header("Filling Settings")]
    public SpringPointTest springPointPrefab;
    public bool visualizeConnections = true;
    public float minNodeSize = 0.5f;
    public float PointSpacing = 0.5f;
    public bool isSolid = true;

    [Header("Spring Settings")]
    public float springConstant = 10f;
    public float damperConstant = 0.5f;
    public float connectionRadius = 2f;
    public float maxRestLength = 2f;

    [Header("Mesh Settings")]
    public float totalMass = 100f;
    public bool applyGravity = true;
    public Vector3 gravity => new Vector3(0, -9.81f, 0);

    private Mesh targetMesh;
    private Bounds meshBounds;
    private Vector3[] meshVertices;
    private int[] meshTriangles;
    private Vector3 lastPos;

    [Header("Ground Collision")]
    public float groundLevel = 0f;       // Y-position of the ground plane
    public float groundBounce = 0.5f;   // Bounce coefficient (0 = no bounce, 1 = full bounce)
    public float groundFriction = 0.8f; // Friction (0 = full stop, 1 = no friction)


    // Lists
    private List<Vector3> allPointPositions = new List<Vector3>();
    public List<ConnectionTest> allConnectionsTest = new List<ConnectionTest>();
    public List<SpringPointTest> allSpringPointsTest = new List<SpringPointTest>();
    
    // Jobs
    private SpringJobManager jobManager;


    void Start()
    {
        // save transform
        lastPos = transform.position;

        // Get mesh
        targetMesh = GetComponent<MeshFilter>().mesh;
        targetMesh.RecalculateBounds();

        meshBounds = targetMesh.bounds;
        meshVertices = targetMesh.vertices;
        meshTriangles = targetMesh.triangles;

        FillObjectWithSpringPoints();

        // Update positions and bounds on start
        foreach (SpringPointTest point in allSpringPointsTest)
        {
            point.mass = totalMass / allSpringPointsTest.Count;
            Vector3 moveStep = transform.position - lastPos;
            point.UpdateBounds(moveStep);
        }


        // Use Jobs to calculate physics on GPU threads
        // Parallelizing calculations improves performance
        jobManager = gameObject.AddComponent<SpringJobManager>();
        jobManager.InitializeArrays(this, allSpringPointsTest.Count, allConnectionsTest.Count);

        // Initial connection data setup
        jobManager.UpdateConnectionData(allConnectionsTest);

    }

    private void Update()
    {
        if (Time.frameCount % 5 == 0)
        { // Run every 5th frame
          // Spread out expensive operations
        
            // Update spring connections visuals
            foreach (ConnectionTest connection in allConnectionsTest) {
                connection.UpdateLinePos();
            }

            foreach (SpringPointTest point in allSpringPointsTest)
            {
                if (transform.position != lastPos)
                {
                    Vector3 moveStep = transform.position - lastPos;
                    if(moveStep.magnitude > 0.001f)
                    {
                        point.UpdateBounds(moveStep);
                    }
                }
            }
            if (transform.position != lastPos)
            {
                lastPos = transform.position;
            }
        }
    }

    void FixedUpdate()
    {
        // 1. Schedule gravity job
        jobManager.ScheduleGravityJobs(gravity, applyGravity);

        // 2. Schedule spring jobs
        jobManager.ScheduleSpringJobs(springConstant, damperConstant);

        // 3. Schedule collision jobs
        //jobManager.ScheduleCollisionJobs(groundLevel, groundBounce, groundFriction);

        // 4. Complete all jobs and apply results
        jobManager.CompleteAllJobsAndApply();

        //// Update springs
        //// This was moved to Jobs
        //foreach (var connection in allConnectionsTest)
        //{
        //    connection.CalculateAndApplyForces();
        //}

        // 5. Update mesh (consider throttling this)
        if (Time.frameCount % 3 == 0) // Update mesh every 3 physics frames
        {
            // Update mesh to follow points
            UpdateMeshFromPoints();
        }

        // Handle collisions
        if (true)
        {
            foreach (var point in allSpringPointsTest)
            {
                HandleGroundCollisionTest(point);
            }
        }

        // Update points (if needed)
        foreach (var point in allSpringPointsTest)
        {
            point.UpdatePoint(Time.fixedDeltaTime);
        }

    }

    // Call this when connections change
    public void UpdateConnections()
    {
        if (jobManager != null)
        {
            jobManager.UpdateConnectionData(allConnectionsTest);
        }
    }


    public void HandleGroundCollisionTest(SpringPointTest point)
    {
        if (point.transform.position.y < groundLevel)
        {
            point.transform.position = new Vector3(point.transform.position.x, groundLevel, point.transform.position.z);

            if (point.velocity.y < 0)
            {
                point.velocity = new Vector3(
                    point.velocity.x * groundFriction,
                    -point.velocity.y * groundBounce,
                    point.velocity.z * groundFriction
                );
            }
        }
    }

    void UpdateMeshFromPoints()
    {
        // Get the mesh we want to modify
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;

        // Find average position of all points
        Vector3 averagePos = Vector3.zero;
        foreach (var point in allSpringPointsTest)
        {
            averagePos += point.transform.position;
        }
        averagePos /= allSpringPointsTest.Count;

        // Update mesh position to follow points
        transform.position = averagePos;

        // Update each vertex based on its corresponding point
        for (int i = 0; i < vertices.Length; i++)
        {
            // Find closest spring point to this vertex
            Vector3 worldVertex = transform.TransformPoint(vertices[i]);
            SpringPointTest closestPoint = FindClosestPoint(worldVertex);

            if (closestPoint != null)
            {
                // Update vertex position relative to new mesh position
                vertices[i] = transform.InverseTransformPoint(closestPoint.transform.position);
            }
        }

        // Apply changes to mesh
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    SpringPointTest FindClosestPoint(Vector3 worldPos)
    {
        SpringPointTest closest = null;
        float minDist = float.MaxValue;

        foreach (var point in allSpringPointsTest)
        {
            float dist = Vector3.Distance(worldPos, point.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = point;
            }
        }

        return closest;
    }

    public void FillObjectWithSpringPoints()
    {
        ClearExistingPoints();

        // Recalculate accurate world-space bounds
        if (meshVertices.Length <= 0)
        {
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

        // Fill Object using Octree Algorithms
        OctreeNode rootNode = new OctreeNode(worldBounds, meshBounds);
        int total_nodes = BuildOctree(rootNode);
        //CreateSpringConnections();
        CreateSpringConnectionsTest();

        // Some logs
        Debug.Log($"Octree Nodes: {total_nodes}");
        Debug.Log($"Created {allSpringPointsTest.Count} spring points test.");
    }

    void ClearExistingPoints()
    {
        foreach (var point in allSpringPointsTest)
        {
            if (point != null && point.gameObject != null)
                Destroy(point.gameObject);
        }
        allSpringPointsTest.Clear();
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
            if (NodeIntersectsMesh(node))
            {
                FillNodeVertices(node);
            }
            else
            {
                FillNodeWithSpringPoints(node);
            }
        }

        return total_nodes;
    }

    bool NodeIntersectsMesh(OctreeNode node)
    {
        Bounds localBounds = node.localBounds;

        foreach (var vertex in meshVertices)
        {
            if (localBounds.Contains(vertex))
            {
                return true;
            }
        }

        return false;
    }

    void FillNodeVertices(OctreeNode node)
    {
        Bounds localBounds = node.localBounds;

        foreach (var vertex in meshVertices)
        {
            if (localBounds.Contains(vertex))
            {
                Vector3 worldPos = transform.TransformPoint(vertex);

                // Use approximate comparison instead of exact Contains
                bool alreadyExists = allPointPositions.Any(p =>
                    Vector3.Distance(p, worldPos) < PointSpacing * 0.5f);

                if (!alreadyExists)
                {
                    if (IsPointInsideMesh(worldPos))
                    {
                        allPointPositions.Add(worldPos);
                        CreateSpringPoint(worldPos, node.worldBounds, true);
                    }
                }
            }
        }
    }

    void FillNodeWithSpringPoints(OctreeNode node)
    {
        Bounds localBounds = node.localBounds;
        int stepsX = Mathf.Max(2, Mathf.FloorToInt(localBounds.size.x / PointSpacing));
        int stepsY = Mathf.Max(2, Mathf.FloorToInt(localBounds.size.y / PointSpacing));
        int stepsZ = Mathf.Max(2, Mathf.FloorToInt(localBounds.size.z / PointSpacing));

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
                        Vector3.Distance(p, worldPos) < PointSpacing * 0.5f);

                    if (!alreadyExists)
                    {
                        if (IsPointInsideMesh(worldPos))
                        {
                            allPointPositions.Add(worldPos);
                            CreateSpringPoint(worldPos, node.worldBounds, false);
                        }
                    }
                }
            }
        }
    }

    void CreateSpringPoint(Vector3 worldPos, Bounds bounds, bool isMeshVertex)
    {
        //SpringPoint point;
        SpringPointTest point;
        if (springPointPrefab != null)
        {
            point = Instantiate(springPointPrefab, worldPos, Quaternion.identity);
            // Instantiate as child and use localPosition
            //point = Instantiate(springPointPrefab, transform);
            //point.transform.localPosition = transform.InverseTransformPoint(worldPos);
        }
        else
        {
            // Create fallback GameObject with SpringPoint if prefab is missing
            GameObject fallback = new GameObject("SpringPoint_Fallback");

            //fallback.transform.parent = transform;
            //fallback.transform.localPosition = transform.InverseTransformPoint(worldPos);

            point = fallback.AddComponent<SpringPointTest>();
        }

        point.mass = 1.0f;
        point.radius = 0.1f;
        point.nodeBounds = bounds;
        point.name = $"Point_{worldPos.x}_{worldPos.y}_{worldPos.z}";

        //if (isMeshVertex)
        //{
        //    point.isMeshVertex = true;
        //}
        //else
        //{
        //    Vector3 meshPoint = point.FindClosestMeshPoint(targetMesh, transform);

        //    // Find the index of the triangle that contains the meshPoint
        //    int triangleIndex = FindTriangleIndex(targetMesh, meshPoint);

        //    if (triangleIndex != -1)
        //    {
        //        point.triangleIndex = triangleIndex;
        //    }
        //    else
        //    {
        //        point.triangleIndex = -1;
        //    }
        //}

        allSpringPointsTest.Add(point);

        // Function to find the index of the triangle that contains a point
        int FindTriangleIndex(Mesh mesh, Vector3 point)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v1 = vertices[triangles[i]];
                Vector3 v2 = vertices[triangles[i + 1]];
                Vector3 v3 = vertices[triangles[i + 2]];

                // Check if the point is inside the triangle
                if (IsPointInTriangle(point, v1, v2, v3))
                {
                    return i / 3; // Return the triangle index
                }
            }

            return -1; // Return -1 if the point is not inside any triangle
        }

        // Function to check if a point is inside a triangle using Barycentric coordinates
        bool IsPointInTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
        {
            // Compute the vectors for the triangle edges
            Vector3 v0 = b - a;
            Vector3 v1 = c - a;
            Vector3 v2 = point - a;

            // Compute dot products
            float dot00 = Vector3.Dot(v0, v0);
            float dot01 = Vector3.Dot(v0, v1);
            float dot02 = Vector3.Dot(v0, v2);
            float dot11 = Vector3.Dot(v1, v1);
            float dot12 = Vector3.Dot(v1, v2);

            // Compute Barycentric coordinates
            float invDenom = 1 / (dot00 * dot11 - dot01 * dot01);
            float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

            // Check if point is in triangle
            return (u >= 0) && (v >= 0) && (u + v < 1);
        }
    }

    void CreateSpringConnectionsTest()
    {
        // Clear existing connections
        allConnectionsTest.Clear();

        // For each point, find nearby points and create connections
        for (int i = 0; i < allSpringPointsTest.Count; i++)
        {
            SpringPointTest currentPoint = allSpringPointsTest[i];

            for (int j = i + 1; j < allSpringPointsTest.Count; j++)
            {
                SpringPointTest otherPoint = allSpringPointsTest[j];
                float distance = Vector3.Distance(currentPoint.transform.position, otherPoint.transform.position);

                // Connect if within radius and not already connected
                if (distance <= connectionRadius * PointSpacing && !IsConnectedTest(currentPoint, otherPoint))
                {
                    // Clamp rest length to reasonable values
                    float restLength = Mathf.Clamp(distance, 0.5f, 3f);

                    ConnectionTest c = new ConnectionTest(currentPoint, otherPoint, restLength, springConstant, damperConstant);
                    c.InitLineRenderer(transform);
                    allConnectionsTest.Add(c);
                }
            }
        }
    }

    bool IsConnectedTest(SpringPointTest point1, SpringPointTest point2)
    {
        foreach (var conn in allConnectionsTest)
        {
            if ((conn.point1 == point1 && conn.point2 == point2) ||
                (conn.point1 == point2 && conn.point2 == point1))
                return true;
        }
        return false;
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

        float originOffset = 1e-9f; // very Small offset
        float jitterAmount = 1e-6f; // Small angle jitter
        int len = baseDirections.Length;
        Vector3[] testDirections = new Vector3[len * 2];
        for (int i = 0; i < len; i++)
        {
            // Create small random jitter vector
            Vector3 jitter_negative = new Vector3(-jitterAmount, -jitterAmount, -jitterAmount);
            Vector3 jitter_positive = new Vector3(jitterAmount, jitterAmount, jitterAmount);

            // Add jitter and normalize to keep direction unit length
            // reduces the edges or vertices where the ray barely grazes the mesh
            testDirections[i] = (baseDirections[i] + jitter_negative).normalized;
            testDirections[len + i] = (baseDirections[i] + jitter_positive).normalized;
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

    bool RayTriangleIntersection(Vector3 origin, Vector3 direction, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        Vector3 e1 = v2 - v1;
        Vector3 e2 = v3 - v1;
        Vector3 p = Vector3.Cross(direction, e2);
        float det = Vector3.Dot(e1, p);

        float epsilon = 1e-3f;
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
        return dist >= -epsilon; 
    }
    //  

}
