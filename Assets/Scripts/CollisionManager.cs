using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class CollisionManager : MonoBehaviour
{

    public static List<OctreeSpringFiller> AllSoftBodies = new List<OctreeSpringFiller>();

    [Header("Inter-Object Collision Settings")]
    public bool enableInterObjectCollision = true;
    public float interObjectCollisionRadius = 0.8f;
    public LayerMask collisionLayers = -1;

    [Header("Collision Response Types")]
    [Range(0f, 1f)]
    public float coefficientOfRestitution = 0.6f; // 0 = perfectly inelastic, 1 = perfectly elastic
    [Range(0f, 1f)]
    public float coefficientOfFriction = 0.4f;
    public bool useElasticCollision = true;
    public bool useInelasticCollision = false;
    public bool useMaterialProperties = true;

    [Header("Advanced Collision Settings")]
    public float penetrationCorrection = 0.8f;
    public float velocityThreshold = 0.1f; // Minimum velocity for collision response
    public float separationForce = 150f;
    public float dampingFactor = 0.98f;

    [Header("Performance")]
    public bool useSpatialHashing = true;
    public float spatialHashCellSize = 2f;

    private OctreeSpringFiller octreeSpringFiller;
    private List<SpringPoint> allSpringPoints;
    private MaterialManager materialManager;

    // Collision statistics
    [Header("Debug Info")]
    public int totalCollisionsThisFrame = 0;
    public bool showCollisionGizmos = false;
    public bool verboseDebugLog = false;
    private List<Vector3> collisionPoints = new List<Vector3>();

    // Track processed collisions to avoid duplicates
    private HashSet<(SpringPoint, SpringPoint)> processedCollisions = new HashSet<(SpringPoint, SpringPoint)>();


    public void ResolveInterObjectCollisions()
    {
        if (!enableInterObjectCollision) return;

        totalCollisionsThisFrame = 0;
        collisionPoints.Clear();
        processedCollisions.Clear();

        for (int i = 0; i < AllSoftBodies.Count; i++)
        {
            OctreeSpringFiller obj1 = AllSoftBodies[i];
            if (!obj1.gameObject.activeInHierarchy || obj1.allSpringPoints == null) continue;

            for (int j = i + 1; j < AllSoftBodies.Count; j++)
            {
                OctreeSpringFiller obj2 = AllSoftBodies[j];
                if (!obj2.gameObject.activeInHierarchy || obj2.allSpringPoints == null) continue;

                // Broad-phase: bounding volume check
                obj1.UpdateBoundingVolume();
                obj2.UpdateBoundingVolume();

                if (!obj1.boundingVolume.Intersects(obj2.boundingVolume)) continue;

                // Narrow-phase: spring point collisions (surface points only)
                foreach (SpringPoint p1 in obj1.SurfacePoints)
                {
                    foreach (SpringPoint p2 in obj2.SurfacePoints)
                    {
                        var collisionPair = (p1, p2);
                        var reversePair = (p2, p1);

                        if (processedCollisions.Contains(collisionPair) || processedCollisions.Contains(reversePair))
                            continue;

                        HandleSpringPointCollision(p1, p2, obj1, obj2);
                        processedCollisions.Add(collisionPair);
                    }
                }
            }
        }

        if (verboseDebugLog && totalCollisionsThisFrame > 0)
        {
            Debug.Log($"{gameObject.name}: Processed {totalCollisionsThisFrame} collisions this frame");
        }
    }


    // MAIN COLLISION HANDLING METHODS

    public void HandleInterObjectCollisions()
    {
        OctreeSpringFiller[] allSpringObjects = FindObjectsOfType<OctreeSpringFiller>();

        if (verboseDebugLog)
            Debug.Log($"Found {allSpringObjects.Length} spring objects in scene");

        // Check all pairs of objects (including self for internal collisions if needed)
        for (int i = 0; i < allSpringObjects.Length; i++)
        {
            for (int j = i + 1; j < allSpringObjects.Length; j++)
            {
                OctreeSpringFiller obj1 = allSpringObjects[i];
                OctreeSpringFiller obj2 = allSpringObjects[j];

                // Skip if either object has no spring points
                if (obj1.allSpringPoints == null || obj1.allSpringPoints.Count == 0 ||
                    obj2.allSpringPoints == null || obj2.allSpringPoints.Count == 0)
                    continue;

                float objectDistance = Vector3.Distance(obj1.transform.position, obj2.transform.position);
                float maxCollisionDistance = obj1.GetObjectRadius() + obj2.GetObjectRadius() + interObjectCollisionRadius * 2f;

                if (objectDistance < maxCollisionDistance)
                {
                    if (verboseDebugLog)
                        Debug.Log($"Checking collisions between {obj1.name} and {obj2.name}");

                    HandleCollisionBetweenTwoObjects(obj1, obj2);
                }
            }
        }
    }

    private void HandleCollisionBetweenTwoObjects(OctreeSpringFiller obj1, OctreeSpringFiller obj2)
    {
        foreach (SpringPoint point1 in obj1.SurfacePoints)
        {
            foreach (SpringPoint point2 in obj2.SurfacePoints)
            {
                // Check if we've already processed this collision pair
                var collisionPair = (point1, point2);
                var reverseCollisionPair = (point2, point1);

                if (processedCollisions.Contains(collisionPair) || processedCollisions.Contains(reverseCollisionPair))
                    continue;
                float distance = Vector3.Distance(point1.position, point2.position);
                float minDistance = point1.radius + point2.radius + interObjectCollisionRadius;
                Debug.Log($"Checking pair. Distance: {distance:F3}, MinDistance: {minDistance:F3} (p1.r={point1.radius}, p2.r={point2.radius}, inter.r={interObjectCollisionRadius})");
                HandleSpringPointCollision(point1, point2, obj1, obj2);
                processedCollisions.Add(collisionPair);
            }
        }
    }

    // CORE COLLISION PHYSICS

    private void HandleSpringPointCollision(SpringPoint point1, SpringPoint point2, OctreeSpringFiller obj1, OctreeSpringFiller obj2)
    {
        Vector3 direction = point1.position - point2.position;
        float distance = direction.magnitude;
        float minDistance = point1.radius + point2.radius + interObjectCollisionRadius;

        if (distance < minDistance && distance > 0.001f)
        {
            // Record collision for debugging
            totalCollisionsThisFrame++;
            if (showCollisionGizmos)
            {
                collisionPoints.Add((point1.position + point2.position) * 0.5f);
            }

            direction = direction.normalized;
            float penetration = minDistance - distance;

            // Skip if relative velocity is below threshold
            Vector3 relativeVelocity = point1.velocity - point2.velocity;
            if (relativeVelocity.magnitude < velocityThreshold && penetration < 0.01f)
                return;

            // Get material properties
            CollisionProperties? mat1 = GetMaterialProperties(obj1.GetComponent<MaterialManager>());
            CollisionProperties? mat2 = GetMaterialProperties(obj2.GetComponent<MaterialManager>());

            if (verboseDebugLog)
            {
                Debug.Log($"Collision detected: {obj1.name} <-> {obj2.name}, " +
                         $"distance: {distance:F3}, penetration: {penetration:F3}");
            }

            // Apply collision response based on type
            if (useElasticCollision)
            {
                HandleElasticCollision(point1, point2, direction, penetration, mat1, mat2);
            }
            else if (useInelasticCollision)
            {
                HandleInelasticCollision(point1, point2, direction, penetration, mat1, mat2);
            }
            else
            {
                // Mixed collision using coefficient of restitution
                HandleMixedCollision(point1, point2, direction, penetration, mat1, mat2);
            }

            if (penetration > 0.05f)
            {
                // Backtrack both points halfway along normal
                point1.position += direction * (penetration * 0.5f);
                point2.position -= direction * (penetration * 0.5f);
            }

            // Apply separation forces
            ApplySeparationForces(point1, point2, direction, penetration);
        }
    }

    // ELASTIC COLLISION - Conserves kinetic energy
    private void HandleElasticCollision(SpringPoint point1, SpringPoint point2, Vector3 normal, float penetration,
        CollisionProperties? mat1, CollisionProperties? mat2)
    {
        // Separate objects
        SeparateObjects(point1, point2, normal, penetration);

        Vector3 relativeVelocity = point1.velocity - point2.velocity;
        float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

        if (velocityAlongNormal > 0) return; // Objects separating

        // Calculate collision impulse for elastic collision (e = 1)
        float restitution = 1.0f;
        if (useMaterialProperties && mat1.HasValue && mat2.HasValue)
        {
            restitution = (mat1.Value.bounciness + mat2.Value.bounciness) * 0.5f;
        }

        float impulse = -(1 + restitution) * velocityAlongNormal;
        impulse /= (1f / point1.mass + 1f / point2.mass);

        Vector3 impulseVector = impulse * normal;

        // Apply velocity changes
        point1.velocity += impulseVector / point1.mass;
        point2.velocity -= impulseVector / point2.mass;

        // Apply friction
        ApplyFriction(point1, point2, normal, relativeVelocity, impulse, mat1, mat2);

        if (verboseDebugLog)
        {
            Debug.Log($"Elastic Collision: Restitution = {restitution}, Impulse = {impulse}");
        }
    }

    // INELASTIC COLLISION - Does not conserve kinetic energy
    private void HandleInelasticCollision(SpringPoint point1, SpringPoint point2, Vector3 normal, float penetration,
        CollisionProperties? mat1, CollisionProperties? mat2)
    {
        // Separate objects
        SeparateObjects(point1, point2, normal, penetration);

        Vector3 relativeVelocity = point1.velocity - point2.velocity;
        float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

        if (velocityAlongNormal > 0) return; // Objects separating

        // For perfectly inelastic collision, objects stick together (e = 0)
        float restitution = 0f;
        float impulse = -(1 + restitution) * velocityAlongNormal;
        impulse /= (1f / point1.mass + 1f / point2.mass);

        Vector3 impulseVector = impulse * normal;

        // Apply velocity changes
        point1.velocity += impulseVector / point1.mass;
        point2.velocity -= impulseVector / point2.mass;

        // Increased friction for inelastic collisions
        float frictionCoeff = coefficientOfFriction * 1.5f;
        if (useMaterialProperties && mat1.HasValue && mat2.HasValue)
        {
            frictionCoeff = (mat1.Value.friction + mat2.Value.friction) * 0.7f;
        }

        ApplyFriction(point1, point2, normal, relativeVelocity, impulse, mat1, mat2, frictionCoeff);

        // Additional energy dissipation
        point1.velocity *= 0.9f;
        point2.velocity *= 0.9f;

        if (verboseDebugLog)
        {
            Debug.Log($"Inelastic Collision: Energy dissipated, Impulse = {impulse}");
        }
    }

    // MIXED COLLISION - Uses coefficient of restitution
    private void HandleMixedCollision(SpringPoint point1, SpringPoint point2, Vector3 normal, float penetration,
        CollisionProperties? mat1, CollisionProperties? mat2)
    {
        // Separate objects
        SeparateObjects(point1, point2, normal, penetration);

        Vector3 relativeVelocity = point1.velocity - point2.velocity;
        float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

        if (velocityAlongNormal > 0) return; // Objects separating

        // Use coefficient of restitution or material properties
        float restitution = coefficientOfRestitution;
        if (useMaterialProperties && mat1.HasValue && mat2.HasValue)
        {
            restitution = Mathf.Sqrt(mat1.Value.bounciness * mat2.Value.bounciness);
        }

        float impulse = -(1 + restitution) * velocityAlongNormal;
        impulse /= (1f / point1.mass + 1f / point2.mass);

        Vector3 impulseVector = impulse * normal;

        // Apply velocity changes
        point1.velocity += impulseVector / point1.mass;
        point2.velocity -= impulseVector / point2.mass;

        // Apply friction
        ApplyFriction(point1, point2, normal, relativeVelocity, impulse, mat1, mat2);

        if (verboseDebugLog)
        {
            Debug.Log($"Mixed Collision: e = {restitution}, Impulse = {impulse}");
        }
    }

    // HELPER METHODS

    private void SeparateObjects(SpringPoint point1, SpringPoint point2, Vector3 normal, float penetration)
    {
        float totalMass = point1.mass + point2.mass;
        float point1Factor = point2.mass / totalMass;
        float point2Factor = point1.mass / totalMass;

        Vector3 separation = normal * penetration * penetrationCorrection;
        point1.position += separation * point1Factor;
        point2.position -= separation * point2Factor;
    }

    private void ApplyFriction(SpringPoint point1, SpringPoint point2, Vector3 normal, Vector3 relativeVelocity,
        float normalImpulse, CollisionProperties? mat1, CollisionProperties? mat2, float frictionOverride = -1f)
    {
        Vector3 tangent = relativeVelocity - Vector3.Dot(relativeVelocity, normal) * normal;

        if (tangent.magnitude < 0.001f) return;

        tangent = tangent.normalized;

        float frictionCoeff;
        if (frictionOverride >= 0f)
        {
            frictionCoeff = frictionOverride;
        }
        else if (useMaterialProperties && mat1.HasValue && mat2.HasValue)
        {
            frictionCoeff = Mathf.Sqrt(mat1.Value.friction * mat2.Value.friction);
        }
        else
        {
            frictionCoeff = coefficientOfFriction;
        }

        float frictionImpulse = -Vector3.Dot(relativeVelocity, tangent);
        frictionImpulse /= (1f / point1.mass + 1f / point2.mass);

        // Coulomb friction model
        float maxFrictionImpulse = Mathf.Abs(normalImpulse) * frictionCoeff;
        frictionImpulse = Mathf.Clamp(frictionImpulse, -maxFrictionImpulse, maxFrictionImpulse);

        Vector3 frictionVector = frictionImpulse * tangent;
        point1.velocity += frictionVector / point1.mass;
        point2.velocity -= frictionVector / point2.mass;
    }

    private void ApplySeparationForces(SpringPoint point1, SpringPoint point2, Vector3 normal, float penetration)
    {
        float totalMass = point1.mass + point2.mass;
        float point1Factor = point2.mass / totalMass;
        float point2Factor = point1.mass / totalMass;

        Vector3 separationForceVector = normal * (penetration * separationForce);
        point1.force += separationForceVector * point1Factor;
        point2.force -= separationForceVector * point2Factor;

        // Apply damping to reduce oscillations
        point1.velocity *= dampingFactor;
        point2.velocity *= dampingFactor;
    }

    private CollisionProperties? GetMaterialProperties(MaterialManager matManager)
    {
        if (matManager != null && useMaterialProperties)
        {
            var preset = matManager.GetMaterialProperties();
            if (preset != null)
            {
                return new CollisionProperties
                {
                    bounciness = preset.bounciness,
                    friction = preset.friction
                };
            }
        }

        // Return default properties wrapped in nullable
        return new CollisionProperties
        {
            bounciness = coefficientOfRestitution,
            friction = coefficientOfFriction
        };
    }

    private float GetObjectRadius()
    {
        if (octreeSpringFiller != null)
            return octreeSpringFiller.GetObjectRadius();
        return 1f; // Default fallback
    }

    // COLLISION TYPES ENUM AND STRUCT

    [System.Serializable]
    public struct CollisionProperties
    {
        public float bounciness;
        public float friction;
    }

    // DEBUGGING AND VISUALIZATION

    private void OnDrawGizmos()
    {
        if (!showCollisionGizmos || !Application.isPlaying)
            return;

        // Draw collision points
        Gizmos.color = Color.red;
        foreach (Vector3 point in collisionPoints)
        {
            Gizmos.DrawWireSphere(point, 0.1f);
        }

        // Draw collision radius
        if (octreeSpringFiller != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, GetObjectRadius() + interObjectCollisionRadius);
        }

        // Draw connections to nearby objects
        if (verboseDebugLog)
        {
            Gizmos.color = Color.green;
            OctreeSpringFiller[] allObjects = FindObjectsOfType<OctreeSpringFiller>();
            foreach (var other in allObjects)
            {
                if (other != octreeSpringFiller)
                {
                    float dist = Vector3.Distance(transform.position, other.transform.position);
                    float maxDist = GetObjectRadius() + other.GetObjectRadius() + interObjectCollisionRadius * 2f;
                    if (dist < maxDist)
                    {
                        Gizmos.DrawLine(transform.position, other.transform.position);
                    }
                }
            }
        }
    }

    private void OnGUI()
    {
        if (showCollisionGizmos)
        {
            GUI.Label(new Rect(10, 10, 300, 20), $"Collisions This Frame: {totalCollisionsThisFrame}");
            GUI.Label(new Rect(10, 30, 300, 20), $"Collision Type: {GetCurrentCollisionType()}");
            GUI.Label(new Rect(10, 50, 300, 20), $"Restitution: {coefficientOfRestitution:F2}");
            GUI.Label(new Rect(10, 70, 300, 20), $"Friction: {coefficientOfFriction:F2}");
            GUI.Label(new Rect(10, 90, 300, 20), $"Spring Points: {allSpringPoints?.Count ?? 0}");
        }
    }

    private string GetCurrentCollisionType()
    {
        if (useElasticCollision) return "Elastic";
        if (useInelasticCollision) return "Inelastic";
        return "Mixed";
    }
}