using UnityEngine;
using System.Collections.Generic;

public class SpringCubeGenerator : MonoBehaviour
{
    public SpringPoint springPointPrefab;
    public bool fixBottomCorners = true;
    public float spacing = 2f;
    [Range(0.1f, 5f)] public float connectionRadius = 2f;
    public float springConstant = 100f;
    public float damperConstant = 1.0f;
    public int gridSize = 3; // Creates a gridSize x gridSize x gridSize cube

    private List<SpringPoint> points = new List<SpringPoint>();

    void Start()
    {
        GenerateCube();
    }

    void FixedUpdate()
    {
        foreach (SpringPoint point in points)
        {
            point.HandleBoundaryBox();
        }
    }

    void GenerateCube()
    {
        // Clear any existing points
        foreach (var point in points)
        {
            if (point != null) Destroy(point.gameObject);
        }
        points.Clear();

        // Create grid of points
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    Vector3 worldPos = transform.position + new Vector3(x, y, z) * spacing;
                    SpringPoint newPoint = Instantiate(springPointPrefab, worldPos, Quaternion.identity);
                    newPoint.name = $"Point_{x}_{y}_{z}";

                    // Fix bottom layer if enabled
                    if (fixBottomCorners && y == 0)
                    {
                        newPoint.isFixed = true;
                    }

                    points.Add(newPoint);
                }
            }
        }

        // Create dynamic connections based on proximity
        CreateDynamicConnections();
    }

    void CreateDynamicConnections()
    {
        // For each point, find nearby points and create connections
        for (int i = 0; i < points.Count; i++)
        {
            SpringPoint currentPoint = points[i];

            for (int j = i + 1; j < points.Count; j++)
            {
                SpringPoint otherPoint = points[j];
                float distance = Vector3.Distance(currentPoint.transform.position, otherPoint.transform.position);

                // Connect if within radius and not already connected
                if (distance <= connectionRadius * spacing && !IsConnected(currentPoint, otherPoint))
                {
                    ConnectPoints(currentPoint, otherPoint, distance);
                }
            }
        }
    }

    bool IsConnected(SpringPoint a, SpringPoint b)
    {
        // Check if a is already connected to b
        foreach (var conn in a.connections)
        {
            if (conn.point == b) return true;
        }
        return false;
    }

    void ConnectPoints(SpringPoint a, SpringPoint b, float distance)
    {
        // Clamp rest length to reasonable values
        float restLength = Mathf.Clamp(distance, 0.5f, 3f);

        Connection c1 = new Connection(b, restLength, springConstant, damperConstant);
        a.connections.Add(c1);

        Connection c2 = new Connection(a, restLength, springConstant, damperConstant);
        b.connections.Add(c2);
    }

    void OnDrawGizmos()
    {
        // Draw the connection radius for visualization
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            transform.position + new Vector3(gridSize - 1, gridSize - 1, gridSize - 1) * spacing * 0.5f,
            new Vector3(gridSize, gridSize, gridSize) * spacing
        );

        // Draw connection radius spheres at each point
        if (Application.isPlaying)
        {
            Gizmos.color = new Color(1, 0, 0, 0.1f);
            foreach (var point in points)
            {
                if (point != null)
                    Gizmos.DrawSphere(point.transform.position, connectionRadius * spacing);
            }
        }
    }
}