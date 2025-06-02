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
    public float radius = 20f;
    public Vector3 velocity;
    private Vector3 acceleration;
    public bool isFixed = false;

    public bool applyGravity = true;
    public Vector3 gravity => new Vector3(0, -9.81f, 0);

    [Header("Collision")]
    public float bounciness = 0.005f;
    public float friction = 2f;

    [Header("Bounds")]
    public Vector3 boundsMin = new Vector3(-5f, 0f, -5f);
    public Vector3 boundsMax = new Vector3(5f, 5f, 5f);

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
        foreach (SpringPoint particle in allParticles)
        {
            particle.radius = 0.3f;
        }

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
                connection.restLength = Vector3.Distance(transform.position, connection.point.transform.position);
            }
        }
    }

    private void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;
        if (isFixed) return;

        Vector3 netForce = applyGravity ? gravity * mass : Vector3.zero;

        //foreach (Connection connection in connections)
        //{
        //    if (connection.point == null) continue;

        //    Vector3 dir = connection.point.transform.position - transform.position;
        //    float dist = dir.magnitude;
        //    if (dist < 0.001f) continue;

        //    Vector3 norm = dir / dist;
        //    float stretch = dist - connection.restLength;

        //    // Hooke's Law
        //    //Vector3 springForce = connection.springConstant * stretch * norm;
        //    Vector3 springForce = connection.springConstant * Mathf.Clamp(stretch, -connection.restLength, connection.restLength) * norm;

        //    // Damping
        //    Vector3 relativeVelocity = connection.point.velocity - velocity;
        //    Vector3 dampingForce = connection.damperConstant * Vector3.Dot(relativeVelocity, norm) * norm;

        //    Vector3 totalForce = springForce + dampingForce;

        //    // Apply equal and opposite forces
        //    if (!connection.point.isFixed)
        //        connection.point.acceleration -= (totalForce) / connection.point.mass;

        //    netForce += totalForce;
        //}
        ApplySpringForces(connections);
        // Apply force to this point
        acceleration = netForce / mass;

        // Semi-implicit Euler integration
        velocity += acceleration * deltaTime;
        transform.position += velocity * deltaTime;
        MaintainCubeShape();
        HandleCollisions();
        HandleBoundaryBox();
    }


    private void ApplySpringForces(List<Connection> connections)
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
            Vector3 velocityDirection = velocity.normalized;
            float velocityAlongSpring = Vector3.Dot(velocity, direction.normalized);
            Vector3 dampingForce = connection.damperConstant * velocityAlongSpring * direction.normalized;

            // Combine forces
            netForce += springForce - dampingForce;
        }

        // Apply net force to the point (rest of the code for acceleration and velocity updates)
        acceleration = netForce / mass;
        velocity += acceleration * Time.fixedDeltaTime;
    }



    private void MaintainCubeShape()
    {
        foreach (SpringPoint other in allParticles)
        {
            if (other == this || other == null) continue;

            // Calculate the direction between the two particles
            Vector3 direction = other.transform.position - transform.position;
            float dist = direction.magnitude;
            float minDist = radius + other.radius;

            // Apply spring force to maintain the distance between connected points
            if (dist < minDist)
            {
                // If particles are too close, you can use a correction vector
                Vector3 correction = direction.normalized * (minDist - dist);

                // Apply corrective forces to maintain shape
                if (!isFixed)
                    transform.position += correction * 0.5f; // Apply half the correction
                if (!other.isFixed)
                    other.transform.position -= correction * 0.5f; // Apply the other half
            }
        }
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

                Vector3 correction = normal * (penetration / totalInverseMass)*4;
                velocity *= (1 - friction);

                //Vector3 correction = normal * (penetration / totalInverseMass);
                if (!isFixed)
                    transform.position -= correction * (1 / mass) / totalInverseMass;
                if (!other.isFixed)
                    other.transform.position += correction * (1 / other.mass) / totalInverseMass;

                Debug.Log("correction is"+correction);

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
    public void HandleInsideCollision()
    {
        foreach (SpringPoint other in allParticles)
        {
            if (other == this || other == null) continue;

            Vector3 delta = other.transform.position - transform.position;
            float dist = delta.sqrMagnitude;
            float minDist = radius + other.radius;
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

    public void HandleBoundaryBox()
    {
        Vector3 pos = transform.position;

        for (int i = 0; i < 3; i++)
        {
            if (pos[i] - radius < boundsMin[i]) // Point is past the min boundary
            {
                pos[i] = boundsMin[i] + radius;
                velocity[i] = Mathf.Max(velocity[i], 0f); // Prevent moving further into the boundary
                                                          // Apply friction to slow down sliding
                velocity *= (1f - friction);
            }
            else if (pos[i] + radius > boundsMax[i]) // Point is past the max boundary
            {
                pos[i] = boundsMax[i] - radius;
                velocity[i] = Mathf.Min(velocity[i], 0f); // Prevent moving further out of the boundary
                                                          // Apply friction to slow down sliding
                velocity *= (1f - friction);
            }
        }

        transform.position = pos;
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

    private void OnDestroy()
    {
        allParticles.Remove(this);
    }

    // Add this to maintain shape against mesh boundaries
    public void ConstrainToMesh(Mesh mesh, Transform meshTransform)
    {
        Vector3 localPos = meshTransform.InverseTransformPoint(transform.position);

        if (!mesh.bounds.Contains(localPos))
        {
            // Find closest point on mesh surface
            Vector3 closestSurfacePoint = FindClosestMeshPoint(mesh, meshTransform);
            Vector3 correction = closestSurfacePoint - transform.position;

            // Apply correction
            if (!isFixed)
            {
                transform.position += correction * 0.1f; // Small correction to avoid jitter
                velocity -= Vector3.Project(velocity, correction.normalized) * 0.5f;
            }
        }
    }

    private Vector3 FindClosestMeshPoint(Mesh mesh, Transform meshTransform)
    {
        // Simple implementation - for better results use raycasting or proper mesh queries
        Vector3 localPos = meshTransform.InverseTransformPoint(transform.position);
        Vector3 closest = mesh.vertices[0];
        float minDist = Vector3.Distance(localPos, closest);

        foreach (Vector3 vertex in mesh.vertices)
        {
            float dist = Vector3.Distance(localPos, vertex);
            if (dist < minDist)
            {
                minDist = dist;
                closest = vertex;
            }
        }

        return meshTransform.TransformPoint(closest);
    }
}
