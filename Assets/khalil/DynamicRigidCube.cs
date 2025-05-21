using UnityEngine;
using System.Collections.Generic;

public class DynamicRigidCube : MonoBehaviour
{
    public int size = 3;
    public float spacing = 1.1f;
    public GameObject cubePrefab;

    private Transform[,,] cubes;
    private List<DistanceConstraint> constraints = new();

    void Start()
    {
        cubes = new Transform[size, size, size];

        Vector3 centerOffset = Vector3.one * (size - 1) * spacing / 2f;

        // Create cubes
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int z = 0; z < size; z++)
                {
                    Vector3 pos = new Vector3(x, y, z) * spacing - centerOffset;
                    GameObject cube = CreateCube(pos);
                    cubes[x, y, z] = cube.transform;
                }
            }
        }

        // Create constraints between cubes within a distance threshold
        float maxDistance = spacing * 1.5f; // ~nearest 18-26 neighbors
        for (int x1 = 0; x1 < size; x1++)
        {
            for (int y1 = 0; y1 < size; y1++)
            {
                for (int z1 = 0; z1 < size; z1++)
                {
                    Transform a = cubes[x1, y1, z1];

                    for (int x2 = 0; x2 < size; x2++)
                    {
                        for (int y2 = 0; y2 < size; y2++)
                        {
                            for (int z2 = 0; z2 < size; z2++)
                            {
                                if (x2 <= x1 && y2 <= y1 && z2 <= z1) continue; // avoid duplicates
                                Transform b = cubes[x2, y2, z2];

                                float dist = Vector3.Distance(a.position, b.position);
                                if (dist <= maxDistance)
                                {
                                    constraints.Add(new DistanceConstraint(a, b));
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    void Update()
    {
        // Run several iterations for stability (more = stiffer shape)
        for (int i = 0; i < 10; i++) // try 10 or even 20
        {
            foreach (var c in constraints)
            {
                c.Enforce();
            }
        }
    }

    void ConnectIfExists(Transform a, int x, int y, int z)
    {
        if (x >= size || y >= size || z >= size) return;
        Transform b = cubes[x, y, z];
        constraints.Add(new DistanceConstraint(a, b));
    }

    GameObject CreateCube(Vector3 position)
    {
        GameObject cube = cubePrefab
            ? Instantiate(cubePrefab, position, Quaternion.identity)
            : GameObject.CreatePrimitive(PrimitiveType.Cube);

        cube.transform.position = position;
        cube.transform.localScale = Vector3.one * 0.9f;
        cube.transform.parent = this.transform;

        // Make movable with mouse (optional)
        cube.AddComponent<Draggable>();

        return cube;
    }

    class DistanceConstraint
    {
        Transform a, b;
        float restLength;

        public DistanceConstraint(Transform a, Transform b)
        {
            this.a = a;
            this.b = b;
            restLength = Vector3.Distance(a.position, b.position);
        }

        public void Enforce()
        {
            Vector3 delta = b.position - a.position;
            float current = delta.magnitude;
            float error = current - restLength;

            if (Mathf.Abs(error) > 0.0001f)
            {
                Vector3 correction = delta.normalized * (error / 2f);
                a.position += correction;
                b.position -= correction;
            }
        }
    }
}
