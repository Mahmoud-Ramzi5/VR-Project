using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Linq;


public class OctreeSpringFiller : MonoBehaviour
{
    [Header("Filling Settings")]
    public GameObject springPointPrefab;
    public bool visualizeSpringPoints = true;
    public bool visualizeSpringConnections = true;

    public float minNodeSize = 0.5f;
    public float PointSpacing = 0.5f;
    public bool isSolid = true;

    [Header("Spring Settings")]
    public float springConstant = 10f;
    public float damperConstant = 0.5f;
    public float connectionRadius = 2f;
    public float maxRestLength = 3f;

    [Header("Mesh Settings")]
    public float totalMass = 100f;
    public bool applyGravity = true;
    public Vector3 gravity => new Vector3(0, -9.81f, 0);

    private Mesh targetMesh;
    private Bounds meshBounds;
    private Vector3[] meshVertices;
    private int[] meshTriangles;
    private MeshManager meshManager;
    private Vector3 lastPos;

    [Header("Ground Collision")]
    public float groundLevel = 0f;       // Y-position of the ground plane
    public float groundBounce = 0.5f;   // Bounce coefficient (0 = no bounce, 1 = full bounce)
    public float groundFriction = 0.8f; // Friction (0 = full stop, 1 = no friction)
    public bool applyGroundCollision = true;

    // Lists
    private List<Vector3> allPointPositions = new List<Vector3>();
    public List<SpringPoint> allSpringPoints = new List<SpringPoint>();
    public List<SpringConnection> allSpringConnections = new List<SpringConnection>();


    // Jobs
    private SpringJobManager jobManager;

    // Debug
    public List<GameObject> objects = new List<GameObject>();
    private LineRenderer lineRenderer;

    private void Awake()
    {
        GameObject obj = new GameObject("LineRenderer");
        obj.transform.SetParent(transform);

        lineRenderer = obj.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 0;

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;
    }

    void Start()
    {
        meshManager = GetComponent<MeshManager>();
        // save transform
        lastPos = transform.position;

        targetMesh = meshManager.CurrentMesh;
        meshVertices=targetMesh.vertices;
        meshBounds = targetMesh.bounds;

        FillObjectWithSpringPoints();

        // Update positions and bounds on start
        foreach (SpringPoint point in allSpringPoints)
        {
            point.mass = totalMass / allSpringPoints.Count;
            Vector3 moveStep = transform.position - lastPos;
            point.UpdateBounds(moveStep);
        }


        // Use Jobs to calculate physics on GPU threads
        // Parallelizing calculations improves performance
        jobManager = gameObject.AddComponent<SpringJobManager>();
        jobManager.InitializeArrays(this, allSpringPoints.Count, allSpringConnections.Count);

        // Initial connection data setup
        jobManager.UpdateConnectionData(allSpringConnections);

    }

    private void Update()
    {
        if (Time.frameCount % 5 == 0)
        {
            // Run every 5th frame
            // Spread out expensive operations
            foreach (SpringPoint point in allSpringPoints)
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
        jobManager.ScheduleSpringJobs(damperConstant);  

        // 3. Complete all jobs and apply results
        jobManager.CompleteAllJobsAndApply();

        //// Update springs
        //// This was moved to Jobs
        //foreach (var connection in allSpringConnections)
        //{
        //    connection.CalculateAndApplyForces();
        //}

        // 4. Update mesh (consider throttling this)
        //if (Time.frameCount % 3 == 0) // Update mesh every 3 physics frames
        //{
            // Update mesh to follow points
            UpdateMeshFromPoints();
        //}

        // 5. Handle collisions
        if (applyGroundCollision)
        {
            foreach (var point in allSpringPoints)
            {
                HandleGroundCollision(point);
            }
        }

        // Update points
        foreach (var point in allSpringPoints)
        {
            point.UpdatePoint(Time.fixedDeltaTime);
        }


        // Update Visualization
        UpdatePointsVisualization();
        UpdateConnectionsVisualization();
    }

    // Call this when connections change
    public void UpdateConnections()
    {
        if (jobManager != null)
        {
            jobManager.UpdateConnectionData(allSpringConnections);
        }
    }


    public void HandleGroundCollision(SpringPoint point)
    {
        if (point.position.y < groundLevel)
        {
            point.position = new Vector3(point.position.x, groundLevel, point.position.z);

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
        Vector3[] vertices = meshVertices;

        // Find average position of all points
        Vector3 averagePos = Vector3.zero;
        foreach (var point in allSpringPoints)
        {
            averagePos += point.position;
        }
        averagePos /= allSpringPoints.Count;

        // Update mesh position to follow points
        transform.position = averagePos;

        // Update each vertex based on its corresponding point
        for (int i = 0; i < vertices.Length; i++)
        {
            // Find closest spring point to this vertex
            Vector3 worldVertex = transform.TransformPoint(vertices[i]);
            SpringPoint closestPoint = FindClosestPoint(worldVertex);
           
            if (closestPoint != null)
            {
                // Update vertex position relative to new mesh position
                vertices[i] = transform.InverseTransformPoint(closestPoint.position);
            }
        }

        meshManager.UpdateVertexPositions(vertices);
    }

    SpringPoint FindClosestPoint(Vector3 worldPos)
    {
        SpringPoint closest = null;
        float minDist = float.MaxValue;

        foreach (var point in allSpringPoints)
        {
            float dist = Vector3.Distance(worldPos, point.position);
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
        CreateSpringConnections();

        // initlize visualization
        SetPointsVisualization();
        SetConnectionsVisualization();

        // Some logs
        Debug.Log($"Octree Nodes: {total_nodes}");
        Debug.Log($"Created {allSpringPoints.Count} spring points test.");
    }

    void ClearExistingPoints()
    {
        foreach (var point in objects)
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

    // Add this method to the OctreeSpringFiller class
    public void AddSpringPointAtPosition(Vector3 worldPosition)
    {
       

        // 3. Create world bounds for the new point
        Vector3 worldCenter = transform.TransformPoint(meshBounds.center);
        Vector3 worldSize = Vector3.Scale(meshBounds.size, transform.lossyScale);
        Bounds worldBounds = new Bounds(worldCenter, worldSize);

        SpringPoint newPoint = CreateSpringPoint(worldPosition, worldBounds, false);

        // 5. Update mesh data
        UpdateMeshDataWithNewPoint(worldPosition);

        // 5. Update masses for all points
        float newMass = totalMass / allSpringPoints.Count;
        foreach (var point in allSpringPoints)
        {
            point.mass = newMass;
        }

        // 6. Create connections for the new point
        for (int i = 0; i < allSpringPoints.Count - 1; i++) // Skip the last point (itself)
        {
            SpringPoint other = allSpringPoints[i];
            float dist = Vector3.Distance(newPoint.position, other.position);

            if (dist <= connectionRadius * PointSpacing && !IsConnected(newPoint, other))
            {
                float restLength = Mathf.Clamp(dist, 0.5f, maxRestLength);

                // Calculate spring constant based on distance
                float k = springConstant * (10f / dist);

                SpringConnection conn = new SpringConnection(
                    newPoint,
                    other,
                    restLength,
                    k,  // Per-connection spring constant
                    damperConstant
                );
                allSpringConnections.Add(conn);
            }
        }

        // 7. Update job manager
        if (jobManager != null)
        {
            jobManager.CheckAndResizeArrays(allSpringPoints.Count, allSpringConnections.Count);
            jobManager.UpdateConnectionData(allSpringConnections);
        }

        // 8. Update visualization
        GameObject newObj;
        if (springPointPrefab != null)
        {
            newObj = Instantiate(springPointPrefab, newPoint.position, Quaternion.identity);
            newObj.name = $"Point_{worldPosition.x}_{worldPosition.y}_{worldPosition.z}";
        }
        else
        {
            newObj = new GameObject($"Point_{worldPosition.x}_{worldPosition.y}_{worldPosition.z}");
        }
        objects.Add(newObj);
        

        SetConnectionsVisualization();
    }
    private void  UpdateMeshDataWithNewPoint(Vector3 newWorldPosition)
    {
        // Convert to local space
        Vector3 newLocalPosition = transform.InverseTransformPoint(newWorldPosition);

        // Create new arrays with increased size
        Vector3[] newVertices = new Vector3[meshVertices.Length + 1];
        int[] newTriangles = new int[meshTriangles.Length + 3]; // Adding one triangle

        // Copy existing data
        System.Array.Copy(meshVertices, newVertices, meshVertices.Length);
        System.Array.Copy(meshTriangles, newTriangles, meshTriangles.Length);

        // Add new vertex
        newVertices[meshVertices.Length] = newLocalPosition;

        // Create a new triangle connecting to nearby vertices
        // Find 2 closest existing vertices
        int closest1 = 0;
        int closest2 = 1;
        float minDist1 = float.MaxValue;
        float minDist2 = float.MaxValue;

        for (int i = 0; i < meshVertices.Length; i++)
        {
            float dist = Vector3.Distance(newLocalPosition, meshVertices[i]);
            if (dist < minDist1)
            {
                minDist2 = minDist1;
                closest2 = closest1;
                minDist1 = dist;
                closest1 = i;
            }
            else if (dist < minDist2)
            {
                minDist2 = dist;
                closest2 = i;
            }
        }
        
        // Add new triangle (order matters for normal direction)
        newTriangles[meshTriangles.Length] = closest1;
        newTriangles[meshTriangles.Length + 1] = closest2;
        newTriangles[meshTriangles.Length + 2] = meshVertices.Length; // New vertex index

        // Update mesh references
        meshVertices = newVertices;
        meshTriangles = newTriangles;

        // Update the actual mesh
        targetMesh.vertices = newVertices;
        targetMesh.triangles = newTriangles;
        targetMesh.RecalculateNormals();
        targetMesh.RecalculateBounds();
    }

    // Modify existing CreateSpringPoint to return the SpringPoint
    private SpringPoint CreateSpringPoint(Vector3 worldPos, Bounds bounds, bool isMeshVertex)
    {
        SpringPoint point = new SpringPoint(worldPos);
        point.mass = 1.0f;
        point.radius = 0.1f;
        point.nodeBounds = bounds;

        // Existing commented code remains unchanged...

        allSpringPoints.Add(point);
        allPointPositions.Add(worldPos);

        return point; // Return the created point
    }

    public void CreateSpringConnections()
    {
        // Clear existing connections
        allSpringConnections.Clear();

        // For each point, find nearby points and create connections
        for (int i = 0; i < allSpringPoints.Count; i++)
        {
            SpringPoint currentPoint = allSpringPoints[i];

            for (int j = i + 1; j < allSpringPoints.Count; j++)
            {
                SpringPoint otherPoint = allSpringPoints[j];
                float distance = Vector3.Distance(currentPoint.position, otherPoint.position);

                // Connect if within radius and not already connected
                if (distance <= connectionRadius * PointSpacing && !IsConnected(currentPoint, otherPoint))
                {
                    // Clamp rest length to reasonable values
                    float restLength = Mathf.Clamp(distance, 0.5f, maxRestLength);
                    float k = springConstant * (10f / distance);

                    SpringConnection c = new SpringConnection(currentPoint, otherPoint, restLength, k, damperConstant);
                    allSpringConnections.Add(c);
                }
            }
        }
    }

    bool IsConnected(SpringPoint point1, SpringPoint point2)
    {
        foreach (var conn in allSpringConnections)
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

    // Debug
    public void SetPointsVisualization()
    {
        foreach(var point in allSpringPoints)
        {
            GameObject obj;
            Vector3 pos = point.position;
            if (springPointPrefab != null)
            {
                obj = Instantiate(springPointPrefab, pos, Quaternion.identity);
                obj.name = $"Point_{pos.x}_{pos.y}_{pos.z}";
            }
            else
            {
                obj = new GameObject($"Point_{pos.x}_{pos.y}_{pos.z}");
            }
            objects.Add(obj);
        }
    }

    public void UpdatePointsVisualization()
    {
        //if (Time.frameCount % 3 == 0)   // Only update every 3 frames
        //{
            if (!visualizeSpringPoints)
            {
                foreach(var obj in objects)
                {
                    obj.SetActive(false);
                }
                return;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                objects[i].SetActive(true);
                objects[i].transform.position = allSpringPoints[i].position;
            }
        //}
    }

    public void SetConnectionsVisualization()
    {
        lineRenderer.positionCount = allSpringConnections.Count * 2;

        Vector3[] positions = new Vector3[allSpringConnections.Count * 2];
        for (int i = 0; i < allSpringConnections.Count; i++)
        {
            positions[i * 2] = allSpringConnections[i].point1.position;
            positions[i * 2 + 1] = allSpringConnections[i].point2.position;
        }

        lineRenderer.SetPositions(positions);
    }

    public void UpdateConnectionsVisualization()
    {
        //if (Time.frameCount % 3 == 0)   // Only update every 3 frames
        //{
            if (!visualizeSpringConnections)
            {
                lineRenderer.enabled = false;
                return;
            }

            lineRenderer.enabled = true;

            for (int i = 0; i < allSpringConnections.Count; i++)
            {
                lineRenderer.SetPosition(i * 2, allSpringConnections[i].point1.position);
                lineRenderer.SetPosition(i * 2 + 1, allSpringConnections[i].point2.position);
            }
        //}
    }
    //
}
