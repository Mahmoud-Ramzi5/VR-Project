using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Connection
{
    public SpringPoint point;
    public float springConstant = 10f;
    public float damperConstant = 0.5f;
    public float restLength = 1f;
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

    private void Start()
    {
        allParticles.Add(this);

        foreach (Connection connection in connections)
        {
            if (connection.point != null)
                connection.restLength = Vector3.Distance(transform.position, connection.point.transform.position);
        }

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = Color.white;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
    }

    private void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;
        if (isFixed) return;

        Vector3 netForce = applyGravity ? gravity * mass : Vector3.zero;

        foreach (Connection connection in connections)
        {
            if (connection.point == null) continue;

            Vector3 dir = connection.point.transform.position - transform.position;
            float dist = dir.magnitude;
            if (dist < 0.001f) continue;

            Vector3 norm = dir / dist;
            float stretch = dist - connection.restLength;

            // Hooke's Law
            Vector3 springForce = connection.springConstant * stretch * norm;

            // Damping
            Vector3 relativeVelocity = connection.point.velocity - velocity;
            Vector3 dampingForce = connection.damperConstant * Vector3.Dot(relativeVelocity, norm) * norm;

            Vector3 totalForce = springForce + dampingForce;

            // Apply equal and opposite forces
            if (!connection.point.isFixed)
                connection.point.acceleration -= (totalForce) / connection.point.mass;

            netForce += totalForce;
        }

        // Apply force to this point
        acceleration = netForce / mass;

        // Semi-implicit Euler integration
        velocity += acceleration * deltaTime;
        transform.position += velocity * deltaTime;

        HandleCollisions();
        HandleBoundaryBox();
    }

    private void HandleCollisions()
    {
        foreach (SpringPoint other in allParticles)
        {
            if (other == this || other == null) continue;

            Vector3 delta = other.transform.position - transform.position;
            float dist = delta.magnitude;
            float minDist = radius + other.radius;

            if (dist < minDist && dist > 0.001f)
            {
                Vector3 normal = delta.normalized;
                float penetration = minDist - dist;
                Vector3 correction = normal * (10);

                if (!isFixed)
                    transform.position -= correction;
                if (!other.isFixed)
                    other.transform.position += correction;

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
}
