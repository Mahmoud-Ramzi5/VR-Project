using UnityEngine;
using System.Collections.Generic;

public class MeshManager : MonoBehaviour
{
    public MeshFilter meshFilter;
    public OctreeSpringFiller springFiller;

    private Mesh originalMesh;
    private Mesh currentMesh;
    private List<Vector3> vertices;
    private List<int> triangles;

    public Mesh CurrentMesh => currentMesh;
    public List<Vector3> Vertices => vertices;
    public List<int> Triangles => triangles;


    public void InitializeMesh(Mesh mesh)
    {
        currentMesh = Instantiate(mesh);
        meshFilter.mesh = currentMesh;

        vertices = new List<Vector3>(currentMesh.vertices);
        triangles = new List<int>(currentMesh.triangles);
    }

    
}