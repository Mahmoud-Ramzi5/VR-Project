using System.Collections.Generic;
using UnityEngine;

public class DeformTest : MonoBehaviour
{
    [SerializeField] private GameObject cube;
    private List<SpringPointTest> allSpringPointsTest;
    [SerializeField] private MeshDeformer deformer;
    private List<Vector3> pointPositions;
    void Start()
    {
       allSpringPointsTest=  cube.GetComponent<OctreeSpringFillerTest>().allSpringPointsTest;
        pointPositions= new List<Vector3>();

    }

    // Update is called once per frame
    void FixedUpdate()
    {
        foreach (var point in allSpringPointsTest) {
            pointPositions.Add(point.transform.position);
        }
        deformer.HandleCollisionPoints(pointPositions);
    }
}
