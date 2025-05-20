using UnityEngine;
using System.Collections.Generic;
using NUnit.Framework;


[System.Serializable]
public class Connection
{
    public SpringPoint point;
    public float springConstant = 0.1f;
    public float damperConstant = 0.05f;
    public float restLength = 1f;
    public Vector3 force;
}

public class SpringPoint : MonoBehaviour
{
    public List<Connection> connections;
    private static List<SpringPoint> allParticles = new List<SpringPoint>();

    // no rigidbody
    public float mass = 1f;
    public float radius = 0.1f;
    public Vector3 velocity;
    public Vector3 acceleration;
    public bool isFixed = false;

    public Vector3 gravity => new Vector3(0, -9.81f, 0);
    public bool applyGravity = true;

    [Header("Collision")]
    public float bounciness = 0.005f;
    public float friction = 2f;

    [Header("Bounds")]
    public Vector3 boundsMin = new Vector3(-5f, 0f, -5f);
    public Vector3 boundsMax = new Vector3(5f, 5f, 5f);

    // only renderer
    private LineRenderer lineRenderer;


    private void Start()
    {
        allParticles.Add(this);

        // Initialize connections
        foreach (Connection connection in connections)
        {
            connection.springConstant = 2f;
            connection.damperConstant = 0.05f;
            /*
            if (connection.point != null)
            {
                // Optional: auto-set rest length based on current distance
                connection.restLength = Vector3.Distance(transform.position, connection.point.transform.position);
            }
            */
            // connection.restLength = 1f;
        }

        // Setup LineRenderer
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = Color.white;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
    }

    private void FixedUpdate()
    {
        // new
        float deltaTime = Time.fixedDeltaTime;

        foreach (Connection connection in connections)
        {
            if (connection.point == null) continue;

            // no rigidbody
            SpringPoint other_point = connection.point;

            if (isFixed || other_point.isFixed)
            {
                Debug.Log("isFixed");
                Debug.Log(isFixed);
                return;
            }

            // Reset force
            Vector3 weight = Vector3.zero;
            connection.force = Vector3.zero;

            // Apply gravity
            if (applyGravity)
            {
                float t_mass = mass + other_point.mass;
                float d_mass = t_mass / 2;

                weight += d_mass * gravity.normalized;
            }

            // Calculate important things
            Vector3 otherPosition = other_point.transform.position;
            Vector3 position = transform.position;

            Vector3 displacement = otherPosition - position;
            float currentDistance = displacement.magnitude;
            if (currentDistance < 0.001f) continue; // Prevent division by zero

            Vector3 direction = displacement.normalized;
            float stretch = currentDistance - connection.restLength;

            // SPRING FORCE: Hooke's Law: F = -k(x - L)
            Vector3 springForce = connection.springConstant * stretch * direction;
            connection.force += springForce;

            // DAMPER FORCE (using velocity difference)
            Vector3 relativeVelocity = other_point.velocity - velocity;
            Vector3 dampingForce = connection.damperConstant * Vector3.Dot(relativeVelocity, direction) * direction;
            connection.force += dampingForce;

            // Combine Forces
            Vector3 other_force = weight - connection.force;
            Vector3 force = weight + connection.force;

            // Integrate motion
            if (other_force.magnitude > 0.0001f)
            {
                other_point.acceleration = other_force / other_point.mass;
                other_point.velocity += other_point.acceleration * deltaTime;
                other_point.transform.position += other_point.velocity * deltaTime;
            }


            if (force.magnitude > 0.0001f)
            {
                acceleration = force / mass;
                velocity += acceleration * deltaTime;
                transform.position += velocity * deltaTime;
            }

            HandleCollisions();
            HandleBoundaryBox();
        }
    }

    private void HandleCollisions()
    {
        foreach (SpringPoint other in allParticles)
        {
            if (other == this) continue;

            Vector3 delta = other.transform.position - transform.position;
            float dist = delta.magnitude;
            float minDist = radius + other.radius;

            if (dist < minDist && dist > 0.001f)
            {
                Vector3 normal = delta.normalized;
                float penetration = minDist - dist;

                // Resolve overlap (push apart)
                Vector3 correction = normal * (penetration / 2);
                transform.position -= correction;
                other.transform.position += correction;

                // Relative velocity
                Vector3 relVel = velocity - other.velocity;
                float velAlongNormal = Vector3.Dot(relVel, normal);

                // Only resolve if objects are moving toward each other
                if (velAlongNormal > 0) continue;

                // Bounce
                float e = Mathf.Min(bounciness, other.bounciness);
                float j = -(1 + e) * velAlongNormal / (1 / mass + 1 / other.mass);
                Vector3 impulse = j * normal;

                velocity += impulse / mass;
                other.velocity -= impulse / other.mass;

                // Friction
                Vector3 tangent = (relVel - velAlongNormal * normal).normalized;
                float jt = -Vector3.Dot(relVel, tangent);
                jt /= (1 / mass + 1 / other.mass);

                float mu = Mathf.Sqrt(friction * other.friction);
                Vector3 frictionImpulse = Mathf.Abs(jt) < j * mu
                    ? jt * tangent
                    : -j * mu * tangent;

                velocity += frictionImpulse / mass;
                other.velocity -= frictionImpulse / other.mass;
            }
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

        // Update LineRenderer positions
        lineRenderer.positionCount = connections.Count * 2;
        int index = 0;
        foreach (Connection conn in connections)
        {
            if (conn.point == null) continue;
            lineRenderer.SetPosition(index++, transform.position);
            lineRenderer.SetPosition(index++, conn.point.transform.position);
            //index += 2;
        }
    }

    private void OnDestroy()
    {
        allParticles.Remove(this);
    }
}