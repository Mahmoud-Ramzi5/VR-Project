using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;


public class SpringJobManager : MonoBehaviour
{
    // NativeMultiHashMap was not found
    private NativeParallelMultiHashMap<int, float3> forceMap;

    private NativeArray<float3> velocities;
    private NativeArray<float3> positions;

    private NativeArray<int2> connections;
    private NativeArray<float> restLengths;

    private NativeArray<float> masses;

    // Double buffered forces
    private NativeArray<float3> forcesBufferA;
    private NativeArray<float3> forcesBufferB;
    private bool usingBufferA;

    private JobHandle springJobHandle;
    private JobHandle gravityJobHandle;
    private JobHandle collisionJobHandle;

    // Reference to the parent system
    private OctreeSpringFiller parentSystem;

    public void InitializeArrays(OctreeSpringFiller parent, int pointCount, int connectionCount)
    {
        currentPointCount = pointCount;
        currentConnectionCount = connectionCount;
        parentSystem = parent;

        velocities = new NativeArray<float3>(pointCount, Allocator.Persistent);
        positions = new NativeArray<float3>(pointCount, Allocator.Persistent);

        connections = new NativeArray<int2>(connectionCount, Allocator.Persistent);
        restLengths = new NativeArray<float>(connectionCount, Allocator.Persistent);

        masses = new NativeArray<float>(pointCount, Allocator.Persistent);
        for (int i = 0; i < parent.allSpringPoints.Count; i++)
        {
            masses[i] = parent.allSpringPoints[i].mass;
        }

        // Initialize both force buffers
        forcesBufferA = new NativeArray<float3>(pointCount, Allocator.Persistent);
        forcesBufferB = new NativeArray<float3>(pointCount, Allocator.Persistent);
        usingBufferA = true;

        // Initialize force map with estimated capacity
        int estimatedForceCount = connectionCount * 2 + pointCount;
        forceMap = new NativeParallelMultiHashMap<int, float3>(estimatedForceCount, Allocator.Persistent);
    }
    private int currentPointCount;
    private int currentConnectionCount;

    public void CheckAndResizeArrays(int newPointCount, int newConnectionCount)
    {
        // Only resize if necessary
        if (newPointCount != currentPointCount || newConnectionCount != currentConnectionCount)
        {
            // Dispose old arrays if they exist
            OnDestroy();

            // Create new arrays
            velocities = new NativeArray<float3>(newPointCount, Allocator.Persistent);
            positions = new NativeArray<float3>(newPointCount, Allocator.Persistent);
            masses = new NativeArray<float>(newPointCount, Allocator.Persistent);

            connections = new NativeArray<int2>(newConnectionCount, Allocator.Persistent);
            restLengths = new NativeArray<float>(newConnectionCount, Allocator.Persistent);

            forcesBufferA = new NativeArray<float3>(newPointCount, Allocator.Persistent);
            forcesBufferB = new NativeArray<float3>(newPointCount, Allocator.Persistent);

            // Update estimated capacity for force map
            int estimatedForceCount = newConnectionCount * 2 + newPointCount;
            forceMap = new NativeParallelMultiHashMap<int, float3>(estimatedForceCount, Allocator.Persistent);

            currentPointCount = newPointCount;
            currentConnectionCount = newConnectionCount;

            // Reinitialize masses
            for (int i = 0; i < Mathf.Min(parentSystem.allSpringPoints.Count, newPointCount); i++)
            {
                masses[i] = parentSystem.allSpringPoints[i].mass;
            }
            
        }
    }

    [BurstCompile]
    public struct GravityJob : IJobParallelFor
    {
        public NativeParallelMultiHashMap<int, float3>.ParallelWriter forceMap;

        public float3 gravity;
        public bool applyGravity;
        public NativeArray<float> masses;

        public void Execute(int index)
        {
            if (applyGravity)
            {
                // Add gravity force to each point
                forceMap.Add(index, gravity * masses[index]);
            }
        }
    }

    [BurstCompile]
    struct CalculateForcesJob : IJobParallelFor
    {
        public NativeParallelMultiHashMap<int, float3>.ParallelWriter forceMap;
        [ReadOnly] public NativeArray<float3> velocities;
        [ReadOnly] public NativeArray<float3> positions;
        [ReadOnly] public NativeArray<int2> connections;
        [ReadOnly] public NativeArray<float> restLengths;
        [ReadOnly] public float springConstant;
        [ReadOnly] public float damperConstant;

        // (2) Non-linear spring parameters
        [ReadOnly] public bool useNonLinear;
        [ReadOnly] public float nonLinearFactor;

        public void Execute(int i)
        {
            int2 pts = connections[i];
            float3 p1 = positions[pts.x];
            float3 p2 = positions[pts.y];
            float3 dir = p2 - p1;
            float dist = math.length(dir);
            if (dist > 0f)
            {
                dir /= dist;
                float stretch = dist - restLengths[i];

                float3 springForce;
                if (useNonLinear)
                {
                    // Increase force for larger stretch (stiffening)
                    float scale = 1f + math.abs(stretch) / restLengths[i] * (nonLinearFactor - 1f);
                    springForce = springConstant * stretch * scale * dir;
                }
                else
                {
                    // Linear Hooke's law: F = k * x:contentReference[oaicite:0]{index=0}
                    springForce = springConstant * stretch * dir;
                }

                // Damping (viscous)
                float3 relVel = velocities[pts.y] - velocities[pts.x];
                float velAlong = math.dot(relVel, dir);
                float3 dampingForce = damperConstant * velAlong * dir;

                float3 netForce = springForce + dampingForce;
                forceMap.Add(pts.x, netForce);
                forceMap.Add(pts.y, -netForce);
            }
        }
    }


    [BurstCompile]
    struct AccumulateForcesJob : IJobParallelFor
    {
        [ReadOnly] public NativeParallelMultiHashMap<int, float3> forceMap;
        [WriteOnly] public NativeArray<float3> accumulatedForces;

        public void Execute(int pointIndex)
        {
            float3 totalForce = float3.zero;

            if (forceMap.TryGetFirstValue(pointIndex, out float3 force, out var it))
            {
                do
                {
                    totalForce += force;
                } while (forceMap.TryGetNextValue(out force, ref it));
            }

            accumulatedForces[pointIndex] = totalForce;
        }
    }

    public void ScheduleGravityJobs(float3 gravity, bool applyGravity)
    {
        CheckAndResizeArrays(parentSystem.allSpringPoints.Count, parentSystem.allSpringConnections.Count);
        // Clear the force map
        forceMap.Clear();

        var gravityJob = new GravityJob
        {
            forceMap = forceMap.AsParallelWriter(),

            masses = masses,
            gravity = gravity,
            applyGravity = applyGravity
        };

        gravityJobHandle = gravityJob.Schedule(masses.Length, 64);
    }

    public void ScheduleSpringJobs(float springConstant, float damperConstant)
    {
        CheckAndResizeArrays(parentSystem.allSpringPoints.Count, parentSystem.allSpringConnections.Count);
        // Clear the force buffer by setting each element to zero
        var currentForceBuffer = usingBufferA ? forcesBufferA : forcesBufferB;
        for (int i = 0; i < currentForceBuffer.Length; i++)
        {
            currentForceBuffer[i] = float3.zero;
        }

        // Clear the force map
        //forceMap.Clear();

        // Update positions and velocities from parent system
        for (int i = 0; i < parentSystem.allSpringPoints.Count; i++)
        {
            positions[i] = parentSystem.allSpringPoints[i].position;
            velocities[i] = parentSystem.allSpringPoints[i].velocity;
        }

        var calculateJob = new CalculateForcesJob
        {
            forceMap = forceMap.AsParallelWriter(),

            velocities = velocities,
            positions = positions,

            connections = connections,
            restLengths = restLengths,

            springConstant = springConstant,
            damperConstant = damperConstant,

            // Pass non-linear parameters from simulation
            useNonLinear = parentSystem.useNonLinearSprings,
            nonLinearFactor = parentSystem.nonLinearFactor
        };

        var accumulateJob = new AccumulateForcesJob
        {
            forceMap = forceMap,
            accumulatedForces = currentForceBuffer
        };

        // Schedule with dependency chain
        springJobHandle = calculateJob.Schedule(connections.Length, 64, gravityJobHandle);
        springJobHandle = accumulateJob.Schedule(parentSystem.allSpringPoints.Count, 64, springJobHandle);

        // Switch buffers for next frame
        usingBufferA = !usingBufferA;
    }

    public void CompleteAllJobsAndApply()
    {
        CheckAndResizeArrays(parentSystem.allSpringPoints.Count, parentSystem.allSpringConnections.Count);
        // Complete All jobs
        JobHandle.CombineDependencies(gravityJobHandle, springJobHandle).Complete();

        // Get the buffer we just finished writing to
        var completedForceBuffer = usingBufferA ? forcesBufferB : forcesBufferA;

        // Apply forces to SpringPointTest objects
        for (int i = 0; i < parentSystem.allSpringPoints.Count; i++)
        {
            // Convert float3 to Vector3
            var forceX = completedForceBuffer[i].x;
            var forceY = completedForceBuffer[i].y;
            var forceZ = completedForceBuffer[i].z;
            Vector3 forceVector = new Vector3(forceX, forceY, forceZ);
            parentSystem.allSpringPoints[i].force += forceVector;
        }

        // Clear forces for next frame
        forceMap.Clear();
    }

    public void UpdateConnectionData(List<SpringConnection> connections)
    {
        // Update connection indices and rest lengths
        for (int i = 0; i < connections.Count; i++)
        {
            int index1 = parentSystem.allSpringPoints.IndexOf(connections[i].point1);
            int index2 = parentSystem.allSpringPoints.IndexOf(connections[i].point2);
            this.connections[i] = new int2(index1, index2);
            restLengths[i] = connections[i].restLength;
        }
    }

    private void OnDestroy()
    {
        if (forceMap.IsCreated) forceMap.Dispose();
        if (positions.IsCreated) positions.Dispose();
        if (velocities.IsCreated) velocities.Dispose();

        if (connections.IsCreated) connections.Dispose();
        if (restLengths.IsCreated) restLengths.Dispose();

        if (masses.IsCreated) masses.Dispose();

        if (forcesBufferA.IsCreated) forcesBufferA.Dispose();
        if (forcesBufferB.IsCreated) forcesBufferB.Dispose();
    }
}
