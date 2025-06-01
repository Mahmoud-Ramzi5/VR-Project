using UnityEngine;

public class ManualRubberSphere : MonoBehaviour
{
    public Vector3 velocity;
    public float radius = 0.5f;
    public float restitution = 0.8f;
    public float deformationScale = 0.15f;
    public float recoverySpeed = 4f;

    public Transform cubeTarget; // Assign the cube in the Inspector

    private Mesh mesh;
    private Vector3[] baseVertices;
    private Vector3[] deformedVertices;

    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh = Instantiate(GetComponent<MeshFilter>().mesh);
        baseVertices = mesh.vertices;
        deformedVertices = new Vector3[baseVertices.Length];
        baseVertices.CopyTo(deformedVertices, 0);
    }

    void Update()
    {
        // Move manually
        transform.position += velocity * Time.deltaTime;

        // Check for collision with cube
        if (cubeTarget != null)
        {
            Bounds cubeBounds = cubeTarget.GetComponent<Renderer>().bounds;
            Vector3 closestPoint = ClosestPointOnBounds(cubeBounds, transform.position);

            float distance = Vector3.Distance(transform.position, closestPoint);
            if (distance < radius)
            {
                // Collision occurred
                Vector3 collisionNormal = (transform.position - closestPoint).normalized;

                // Reflect velocity manually
                velocity = Vector3.Reflect(velocity, collisionNormal) * restitution;

                // Deform the sphere on contact
                ApplyDeformation(collisionNormal);
            }
        }

        // Smoothly recover shape
        for (int i = 0; i < deformedVertices.Length; i++)
        {
            deformedVertices[i] = Vector3.Lerp(deformedVertices[i], baseVertices[i], Time.deltaTime * recoverySpeed);
        }

        mesh.vertices = deformedVertices;
        mesh.RecalculateNormals();
    }

    Vector3 ClosestPointOnBounds(Bounds bounds, Vector3 point)
    {
        return new Vector3(
            Mathf.Clamp(point.x, bounds.min.x, bounds.max.x),
            Mathf.Clamp(point.y, bounds.min.y, bounds.max.y),
            Mathf.Clamp(point.z, bounds.min.z, bounds.max.z)
        );
    }

    void ApplyDeformation(Vector3 direction)
    {
        for (int i = 0; i < deformedVertices.Length; i++)
        {
            Vector3 worldPos = transform.TransformPoint(deformedVertices[i]);
            float influence = Mathf.Clamp01(1f - Vector3.Distance(worldPos, transform.position) / radius);
            Vector3 localDir = transform.InverseTransformDirection(direction);
            deformedVertices[i] += localDir * deformationScale * influence;
        }
    }
}
