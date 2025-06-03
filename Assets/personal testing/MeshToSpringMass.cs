// Perfect

using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class MeshToSpringMass : MonoBehaviour
{
    [Header("Particle Settings")]
    public SpringPoint springPointPrefab;
    public float particleSpacing = 0.5f;
    public float particleRadius = 0.3f;
    public bool constrainToMesh = true;
    public bool createSurfaceParticles = true;
    public bool createInternalParticles = true;

    [Header("Spring Settings")]
    public float springConstant = 10f;
    public float damperConstant = 0.5f;
    public float maxConnectionDistance = 1.5f;

    [Header("Fixed Particles")]
    public bool fixSurfaceParticles = false;
    public bool fixCornerParticles = false;

    private Mesh mesh;
    private List<SpringPoint> particles = new List<SpringPoint>();
    private Bounds meshBounds;
    private Vector3[] meshVertices;
    private int[] meshTriangles;

    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        meshBounds = mesh.bounds;
        meshVertices = mesh.vertices;
        meshTriangles = mesh.triangles;

        GenerateParticles();
        ConnectParticles();
    }

    void GenerateParticles()
    {
        // Clear existing particles
        foreach (var particle in particles)
        {
            if (particle != null) Destroy(particle.gameObject);
        }
        particles.Clear();

        // Calculate grid dimensions based on mesh bounds and spacing
        Vector3 size = meshBounds.size;
        int xCount = Mathf.Max(2, Mathf.FloorToInt(size.x / particleSpacing));
        int yCount = Mathf.Max(2, Mathf.FloorToInt(size.y / particleSpacing));
        int zCount = Mathf.Max(2, Mathf.FloorToInt(size.z / particleSpacing));

        // Create particles in a 3D grid
        for (int x = 0; x < xCount; x++)
        {
            for (int y = 0; y < yCount; y++)
            {
                for (int z = 0; z < zCount; z++)
                {
                    // Calculate normalized position in grid (0-1 range)
                    Vector3 normalizedPos = new Vector3(
                        xCount > 1 ? x / (float)(xCount - 1) : 0.5f,
                        yCount > 1 ? y / (float)(yCount - 1) : 0.5f,
                        zCount > 1 ? z / (float)(zCount - 1) : 0.5f
                    );

                    // Calculate position in local space (within bounds)
                    Vector3 localPos = new Vector3(
                        meshBounds.min.x + normalizedPos.x * size.x,
                        meshBounds.min.y + normalizedPos.y * size.y,
                        meshBounds.min.z + normalizedPos.z * size.z
                    );

                    // Convert to world space
                    Vector3 worldPos = transform.TransformPoint(localPos);

                    // Check if point is inside mesh
                    bool isInside = IsPointInsideMesh(worldPos);

                    // Create particle if it meets our criteria
                    if ((isInside && createInternalParticles) || (!isInside && createSurfaceParticles))
                    {
                        CreateParticle(worldPos, isInside);
                    }
                }
            }
        }

        Debug.Log($"Created {particles.Count} particles");
    }

    void CreateParticle(Vector3 position, bool isInside)
    {
        SpringPoint particle = Instantiate(springPointPrefab, position, Quaternion.identity);
        particle.transform.SetParent(transform);
        particle.radius = particleRadius;
        particle.mass = 1f;

        // Set bounds to match mesh bounds
        particle.boundsMin = transform.TransformPoint(meshBounds.min);
        particle.boundsMax = transform.TransformPoint(meshBounds.max);

        // Mark surface particles as fixed if needed
        if (!isInside)
        {
            particle.isFixed = fixSurfaceParticles;

            // Mark corner particles as fixed if needed
            if (fixCornerParticles && IsCornerPoint(transform.InverseTransformPoint(position)))
            {
                particle.isFixed = true;
            }
        }

        particles.Add(particle);
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

    void ConnectParticles()
    {
        // Clear existing connections
        foreach (var particle in particles)
        {
            particle.connections.Clear();
        }

        // Create a spatial hash for faster neighbor finding
        SpatialHash spatialHash = new SpatialHash(maxConnectionDistance);
        foreach (var particle in particles)
        {
            spatialHash.Add(particle.transform.position, particle);
        }

        // Connect nearby particles
        foreach (var particle in particles)
        {
            List<SpringPoint> neighbors = spatialHash.Query(particle.transform.position, maxConnectionDistance);

            foreach (var neighbor in neighbors)
            {
                if (neighbor == particle) continue;

                float distance = Vector3.Distance(
                    particle.transform.position,
                    neighbor.transform.position
                );

                if (distance <= maxConnectionDistance)
                {
                    // Check if connection already exists
                    bool alreadyConnected = false;
                    foreach (var conn in particle.connections)
                    {
                        if (conn.point == neighbor)
                        {
                            alreadyConnected = true;
                            break;
                        }
                    }

                    if (!alreadyConnected)
                    {
                        // Create bidirectional connections
                        particle.connections.Add(new Connection(
                            neighbor,
                            distance,
                            springConstant,
                            damperConstant
                        ));

                        neighbor.connections.Add(new Connection(
                            particle,
                            distance,
                            springConstant,
                            damperConstant
                        ));
                    }
                }
            }
        }
    }

    bool IsPointInsideMesh(Vector3 worldPoint)
    {
        if (!constrainToMesh) return true;

        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

        // Simple bounds check first
        if (!meshBounds.Contains(localPoint)) return false;

        // Raycast-based containment check without using Physics
        return IsPointInsideMeshManual(localPoint);
    }

    bool IsPointInsideMeshManual(Vector3 point)
    {
        // Create a ray from the point in an arbitrary direction
        Vector3 rayDirection = Vector3.forward;
        int intersectionCount = 0;

        // Check against all triangles in the mesh
        for (int i = 0; i < meshTriangles.Length; i += 3)
        {
            Vector3 v1 = meshVertices[meshTriangles[i]];
            Vector3 v2 = meshVertices[meshTriangles[i + 1]];
            Vector3 v3 = meshVertices[meshTriangles[i + 2]];

            if (RayIntersectsTriangle(point, rayDirection, v1, v2, v3))
            {
                intersectionCount++;
            }
        }

        // Odd number of intersections means point is inside
        return intersectionCount % 2 == 1;
    }

    bool RayIntersectsTriangle(Vector3 rayOrigin, Vector3 rayDirection,
                             Vector3 v1, Vector3 v2, Vector3 v3)
    {
        // Möller–Trumbore intersection algorithm
        Vector3 edge1 = v2 - v1;
        Vector3 edge2 = v3 - v1;
        Vector3 h = Vector3.Cross(rayDirection, edge2);
        float a = Vector3.Dot(edge1, h);

        if (a > -Mathf.Epsilon && a < Mathf.Epsilon)
            return false; // Ray is parallel to triangle

        float f = 1.0f / a;
        Vector3 s = rayOrigin - v1;
        float u = f * Vector3.Dot(s, h);

        if (u < 0.0 || u > 1.0)
            return false;

        Vector3 q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(rayDirection, q);

        if (v < 0.0 || u + v > 1.0)
            return false;

        float t = f * Vector3.Dot(edge2, q);
        return t > Mathf.Epsilon;
    }

    void Update()
    {
        if (constrainToMesh)
        {
            ConstrainParticlesToMesh();
        }
    }

    void ConstrainParticlesToMesh()
    {
        foreach (var particle in particles)
        {
            if (particle.isFixed) continue;

            Vector3 worldPos = particle.transform.position;
            if (!IsPointInsideMesh(worldPos))
            {
                // Push particle back to nearest surface point
                Vector3 closestSurface = FindClosestSurfacePoint(worldPos);
                Vector3 correction = closestSurface - worldPos;
                particle.transform.position += correction * 0.1f; // Small correction to avoid jitter
                particle.velocity -= Vector3.Project(particle.velocity, correction.normalized) * 0.5f;
            }
        }
    }

    Vector3 FindClosestSurfacePoint(Vector3 worldPoint)
    {
        // Find closest vertex as fallback
        Vector3 closestVertex = meshVertices[0];
        float closestDistance = Vector3.Distance(worldPoint, transform.TransformPoint(closestVertex));

        foreach (Vector3 vertex in meshVertices)
        {
            Vector3 worldVertex = transform.TransformPoint(vertex);
            float distance = Vector3.Distance(worldPoint, worldVertex);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestVertex = vertex;
            }
        }

        // Find closest point on triangles near the closest vertex
        Vector3 closestPoint = transform.TransformPoint(closestVertex);
        float minDistance = closestDistance;

        for (int i = 0; i < meshTriangles.Length; i += 3)
        {
            Vector3 v1 = meshVertices[meshTriangles[i]];
            Vector3 v2 = meshVertices[meshTriangles[i + 1]];
            Vector3 v3 = meshVertices[meshTriangles[i + 2]];

            // Check if this triangle contains the closest vertex
            if (v1 == closestVertex || v2 == closestVertex || v3 == closestVertex)
            {
                Vector3 triangleClosest = ClosestPointOnTriangle(
                    transform.TransformPoint(v1),
                    transform.TransformPoint(v2),
                    transform.TransformPoint(v3),
                    worldPoint
                );

                float dist = Vector3.Distance(worldPoint, triangleClosest);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestPoint = triangleClosest;
                }
            }
        }

        return closestPoint;
    }

    Vector3 ClosestPointOnTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 p)
    {
        // Check if P is in vertex region outside A
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = p - a;

        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0.0f && d2 <= 0.0f)
            return a; // Barycentric coordinates (1,0,0)

        // Check if P is in vertex region outside B
        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0.0f && d4 <= d3)
            return b; // Barycentric coordinates (0,1,0)

        // Check if P is in edge region of AB
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0.0f && d1 >= 0.0f && d3 <= 0.0f)
        {
            float v = d1 / (d1 - d3);
            return a + v * ab; // Barycentric coordinates (1-v,v,0)
        }

        // Check if P is in vertex region outside C
        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0.0f && d5 <= d6)
            return c; // Barycentric coordinates (0,0,1)

        // Check if P is in edge region of AC
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0.0f && d2 >= 0.0f && d6 <= 0.0f)
        {
            float w = d2 / (d2 - d6);
            return a + w * ac; // Barycentric coordinates (1-w,0,w)
        }

        // Check if P is in edge region of BC
        float va = d3 * d6 - d5 * d4;
        if (va <= 0.0f && (d4 - d3) >= 0.0f && (d5 - d6) >= 0.0f)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + w * (c - b); // Barycentric coordinates (0,1-w,w)
        }

        // P inside face region
        float denom = 1.0f / (va + vb + vc);
        float v2 = vb * denom;
        float w2 = vc * denom;
        return a + ab * v2 + ac * w2; // Barycentric coordinates (1-v-w,v,w)
    }

    private class SpatialHash
    {
        private Dictionary<Vector3Int, List<SpringPoint>> cells = new Dictionary<Vector3Int, List<SpringPoint>>();
        private float cellSize;

        public SpatialHash(float cellSize)
        {
            this.cellSize = cellSize;
        }

        public void Add(Vector3 position, SpringPoint particle)
        {
            Vector3Int cell = new Vector3Int(
                Mathf.FloorToInt(position.x / cellSize),
                Mathf.FloorToInt(position.y / cellSize),
                Mathf.FloorToInt(position.z / cellSize)
            );

            if (!cells.ContainsKey(cell))
            {
                cells[cell] = new List<SpringPoint>();
            }
            cells[cell].Add(particle);
        }

        public List<SpringPoint> Query(Vector3 position, float radius)
        {
            List<SpringPoint> results = new List<SpringPoint>();
            int cellsToCheck = Mathf.CeilToInt(radius / cellSize);

            Vector3Int centerCell = new Vector3Int(
                Mathf.FloorToInt(position.x / cellSize),
                Mathf.FloorToInt(position.y / cellSize),
                Mathf.FloorToInt(position.z / cellSize)
            );

            for (int x = -cellsToCheck; x <= cellsToCheck; x++)
            {
                for (int y = -cellsToCheck; y <= cellsToCheck; y++)
                {
                    for (int z = -cellsToCheck; z <= cellsToCheck; z++)
                    {
                        Vector3Int cell = centerCell + new Vector3Int(x, y, z);
                        if (cells.TryGetValue(cell, out List<SpringPoint> cellParticles))
                        {
                            results.AddRange(cellParticles);
                        }
                    }
                }
            }

            return results;
        }
    }
}


//working almost perfeectly

//using UnityEngine;
//using System.Collections.Generic;

//[RequireComponent(typeof(MeshFilter))]
//public class MeshToSpringMass : MonoBehaviour
//{
//    [Header("Particle Settings")]
//    public SpringPoint springPointPrefab;
//    public float particleSpacing = 0.5f;
//    public float particleRadius = 0.3f;
//    public bool constrainToMesh = true;
//    public bool createSurfaceParticles = true;
//    public bool createInternalParticles = true;

//    [Header("Spring Settings")]
//    public float springConstant = 10f;
//    public float damperConstant = 0.5f;
//    public float maxConnectionDistance = 1.5f;

//    [Header("Fixed Particles")]
//    public bool fixSurfaceParticles = false;
//    public bool fixCornerParticles = false;

//    private Mesh mesh;
//    private List<SpringPoint> particles = new List<SpringPoint>();
//    private Bounds meshBounds;

//    void Start()
//    {
//        mesh = GetComponent<MeshFilter>().mesh;
//        meshBounds = mesh.bounds;

//        GenerateParticles();
//        ConnectParticles();
//    }

//    void GenerateParticles()
//    {
//        // Clear existing particles
//        foreach (var particle in particles)
//        {
//            if (particle != null) Destroy(particle.gameObject);
//        }
//        particles.Clear();

//        // Calculate grid dimensions based on mesh bounds and spacing
//        Vector3 size = meshBounds.size;
//        int xCount = Mathf.Max(2, Mathf.FloorToInt(size.x / particleSpacing));
//        int yCount = Mathf.Max(2, Mathf.FloorToInt(size.y / particleSpacing));
//        int zCount = Mathf.Max(2, Mathf.FloorToInt(size.z / particleSpacing));

//        // Create particles in a 3D grid
//        for (int x = 0; x < xCount; x++)
//        {
//            for (int y = 0; y < yCount; y++)
//            {
//                for (int z = 0; z < zCount; z++)
//                {
//                    // Calculate normalized position in grid (0-1 range)
//                    Vector3 normalizedPos = new Vector3(
//                        xCount > 1 ? x / (float)(xCount - 1) : 0.5f,
//                        yCount > 1 ? y / (float)(yCount - 1) : 0.5f,
//                        zCount > 1 ? z / (float)(zCount - 1) : 0.5f
//                    );

//                    // Calculate position in local space (within bounds)
//                    Vector3 localPos = new Vector3(
//                        meshBounds.min.x + normalizedPos.x * size.x,
//                        meshBounds.min.y + normalizedPos.y * size.y,
//                        meshBounds.min.z + normalizedPos.z * size.z
//                    );

//                    // Convert to world space
//                    Vector3 worldPos = transform.TransformPoint(localPos);

//                    // Check if point is inside mesh
//                    bool isInside = IsPointInsideMesh(worldPos);

//                    // Create particle if it meets our criteria
//                    if ((isInside && createInternalParticles) || (!isInside && createSurfaceParticles))
//                    {
//                        CreateParticle(worldPos, isInside);
//                    }
//                }
//            }
//        }

//        Debug.Log($"Created {particles.Count} particles");
//    }

//    void CreateParticle(Vector3 position, bool isInside)
//    {
//        SpringPoint particle = Instantiate(springPointPrefab, position, Quaternion.identity);
//        particle.transform.SetParent(transform);
//        particle.radius = particleRadius;
//        particle.mass = 1f;

//        // Set bounds to match mesh bounds
//        particle.boundsMin = transform.TransformPoint(meshBounds.min);
//        particle.boundsMax = transform.TransformPoint(meshBounds.max);

//        // Mark surface particles as fixed if needed
//        if (!isInside)
//        {
//            particle.isFixed = fixSurfaceParticles;

//            // Mark corner particles as fixed if needed
//            if (fixCornerParticles)
//            {
//                Vector3 localPos = transform.InverseTransformPoint(position);
//                if (IsCornerPoint(localPos))
//                {
//                    particle.isFixed = true;
//                }
//            }
//        }

//        particles.Add(particle);
//    }

//    bool IsCornerPoint(Vector3 localPos)
//    {
//        Vector3 min = meshBounds.min;
//        Vector3 max = meshBounds.max;
//        float threshold = particleSpacing * 0.5f;

//        return (Vector3.Distance(localPos, min) < threshold ||
//               Vector3.Distance(localPos, new Vector3(min.x, min.y, max.z)) < threshold ||
//               Vector3.Distance(localPos, new Vector3(min.x, max.y, min.z)) < threshold ||
//               Vector3.Distance(localPos, new Vector3(min.x, max.y, max.z)) < threshold ||
//               Vector3.Distance(localPos, new Vector3(max.x, min.y, min.z)) < threshold ||
//               Vector3.Distance(localPos, new Vector3(max.x, min.y, max.z)) < threshold ||
//               Vector3.Distance(localPos, new Vector3(max.x, max.y, min.z)) < threshold ||
//               Vector3.Distance(localPos, max) < threshold);
//    }

//    void ConnectParticles()
//    {
//        // Clear existing connections
//        foreach (var particle in particles)
//        {
//            particle.connections.Clear();
//        }

//        // Create a spatial hash for faster neighbor finding
//        SpatialHash spatialHash = new SpatialHash(maxConnectionDistance);
//        foreach (var particle in particles)
//        {
//            spatialHash.Add(particle.transform.position, particle);
//        }

//        // Connect nearby particles
//        foreach (var particle in particles)
//        {
//            List<SpringPoint> neighbors = spatialHash.Query(particle.transform.position, maxConnectionDistance);

//            foreach (var neighbor in neighbors)
//            {
//                if (neighbor == particle) continue;

//                float distance = Vector3.Distance(
//                    particle.transform.position,
//                    neighbor.transform.position
//                );

//                if (distance <= maxConnectionDistance)
//                {
//                    // Check if connection already exists
//                    bool alreadyConnected = false;
//                    foreach (var conn in particle.connections)
//                    {
//                        if (conn.point == neighbor)
//                        {
//                            alreadyConnected = true;
//                            break;
//                        }
//                    }

//                    if (!alreadyConnected)
//                    {
//                        // Create bidirectional connections
//                        particle.connections.Add(new Connection(
//                            neighbor,
//                            distance,
//                            springConstant,
//                            damperConstant
//                        ));

//                        neighbor.connections.Add(new Connection(
//                            particle,
//                            distance,
//                            springConstant,
//                            damperConstant
//                        ));
//                    }
//                }
//            }
//        }
//    }

//    bool IsPointInsideMesh(Vector3 worldPoint)
//    {
//        if (!constrainToMesh) return true;

//        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

//        // Simple bounds check first
//        if (!meshBounds.Contains(localPoint)) return false;

//        // Create a ray from outside the mesh towards the point
//        Vector3 rayStart = worldPoint + (worldPoint - transform.position).normalized * meshBounds.size.magnitude;
//        Ray ray = new Ray(rayStart, (worldPoint - rayStart).normalized);

//        // Count intersections with the mesh
//        RaycastHit[] hits = Physics.RaycastAll(ray, Vector3.Distance(rayStart, worldPoint) * 1.1f);

//        int hitCount = 0;
//        foreach (var hit in hits)
//        {
//            if (hit.collider.gameObject == gameObject)
//            {
//                hitCount++;
//            }
//        }

//        // Odd number of hits means point is inside
//        return hitCount % 2 == 1;
//    }

//    void Update()
//    {
//        if (constrainToMesh)
//        {
//            ConstrainParticlesToMesh();
//        }
//    }

//    void ConstrainParticlesToMesh()
//    {
//        foreach (var particle in particles)
//        {
//            if (particle.isFixed) continue;

//            Vector3 worldPos = particle.transform.position;
//            if (!IsPointInsideMesh(worldPos))
//            {
//                // Push particle back to nearest surface point
//                Vector3 closestSurface = FindClosestSurfacePoint(worldPos);
//                Vector3 correction = closestSurface - worldPos;
//                particle.transform.position += correction * 0.1f; // Small correction to avoid jitter
//                particle.velocity -= Vector3.Project(particle.velocity, correction.normalized) * 0.5f;
//            }
//        }
//    }

//    Vector3 FindClosestSurfacePoint(Vector3 worldPoint)
//    {
//        // Simple implementation - projects point to nearest mesh surface
//        Vector3 directionToCenter = (transform.position - worldPoint).normalized;
//        Ray ray = new Ray(worldPoint + directionToCenter * meshBounds.size.magnitude * 0.5f, -directionToCenter);

//        RaycastHit hit;
//        if (Physics.Raycast(ray, out hit, meshBounds.size.magnitude))
//        {
//            if (hit.collider.gameObject == gameObject)
//            {
//                return hit.point;
//            }
//        }

//        // Fallback: return the point projected to the bounds
//        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
//        Vector3 closestBoundsPoint = meshBounds.ClosestPoint(localPoint);
//        return transform.TransformPoint(closestBoundsPoint);
//    }

//    // Helper class for spatial partitioning
//    private class SpatialHash
//    {
//        private Dictionary<Vector3Int, List<SpringPoint>> cells = new Dictionary<Vector3Int, List<SpringPoint>>();
//        private float cellSize;

//        public SpatialHash(float cellSize)
//        {
//            this.cellSize = cellSize;
//        }

//        public void Add(Vector3 position, SpringPoint particle)
//        {
//            Vector3Int cell = new Vector3Int(
//                Mathf.FloorToInt(position.x / cellSize),
//                Mathf.FloorToInt(position.y / cellSize),
//                Mathf.FloorToInt(position.z / cellSize)
//            );

//            if (!cells.ContainsKey(cell))
//            {
//                cells[cell] = new List<SpringPoint>();
//            }
//            cells[cell].Add(particle);
//        }

//        public List<SpringPoint> Query(Vector3 position, float radius)
//        {
//            List<SpringPoint> results = new List<SpringPoint>();
//            int cellsToCheck = Mathf.CeilToInt(radius / cellSize);

//            Vector3Int centerCell = new Vector3Int(
//                Mathf.FloorToInt(position.x / cellSize),
//                Mathf.FloorToInt(position.y / cellSize),
//                Mathf.FloorToInt(position.z / cellSize)
//            );

//            for (int x = -cellsToCheck; x <= cellsToCheck; x++)
//            {
//                for (int y = -cellsToCheck; y <= cellsToCheck; y++)
//                {
//                    for (int z = -cellsToCheck; z <= cellsToCheck; z++)
//                    {
//                        Vector3Int cell = centerCell + new Vector3Int(x, y, z);
//                        if (cells.TryGetValue(cell, out List<SpringPoint> cellParticles))
//                        {
//                            results.AddRange(cellParticles);
//                        }
//                    }
//                }
//            }

//            return results;
//        }
//    }
//}