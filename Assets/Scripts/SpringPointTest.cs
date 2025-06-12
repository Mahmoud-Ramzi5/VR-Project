using UnityEngine;
using System.Collections.Generic;


[System.Serializable]
public class ConnectionTest
{
    public SpringPointTest point1, point2;
    public float springConstant = 100f;
    public float damperConstant = 0.5f;
    public float restLength = 1f;

    // For Debug
    private LineRenderer lineRenderer;

    public ConnectionTest(SpringPointTest point1, SpringPointTest point2, float restLength, float springConstant, float damperConstant)
    {
        this.point1 = point1;
        this.point2 = point2;
        this.restLength = restLength;
        this.springConstant = springConstant;
        this.damperConstant = damperConstant;
    }

    public void InitLineRenderer(Transform meshTransform)
    {
        var lineObj = new GameObject("Connection");
        lineObj.transform.SetParent(meshTransform);
        lineRenderer = lineObj.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
    }

    public void UpdateLinePos()
    {
        lineRenderer.SetPosition(0, point1.transform.position);
        lineRenderer.SetPosition(1, point2.transform.position);
    }


    public void CalculateAndApplyForces()
    {
        // --- NaN/Zero Distance Check ---
        if (point1 == null || point2 == null || point1.transform.position == point2.transform.position)
            return;
        Vector3 direction = point2.transform.position - point1.transform.position;
        float dist = direction.magnitude;

        if (dist == 0 || float.IsNaN(dist)) return;

        // Hooke's Law
        // Calculate spring force using Hooke's Law
        float stretch = dist - restLength;
        Vector3 springForce = springConstant * stretch * direction.normalized; // Normalized direction

        // Damping
        Vector3 relativeVelocity = point2.velocity - point1.velocity;
        // Apply damping to prevent sliding at higher speeds
        float velocityAlongSpring = Vector3.Dot(relativeVelocity, direction.normalized);
        Vector3 dampingForce = damperConstant * velocityAlongSpring * direction.normalized;

        // Apply forces
        Vector3 netForce = springForce + dampingForce;

        point1.force += netForce;
        point2.force -= netForce;
    }
}

public class SpringPointTest : MonoBehaviour
{
    public float mass = 1f;
    public float radius = 0.5f;

    public Vector3 force;
    public Vector3 velocity;
    public bool isFixed = false;

    [Header("Collision")]
    public float bounciness = 0.2f;
    public float friction = 0.1f;

    [Header("Bounds")]
    public Bounds nodeBounds;
    public Vector3 boundsMin = new Vector3(0, 0, 0);
    public Vector3 boundsMax = new Vector3(0, 0, 0);

    [Header("Mesh")]
    public bool isMeshVertex = false;
    public int triangleIndex;

    [HideInInspector] public Vector3 initialPosition;

    private void Start()
    {
        transform.SetParent(null); // Make independent
        initialPosition = transform.position;
    }

    private void FixedUpdate()
    {
        if (isFixed) return;

        // --- NaN/Origin Checks ---
        if (float.IsNaN(transform.position.x))
        {
            Debug.LogWarning($"NaN detected in position! Resetting point {name}.");
            transform.position = initialPosition;
            velocity = Vector3.zero;
            return;
        }

        // Prevent division by zero
        if (mass <= 0) mass = 0.001f;

        float deltaTime = Time.fixedDeltaTime;

        // --- Force/Velocity Validation ---
        if (!float.IsNaN(force.x))
        {
            Vector3 acceleration = force / mass;
            velocity += acceleration * deltaTime;

            // Clamp velocity to prevent explosions
            if (velocity.magnitude > 100f)
            {
                velocity = velocity.normalized * 100f;
            }
        }

        // --- Position Update ---
        Vector3 newPosition = transform.position + velocity * deltaTime;
        if (!float.IsNaN(newPosition.x) && newPosition.magnitude < 100000f)
        {
            transform.position = newPosition;
        }
        else
        {
            transform.position = initialPosition;
            velocity = Vector3.zero;
        }

        force = Vector3.zero;
    }

    private void HandleBoundaryBox()
    {
        Vector3 pos = transform.position;

        for (int i = 0; i < 3; i++)
        {
            if (pos[i] - radius < boundsMin[i])
            {
                pos[i] = boundsMin[i] + radius;
                velocity[i] *= -bounciness;
                velocity *= (1f - friction);
            }
            else if (pos[i] + radius > boundsMax[i])
            {
                pos[i] = boundsMax[i] - radius;
                velocity[i] *= -bounciness;
                velocity *= (1f - friction);
            }
        }

        transform.position = pos;
    }

    public void UpdateBounds(Vector3 moveStep)
    {
        Vector3 newCenter = nodeBounds.center + moveStep;
        nodeBounds = new Bounds(newCenter, nodeBounds.size);

        boundsMin = newCenter - nodeBounds.extents;
        boundsMax = newCenter + nodeBounds.extents;
        //Debug.Log($"boundsMin {boundsMin}, boundsMax {boundsMax}");
    }

    public void DrawBoundingBox()
    {
        Vector3 center = (boundsMin + boundsMax) * 0.5f;
        Vector3 size = boundsMax - boundsMin;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(center, size);
    }

    private void OnDrawGizmos()
    {
        DrawBoundingBox();
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube((boundsMin + boundsMax) * 0.5f, boundsMax - boundsMin);
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

        // --- NaN Check ---
        if (float.IsNaN(closestSurfacePoint.x))
        {
            Debug.LogWarning("Invalid mesh point detected! Using fallback.");
            closestSurfacePoint = initialPosition;
        }

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
    public Vector3 FindClosestMeshPoint(Mesh mesh, Transform meshTransform)
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