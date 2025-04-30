using UnityEngine;
using System.Collections.Generic;

public class GravityZone : MonoBehaviour
{
    [Header("Zone Settings")]
    public Vector3 zoneCenter = Vector3.zero;
    public Vector3 zoneSize = new Vector3(10, 10, 10);
    public float gravity = -9.81f;

    [Header("Floor Settings")]
    public float floorY = 0f; // Y-position of the floor
    private MaterialManager materialManager;
    private MaterialProperties floorMaterial;

    private List<GravityObject> objects = new List<GravityObject>();

    void Start()
    {
        materialManager = GetComponent<MaterialManager>();
        if (materialManager != null)
        {
            floorMaterial = materialManager.GetMaterialProperties();
        }
        else
        {
            Debug.LogWarning($"No MaterialManager found on {gameObject.name}. Using default material.");
            floorMaterial = new MaterialProperties
            {
                materialType = MaterialType.Stone,
                bounciness = 0f,
                friction = 0.9f
            };
        }

        // Find all gravity-affected objects in the scene
        GravityObject[] found = GameObject.FindObjectsOfType<GravityObject>();
        objects.AddRange(found);
    }

    void Update()
    {
        foreach (var obj in objects)
        {
            if (IsInsideZone(obj.transform.position))
            {
                obj.ApplyGravity(gravity, floorY, floorMaterial);
            }
        }
    }

    bool IsInsideZone(Vector3 position)
    {
        Vector3 halfSize = zoneSize * 0.5f;
        Vector3 min = zoneCenter - halfSize;
        Vector3 max = zoneCenter + halfSize;

        return position.x >= min.x && position.x <= max.x &&
               position.y >= min.y && position.y <= max.y &&
               position.z >= min.z && position.z <= max.z;
    }

    void OnDrawGizmos()
    {
        // Draw the gravity zone
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(zoneCenter, zoneSize);

        // Draw the floor
        Gizmos.color = Color.green;
        Vector3 floorStart = new Vector3(zoneCenter.x - zoneSize.x / 2, floorY, zoneCenter.z - zoneSize.z / 2);
        Vector3 floorEnd = new Vector3(zoneCenter.x + zoneSize.x / 2, floorY, zoneCenter.z + zoneSize.z / 2);
        Gizmos.DrawLine(floorStart, floorEnd);
    }
}
