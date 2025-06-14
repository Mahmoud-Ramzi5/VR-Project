using System.Collections.Generic;
using UnityEngine;

public class DeformTest : MonoBehaviour
{
    [SerializeField] private GameObject cube;
    private List<SpringPoint> allSpringPoints;
    [SerializeField] private MeshDeformer deformer;
    private List<Vector3> pointPositions;
    void Start()
    {
       allSpringPoints=  cube.GetComponent<OctreeSpringFiller>().allSpringPoints;
        pointPositions= new List<Vector3>();

    }

    // Update is called once per frame
    void FixedUpdate()
    {
        foreach (var point in allSpringPoints) {
            pointPositions.Add(point.position);
        }
        deformer.HandleCollisionPoints(pointPositions);
    }
}
