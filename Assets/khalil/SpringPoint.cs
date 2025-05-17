using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Connection
{
    public SpringPoint point;
    public float springConstant;
    public float damperConstant;
    public float restLength;

    public Connection()
    {
        // Default values in constructor
        springConstant = 100f;
        damperConstant = 5f;
        restLength = 1f;
    }
}

public class SpringPoint : MonoBehaviour
{
    public List<Connection> connections;

    private Rigidbody rb;
    private LineRenderer lineRenderer;

    private void Start()
    {
        
        if (connections == null) connections = new List<Connection>();

        // Initialize any new connections added via Inspector
        foreach (var conn in connections)
        {
            if (conn.springConstant == 0) conn.springConstant = 10f;
            if (conn.damperConstant == 0) conn.damperConstant = 0.2f;
            if (conn.restLength == 0) conn.restLength = 1f;
        }
        rb = GetComponent<Rigidbody>();

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
        foreach (Connection connection in connections)
        {
            if (connection.point == null) continue;

            // Ensure each connection is processed only once
            if (this.GetInstanceID() >= connection.point.GetInstanceID())
            {
                continue;
            }

            Rigidbody otherRb = connection.point.GetComponent<Rigidbody>();
            rb.velocity = Vector3.zero;
            otherRb.velocity = Vector3.zero;
            Vector3 otherPosition = otherRb.position;
            Vector3 position = rb.position;

            Vector3 displacement = otherPosition - position;
            float currentDistance = displacement.magnitude;
            if (currentDistance < 0.001f) continue;

            Vector3 direction = displacement / currentDistance;

            // Spring force calculation
            float springForceMagnitude = connection.springConstant * (currentDistance - connection.restLength);

            // Damper force calculation
            Vector3 relativeVelocity = otherRb.velocity - rb.velocity;
            float dampingForceMagnitude = Vector3.Dot(relativeVelocity, direction) * connection.damperConstant;

            // Combined force
            float totalForce = springForceMagnitude + dampingForceMagnitude;

            // Force limiting
            float maxForce = 100f;
            totalForce = Mathf.Clamp(totalForce, -maxForce, maxForce);

            Vector3 forceVector = totalForce * direction;

            // Apply forces based on mass ratio
            //float massRatio = rb.mass / (rb.mass + otherRb.mass);
           float massRatio = rb.mass;
            rb.AddForce(forceVector * (1 - massRatio), ForceMode.Force);
            otherRb.AddForce(-forceVector * massRatio, ForceMode.Force);
        }
    }

    private void Update()
    {
        // Update LineRenderer positions
        lineRenderer.positionCount = connections.Count * 2;
        int index = 0;
        foreach (Connection conn in connections)
        {
            if (conn.point == null) continue;
            lineRenderer.SetPosition(index, transform.position);
            lineRenderer.SetPosition(index + 1, conn.point.transform.position);
            index += 2;
        }
    }
}