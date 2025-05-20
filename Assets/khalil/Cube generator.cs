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
                    if ((x == 0 || x == dim.x - 1) &&
                       (y == 0 || y == dim.y - 1) &&
                       (z == 0 || z == dim.z - 1))
                    {
                        go.GetComponent<SpringPoint>().isFixed = true; // Fix corners
                    }
                    SpringPoint sp = go.GetComponent<SpringPoint>();
                    grid[x, y, z] = sp;
                }
            }
        }

        // Connect adjacent points with springs
        // Add shear and bend connections
        for (int x = 0; x < dim.x; x++)
        {
            for (int y = 0; y < dim.y; y++)
            {
                for (int z = 0; z < dim.z; z++)
                {
                    SpringPoint current = grid[x, y, z];

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                if (dx == 0 && dy == 0 && dz == 0) continue;

                                int nx = x + dx;
                                int ny = y + dy;
                                int nz = z + dz;

                                if (nx >= 0 && nx < dim.x &&
                                    ny >= 0 && ny < dim.y &&
                                    nz >= 0 && nz < dim.z)
                                {
                                    SpringPoint neighbor = grid[nx, ny, nz];
                                    if (!current.connections.Exists(c => c.point == neighbor))
                                    {
                                        CreateConnection(current, neighbor);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    void CreateConnection(SpringPoint a, SpringPoint b)
    {
        float restLength = Vector3.Distance(a.transform.position, b.transform.position);

        Connection conn = new Connection
        {
            point = b,
            springConstant = cubeConfig.springConstant,
            damperConstant = cubeConfig.damperConstant,
            restLength = restLength
        };
        a.connections.Add(conn);
    }

}
