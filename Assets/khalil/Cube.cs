using UnityEngine;

[CreateAssetMenu(fileName = "Cube", menuName = "Scriptable Objects/Cube")]
public class Cube : ScriptableObject
{
    [Header("Grid Configuration")]
    public Vector3Int gridDimensions = new Vector3Int(5, 5, 5); // Number of points along each axis
    public float spacing = 1f; // Distance between adjacent points (also rest length for springs)

    [Header("Spring Parameters")]
    public float springConstant = 100f; // Spring stiffness
    public float damperConstant = 5f;   // Damping factor
}
