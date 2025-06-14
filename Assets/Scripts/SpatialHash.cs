using UnityEngine;
using System.Collections.Generic;


public class SpatialHash
{
    //private float cellSize;
    //private float connectionRadius;
    //private Dictionary<int, List<SpringPointTest>> cells;

    //public SpatialHash(float cellSize, float connectionRadius)
    //{
    //    this.cellSize = cellSize;
    //    this.connectionRadius = connectionRadius;
    //    cells = new Dictionary<int, List<SpringPointTest>>();
    //}

    //private int Hash(Vector3 position)
    //{
    //    int x = Mathf.FloorToInt(position.x / cellSize);
    //    int y = Mathf.FloorToInt(position.y / cellSize);
    //    int z = Mathf.FloorToInt(position.z / cellSize);

    //    // Simple hash function - you might want to use a better one for production
    //    unchecked // Overflow is fine
    //    {
    //        int hash = 17;
    //        hash = hash * 31 + x.GetHashCode();
    //        hash = hash * 31 + y.GetHashCode();
    //        hash = hash * 31 + z.GetHashCode();
    //        return hash;
    //    }
    //}

    //public void Insert(SpringPointTest point)
    //{
    //    int hash = Hash(point.transform.position);
    //    if (!cells.ContainsKey(hash))
    //    {
    //        cells[hash] = new List<SpringPointTest>();
    //    }
    //    cells[hash].Add(point);
    //}

    //public List<SpringPointTest> GetNearby(SpringPointTest point)
    //{
    //    List<SpringPointTest> nearby = new List<SpringPointTest>();
    //    Vector3 pos = point.transform.position;

    //    // Check adjacent cells in 3x3x3 grid around the point
    //    for (int x = -1; x <= 1; x++)
    //    {
    //        for (int y = -1; y <= 1; y++)
    //        {
    //            for (int z = -1; z <= 1; z++)
    //            {
    //                Vector3 offset = new Vector3(x * cellSize, y * cellSize, z * cellSize);
    //                int hash = Hash(pos + offset);

    //                if (cells.TryGetValue(hash, out List<SpringPointTest> cellPoints))
    //                {
    //                    nearby.AddRange(cellPoints);
    //                }
    //            }
    //        }
    //    }

    //    return nearby;
    //}

    //public void Clear()
    //{
    //    cells.Clear();
    //}
}