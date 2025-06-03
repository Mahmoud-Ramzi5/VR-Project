using System.Collections.Generic;
using UnityEngine;

public class OctreeNode
{
    public Bounds worldBounds, localBounds;
    public OctreeNode[] children;
    public List<Vector3> pointsPositions = new List<Vector3>();
    public bool isDivided;

    public OctreeNode(Bounds nodeWorldBounds, Bounds nodeLocalbounds)
    {
        worldBounds = nodeWorldBounds;
        localBounds = nodeLocalbounds;
    }

    public bool Divide(float minSize)
    {
        // If the node size is already small enough, do not subdivide further
        // we are comparing node size on x-axis only
        //if (bounds.size.x <= minSize) return false;
        if (worldBounds.size.x <= minSize) return false;

        // Calculate offset to place child nodes relative to this node's center
        float worldQuarter = worldBounds.size.x / 4f;
        float localQuarter = localBounds.size.x / 4f;

        // Each child node will be half the size of this node
        float worldChildSize = worldBounds.size.x / 2f;
        float localChildSize = localBounds.size.x / 2f;
        Vector3 worldChildExtents = new Vector3(worldChildSize, worldChildSize, worldChildSize);
        Vector3 localChildExtents = new Vector3(localChildSize, localChildSize, localChildSize);


        // Initialize the array of 8 children nodes
        children = new OctreeNode[8];

        // Loop through each of the 8 octants (children)
        for (int i = 0; i < 8; i++)
        {
            // Start from this node's center
            Vector3 worldCenter = worldBounds.center;
            Vector3 localCenter = localBounds.center;

            // Calculate the offset for each child on x, y, and z axes
            // The bitwise checks on 'i' determine which side along each axis:
            // - If bit is 0, offset is negative (left/down/back)
            // - If bit is 1, offset is positive (right/up/front)

            worldCenter.x += (i & 1) == 0 ? -worldQuarter : worldQuarter; // Check bit 0 for x-axis
            worldCenter.y += (i & 2) == 0 ? -worldQuarter : worldQuarter; // Check bit 1 for y-axis
            worldCenter.z += (i & 4) == 0 ? -worldQuarter : worldQuarter; // Check bit 2 for z-axis

            localCenter.x += (i & 1) == 0 ? -localQuarter : localQuarter; // Check bit 0 for x-axis
            localCenter.y += (i & 2) == 0 ? -localQuarter : localQuarter; // Check bit 1 for y-axis
            localCenter.z += (i & 4) == 0 ? -localQuarter : localQuarter; // Check bit 2 for z-axis

            // Create a new child node with the calculated center and size
            children[i] = new OctreeNode(new Bounds(worldCenter, worldChildExtents), new Bounds(localCenter, localChildExtents));
        }

        // Mark this node as divided
        isDivided = true;

        return true;
    }

    public void DrawGizmos(Color leafColor, Color branchColor)
    {
        Gizmos.color = isDivided ? branchColor : leafColor;
        Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);

        if (isDivided && children != null)
        {
            foreach (var child in children)
            {
                if (child != null)
                    child.DrawGizmos(leafColor, branchColor);
            }
        }
    }

}
