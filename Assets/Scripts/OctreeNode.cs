using System.Collections.Generic;
using UnityEngine;

public class OctreeNode
{
    public Bounds bounds;
    public OctreeNode[] children;
    public List<Vector3> pointsPositions = new List<Vector3>();
    public bool isDivided;

    public OctreeNode(Bounds nodeBounds)
    {
        bounds = nodeBounds;
    }

    public bool Divide(float minSize)
    {
        // If the node size is already small enough, do not subdivide further
        // we are comparing node size on x-axis only
        if (bounds.size.x <= minSize) return false;

        // Calculate offset to place child nodes relative to this node's center
        float quarter = bounds.size.x / 4f;

        // Each child node will be half the size of this node
        float childSize = bounds.size.x / 2f;
        Vector3 childExtents = new Vector3(childSize, childSize, childSize);

        // Initialize the array of 8 children nodes
        children = new OctreeNode[8];

        // Loop through each of the 8 octants (children)
        for (int i = 0; i < 8; i++)
        {
            // Start from this node's center
            Vector3 center = bounds.center;

            // Calculate the offset for each child on x, y, and z axes
            // The bitwise checks on 'i' determine which side along each axis:
            // - If bit is 0, offset is negative (left/down/back)
            // - If bit is 1, offset is positive (right/up/front)

            center.x += (i & 1) == 0 ? -quarter : quarter; // Check bit 0 for x-axis
            center.y += (i & 2) == 0 ? -quarter : quarter; // Check bit 1 for y-axis
            center.z += (i & 4) == 0 ? -quarter : quarter; // Check bit 2 for z-axis

            // Create a new child node with the calculated center and size
            children[i] = new OctreeNode(new Bounds(center, childExtents));
        }

        // Mark this node as divided
        isDivided = true;

        return true;
    }
}
