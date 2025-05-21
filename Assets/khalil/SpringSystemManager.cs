/*
using UnityEngine;
using System.Collections.Generic;

public class SpringSystemManager : MonoBehaviour
{
    public List<SpringPoint> allPoints = new List<SpringPoint>();
    public float timeStep = 0.01f;

    void FixedUpdate()
    {
        // Step 1: Reset and apply gravity
        foreach (var p in allPoints)
            p.AccumulateForces();

        // Step 2: Apply spring forces (once per connection)
        foreach (var p in allPoints)
        {
            foreach (var conn in p.connections)
            {
                SpringPoint q = conn.point;
                if (q == null || p.GetInstanceID() > q.GetInstanceID()) continue;

                Vector3 delta = q.transform.position - p.transform.position;
                float dist = delta.magnitude;
                if (dist < 0.001f) continue;

                Vector3 dir = delta.normalized;
                float stretch = dist - conn.restLength;

                // Hooke’s Law
                Vector3 springForce = conn.springConstant * stretch * dir;

                // Damping
                Vector3 relativeVelocity = q.velocity - p.velocity;
                Vector3 damping = conn.damperConstant * Vector3.Dot(relativeVelocity, dir) * dir;

                Vector3 totalForce = springForce + damping;

                if (!p.isFixed) p.acceleration += totalForce / p.mass;
                if (!q.isFixed) q.acceleration -= totalForce / q.mass;
            }
        }

        // Step 3: Integrate motion
        foreach (var p in allPoints)
            p.Integrate(timeStep);
    }
}
*/