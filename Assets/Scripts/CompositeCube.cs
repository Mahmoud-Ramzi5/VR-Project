using UnityEngine;
using System.Collections.Generic;

public class CompositeCube : MonoBehaviour
{
    public int resolution = 3; // Number of voxels per edge
    public float voxelSize = 0.3f;
    public float spacing = 0.05f;

    private List<GameObject> voxels = new List<GameObject>();
    public Vector3 velocity = new Vector3(2f, 1f, 0f);
    public Vector3 roomMinBounds = new Vector3(-5f, 0f, -5f);
    public Vector3 roomMaxBounds = new Vector3(5f, 5f, 5f);

    void Start()
    {
        CreateVoxelCube();
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        transform.position += velocity * dt;

        HandleCollision();

        // Optionally: jiggle or deform individual voxels slightly
        foreach (GameObject voxel in voxels)
        {
            // You can add springy motion or shake here
        }
    }

    void CreateVoxelCube()
    {
        float totalSize = resolution * (voxelSize + spacing);
        Vector3 origin = -Vector3.one * totalSize / 2f + new Vector3(voxelSize, voxelSize, voxelSize) * 0.5f;

        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                for (int z = 0; z < resolution; z++)
                {
                    Vector3 localPos = origin + new Vector3(
                        x * (voxelSize + spacing),
                        y * (voxelSize + spacing),
                        z * (voxelSize + spacing)
                    );

                    GameObject voxel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    voxel.transform.parent = transform;
                    voxel.transform.localPosition = localPos;
                    voxel.transform.localScale = Vector3.one * voxelSize;

                    Destroy(voxel.GetComponent<Collider>()); // remove built-in physics
                    voxels.Add(voxel);
                }
            }
        }
    }

    void HandleCollision()
    {
        Vector3 pos = transform.position;
        float halfSize = resolution * (voxelSize + spacing) * 0.5f;

        // X
        if (pos.x - halfSize < roomMinBounds.x || pos.x + halfSize > roomMaxBounds.x)
        {
            velocity.x *= -1;
            pos.x = Mathf.Clamp(pos.x, roomMinBounds.x + halfSize, roomMaxBounds.x - halfSize);
        }

        // Y
        if (pos.y - halfSize < roomMinBounds.y || pos.y + halfSize > roomMaxBounds.y)
        {
            velocity.y *= -1;
            pos.y = Mathf.Clamp(pos.y, roomMinBounds.y + halfSize, roomMaxBounds.y - halfSize);
        }

        // Z
        if (pos.z - halfSize < roomMinBounds.z || pos.z + halfSize > roomMaxBounds.z)
        {
            velocity.z *= -1;
            pos.z = Mathf.Clamp(pos.z, roomMinBounds.z + halfSize, roomMaxBounds.z - halfSize);
        }

        transform.position = pos;
    }
}
