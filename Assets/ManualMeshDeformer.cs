using UnityEngine;

public class ManualMeshDeformer : MonoBehaviour
{
    
    public GameObject otherCube;
    public float deformationAmount = 0.1f;

    void Update()
    {
        Bounds myBounds = GetComponent<Renderer>().bounds;
        Bounds otherBounds = otherCube.GetComponent<Renderer>().bounds;

        if (CheckAABBCollision(myBounds, otherBounds))
        {
            Bounds intersection = GetIntersectionBounds(myBounds, otherBounds);
            DeformMeshIfIntersecting(intersection);
        }
    }

    bool CheckAABBCollision(Bounds a, Bounds b)
    {
        return a.min.x <= b.max.x && a.max.x >= b.min.x &&
               a.min.y <= b.max.y && a.max.y >= b.min.y &&
               a.min.z <= b.max.z && a.max.z >= b.min.z;
    }

    Bounds GetIntersectionBounds(Bounds a, Bounds b)
    {
        Vector3 min = Vector3.Max(a.min, b.min);
        Vector3 max = Vector3.Min(a.max, b.max);
        return new Bounds((min + max) / 2, max - min);
    }

    void DeformMeshIfIntersecting(Bounds intersection)
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        Mesh mesh = meshFilter.mesh;

        Vector3[] originalVertices = mesh.vertices;
        Vector3[] deformedVertices = new Vector3[originalVertices.Length];

        Transform t = transform;

        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 worldPos = t.TransformPoint(originalVertices[i]);

            if (intersection.Contains(worldPos))
            {
                // Push vertex inward toward cube center
                Vector3 direction = (worldPos - intersection.center).normalized;
                worldPos -= direction * deformationAmount;
                deformedVertices[i] = t.InverseTransformPoint(worldPos);
            }
            else
            {
                deformedVertices[i] = originalVertices[i];
            }
        }

        mesh.vertices = deformedVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
    
 /*   public Vector3 velocity;
    public float radius = 0.5f;
    public float mass = 1f;

    public ManualMeshDeformer otherSphere; // Assign in Inspector

    public float restitution = 0.9f; // Bounciness
    public float deformationScale = 0.2f;
    public float recoverySpeed = 4f;

    private Mesh mesh;
    private Vector3[] baseVertices;
    private Vector3[] deformedVertices;

    void Start()
    {
        // Make sure each sphere has its own mesh
        mesh = GetComponent<MeshFilter>().mesh = Instantiate(GetComponent<MeshFilter>().mesh);
        baseVertices = mesh.vertices;
        deformedVertices = new Vector3[baseVertices.Length];
        baseVertices.CopyTo(deformedVertices, 0);
    }

    void Update()
    {
        // Move the sphere
        transform.position += velocity * Time.deltaTime;

        // Check for collision with the other sphere
        if (otherSphere != null)
        {
            Vector3 dir = otherSphere.transform.position - transform.position;
            float dist = dir.magnitude;
            float minDist = radius + otherSphere.radius;

            if (dist < minDist)
            {
                // Normalize direction
                Vector3 normal = dir.normalized;

                // Simple elastic collision response
                Vector3 relativeVelocity = velocity - otherSphere.velocity;
                float impulse = (-(1 + restitution) * Vector3.Dot(relativeVelocity, normal)) / (1 / mass + 1 / otherSphere.mass);

                Vector3 impulseVec = impulse * normal;
                velocity += impulseVec / mass;
                otherSphere.velocity -= impulseVec / otherSphere.mass;

                // Apply deformation
                ApplyDeformation(normal);
                otherSphere.ApplyDeformation(-normal);
            }
        }

        // Recover shape
        for (int i = 0; i < deformedVertices.Length; i++)
        {
            deformedVertices[i] = Vector3.Lerp(deformedVertices[i], baseVertices[i], Time.deltaTime * recoverySpeed);
        }
        mesh.vertices = deformedVertices;
        mesh.RecalculateNormals();
    }

    void ApplyDeformation(Vector3 direction)
    {
        for (int i = 0; i < deformedVertices.Length; i++)
        {
            Vector3 worldPos = transform.TransformPoint(deformedVertices[i]);
            Vector3 localImpactDir = transform.InverseTransformDirection(direction);
            float influence = Mathf.Clamp01(1f - worldPos.magnitude / (radius * 2));
            deformedVertices[i] += localImpactDir * deformationScale * influence;
        }
    }
 */
}
