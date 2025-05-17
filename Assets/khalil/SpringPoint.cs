using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Connection
{
    public SpringPoint point;
    public float springConstant = 100f;
    public float damperConstant = 5f;
    public float restLength = 1f;
}

public class SpringPoint : MonoBehaviour
{
    public List<Connection> connections;

    private Rigidbody rb;
    private LineRenderer lineRenderer;

    private void Start()
    {
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

            Rigidbody otherRb = connection.point.GetComponent<Rigidbody>();
            Vector3 otherPosition = otherRb.position;
            Vector3 position = rb.position;

            Vector3 displacement = otherPosition - position;
            float currentDistance = displacement.magnitude;
            if (currentDistance < 0.001f) continue; // Prevent division by zero

            Vector3 direction = displacement / currentDistance;

            // SPRING FORCE
            float springForceMagnitude = connection.springConstant * (currentDistance - connection.restLength);

            // DAMPER FORCE (using velocity difference)
            Vector3 relativeVelocity = otherRb.linearVelocity - rb.linearVelocity;
            float dampingForceMagnitude = Vector3.Dot(relativeVelocity, direction) * connection.damperConstant;

            // COMBINED FORCE
            float totalForce = springForceMagnitude + dampingForceMagnitude;

            // FORCE LIMITING (critical fix)
            float maxForce = 100f; // Adjust based on your scale
            totalForce = Mathf.Clamp(totalForce, -maxForce, maxForce);

            Vector3 forceVector = totalForce * direction;

            // Apply forces proportionally based on mass
            float massRatio = rb.mass / (rb.mass + otherRb.mass);
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