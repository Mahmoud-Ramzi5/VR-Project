using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Linq;
using System;


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
    public float springConstant;
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
    private Vector3 lastPos;

    [Header("Ground Collision")]
    public float groundLevel = 0f;       // Y-position of the ground plane
    public float groundBounce = 0.5f;   // Bounce coefficient (0 = no bounce, 1 = full bounce)
    public float groundFriction = 0.8f; // Friction (0 = full stop, 1 = no friction)
    public bool applyGroundCollision = true;

    [Header("External Systems")]
    public CollisionManager collisionManager;

    // Lists
    private List<Vector3> allPointPositions = new List<Vector3>();
    public List<SpringPoint> allSpringPoints = new List<SpringPoint>();
    public List<SpringConnection> allSpringConnections = new List<SpringConnection>();


    // Jobs
    private SpringJobManager jobManager;

    // Debug
    public List<GameObject> objects = new List<GameObject>();
    private LineRenderer lineRenderer;

    // NEW: Surface point tracking
    private List<SpringPoint> surfaceSpringPoints = new List<SpringPoint>();
    public List<Vector3> convexHullVertices;
    public List<Vector3> surfacePointsLocalSpace;
    private Dictionary<SpringPoint, int> surfacePointToVertexIndex = new Dictionary<SpringPoint, int>();
    private int originalVertexCount;
    // NEW: Surface integration settings
    [Header("Surface Integration")]
    public float surfaceDetectionThreshold = 0.2f;
    public bool enableMeshSubdivision = true;
    public bool autoUpdateMeshFromSurface = true;

    public bool applyVelocity = false;

    public Bounds boundingVolume;
    public List<SpringPoint> SurfacePoints => surfaceSpringPoints;


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

    private void OnEnable()
    {
        if (!CollisionManager.AllSoftBodies.Contains(this))
        {
            CollisionManager.AllSoftBodies.Add(this);
            // NEW DEBUG LOG
            Debug.Log($"{gameObject.name} added to CollisionManager. Total bodies: {CollisionManager.AllSoftBodies.Count}", this);
        }
    }

    private void OnDisable()
    {
        if (CollisionManager.AllSoftBodies.Contains(this))
        {
            CollisionManager.AllSoftBodies.Remove(this);
            // NEW DEBUG LOG
            Debug.Log($"{gameObject.name} removed from CollisionManager. Total bodies: {CollisionManager.AllSoftBodies.Count}", this);
        }
    }

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

        // NEW: Store original vertex count
        originalVertexCount = meshVertices.Length;

        FillObjectWithSpringPoints();


        RebuildSurfaceRepresentation();
        // NEW: After filling, identify surface points and subdivide mesh
        if (enableMeshSubdivision)
        {
            SubdivideMeshWithSurfacePoints();
        }

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

    private void GenerateConvexHull()
    {
        if (surfaceSpringPoints == null || surfaceSpringPoints.Count == 0)
        {
            Debug.LogWarning("No surface points to generate a convex hull from.");
            convexHullVertices = new List<Vector3>();
            return;
        }

        // We will generate the hull in LOCAL space so it can move with the object.
        var localSurfacePoints = new List<Vector3>();
        foreach (var sp in surfaceSpringPoints)
        {
            localSurfacePoints.Add(transform.InverseTransformPoint(sp.initialPosition));
        }

        // A simple (but not perfectly robust) convex hull algorithm is the Gift Wrapping algorithm.
        // For production, a more robust Quickhull implementation is recommended.
        // For now, we can just use the surface points directly as a point cloud for the support function.
        convexHullVertices = localSurfacePoints;
        Debug.Log($"Generated a convex hull representation with {convexHullVertices.Count} vertices.");
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
                    if (moveStep.magnitude > 0.001f)
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

    public float GetObjectRadius()
    {
        if (allSpringPoints.Count == 0) return 1f;

        Vector3 center = transform.position;
        float maxDistance = 0f;

        foreach (SpringPoint point in allSpringPoints)
        {
            float distance = Vector3.Distance(point.position, center);
            if (distance > maxDistance)
            {
                maxDistance = distance;
            }
        }

        return maxDistance;
    }

    void FixedUpdate()
    {
        // 1. Schedule gravity job
        jobManager.ScheduleGravityJobs(gravity, applyGravity);

        // 2. Schedule spring jobs
        jobManager.ScheduleSpringJobs(damperConstant);

        // 3. Complete all jobs and apply results
        jobManager.CompleteAllJobsAndApply();

        // 4. Update points
        foreach (var point in allSpringPoints)
        {
            point.UpdatePoint(Time.fixedDeltaTime);
        }

        // 5. Handle collisions
        // First, handle ground collisions
        if (applyGroundCollision)
        {
            foreach (var point in allSpringPoints)
            {
                HandleGroundCollision(point);
            }
        }

        // Second, handle inter-object collisions by calling the manager
        if (collisionManager != null)
        {
            collisionManager.ResolveInterObjectCollisions();
        }
        else
        {
            // NEW DEBUG LOG: This will alert you if the manager isn't assigned.
            if (enableMeshSubdivision) Debug.LogWarning($"{gameObject.name}: CollisionManager is not assigned in the inspector!", this);
        }

        if (Time.frameCount % 3 == 0) // Every 3 frames
        {
            UpdateSurfacePointsInMesh();
        }

        // Update Visualization
        UpdateMeshFromPoints();
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

        // Apply changes to mesh
        targetMesh.vertices = vertices;
        targetMesh.RecalculateNormals();
        targetMesh.RecalculateBounds();
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
        if (applyVelocity)
            foreach (var point in allSpringPoints)
                point.velocity = new Vector3(2f, 0, 0);
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
        // NEW: Clear surface point data
        surfaceSpringPoints.Clear();
        surfacePointToVertexIndex.Clear();
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

    public void AddSpringPointAtPosition(Vector3 worldPosition)
    {
        Vector3 worldCenter = transform.TransformPoint(meshBounds.center);
        Vector3 worldSize = Vector3.Scale(meshBounds.size, transform.lossyScale);
        Bounds worldBounds = new Bounds(worldCenter, worldSize);

        SpringPoint newPoint = CreateSpringPoint(worldPosition, worldBounds, false);

        UpdateMeshDataWithNewPoint(worldPosition);

        float newMass = totalMass / allSpringPoints.Count;
        foreach (var point in allSpringPoints)
        {
            point.mass = newMass;
        }

        for (int i = 0; i < allSpringPoints.Count - 1; i++)
        {
            SpringPoint other = allSpringPoints[i];
            float dist = Vector3.Distance(newPoint.position, other.position);

            if (dist <= connectionRadius * PointSpacing && !IsConnected(newPoint, other))
            {
                float restLength = Mathf.Clamp(dist, 0.5f, maxRestLength);
                float k = springConstant * (1f / dist);

                SpringConnection conn = new SpringConnection(newPoint, other, restLength, k, damperConstant);
                allSpringConnections.Add(conn);
            }
        }

        if (jobManager != null)
        {
            jobManager.CheckAndResizeArrays(allSpringPoints.Count, allSpringConnections.Count);
            jobManager.UpdateConnectionData(allSpringConnections);
        }

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
    private void UpdateMeshDataWithNewPoint(Vector3 newWorldPosition)
    {
        Vector3 newLocalPosition = transform.InverseTransformPoint(newWorldPosition);

        Vector3[] newVertices = new Vector3[meshVertices.Length + 1];
        int[] newTriangles = new int[meshTriangles.Length + 3];

        System.Array.Copy(meshVertices, newVertices, meshVertices.Length);
        System.Array.Copy(meshTriangles, newTriangles, meshTriangles.Length);

        newVertices[meshVertices.Length] = newLocalPosition;

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

        newTriangles[meshTriangles.Length] = closest1;
        newTriangles[meshTriangles.Length + 1] = closest2;
        newTriangles[meshTriangles.Length + 2] = meshVertices.Length;

        meshVertices = newVertices;
        meshTriangles = newTriangles;

        targetMesh.vertices = newVertices;
        targetMesh.triangles = newTriangles;
        targetMesh.RecalculateNormals();
        targetMesh.RecalculateBounds();
    }

    private SpringPoint CreateSpringPoint(Vector3 worldPos, Bounds bounds, bool isMeshVertex)
    {
        SpringPoint point = new SpringPoint(worldPos);
        point.mass = 1.0f;
        point.radius = 0.1f;
        point.nodeBounds = bounds;

        allSpringPoints.Add(point);
        allPointPositions.Add(worldPos);

        return point;
    }

    public void CreateSpringConnections()
    {
        allSpringConnections.Clear();

        for (int i = 0; i < allSpringPoints.Count; i++)
        {
            SpringPoint currentPoint = allSpringPoints[i];

            for (int j = i + 1; j < allSpringPoints.Count; j++)
            {
                SpringPoint otherPoint = allSpringPoints[j];
                float distance = Vector3.Distance(currentPoint.position, otherPoint.position);

                if (distance <= connectionRadius * PointSpacing && !IsConnected(currentPoint, otherPoint))
                {
                    float restLength = Mathf.Clamp(distance, 0.5f, maxRestLength);
                    float k = springConstant * (1f / distance);

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

    bool IsPointInsideMesh(Vector3 point)
    {
        Vector3 localPoint = transform.InverseTransformPoint(point);

        if (!meshBounds.Contains(localPoint))
            return false;

        Vector3[] baseDirections = {
        Vector3.left, Vector3.right,
        Vector3.forward, Vector3.back,
        Vector3.up, Vector3.down
        };

        float originOffset = 1e-9f;
        float jitterAmount = 1e-6f;
        int len = baseDirections.Length;
        Vector3[] testDirections = new Vector3[len * 2];
        for (int i = 0; i < len; i++)
        {
            Vector3 jitter_negative = new Vector3(-jitterAmount, -jitterAmount, -jitterAmount);
            Vector3 jitter_positive = new Vector3(jitterAmount, jitterAmount, jitterAmount);
            testDirections[i] = (baseDirections[i] + jitter_negative).normalized;
            testDirections[len + i] = (baseDirections[i] + jitter_positive).normalized;
        }

        foreach (Vector3 direction in testDirections)
        {
            Vector3 rayOrigin = localPoint + direction * originOffset;
            int intersections = CountRayIntersections(rayOrigin, direction);
            if (intersections % 2 == 1)
                return true;
        }

        return false;
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

        if (Mathf.Abs(det) < epsilon)
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

    public void SetPointsVisualization()
    {
        foreach (var point in allSpringPoints)
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
        if (!visualizeSpringPoints)
        {
            foreach (var obj in objects)
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
    }
    private void IdentifySurfacePoints()
    {
        surfaceSpringPoints.Clear();
        foreach (SpringPoint point in allSpringPoints)
        {
            if (IsPointOnSurface(point.position))
            {
                surfaceSpringPoints.Add(point);
            }
        }
        Debug.Log($"Found {surfaceSpringPoints.Count} surface spring points out of {allSpringPoints.Count} total points");
    }

    public void UpdateBoundingVolume()
    {
        Vector3 center = transform.position;
        float radius = GetObjectRadius();
        boundingVolume = new Bounds(center, Vector3.one * (radius * 2f));
    }

    private bool IsPointOnSurface(Vector3 worldPoint)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        float closestDistance = float.MaxValue;
        for (int i = 0; i < originalVertexCount; i++)
        {
            float distance = Vector3.Distance(localPoint, meshVertices[i]);
            if (distance < closestDistance)
            {
                closestDistance = distance;
            }
        }
        return closestDistance <= surfaceDetectionThreshold;
    }

    public void SubdivideMeshWithSurfacePoints()
    {
        if (surfaceSpringPoints.Count == 0)
        {
            Debug.LogWarning("No surface spring points found for subdivision");
            return;
        }
        Debug.Log($"Subdividing mesh with {surfaceSpringPoints.Count} surface points");
        List<Vector3> newVertices = new List<Vector3>(meshVertices);
        foreach (SpringPoint surfacePoint in surfaceSpringPoints)
        {
            Vector3 localPos = transform.InverseTransformPoint(surfacePoint.position);
            newVertices.Add(localPos);
            int newVertexIndex = newVertices.Count - 1;
            surfacePointToVertexIndex[surfacePoint] = newVertexIndex;
        }
        List<int> newTriangles = new List<int>(meshTriangles);
        CreateTrianglesForSurfacePoints(newVertices, newTriangles);
        UpdateMeshGeometry(newVertices.ToArray(), newTriangles.ToArray());
        Debug.Log($"Mesh subdivision complete. Vertices: {meshVertices.Length}, Triangles: {meshTriangles.Length / 3}");
    }

    private void CreateTrianglesForSurfacePoints(List<Vector3> vertices, List<int> triangles)
    {
        foreach (var kvp in surfacePointToVertexIndex)
        {
            SpringPoint surfacePoint = kvp.Key;
            int surfaceVertexIndex = kvp.Value;
            Vector3 surfaceLocalPos = vertices[surfaceVertexIndex];
            int closestTriangleIndex = FindClosestTriangleToPoint(surfaceLocalPos);

            if (closestTriangleIndex >= 0)
            {
                int baseIndex = closestTriangleIndex * 3;
                int v1 = meshTriangles[baseIndex];
                int v2 = meshTriangles[baseIndex + 1];
                int v3 = meshTriangles[baseIndex + 2];
                triangles.AddRange(new[] { surfaceVertexIndex, v1, v2 });
                triangles.AddRange(new[] { surfaceVertexIndex, v2, v3 });
                triangles.AddRange(new[] { surfaceVertexIndex, v3, v1 });
            }
            else
            {
                List<int> nearestVertices = FindNearestVertices(surfaceLocalPos, vertices, 3);
                if (nearestVertices.Count >= 3)
                {
                    triangles.AddRange(new[] { surfaceVertexIndex, nearestVertices[0], nearestVertices[1] });
                    triangles.AddRange(new[] { surfaceVertexIndex, nearestVertices[1], nearestVertices[2] });
                    triangles.AddRange(new[] { surfaceVertexIndex, nearestVertices[2], nearestVertices[0] });
                }
            }
        }
    }
    private int FindClosestTriangleToPoint(Vector3 localPoint)
    {
        float closestDistance = float.MaxValue;
        int closestTriangle = -1;
        for (int i = 0; i < meshTriangles.Length; i += 3)
        {
            Vector3 v1 = meshVertices[meshTriangles[i]];
            Vector3 v2 = meshVertices[meshTriangles[i + 1]];
            Vector3 v3 = meshVertices[meshTriangles[i + 2]];
            Vector3 triangleCenter = (v1 + v2 + v3) / 3f;
            float distance = Vector3.Distance(localPoint, triangleCenter);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTriangle = i / 3;
            }
        }
        return closestTriangle;
    }

    private List<int> FindNearestVertices(Vector3 position, List<Vector3> vertices, int count)
    {
        var vertexDistances = new List<(int index, float distance)>();
        for (int i = 0; i < originalVertexCount; i++)
        {
            float distance = Vector3.Distance(position, vertices[i]);
            vertexDistances.Add((i, distance));
        }
        return vertexDistances.OrderBy(x => x.distance).Take(count).Select(x => x.index).ToList();
    }

    private void UpdateMeshGeometry(Vector3[] newVertices, int[] newTriangles)
    {
        meshVertices = newVertices;
        meshTriangles = newTriangles;
        targetMesh.Clear();
        targetMesh.vertices = meshVertices;
        targetMesh.triangles = newTriangles;
        targetMesh.RecalculateNormals();
        targetMesh.RecalculateBounds();
        meshBounds = targetMesh.bounds;
    }

    private void UpdateSurfacePointsInMesh()
    {
        if (!autoUpdateMeshFromSurface || surfacePointToVertexIndex.Count == 0) return;
        bool meshChanged = false;
        foreach (var kvp in surfacePointToVertexIndex)
        {
            SpringPoint surfacePoint = kvp.Key;
            int vertexIndex = kvp.Value;
            if (vertexIndex < meshVertices.Length)
            {
                Vector3 newLocalPos = transform.InverseTransformPoint(surfacePoint.position);
                Vector3 oldLocalPos = meshVertices[vertexIndex];
                if (Vector3.Distance(newLocalPos, oldLocalPos) > 0.01f)
                {
                    meshVertices[vertexIndex] = newLocalPos;
                    meshChanged = true;
                }
            }
        }
        if (meshChanged)
        {
            targetMesh.vertices = meshVertices;
            targetMesh.RecalculateNormals();
            targetMesh.RecalculateBounds();
        }
    }

    private void GenerateLocalSurfacePoints()
    {
        surfacePointsLocalSpace = new List<Vector3>();
        if (surfaceSpringPoints == null || surfaceSpringPoints.Count == 0)
        {
            Debug.LogWarning("No surface points found to generate GJK shape.", this);
            return;
        }
        foreach (var sp in surfaceSpringPoints)
        {
            surfacePointsLocalSpace.Add(transform.InverseTransformPoint(sp.initialPosition));
        }
    }

    // NEW Public Method to be called by MeshDeformer
    public void RebuildSurfaceRepresentation()
    {
        IdentifySurfacePoints();
        GenerateLocalSurfacePoints();
        Debug.Log($"{gameObject.name}: Surface representation rebuilt. New surface point count: {surfaceSpringPoints.Count}", this);
    }

    // NEW Gizmos for debugging surface points
    private void OnDrawGizmosSelected()
    {
        if (surfaceSpringPoints != null && surfaceSpringPoints.Count > 0)
        {
            Gizmos.color = Color.yellow;
            foreach (var sp in surfaceSpringPoints)
            {
                Gizmos.DrawSphere(sp.position, 0.05f);
            }
        }
    }
}