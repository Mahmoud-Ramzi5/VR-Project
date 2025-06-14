using UnityEngine;
using System.Collections.Generic;

public class SpringCubeGenerator : MonoBehaviour
{
    //public SpringPointTest springPointPrefab;
    //public bool fixBottomCorners = true;
    //public float spacing = 2f;
    //[Range(0.1f, 5f)] public float connectionRadius = 2f;
    //public float springConstant = 100f;
    //public float damperConstant = 1.0f;
    //public int gridSize = 3; // Creates a gridSize x gridSize x gridSize cube

    //private List<SpringPoint> points = new List<SpringPoint>();
    //public List<ConnectionTest> allConnectionsTest = new List<ConnectionTest>();
    //public List<SpringPointTest> allSpringPointsTest = new List<SpringPointTest>();
    //protected List<LineRenderer> springVisuals = new List<LineRenderer>();


    //void Start()
    //{
    //    GenerateCube();

    //    foreach (var conn in allConnectionsTest)
    //    {
    //        var lineObj = new GameObject("Spring");
    //        lineObj.transform.SetParent(transform);
    //        var lineRenderer = lineObj.AddComponent<LineRenderer>();
    //        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    //        lineRenderer.startWidth = 0.05f;
    //        lineRenderer.endWidth = 0.05f;
    //        springVisuals.Add(lineRenderer);
    //    }
    //}

    //private void Update()
    //{
    //    // Update spring visuals
    //    for (int i = 0; i < allConnectionsTest.Count; i++)
    //    {
    //        springVisuals[i].SetPosition(0, allConnectionsTest[i].point1.transform.position);
    //        springVisuals[i].SetPosition(1, allConnectionsTest[i].point2.transform.position);
    //    }
    //}

    //void FixedUpdate()
    //{
    //    // Apply gravity
    //    foreach (var point in allSpringPointsTest)
    //    {
    //        point.force += new Vector3(0f, -9.81f, 0f) * point.mass;
    //    }

    //    // Update springs
    //    foreach (var conn in allConnectionsTest)
    //    {
    //        conn.CalculateAndApplyForces();
    //    }

    //    // Handle collisions
    //    if (true)
    //    {
    //        foreach (var point in allSpringPointsTest)
    //        {
    //            HandleGroundCollisionTest(point);
    //        }
    //    }

    //    // Update points
    //    //foreach (var point in allSpringPointsTest)
    //    //{
    //    //    point.CalculateForce(Time.deltaTime);
    //    //}

    //}

    //[Header("Ground Collision")]
    //public float groundLevel = 0f;       // Y-position of the ground plane
    //public float groundBounce = 0.5f;   // Bounce coefficient (0 = no bounce, 1 = full bounce)
    //public float groundFriction = 0.8f; // Friction (0 = full stop, 1 = no friction)
    //protected virtual void HandleGroundCollisionTest(SpringPointTest point)
    //{
    //    if (point.transform.position.y < groundLevel)
    //    {
    //        point.transform.position = new Vector3(point.transform.position.x, groundLevel, point.transform.position.z);

    //        if (point.velocity.y < 0)
    //        {
    //            point.velocity = new Vector3(
    //                point.velocity.x * groundFriction,
    //                -point.velocity.y * groundBounce,
    //                point.velocity.z * groundFriction
    //            );
    //        }
    //    }
    //}

    //void GenerateCube()
    //{
    //    // Clear any existing points
    //    foreach (var point in points)
    //    {
    //        if (point != null) Destroy(point.gameObject);
    //    }
    //    points.Clear();

    //    // Create grid of points
    //    for (int x = 0; x < gridSize; x++)
    //    {
    //        for (int y = 0; y < gridSize; y++)
    //        {
    //            for (int z = 0; z < gridSize; z++)
    //            {
    //                Vector3 worldPos = transform.position + new Vector3(x, y, z) * spacing;
    //                SpringPointTest newPoint = Instantiate(springPointPrefab, worldPos, Quaternion.identity);
    //                newPoint.name = $"Point_{x}_{y}_{z}";

    //                newPoint.isFixed = false;

    //                // Fix bottom layer if enabled
    //                if (fixBottomCorners && y == 0)
    //                {
    //                    newPoint.isFixed = true;
    //                }

    //                allSpringPointsTest.Add(newPoint);
    //            }
    //        }
    //    }

    //    // Create dynamic connections based on proximity
    //    CreateSpringConnectionsTest();
    //}

    ////void CreateDynamicConnections()
    ////{
    ////    // For each point, find nearby points and create connections
    ////    for (int i = 0; i < points.Count; i++)
    ////    {
    ////        SpringPoint currentPoint = points[i];

    ////        for (int j = i + 1; j < points.Count; j++)
    ////        {
    ////            SpringPoint otherPoint = points[j];
    ////            float distance = Vector3.Distance(currentPoint.transform.position, otherPoint.transform.position);

    ////            // Connect if within radius and not already connected
    ////            if (distance <= connectionRadius * spacing && !IsConnected(currentPoint, otherPoint))
    ////            {
    ////                ConnectPoints(currentPoint, otherPoint, distance);
    ////            }
    ////        }
    ////    }
    ////}

    ////bool IsConnected(SpringPoint a, SpringPoint b)
    ////{
    ////    // Check if a is already connected to b
    ////    foreach (var conn in a.connections)
    ////    {
    ////        if (conn.point == b) return true;
    ////    }
    ////    return false;
    ////}

    ////void ConnectPoints(SpringPoint a, SpringPoint b, float distance)
    ////{
    ////    // Clamp rest length to reasonable values
    ////    float restLength = Mathf.Clamp(distance, 0.5f, 3f);

    ////    Connection c1 = new Connection(b, restLength, springConstant, damperConstant);
    ////    a.connections.Add(c1);

    ////    Connection c2 = new Connection(a, restLength, springConstant, damperConstant);
    ////    b.connections.Add(c2);
    ////}



    //void CreateSpringConnectionsTest()
    //{
    //    // Clear existing connections
    //    allConnectionsTest.Clear();

    //    // For each point, find nearby points and create connections
    //    for (int i = 0; i < allSpringPointsTest.Count; i++)
    //    {
    //        SpringPointTest currentPoint = allSpringPointsTest[i];

    //        for (int j = i + 1; j < allSpringPointsTest.Count; j++)
    //        {
    //            SpringPointTest otherPoint = allSpringPointsTest[j];
    //            float distance = Vector3.Distance(currentPoint.transform.position, otherPoint.transform.position);

    //            // Connect if within radius and not already connected
    //            if (distance <= connectionRadius * spacing && !IsConnectedTest(currentPoint, otherPoint))
    //            {
    //                // Clamp rest length to reasonable values
    //                float restLength = Mathf.Clamp(distance, 0.5f, 3f);

    //                ConnectionTest c1 = new ConnectionTest(currentPoint, otherPoint, restLength, springConstant, damperConstant);
    //                allConnectionsTest.Add(c1);

    //                ConnectionTest c2 = new ConnectionTest(otherPoint, currentPoint, restLength, springConstant, damperConstant);
    //                allConnectionsTest.Add(c2);
    //            }
    //        }
    //    }
    //}

    //bool IsConnectedTest(SpringPointTest point1, SpringPointTest point2)
    //{
    //    foreach (var conn in allConnectionsTest)
    //    {
    //        if ((conn.point1 == point1 && conn.point2 == point2) ||
    //            (conn.point1 == point2 && conn.point2 == point1))
    //            return true;
    //    }
    //    return false;
    //}

}