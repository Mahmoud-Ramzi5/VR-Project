using UnityEngine;
using System.Collections.Generic;


[System.Serializable]
public class Connection
{
    public SpringPoint point;
    public float springConstant = 10f;
    public float damperConstant = 0.5f;
    public float restLength = 1f;

    public Connection(SpringPoint point, float restLength, float springConstant, float damperConstant)
    {
        this.point = point;
        this.restLength = restLength;
        this.springConstant = springConstant;
        this.damperConstant = damperConstant;
    }
}

public class SpringPoint : MonoBehaviour
{
    public List<Connection> connections;
    private static List<SpringPoint> allParticles = new List<SpringPoint>();

    public float mass = 1f;
    public float radius = 0.5f;
    public Vector3 velocity;
    private Vector3 acceleration;
    public bool isFixed = false;

    public bool applyGravity = true;
    public Vector3 gravity => new Vector3(0, -9.81f, 0);

    [Header("Collision")]
    public float bounciness = 0.2f;
    public float friction = 0.1f;

    [Header("Bounds")]
    public Vector3 boundsMin = new Vector3(0, 0, 0);
    public Vector3 boundsMax = new Vector3(0, 0, 0);

    private LineRenderer lineRenderer;

    private void Awake()
    {
        // Initialize connections if null
        if (connections == null)
        {
            connections = new List<Connection>();
        }
    }

    private void Start()
    {
        allParticles.Add(this);

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = Color.white;
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;

        foreach (Connection connection in connections)
        {
            if (connection.point != null)
            {
                // Set the correct rest length based on the initial positions of the SpringPoints.
                //connection.restLength = Vector3.Distance(transform.position, connection.point.transform.position);
            }
        }
    }

    private void FixedUpdate()
    {
        if (isFixed) return;

        float deltaTime = Time.fixedDeltaTime;
        float fixedDt = Mathf.Min(Time.deltaTime, 0.02f); // 50Hz max
        Vector3 netForce = applyGravity ? gravity * mass : Vector3.zero;

        netForce += CalculateSpringForces();
        acceleration = netForce / mass;

        // Semi-implicit Euler integration
        velocity += acceleration * deltaTime;
        transform.position += velocity * deltaTime;

        if (velocity.magnitude < 0.001f)
            velocity = Vector3.zero;

        HandleCollisions();
        HandleBoundaryBox();
    }

    private void LateUpdate()
    {
        UpdateDynamicBounds();
    }

    private void Update()
    {
        if (lineRenderer == null) return;

        lineRenderer.positionCount = connections.Count * 2;

        int index = 0;
        foreach (Connection conn in connections)
        {
            if (conn.point == null) continue;
            lineRenderer.SetPosition(index++, transform.position);
            lineRenderer.SetPosition(index++, conn.point.transform.position);
        }
    }


    private Vector3 CalculateSpringForces()
    {
        Vector3 netForce = Vector3.zero;

        foreach (Connection connection in connections)
        {
            if (connection.point == null) continue;

            Vector3 direction = connection.point.transform.position - transform.position;
            float dist = direction.magnitude;

            // Calculate spring force using Hooke's Law
            float stretch = dist - connection.restLength;
            Vector3 springForce = connection.springConstant * stretch * direction.normalized;

            // Apply a reduction in spring force if the point is near the boundary
            float boundaryFactor = 1f;

            // Calculate how far the point is from the boundary
            for (int i = 0; i < 3; i++)
            {
                if (transform.position[i] - radius < boundsMin[i] || transform.position[i] + radius > boundsMax[i])
                {
                    // Reduce spring force near boundary
                    boundaryFactor = Mathf.Max(0.1f, boundaryFactor - 0.05f);
                }
            }

            // Scale the spring force by boundary factor
            springForce *= boundaryFactor;

            // Apply damping to prevent sliding at higher speeds
            float velocityAlongSpring = Vector3.Dot(velocity, direction.normalized);
            Vector3 dampingForce = connection.damperConstant * velocityAlongSpring * direction.normalized;
            
            // Combine forces
            netForce += springForce - dampingForce;
        }

        return netForce;
    }

    private void HandleCollisions()
    {
        foreach (SpringPoint other in allParticles)
        {
            if (other == this || other == null) continue;

            Vector3 delta = other.transform.position - transform.position;
            float dist = delta.magnitude;
            float minDist = radius + other.radius;

            if (dist < minDist)
            {
                Vector3 normal = delta.normalized;
                float penetration = minDist - dist;
                float totalInverseMass = (isFixed ? 0 : 1 / mass) + (other.isFixed ? 0 : 1 / other.mass);
                if (totalInverseMass == 0) continue;

                Vector3 correction = normal * (penetration / totalInverseMass);
                //Vector3 correction = normal * (penetration / totalInverseMass)*4;
                velocity *= (1 - friction);

                if (!isFixed)
                    transform.position -= correction * (1 / mass) / totalInverseMass;
                if (!other.isFixed)
                    other.transform.position += correction * (1 / other.mass) / totalInverseMass;

                Vector3 relVel = velocity - other.velocity;
                float velAlongNormal = Vector3.Dot(relVel, normal);
                if (velAlongNormal > 0) continue;

                float e = Mathf.Min(bounciness, other.bounciness);
                float j = -(1 + e) * velAlongNormal / (1 / mass + 1 / other.mass);
                Vector3 impulse = j * normal;

                if (!isFixed)
                    velocity += impulse / mass;
                if (!other.isFixed)
                    other.velocity -= impulse / other.mass;

                Vector3 tangent = (relVel - velAlongNormal * normal).normalized;
                float jt = -Vector3.Dot(relVel, tangent) / (1 / mass + 1 / other.mass);
                float mu = Mathf.Sqrt(friction * other.friction);
                Vector3 frictionImpulse = Mathf.Abs(jt) < j * mu ? jt * tangent : -j * mu * tangent;

                if (!isFixed)
                    velocity += frictionImpulse / mass;
                if (!other.isFixed)
                    other.velocity -= frictionImpulse / other.mass;
            }
        }
    }

    private void UpdateDynamicBounds()
    {
        if (transform != null)
        {
            Vector3 currentPos = transform.position;
            boundsMin = currentPos + new Vector3(-radius, -radius, -radius);
            boundsMax = currentPos + new Vector3(radius, radius, radius);
        }
    }

    public void HandleBoundaryBox()
    {
        if (transform != null)
        {
            Vector3 currentPos = transform.position;

            for (int i = 0; i < 3; i++)
            {
                if (currentPos[i] < boundsMin[i]) // Point is past the min boundary
                {
                    currentPos[i] = boundsMin[i];
                    velocity[i] = Mathf.Max(velocity[i], 0f); // Prevent moving further into the boundary
                    velocity *= (1f - friction);              // Apply friction to slow down sliding
                }
                else if (currentPos[i] > boundsMax[i]) // Point is past the max boundary
                {
                    currentPos[i] = boundsMax[i];
                    velocity[i] = Mathf.Min(velocity[i], 0f); // Prevent moving further out of the 
                    velocity *= (1f - friction);              // Apply friction to slow down sliding
                }
            }

            transform.position = currentPos;
        }
    }

    //private void HandleBoundaryBox()
    //{
    //    Vector3 pos = transform.position;

    //    for (int i = 0; i < 3; i++)
    //    {
    //        if (pos[i] - radius < boundsMin[i])
    //        {
    //            pos[i] = boundsMin[i] + radius;
    //            velocity[i] *= -bounciness;
    //            velocity *= (1f - friction);
    //        }
    //        else if (pos[i] + radius > boundsMax[i])
    //        {
    //            pos[i] = boundsMax[i] - radius;
    //            velocity[i] *= -bounciness;
    //            velocity *= (1f - friction);
    //        }
    //    }

    //    transform.position = pos;
    //}


    public void DrawBoundingBox()
    {
        Vector3 center = (boundsMin + boundsMax) * 0.5f;
        Vector3 size = boundsMax - boundsMin;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(center, size);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
    }

    private void OnDrawGizmos()
    {
        DrawBoundingBox();
    }

    private void OnDestroy()
    {
        allParticles.Remove(this);
    }


    // maintain shape against mesh boundaries
    [Header("Mesh Constraints")]
    //public float constraintStiffness = 50f;
    //public float constraintDamping = 5f;

    //public float maxAllowedDistance = 0.2f; // fallback for large violations

    //public void ConstrainToMesh(Mesh mesh, Transform meshTransform)
    //{
    //    if (mesh == null || meshTransform == null || isFixed) return;

    //    Vector3 localPos = meshTransform.InverseTransformPoint(transform.position);
    //    Vector3 closestSurfacePoint = FindClosestMeshPoint(mesh, meshTransform);
    //    Vector3 toSurface = closestSurfacePoint - transform.position;
    //    float distance = toSurface.magnitude;

    //    if (distance > 0.001f)
    //    {
    //        Vector3 direction = toSurface.normalized;

    //        // --------- 1. Physics-based spring constraint ---------
    //        float penetration = Mathf.Max(0f, radius - distance); // only push back if inside
    //        if (penetration > 0f)
    //        {
    //            Vector3 springForce = constraintStiffness * penetration * direction;
    //            Vector3 dampingForce = constraintDamping * Vector3.Dot(velocity, direction) * direction;
    //            velocity += (springForce - dampingForce) * Time.fixedDeltaTime / mass;
    //        }

    //        // --------- 2. Fallback: hard position snap if very far ---------
    //        if (distance > maxAllowedDistance)
    //        {
    //            transform.position = Vector3.Lerp(transform.position, closestSurfacePoint, 0.5f);
    //            velocity -= Vector3.Project(velocity, direction) * 0.5f;
    //        }
    //    }
    //}
    public float constraintStiffness = 500f;
    public float constraintDamping = 15f;
    public float surfaceOffset = 0.05f;  // Prevents surface z-fighting

    public void ConstrainToMesh(Mesh mesh, Transform meshTransform)
    {
        if (mesh == null || meshTransform == null) return;

        // 1. Find closest surface point
        Vector3 closestSurfacePoint = FindClosestMeshPoint(mesh, meshTransform);

        // 2. Calculate surface direction and distance
        Vector3 toSurface = closestSurfacePoint - transform.position;
        float distance = toSurface.magnitude;
        if (distance < 0.0001f) return;  // Avoid division by zero

        Vector3 direction = toSurface / distance;  // Normalized direction
        float penetration = radius + surfaceOffset - distance;

        // 3. Apply constraint only when penetrating
        if (penetration > 0)
        {
            // 4. Spring force (Hooke's Law)
            Vector3 springForce = constraintStiffness * penetration * direction;

            // 5. Velocity damping (directional friction)
            float velocityProjection = Vector3.Dot(velocity, direction);
            Vector3 dampingForce = constraintDamping * velocityProjection * direction;

            // 6. Apply final force (F = ma)
            Vector3 totalForce = springForce - dampingForce;
            velocity += totalForce * Time.fixedDeltaTime / mass;
        }
    }

    // 1. Vertex Approximate
    // Speed:?????
    // Accuracy:???
    // Low-poly meshes, mobile games
    private Vector3 FindClosestMeshPoint(Mesh mesh, Transform meshTransform)
    {
        // Convert position to mesh local space
        Vector3 localPos = meshTransform.InverseTransformPoint(transform.position);

        // Find closest vertex (fast approximation)
        Vector3 closestVertex = mesh.vertices[0];
        float minDistance = Vector3.Distance(localPos, closestVertex);

        foreach (Vector3 vertex in mesh.vertices)
        {
            float dist = Vector3.Distance(localPos, vertex);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestVertex = vertex;
            }
        }

        // Convert back to world space
        return meshTransform.TransformPoint(closestVertex);
    }

    // 2. Triangle Precise
    // Speed:??
    // Accuracy:?????
    // High-precision physics, PC/console
    //private Vector3 FindClosestMeshPoint(Mesh mesh, Transform meshTransform)
    //{
    //    Vector3 localPos = meshTransform.InverseTransformPoint(transform.position);
    //    Vector3 closestPoint = Vector3.zero;
    //    float closestDistance = Mathf.Infinity;

    //    // Iterate through all triangles
    //    int[] triangles = mesh.triangles;
    //    Vector3[] vertices = mesh.vertices;

    //    for (int i = 0; i < triangles.Length; i += 3)
    //    {
    //        Vector3 v1 = vertices[triangles[i]];
    //        Vector3 v2 = vertices[triangles[i + 1]];
    //        Vector3 v3 = vertices[triangles[i + 2]];

    //        // Find closest point on this triangle
    //        Vector3 triangleClosest = ClosestPointOnTriangle(v1, v2, v3, localPos);
    //        float dist = Vector3.Distance(localPos, triangleClosest);

    //        if (dist < closestDistance)
    //        {
    //            closestDistance = dist;
    //            closestPoint = triangleClosest;
    //        }
    //    }

    //    return meshTransform.TransformPoint(closestPoint);
    //}

    //// Helper: Finds closest point on a triangle
    //private Vector3 ClosestPointOnTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 p)
    //{
    //    // Check if point is in vertex region outside A
    //    Vector3 ab = b - a;
    //    Vector3 ac = c - a;
    //    Vector3 ap = p - a;

    //    float d1 = Vector3.Dot(ab, ap);
    //    float d2 = Vector3.Dot(ac, ap);
    //    if (d1 <= 0f && d2 <= 0f) return a;

    //    // Check if point is in vertex region outside B
    //    Vector3 bp = p - b;
    //    float d3 = Vector3.Dot(ab, bp);
    //    float d4 = Vector3.Dot(ac, bp);
    //    if (d3 >= 0f && d4 <= d3) return b;

    //    // Check if point is in vertex region outside C
    //    Vector3 cp = p - c;
    //    float d5 = Vector3.Dot(ab, cp);
    //    float d6 = Vector3.Dot(ac, cp);
    //    if (d6 >= 0f && d5 <= d6) return c;

    //    // Check if point is in edge region AB
    //    float vc = d1 * d4 - d3 * d2;
    //    if (vc <= 0f && d1 >= 0f && d3 <= 0f)
    //    {
    //        float v = d1 / (d1 - d3);
    //        return a + v * ab;
    //    }

    //    // Check if point is in edge region AC
    //    float vb = d5 * d2 - d1 * d6;
    //    if (vb <= 0f && d2 >= 0f && d6 <= 0f)
    //    {
    //        float w = d2 / (d2 - d6);
    //        return a + w * ac;
    //    }

    //    // Check if point is in edge region BC
    //    float va = d3 * d6 - d5 * d4;
    //    if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
    //    {
    //        float w = (d4 - d3) / (d4 - d3 + d5 - d6);
    //        return b + w * (c - b);
    //    }

    //    // Point is inside face region
    //    float denom = 1f / (va + vb + vc);
    //    float v2 = vb * denom;
    //    float w2 = vc * denom;
    //    return a + ab * v2 + ac * w2;
    //}

    // 3. Optimized Hybrid
    // Speed:????
    // Accuracy:????
    // General-purpose, dynamic objects
    // TODO:
}
