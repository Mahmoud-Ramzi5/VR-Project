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
    private NativeArray<float> springConstants;
    private NativeArray<float> damperConstants;
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
        parentSystem = parent;

        velocities = new NativeArray<float3>(pointCount, Allocator.Persistent);
        positions = new NativeArray<float3>(pointCount, Allocator.Persistent);

        connections = new NativeArray<int2>(connectionCount, Allocator.Persistent);
        springConstants = new NativeArray<float>(connectionCount, Allocator.Persistent);
        damperConstants = new NativeArray<float>(connectionCount, Allocator.Persistent);
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
        [ReadOnly] public NativeArray<float> springConstants;
        [ReadOnly] public NativeArray<float> damperConstants;
        [ReadOnly] public NativeArray<float> restLengths;

        public void Execute(int connectionIndex)
        {
            int2 points = connections[connectionIndex];
            float3 position1 = positions[points.x];
            float3 position2 = positions[points.y];

            float3 direction = position2 - position1;
            float distance = math.length(direction);
            if (distance > 0)
            {
                direction = direction / distance;
                // Calculate spring force using Hooke's Law
                float stretch = distance - restLengths[connectionIndex];
                float3 springForce = springConstants[connectionIndex] * stretch * direction;

                // Apply damping to prevent sliding at higher speeds
                float3 relativeVel = velocities[points.y] - velocities[points.x];
                float velocityAlongSpring = math.dot(relativeVel, direction);
                float3 dampingForce = damperConstants[connectionIndex] * velocityAlongSpring * direction;

                // Combine forces
                float3 netForce = springForce + dampingForce;

                // Add forces to the map
                forceMap.Add(points.x, netForce);
                forceMap.Add(points.y, -netForce);
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

    public void ScheduleSpringJobs()
    {
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

            springConstants = springConstants,
            damperConstants = damperConstants,
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
            springConstants[i] = connections[i].springConstant;
            damperConstants[i] = connections[i].damperConstant;
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
