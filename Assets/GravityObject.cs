using UnityEngine;

public class GravityObject : MonoBehaviour
{
    public float verticalVelocity = 0f;

    private MaterialManager materialManager;
    private MaterialProperties objectMaterial;

    void Start()
    {
        materialManager = GetComponent<MaterialManager>();
        if (materialManager != null)
        {
            objectMaterial = materialManager.GetMaterialProperties();
        }
        else
        {
            Debug.LogWarning($"No MaterialManager found on {gameObject.name}. Using default material.");
            objectMaterial = new MaterialProperties
            {
                materialType = MaterialType.Metal,
                bounciness = 0.1f,
                friction = 0.2f
            };
        }
    }

    public void ApplyGravity(float gravity, float floorY, MaterialProperties floorMaterial)
    {
        verticalVelocity += gravity * Time.deltaTime;
        transform.position += new Vector3(0, verticalVelocity * Time.deltaTime, 0);

        if (transform.position.y <= floorY)
        {
            transform.position = new Vector3(transform.position.x, floorY, transform.position.z);

            // Combine bounciness from object and floor (average or other rule)
            float combinedBounciness = (objectMaterial.bounciness + floorMaterial.bounciness) * 0.5f;
            float combinedFriction = (objectMaterial.friction + floorMaterial.friction) * 0.5f;

            // Reverse velocity for bounce
            verticalVelocity = -verticalVelocity * combinedBounciness;

            // If velocity is too small, stop bouncing
            if (Mathf.Abs(verticalVelocity) < 0.1f)
            {
                verticalVelocity = 0f;
            }

            // Apply friction (if needed for horizontal movement or damping)
            verticalVelocity *= (1 - combinedFriction);
        }
    }
}
