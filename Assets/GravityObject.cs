using UnityEngine;

public class GravityObject : MonoBehaviour
{
    public Vector3 velocity = Vector3.zero;

    private MaterialManager materialManager;
    private MaterialProperties objectMaterial;

    private bool onGround = false;
    private bool onWall = false;
    private float contactFriction = 0f;

    void Start()
    {
        materialManager = GetComponent<MaterialManager>();
        if (materialManager != null)
        {
            objectMaterial = materialManager.GetMaterialProperties();
        }
        else
        {
            objectMaterial = new MaterialProperties
            {
                materialType = MaterialType.Metal,
                bounciness = 0.1f,
                friction = 0.2f
            };
        }
    }

    public void ApplyGravityAndCollisions(float gravity, Bounds zoneBounds, MaterialProperties zoneMaterial)
    {
        // Apply gravity only if not grounded
        velocity.y += gravity * Time.deltaTime;

        // Move object
        transform.position += velocity * Time.deltaTime;

        Vector3 pos = transform.position;
        Vector3 halfSize = Vector3.one * 0.5f;

        // Combined material effects
        float bounciness = (objectMaterial.bounciness + zoneMaterial.bounciness) * 0.5f;
        contactFriction = (objectMaterial.friction + zoneMaterial.friction) * 0.5f;

        // Reset contact flags
        onGround = false;
        onWall = false;

        // Collision checks (with bounce + contact detection)
        if (pos.y - halfSize.y <= zoneBounds.min.y) // floor
        {
            pos.y = zoneBounds.min.y + halfSize.y;
            if (velocity.y < 0) velocity.y = -velocity.y * bounciness;
            onGround = true;
        }
        else if (pos.y + halfSize.y >= zoneBounds.max.y) // ceiling
        {
            pos.y = zoneBounds.max.y - halfSize.y;
            if (velocity.y > 0) velocity.y = -velocity.y * bounciness;
            onGround = true;
        }

        if (pos.x - halfSize.x <= zoneBounds.min.x) // left
        {
            pos.x = zoneBounds.min.x + halfSize.x;
            if (velocity.x < 0) velocity.x = -velocity.x * bounciness;
            onWall = true;
        }
        else if (pos.x + halfSize.x >= zoneBounds.max.x) // right
        {
            pos.x = zoneBounds.max.x - halfSize.x;
            if (velocity.x > 0) velocity.x = -velocity.x * bounciness;
            onWall = true;
        }

        if (pos.z - halfSize.z <= zoneBounds.min.z) // back
        {
            pos.z = zoneBounds.min.z + halfSize.z;
            if (velocity.z < 0) velocity.z = -velocity.z * bounciness;
            onWall = true;
        }
        else if (pos.z + halfSize.z >= zoneBounds.max.z) // front
        {
            pos.z = zoneBounds.max.z - halfSize.z;
            if (velocity.z > 0) velocity.z = -velocity.z * bounciness;
            onWall = true;
        }

        // Apply continuous friction while in contact with surfaces
        if (onGround || onWall)
        {
            float frictionFactor = 1f - (contactFriction * Time.deltaTime * 5f); // scale as needed
            velocity.x *= frictionFactor;
            velocity.z *= frictionFactor;

            // optional: slight vertical damping
            if (onGround)
                velocity.y *= (1f - contactFriction * 0.5f);
        }

        // Stop tiny velocity
        if (velocity.magnitude < 0.01f)
        {
            velocity = Vector3.zero;
        }

        transform.position = pos;
    }
}
