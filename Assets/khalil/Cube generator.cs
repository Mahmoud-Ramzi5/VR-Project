using UnityEngine;
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
                        CreateConnection(current, grid[x + 1, y, z]);

                    // Connect to above neighbor
                    if (y < dim.y - 1)
                        CreateConnection(current, grid[x, y + 1, z]);

                    // Connect to forward neighbor
                    if (z < dim.z - 1)
                        CreateConnection(current, grid[x, y, z + 1]);
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