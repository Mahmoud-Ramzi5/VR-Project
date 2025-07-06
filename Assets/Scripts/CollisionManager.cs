using System.Collections.Generic;
using UnityEngine;

public class CollisionManager : MonoBehaviour
{
    public static List<OctreeSpringFiller> AllSoftBodies = new List<OctreeSpringFiller>();

    [Header("Inter-Object Collision Settings")]
    public bool enableInterObjectCollision = true;

    [Header("Collision Response")]
    [Range(0f, 1f)]
    public float coefficientOfRestitution = 0.6f;
    [Range(0f, 1f)]
    public float coefficientOfFriction = 0.4f; // Note: Friction not implemented in this GJK response
    public float penetrationCorrectionFactor = 0.6f; // How strongly to push objects apart (0-1)
    public float penetrationSlop = 0.01f; // A small allowance for penetration to prevent jitter

    // Collision statistics
    [Header("Debug Info")]
    public int totalCollisionsThisFrame = 0;
    public bool showCollisionGizmos = false;
    private List<CollisionInfo> lastFrameCollisions = new List<CollisionInfo>();


    /// <summary>
    /// This is now the primary collision resolution method called in FixedUpdate.
    /// </summary>
    public void ResolveInterObjectCollisions()
    {
        if (!enableInterObjectCollision) return;

        // Reset stats for the frame
        totalCollisionsThisFrame = 0;
        if (showCollisionGizmos)
        {
            lastFrameCollisions.Clear();
        }

        // --- The main collision detection loop ---
        for (int i = 0; i < AllSoftBodies.Count; i++)
        {
            OctreeSpringFiller obj1 = AllSoftBodies[i];
            if (!obj1.gameObject.activeInHierarchy || obj1.surfacePointsLocalSpace.Count == 0) continue;

            for (int j = i + 1; j < AllSoftBodies.Count; j++)
            {
                OctreeSpringFiller obj2 = AllSoftBodies[j];
                if (!obj2.gameObject.activeInHierarchy || obj2.surfacePointsLocalSpace.Count == 0) continue;

                // 1. BROAD-PHASE: AABB Check (Efficiently discard distant pairs)
                obj1.UpdateBoundingVolume();
                obj2.UpdateBoundingVolume();

                if (!obj1.boundingVolume.Intersects(obj2.boundingVolume)) continue;

                // 2. NARROW-PHASE: GJK Algorithm (Precise check for colliding pairs)
                if (GJK.DetectCollision(obj1, obj2, out CollisionInfo info))
                {
                    // 3. COLLISION RESPONSE: Apply physics based on GJK/EPA results
                    HandleGJKCollisionResponse(obj1, obj2, info);

                    totalCollisionsThisFrame++;
                    if (showCollisionGizmos)
                    {
                        lastFrameCollisions.Add(info);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles the physics response for two colliding objects using the data from GJK/EPA.
    /// </summary>
    private void HandleGJKCollisionResponse(OctreeSpringFiller obj1, OctreeSpringFiller obj2, CollisionInfo info)
    {
        // --- 1. Calculate Relative Velocity ---
        Vector3 avgVelocity1 = GetAverageVelocity(obj1);
        Vector3 avgVelocity2 = GetAverageVelocity(obj2);
        Vector3 relativeVelocity = avgVelocity1 - avgVelocity2;
        float velocityAlongNormal = Vector3.Dot(relativeVelocity, info.Normal);

        // Don't resolve if velocities are already separating
        if (velocityAlongNormal > 0) return;

        // --- 2. Calculate Impulse (Velocity Change) ---
        float totalMass1 = obj1.totalMass;
        float totalMass2 = obj2.totalMass;
        float invMass1 = (totalMass1 > 0) ? 1.0f / totalMass1 : 0.0f;
        float invMass2 = (totalMass2 > 0) ? 1.0f / totalMass2 : 0.0f;

        // Use the smaller coefficient of restitution
        float e = coefficientOfRestitution; // Simplified for now

        // Impulse scalar formula
        float j = -(1 + e) * velocityAlongNormal;
        j /= (invMass1 + invMass2);

        // Apply impulse to each object
        Vector3 impulse = j * info.Normal;
        Vector3 velocityChange1 = impulse * invMass1;
        Vector3 velocityChange2 = -impulse * invMass2;

        // Distribute velocity change across all points of the soft body
        foreach (var point in obj1.allSpringPoints)
        {
            point.velocity += velocityChange1;
        }
        foreach (var point in obj2.allSpringPoints)
        {
            point.velocity += velocityChange2;
        }

        // --- 3. Positional Correction (Resolve Penetration) ---
        Vector3 correction = Mathf.Max(info.Depth - penetrationSlop, 0.0f) / (invMass1 + invMass2) * penetrationCorrectionFactor * info.Normal;
        Vector3 posChange1 = correction * invMass1;
        Vector3 posChange2 = -correction * invMass2;

        // Distribute position change across all points
        foreach (var point in obj1.allSpringPoints)
        {
            point.position += posChange1;
        }
        foreach (var point in obj2.allSpringPoints)
        {
            point.position += posChange2;
        }
    }

    private Vector3 GetAverageVelocity(OctreeSpringFiller obj)
    {
        if (obj.allSpringPoints.Count == 0) return Vector3.zero;
        Vector3 totalVelocity = Vector3.zero;
        foreach (var p in obj.allSpringPoints)
        {
            totalVelocity += p.velocity;
        }
        return totalVelocity / obj.allSpringPoints.Count;
    }

    private void OnDrawGizmos()
    {
        if (!showCollisionGizmos || !Application.isPlaying || lastFrameCollisions == null)
            return;

        // Draw the collision normal for each detected collision this frame
        Gizmos.color = Color.red;
        foreach (var info in lastFrameCollisions)
        {
            // Find a representative center point for the gizmo
            Vector3 center = Vector3.Lerp(info.Normal * -info.Depth, Vector3.zero, 0.5f); // Simplified center
            Gizmos.DrawRay(center, info.Normal * 2.0f); // Draw the normal
            Gizmos.DrawWireSphere(center, 0.2f); // Mark the approximate collision spot
        }
    }
}