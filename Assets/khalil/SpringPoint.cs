using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Connection
{
    public SpringPoint point;
    public float springConstant = 2f;
    public float damperConstant = 0.05f;
    public float restLength = 1f;
    public Vector3 force;
}

public class SpringPoint : MonoBehaviour
{
    public List<Connection> connections;

    private Rigidbody rb;
    private LineRenderer lineRenderer;

    // new 
    public Vector3 Gravity => new Vector3(0, -9.81f, 0);
    public bool applyGravity = true;
    
    //


    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        foreach (Connection connection in connections)
        {
            connection.springConstant = 2f;
            connection.damperConstant = 0.05f;
            connection.restLength = 1f;
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

            // new

            // Reset force
            Vector3 weight = Vector3.zero;
            connection.force = Vector3.zero;
            // Apply gravity
            if (applyGravity)
                weight += rb.mass * Gravity.normalized;

            //

            Rigidbody otherRb = connection.point.GetComponent<Rigidbody>();
            Vector3 otherPosition = otherRb.position;
            Vector3 position = rb.position;

            // TEST
            Vector3 displacement = otherPosition - position;
            float currentDistance = displacement.magnitude;
            if (currentDistance < 0.001f) continue; // Prevent division by zero

            Vector3 direction = displacement / currentDistance;


            // new


            // SPRING FORCE: Hooke's Law: F = -k(x - L)
            Vector3 forceDir = displacement.normalized;
            Vector3 springForce = connection.springConstant * (currentDistance - connection.restLength) * forceDir;
            connection.force += springForce;


            // DAMPER FORCE (using velocity difference)
            Vector3 relativeVelocity = otherRb.linearVelocity - rb.linearVelocity;
            Vector3 dampingForce = connection.damperConstant * Vector3.Dot(relativeVelocity, forceDir) * forceDir;
            connection.force += dampingForce;

            //


            /*
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
            */


            rb.AddForce(weight);
            otherRb.AddForce(weight);

            rb.AddForce(connection.force);
            otherRb.AddForce(-connection.force);


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