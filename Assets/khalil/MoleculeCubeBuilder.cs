using UnityEngine;
using System.Collections.Generic;

public class ManualMoleculeCube : MonoBehaviour
{
    public int size = 3;
    public float spacing = 1.1f;
    public GameObject cubePrefab; // Optional: Assign a prefab for visual style
    private GameObject[,,] cubes;
    private List<FixedConnection> connections = new List<FixedConnection>();

    void Start()
    {
        cubes = new GameObject[size, size, size];

        Vector3 centerOffset = Vector3.one * (size - 1) * spacing / 2f;

        // Create the grid of mini-cubes
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int z = 0; z < size; z++)
                {
                    Vector3 pos = new Vector3(x, y, z) * spacing - centerOffset;
                    GameObject cube = CreateCube(pos);
                    cubes[x, y, z] = cube;
                }
            }
        }

        // Create manual "fixed joints" between neighbors
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int z = 0; z < size; z++)
                {
                    GameObject current = cubes[x, y, z];

                    TryConnect(current, x + 1, y, z);
                    TryConnect(current, x, y + 1, z);
                    TryConnect(current, x, y, z + 1);
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
            cube.transform.position = position;
            cube.transform.localScale = Vector3.one * 0.9f;
        }

        cube.transform.parent = this.transform;
        return cube;
    }

    void TryConnect(GameObject a, int x, int y, int z)
    {
        if (x >= size || y >= size || z >= size) return;
        GameObject b = cubes[x, y, z];
        connections.Add(new FixedConnection(a.transform, b.transform));
    }

    void Update()
    {
        // Apply constraints to all connected cube pairs
        foreach (var conn in connections)
        {
            conn.MaintainFixedDistance();
        }
    }

    void OnDrawGizmos()
    {
        if (connections == null) return;

        Gizmos.color = Color.cyan;

        foreach (var conn in connections)
        {
            Gizmos.DrawLine(conn.A.position, conn.B.position);
        }
    }

    class FixedConnection
    {
        public Transform A { get; private set; }
        public Transform B { get; private set; }

        float initialDistance;

        public FixedConnection(Transform a, Transform b)
        {
            A = a;
            B = b;
            initialDistance = Vector3.Distance(a.position, b.position);
        }

        public void MaintainFixedDistance()
        {
            Vector3 delta = B.position - A.position;
            float currentDistance = delta.magnitude;
            float error = currentDistance - initialDistance;

            if (Mathf.Abs(error) > 0.001f)
            {
                Vector3 correction = delta.normalized * (error / 2f);
                A.position += correction;
                B.position -= correction;
            }
        }
    }

}
