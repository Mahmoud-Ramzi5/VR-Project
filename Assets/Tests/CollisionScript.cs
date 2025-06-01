using UnityEngine;
using System.Collections.Generic;

public class CollisionScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    bool CheckCollision(Collider3D A, Collider3D B)
    {
        // Identify types and call correct test
        if (A is SphereCollider3D && B is SphereCollider3D)
            return SphereSphereIntersect((SphereCollider3D)A, (SphereCollider3D)B);
        if (A is SphereCollider3D && B is CubeCollider3D)
            return SphereCubeIntersect((SphereCollider3D)A, (CubeCollider3D)B);
        if (A is CubeCollider3D && B is SphereCollider3D)
        {
            bool coll = SphereCubeIntersect((SphereCollider3D)B, (CubeCollider3D)A);
            // we shoyld reverse normal direction
            return coll;
        }
        if (A is CubeCollider3D && B is CubeCollider3D)
            return BoxBoxIntersect((CubeCollider3D)A, (CubeCollider3D)B);
        //Todo:
        // sphere-cylinder, cylinder-cylinder, sphere-cone,......
        return false;
    }

    bool SphereSphereIntersect(SphereCollider3D A, SphereCollider3D B)
    {
        float r = A.radius + B.radius;
        Vector3 d = A.center - B.center;
        return d.sqrMagnitude <= r * r;
    }
    /*
     * To test a sphere against a cube
     * find the point on the box closest to the sphere’s center and compare that distance to the sphere radius.
     * One way is to transform the sphere center into the cube’s local coordinates (using the cube’s inverse rotation),
     * clamp each coordinate to the range [-halfExtents, +halfExtents], then transform back
    */
    bool SphereCubeIntersect(SphereCollider3D s, CubeCollider3D b)
    {
        // Transform sphere center into box local space
        Vector3 localCenter = Quaternion.Inverse(b.orientation) * (s.center - b.center);
        // Clamp to box extents
        Vector3 closestLocal = localCenter;
        closestLocal.x = Mathf.Clamp(closestLocal.x, -b.halfExtents.x, b.halfExtents.x);
        closestLocal.y = Mathf.Clamp(closestLocal.y, -b.halfExtents.y, b.halfExtents.y);
        closestLocal.z = Mathf.Clamp(closestLocal.z, -b.halfExtents.z, b.halfExtents.z);
        // Convert back to world space point on box
        Vector3 closestPoint = b.center + (b.orientation * closestLocal);
        // Check distance to sphere center
        Vector3 diff = closestPoint - s.center;
        return diff.sqrMagnitude <= s.radius * s.radius;
    }

    bool BoxBoxIntersect(CubeCollider3D A, CubeCollider3D B)
    {
        // Get the 3 local axes of each box in world space
        Vector3[] axesA = {
            A.orientation * Vector3.right,
            A.orientation * Vector3.up,
            A.orientation * Vector3.forward
        };

        Vector3[] axesB = {
            B.orientation * Vector3.right,
            B.orientation * Vector3.up,
            B.orientation * Vector3.forward
        };

        // 1) Test face normals of A and B
        for (int i = 0; i < 3; i++)
        {
            if (IsSeparatedOnAxis(A, B, axesA[i])) return false;
            if (IsSeparatedOnAxis(A, B, axesB[i])) return false;
        }

        // 2) Test cross products of edges
        foreach (var a in axesA)
        {
            foreach (var b in axesB)
            {
                Vector3 axis = Vector3.Cross(a, b);
                if (axis.sqrMagnitude < 0) continue; // skip near-zero axis
                axis.Normalize();
                if (IsSeparatedOnAxis(A, B, axis)) return false;
            }
        }

        // No separating axis found
        return true;
    }

    bool IsSeparatedOnAxis(CubeCollider3D A, CubeCollider3D B, Vector3 axis)
    {
        // Skip near-zero axes to avoid numerical issues
        if (axis.sqrMagnitude <= 0)
            return false;

        axis.Normalize();

        // Project both boxes onto the axis
        float projectionA = ProjectBoxOntoAxis(A, axis);
        float projectionB = ProjectBoxOntoAxis(B, axis);

        // Project the vector between centers onto the axis
        float centerDistance = Mathf.Abs(Vector3.Dot(B.center - A.center, axis));

        // If the center distance is greater than the sum of projected half-extents, they are separated
        return centerDistance > (projectionA + projectionB);
    }

    float ProjectBoxOntoAxis(CubeCollider3D box, Vector3 axis)
    {
        // Get the box's local axes in world space
        Vector3 right = box.orientation * Vector3.right;
        Vector3 up = box.orientation * Vector3.up;
        Vector3 forward = box.orientation * Vector3.forward;

        // Compute projection using the absolute dot product of axis with each local axis scaled by the half-extent
        return
            Mathf.Abs(Vector3.Dot(axis, right)) * box.halfExtents.x +
            Mathf.Abs(Vector3.Dot(axis, up)) * box.halfExtents.y +
            Mathf.Abs(Vector3.Dot(axis, forward)) * box.halfExtents.z;
    }

    bool SphereConeIntersect(SphereCollider3D s, ConeCollider3D c)
    {
        // Define cone axis in world space (e.g. local +Y axis)
        Vector3 axis = c.orientation * Vector3.up;
        Vector3 d = s.center - c.center; // assume c.center is apex for simplicity
        float y = Vector3.Dot(d, axis);
        if (y < 0)
        {
            // Check sphere against apex point
            return d.sqrMagnitude <= s.radius * s.radius;
        }
        if (y > c.height)
        {
            // Sphere past base plane; check distance to base circle center
            Vector3 baseCenter = c.center + axis * c.height;
            Vector3 proj = s.center - baseCenter;
            // project onto plane of base
            proj -= Vector3.Dot(proj, axis) * axis;
            return proj.magnitude <= (s.radius + c.baseRadius);
        }
        // Within height bounds: check cone surface
        float coneRadAtY = (y / c.height) * c.baseRadius;
        Vector3 perp = d - axis * y;
        float distPerp = perp.magnitude;
        return distPerp <= s.radius + coneRadAtY;
    }

    bool SphereCylinderIntersect(SphereCollider3D s, CylinderCollider3D c)
    {
        Vector3 d = s.center - c.center;
        float y = Vector3.Dot(d, c.orientation * Vector3.up); // assume local Y is axis
        float halfH = c.height * 0.5f;
        float clampedY = Mathf.Clamp(y, -halfH, halfH);
        Vector3 closest = c.center + (c.orientation * Vector3.up) * clampedY;
        float radialDist2 = (s.center - closest).sqrMagnitude;
        // Check against cylinder radius (side)
        if (radialDist2 <= (s.radius + c.radius) * (s.radius + c.radius))
            return true;
        // Otherwise check against caps if beyond height
        if (y > halfH)
        {
            Vector3 topCenter = c.center + (c.orientation * Vector3.up) * halfH;
            return (s.center - topCenter).sqrMagnitude <= s.radius * s.radius;
        }
        if (y < -halfH)
        {
            Vector3 bottomCenter = c.center + (c.orientation * Vector3.up) * -halfH;
            return (s.center - bottomCenter).sqrMagnitude <= s.radius * s.radius;
        }
        return false;
    }

}
