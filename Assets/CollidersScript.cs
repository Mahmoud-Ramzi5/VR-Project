using UnityEngine;


// Base type for any shape collider
abstract class Collider3D
{
    public Vector3 center;            // World-space center
    public Quaternion orientation;    // Rotation
    public MaterialProperties material;
    public float mass;
}


// Concrete shapes:
class SphereCollider3D : Collider3D
{
    public float radius;
}

class CubeCollider3D : Collider3D
{
    public Vector3 halfExtents; // äÕİ ÇáŞØÑ ãä ãÑßÒ ÇáãßÚÈ Çáì ÃÍÏ ÇáÑÄæÓ
}

class CylinderCollider3D : Collider3D
{
    public float radius;
    public float height;
}

class ConeCollider3D : Collider3D
{
    public float baseRadius;
    public float height;
}

