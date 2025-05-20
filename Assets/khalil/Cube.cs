using UnityEngine;

[CreateAssetMenu(fileName = "Cube", menuName = "Scriptable Objects/Cube")]
public class Cube : ScriptableObject
{
    [Header("Grid Configuration")]
    public Vector3Int gridDimensions = new Vector3Int(3, 3, 3); // Number of points along each axis
    public float spacing = 1f; // Distance between adjacent points (also rest length for springs)

    [Header("Spring Parameters")]
    public float springConstant = 2f; // Spring stiffness
    public float damperConstant = 0.05f;   // Damping factor
}
