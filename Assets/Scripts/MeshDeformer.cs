using System.Collections.Generic;
using UnityEngine;

public class MeshDeformer : MonoBehaviour
{
    public MeshManager meshManager;
    public MeshFilter meshFilter;
    public float influenceRadius = 1.0f;
    public bool showWeights = false;

    private Vector3[] baseVertices;
    private Vector3[] currentVertices;
    private Mesh deformedMesh;
    private List<WeightedInfluence>[] vertexInfluences;

    void Start()
    {
        if (!meshManager) meshManager = GetComponent<MeshManager>();
        if (!meshFilter) meshFilter = GetComponent<MeshFilter>();

        // Create separate mesh for deformation
        deformedMesh = meshManager.CurrentMesh;

        
        //BuildInfluenceMapping();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseClick();
        }
    }

    private void HandleMouseClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (FindClosestTriangle(ray, out int triangleIndex, out Vector3 hitPoint))
        {
            Vector3 localPoint = transform.InverseTransformPoint(hitPoint);
            MeshManager.TriangleData data = meshManager.GetTriangleData(triangleIndex);
            if (data.canSubdivide && data.subdivisionLevel < meshManager.maxSubdivisionLevel)
            {
                meshManager.SubdivideTriangles(new List<int> { triangleIndex }, meshManager.springFiller);
                
            }
        }
    }

  

    

    private bool FindClosestTriangle(Ray ray, out int closestTriangleIndex, out Vector3 hitPoint)
    {
        closestTriangleIndex = -1;
        hitPoint = Vector3.zero;
        float closestDistance = Mathf.Infinity;

        Matrix4x4 localToWorld = transform.localToWorldMatrix;
        Vector3[] vertices = meshManager.Vertices.ToArray();
        int[] meshTriangles = meshManager.Triangles.ToArray();

        for (int i = 0; i < meshTriangles.Length; i += 3)
        {
            Vector3 v0 = localToWorld.MultiplyPoint3x4(vertices[meshTriangles[i]]);
            Vector3 v1 = localToWorld.MultiplyPoint3x4(vertices[meshTriangles[i + 1]]);
            Vector3 v2 = localToWorld.MultiplyPoint3x4(vertices[meshTriangles[i + 2]]);

            if (RayIntersectsTriangle(ray, v0, v1, v2, out Vector3 intersectPoint))
            {
                float distance = Vector3.Distance(ray.origin, intersectPoint);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTriangleIndex = i / 3;
                    hitPoint = intersectPoint;
                }
            }
        }

        return closestTriangleIndex != -1;
    }

    private bool RayIntersectsTriangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, out Vector3 intersectPoint)
    {
        intersectPoint = Vector3.zero;
        Vector3 e1 = v1 - v0;
        Vector3 e2 = v2 - v0;
        Vector3 h = Vector3.Cross(ray.direction, e2);
        float a = Vector3.Dot(e1, h);

        if (a > -Mathf.Epsilon && a < Mathf.Epsilon)
            return false;

        float f = 1.0f / a;
        Vector3 s = ray.origin - v0;
        float u = f * Vector3.Dot(s, h);

        if (u < 0.0 || u > 1.0)
            return false;

        Vector3 q = Vector3.Cross(s, e1);
        float v = f * Vector3.Dot(ray.direction, q);

        if (v < 0.0 || u + v > 1.0)
            return false;

        float t = f * Vector3.Dot(e2, q);
        if (t > Mathf.Epsilon)
        {
            intersectPoint = ray.origin + ray.direction * t;
            return true;
        }

        return false;
    }

    

   

   
    private struct WeightedInfluence
    {
        public SpringPoint springPoint;
        public float weight;
    }
}