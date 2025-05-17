using UnityEngine;
using System.Linq;

public class CubeGenerator : MonoBehaviour
{
    public Cube cubeConfig; // Assign the Cube ScriptableObject in the Inspector
    public GameObject springPointPrefab; // Prefab with SpringPoint and Rigidbody components

    private SpringPoint[,,] grid; // 3D array to hold SpringPoint references

    void Start()
    {
        GenerateCube();
    }

    void GenerateCube()
    {
        Vector3Int dim = cubeConfig.gridDimensions;
        grid = new SpringPoint[dim.x, dim.y, dim.z];

        // Calculate offset to center the cube
        Vector3 offset = new Vector3(
            (dim.x - 1) * cubeConfig.spacing * 0.5f,
            (dim.y - 1) * cubeConfig.spacing * 0.5f,
            (dim.z - 1) * cubeConfig.spacing * 0.5f
        );

        // Instantiate SpringPoints in a grid
        for (int x = 0; x < dim.x; x++)
        {
            for (int y = 0; y < dim.y; y++)
            {
                for (int z = 0; z < dim.z; z++)
                {
                    Vector3 position = new Vector3(
                        x * cubeConfig.spacing,
                        y * cubeConfig.spacing,
                        z * cubeConfig.spacing
                    ) - offset;

                    GameObject go = Instantiate(springPointPrefab, transform);
                    go.transform.position = position;
                    SpringPoint sp = go.GetComponent<SpringPoint>();
                    grid[x, y, z] = sp;
                }
            }
        }

        // Connect adjacent points with springs
        for (int x = 0; x < dim.x; x++)
        {
            for (int y = 0; y < dim.y; y++)
            {
                for (int z = 0; z < dim.z; z++)
                {
                    SpringPoint current = grid[x, y, z];

                    // Connect to right neighbor
                    if (x < dim.x - 1)
                    {
                        SpringPoint right = grid[x + 1, y, z];
                        if (right != null && !current.connections.Any(conn => conn.point == right))
                        {
                            CreateConnection(current, right);
                        }
                    }

                    // Connect to left neighbor (this is redundant as it will be covered when processing the left point)
                    // if (x > 0) 
                    // {
                    //     SpringPoint left = grid[x - 1, y, z];
                    //     if (left != null && !current.connections.Any(conn => conn.point == left))
                    //     {
                    //         CreateConnection(current, left);
                    //     }
                    // }

                    // Connect to up neighbor
                    if (y < dim.y - 1)
                    {
                        SpringPoint up = grid[x, y + 1, z];
                        if (up != null && !current.connections.Any(conn => conn.point == up))
                        {
                            CreateConnection(current, up);
                        }
                    }

                    // Connect to down neighbor (redundant)
                    // if (y > 0)
                    // {
                    //     SpringPoint down = grid[x, y - 1, z];
                    //     if (down != null && !current.connections.Any(conn => conn.point == down))
                    //     {
                    //         CreateConnection(current, down);
                    //     }
                    // }

                    // Connect to front neighbor
                    if (z < dim.z - 1)
                    {
                        SpringPoint front = grid[x, y, z + 1];
                        if (front != null && !current.connections.Any(conn => conn.point == front))
                        {
                            CreateConnection(current, front);
                        }
                    }

                    // Connect to back neighbor (redundant)
                    // if (z > 0)
                    // {
                    //     SpringPoint back = grid[x, y, z - 1];
                    //     if (back != null && !current.connections.Any(conn => conn.point == back))
                    //     {
                    //         CreateConnection(current, back);
                    //     }
                    // }
                }
            }
        }
    }

    void CreateConnection(SpringPoint a, SpringPoint b)
    {
        // Add connection from A to B
        Connection connAB = new Connection
        {
            point = b,
            springConstant = cubeConfig.springConstant,
            damperConstant = cubeConfig.damperConstant,
            restLength = cubeConfig.spacing
        };
        a.connections.Add(connAB);

        // Add connection from B to A
        Connection connBA = new Connection
        {
            point = a,
            springConstant = cubeConfig.springConstant,
            damperConstant = cubeConfig.damperConstant,
            restLength = cubeConfig.spacing
        };
        b.connections.Add(connBA);
    }
}
