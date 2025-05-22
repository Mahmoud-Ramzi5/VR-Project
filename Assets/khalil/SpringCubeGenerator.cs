using UnityEngine;
using System.Collections.Generic;

public class SpringCubeGenerator : MonoBehaviour
{
    public SpringPoint springPointPrefab;
    public float spacing = 1f;
    public bool fixBottomCorners = true;

    private List<SpringPoint> points = new List<SpringPoint>();

    void Start()
    {
        GenerateCube();
    }

    void FixedUpdate()
    {
        foreach (SpringPoint point in points)
        {
            point.HandleBoundaryBox(); // Ensure each point stays within bounds
        }
    }

    void GenerateCube()
    {
        Vector3[] offsets = new Vector3[]
        {
            new Vector3(0, 0, 0), // 0
            new Vector3(1, 0, 0), // 1
            new Vector3(0, 1, 0), // 2
            new Vector3(1, 1, 0), // 3
            new Vector3(0, 0, 1), // 4
            new Vector3(1, 0, 1), // 5
            new Vector3(0, 1, 1), // 6
            new Vector3(1, 1, 1)  // 7
        };

        SpringPoint[] createdPoints = new SpringPoint[8];

        for (int i = 0; i < 8; i++)
        {
            Vector3 worldPos = transform.position + offsets[i] * spacing;
            createdPoints[i] = Instantiate(springPointPrefab, worldPos, Quaternion.identity);
            createdPoints[i].name = "Point_" + i;

            //if (fixBottomCorners && (i == 0 || i == 1 || i == 4 || i == 5))
            //    createdPoints[i].isFixed = true;

            points.Add(createdPoints[i]);
        }

        Connect(createdPoints, 0, 1); // Edges
        Connect(createdPoints, 0, 2);
        Connect(createdPoints, 0, 4);
        Connect(createdPoints, 1, 3);
        Connect(createdPoints, 1, 5);
        Connect(createdPoints, 2, 3);
        Connect(createdPoints, 2, 6);
        Connect(createdPoints, 3, 7);
        Connect(createdPoints, 4, 5);
        Connect(createdPoints, 4, 6);
        Connect(createdPoints, 5, 7);
        Connect(createdPoints, 6, 7);

        // Face diagonals
        Connect(createdPoints, 0, 3);
        Connect(createdPoints, 1, 2);
        Connect(createdPoints, 4, 7);
        Connect(createdPoints, 5, 6);
        Connect(createdPoints, 0, 5);
        Connect(createdPoints, 1, 4);
        Connect(createdPoints, 2, 7);
        Connect(createdPoints, 3, 6);
        Connect(createdPoints, 0, 6);
        Connect(createdPoints, 2, 4);
        Connect(createdPoints, 1, 7);
        Connect(createdPoints, 3, 5);

        // Body diagonals
        Connect(createdPoints, 0, 7);
        Connect(createdPoints, 1, 6);
        Connect(createdPoints, 2, 5);
        Connect(createdPoints, 3, 4);
    }

    //void Connect(SpringPoint[] points, int i, int j)
    //{
    //    float restLength = Vector3.Distance(points[i].transform.position, points[j].transform.position);

    //    Connection c1 = new Connection
    //    {
    //        point = points[j],
    //        restLength = restLength,
    //        springConstant = 20f,
    //        damperConstant = 1.0f
    //    };
    //    points[i].connections.Add(c1);

    //    Connection c2 = new Connection
    //    {
    //        point = points[i],
    //        restLength = restLength,
    //        springConstant = 20f,
    //        damperConstant = 1.0f
    //    };
    //    points[j].connections.Add(c2);
    //}
    void Connect(SpringPoint[] points, int i, int j)
    {
        // Rest length should be the distance between the two points at the start
        float restLength = Vector3.Distance(points[i].transform.position, points[j].transform.position);

        // Prevent rest length from being too large or too small
        restLength = Mathf.Clamp(restLength, 0.5f, 3f);

        Connection c1 = new Connection
        {
            point = points[j],
            restLength = restLength, // Initialize to the actual distance between points
            springConstant = 20f,
            damperConstant = 1.0f
        };
        points[i].connections.Add(c1);

        Connection c2 = new Connection
        {
            point = points[i],
            restLength = restLength, // Same for the reverse connection
            springConstant = 20f,
            damperConstant = 1.0f
        };
        points[j].connections.Add(c2);
    }

}
