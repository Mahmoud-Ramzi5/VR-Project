using UnityEngine;

public class RigidMoleculeCube : MonoBehaviour
{
    public int size = 3;
    public float spacing = 1.1f;
    public GameObject cubePrefab;

    private Transform[,,] cubes;
    private Vector3[,,] localOffsets;

    private Vector3 centerPosition;

    void Start()
    {
        cubes = new Transform[size, size, size];
        localOffsets = new Vector3[size, size, size];

        Vector3 centerOffset = Vector3.one * (size - 1) * spacing / 2f;
        centerPosition = transform.position;

        // Create all cubes and store local offsets
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int z = 0; z < size; z++)
                {
                    Vector3 localPos = new Vector3(x, y, z) * spacing - centerOffset;
                    GameObject cube = CreateCube(centerPosition + localPos);
                    cubes[x, y, z] = cube.transform;
                    localOffsets[x, y, z] = localPos;
                }
            }
        }
    }

    void Update()
    {
        // You could move the whole cube by modifying centerPosition
        if (Input.GetKey(KeyCode.LeftArrow)) centerPosition += Vector3.left * Time.deltaTime * 2;
        if (Input.GetKey(KeyCode.RightArrow)) centerPosition += Vector3.right * Time.deltaTime * 2;
        if (Input.GetKey(KeyCode.UpArrow)) centerPosition += Vector3.up * Time.deltaTime * 2;
        if (Input.GetKey(KeyCode.DownArrow)) centerPosition += Vector3.down * Time.deltaTime * 2;

        // Maintain rigid shape by resetting cube positions relative to center
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int z = 0; z < size; z++)
                {
                    cubes[x, y, z].position = centerPosition + localOffsets[x, y, z];
                }
            }
        }
    }

    GameObject CreateCube(Vector3 position)
    {
        GameObject cube;
        if (cubePrefab != null)
            cube = Instantiate(cubePrefab, position, Quaternion.identity);
        else
        {
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.localScale = Vector3.one * 0.9f;
        }

        cube.transform.position = position;
        cube.transform.parent = this.transform;
        return cube;
    }
}
