using System;
using System.Collections.Generic;
using System.Drawing;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class RigidJobManager : MonoBehaviour
{
    // NativeMultiHashMap was not found
    private NativeParallelMultiHashMap<int, float3> forceMap;
    private NativeParallelMultiHashMap<int, float3> correctionMap;

    private NativeArray<float3> velocities;
    private NativeArray<float3> positions;
    private NativeArray<bool> isFixed;

    private NativeArray<int2> connections;
    private NativeArray<float> springConstants;
    private NativeArray<float> damperConstants;
    private NativeArray<float> restLengths;

    private NativeArray<float> masses;

    // Double buffered forces
    private NativeArray<float3> forcesBufferA;
    private NativeArray<float3> forcesBufferB;
    private bool usingForceBufferA;

    // Double buffered velocities
    private NativeArray<float3> velocitiesBufferA;
    private NativeArray<float3> velocitiesBufferB;
    private bool usingVelocityBufferA;

    // Double buffered positions
    private NativeArray<float3> positionsBufferA;
    private NativeArray<float3> positionsBufferB;
    private bool usingPositionBufferA;

    private JobHandle gravityJobHandle;
    private JobHandle rigidJobHandle;

    // Reference to the parent system
    private OctreeSpringFiller parentSystem;

    public void InitializeArrays(OctreeSpringFiller parent, int pointCount, int connectionCount)
    {
        parentSystem = parent;

        velocities = new NativeArray<float3>(pointCount, Allocator.Persistent);
        positions = new NativeArray<float3>(pointCount, Allocator.Persistent);
        isFixed = new NativeArray<bool>(pointCount, Allocator.Persistent);
        masses = new NativeArray<float>(pointCount, Allocator.Persistent);

        connections = new NativeArray<int2>(connectionCount, Allocator.Persistent);
        springConstants = new NativeArray<float>(connectionCount, Allocator.Persistent);
        damperConstants = new NativeArray<float>(connectionCount, Allocator.Persistent);
        restLengths = new NativeArray<float>(connectionCount, Allocator.Persistent);

        // Initialize both force buffers
        forcesBufferA = new NativeArray<float3>(pointCount, Allocator.Persistent);
        forcesBufferB = new NativeArray<float3>(pointCount, Allocator.Persistent);
        usingForceBufferA = true;

        // Initialize both velocity buffers
        velocitiesBufferA = new NativeArray<float3>(pointCount, Allocator.Persistent);
        velocitiesBufferB = new NativeArray<float3>(pointCount, Allocator.Persistent);
        usingVelocityBufferA = true;

        // Initialize both position buffers
        positionsBufferA = new NativeArray<float3>(pointCount, Allocator.Persistent);
        positionsBufferB = new NativeArray<float3>(pointCount, Allocator.Persistent);
        usingPositionBufferA = true;

        // Fill fixed and mass
        for (int i = 0; i < parent.allSpringPoints.Count; i++)
        {
            isFixed[i] = parent.allSpringPoints[i].isFixed;
            masses[i] = parent.allSpringPoints[i].mass;
        }

        // Initialize force map with estimated capacity
        int estimatedForceCount = connectionCount * 2 + pointCount;
        forceMap = new NativeParallelMultiHashMap<int, float3>(estimatedForceCount, Allocator.Persistent);
        correctionMap = new NativeParallelMultiHashMap<int, float3>(estimatedForceCount, Allocator.Persistent);
    }

    [BurstCompile]
    public struct GravityJob : IJobParallelFor
    {
        public NativeParallelMultiHashMap<int, float3>.ParallelWriter forceMap;

        [ReadOnly] public float3 gravity;
        [ReadOnly] public bool applyGravity;
        [ReadOnly] public NativeArray<float> masses;

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
    struct IntegrateForcesJob : IJobParallelFor
    {
        [ReadOnly] public NativeParallelMultiHashMap<int, float3> forceMap;
        [ReadOnly] public NativeArray<bool> isFixed;
        [ReadOnly] public NativeArray<float> masses;
        [ReadOnly] public float deltaTime;

        public NativeArray<float3> forces;
        public NativeArray<float3> velocities;
        public NativeArray<float3> positions;


        public void Execute(int pointIndex)
        {
            if (isFixed[pointIndex]) return; // Skip fixed points

            float3 totalForce = float3.zero;
            if (forceMap.TryGetFirstValue(pointIndex, out float3 force, out var it))
            {
                do
                {
                    totalForce += force;
                } while (forceMap.TryGetNextValue(out force, ref it));
            }

            forces[pointIndex] = totalForce;
            float3 acceleration = totalForce / masses[pointIndex];

            // Update velocity and predictedPosition
            velocities[pointIndex] += acceleration * deltaTime;
            positions[pointIndex] += velocities[pointIndex] * deltaTime;
        }
    }

    [BurstCompile]
    struct RigidConstraintJob : IJobParallelFor
    {
        public NativeParallelMultiHashMap<int, float3>.ParallelWriter correctionMap;

        [ReadOnly] public NativeArray<float3> positions;
        [ReadOnly] public NativeArray<bool> isFixed;

        [ReadOnly] public NativeArray<int2> connections;
        [ReadOnly] public NativeArray<float> restLengths;

        public void Execute(int connectionIndex)
        {
            int2 points = connections[connectionIndex];
            float3 position1 = positions[points.x];
            float3 position2 = positions[points.y];

            float3 direction = position2 - position1;
            float distance = math.length(direction);

            if (distance < 1e-6f || float.IsNaN(distance)) return;

            direction = direction / distance;
            float stretch = distance - restLengths[connectionIndex];
            float3 correction = direction * (stretch * 0.5f);

            // Add corrections with conditions
            if (!isFixed[points.x] && !isFixed[points.y])
            {
                correctionMap.Add(points.x, correction);
                correctionMap.Add(points.y, -correction);
            }
            else if (!isFixed[points.x])
            {
                correctionMap.Add(points.x, correction * 2f);
            }
            else if (!isFixed[points.y])
            {
                correctionMap.Add(points.y, -correction * 2f);
            }
        }
    }

    [BurstCompile]
    struct RigidCorrectionJob : IJobParallelFor
    {
        [ReadOnly] public NativeParallelMultiHashMap<int, float3> correctionMap;
        [ReadOnly] public NativeArray<float3> integratedPositions; // Add this
        [ReadOnly] public NativeArray<float3> originalPositions;
        [WriteOnly] public NativeArray<float3> velocities;

        public NativeArray<float3> NewPositions;
        [ReadOnly] public float deltaTime;

        public void Execute(int pointIndex)
        {
            float3 totalCorrection = float3.zero;

            if (correctionMap.TryGetFirstValue(pointIndex, out float3 correction, out var it))
            {
                do
                {
                    totalCorrection += correction;
                } while (correctionMap.TryGetNextValue(out correction, ref it));
            }

            NewPositions[pointIndex] = integratedPositions[pointIndex] + totalCorrection;
            velocities[pointIndex] = (NewPositions[pointIndex] - originalPositions[pointIndex]) / deltaTime;
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

    public void ScheduleRigidJobs(float deltaTime)
    {
        // Clear the force buffer by setting each element to zero
        var currentForceBuffer = usingForceBufferA ? forcesBufferA : forcesBufferB; 
        for (int i = 0; i < currentForceBuffer.Length; i++)
        {
            currentForceBuffer[i] = float3.zero;
        }

        // Update positions and velocities from parent system
        var currentVelocityBuffer = usingVelocityBufferA ? velocitiesBufferA : velocitiesBufferB;
        var currentPositionBuffer = usingPositionBufferA ? positionsBufferA : positionsBufferB;
        for (int i = 0; i < parentSystem.allSpringPoints.Count; i++)
        {
            // Update positions and velocities
            velocities[i] = parentSystem.allSpringPoints[i].velocity;
            positions[i] = parentSystem.allSpringPoints[i].position;

            // Update all buffers to maintain consistency
            forcesBufferA[i] = (float3)parentSystem.allSpringPoints[i].force;
            forcesBufferB[i] = (float3)parentSystem.allSpringPoints[i].force;
            velocitiesBufferA[i] = (float3)parentSystem.allSpringPoints[i].velocity;
            velocitiesBufferB[i] = (float3)parentSystem.allSpringPoints[i].velocity;
            positionsBufferA[i] = (float3)parentSystem.allSpringPoints[i].position;
            positionsBufferB[i] = (float3)parentSystem.allSpringPoints[i].position;
        }

        var integrateJob = new IntegrateForcesJob
        {
            forceMap = forceMap,
            isFixed = isFixed,
            masses = masses,
            deltaTime = deltaTime,
            forces = currentForceBuffer,
            velocities = currentVelocityBuffer,
            positions = currentPositionBuffer,
        };

        var constraintJob = new RigidConstraintJob
        {
            correctionMap = correctionMap.AsParallelWriter(),

            positions = currentPositionBuffer,
            isFixed = isFixed,

            connections = connections,
            restLengths = restLengths,
        };

        var correctionJob = new RigidCorrectionJob
        {
            correctionMap = correctionMap,
            originalPositions = positions,

            integratedPositions = currentPositionBuffer,
            //NewPositions = currentPositionBuffer,
            velocities = currentVelocityBuffer,
            deltaTime = deltaTime
        };

        // Save forces
        rigidJobHandle = integrateJob.Schedule(parentSystem.allSpringPoints.Count, 64, gravityJobHandle);

        // Apply constraint
        rigidJobHandle = constraintJob.Schedule(connections.Length, 64, rigidJobHandle);
        rigidJobHandle = correctionJob.Schedule(parentSystem.allSpringPoints.Count, 64, rigidJobHandle);
    }

    public void CompleteAllJobsAndApply(float deltaTime)
    {
        // Complete All jobs
        JobHandle.CombineDependencies(gravityJobHandle, rigidJobHandle).Complete();

        // Get all buffers we finished writing to
        var completedForceBuffer = usingForceBufferA ? forcesBufferA : forcesBufferB;
        var completedVelocityBuffer = usingVelocityBufferA ? velocitiesBufferA : velocitiesBufferB;
        var completedPositionBuffer = usingPositionBufferA ? positionsBufferA : positionsBufferB;

        // Apply forces to SpringPointTest objects
        for (int i = 0; i < parentSystem.allSpringPoints.Count; i++)
        {
            // Convert float3 to Vector3
            var forceX = completedForceBuffer[i].x;
            var forceY = completedForceBuffer[i].y;
            var forceZ = completedForceBuffer[i].z;
            Vector3 forceVector = new Vector3(forceX, forceY, forceZ);
            parentSystem.allSpringPoints[i].force = forceVector;

            if (!parentSystem.allSpringPoints[i].isFixed)
            {
                var velocityX = completedVelocityBuffer[i].x;
                var velocityY = completedVelocityBuffer[i].y;
                var velocityZ = completedVelocityBuffer[i].z;
                Vector3 velocityVector = new Vector3(velocityX, velocityY, velocityZ);
                parentSystem.allSpringPoints[i].velocity = velocityVector;

                var positionX = completedPositionBuffer[i].x;
                var positionY = completedPositionBuffer[i].y;
                var positionZ = completedPositionBuffer[i].z;
                Vector3 positionVector = new Vector3(positionX, positionY, positionZ);
                parentSystem.allSpringPoints[i].position = positionVector;
            }
        }

        // Clear forces for next frame
        forceMap.Clear();
        correctionMap.Clear();

        // Switch buffers for next frame
        usingForceBufferA = !usingForceBufferA;
        usingVelocityBufferA = !usingVelocityBufferA;
        usingPositionBufferA = !usingPositionBufferA;
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

        if (isFixed.IsCreated) isFixed.Dispose();
        if (masses.IsCreated) masses.Dispose();

        if (forcesBufferA.IsCreated) forcesBufferA.Dispose();
        if (forcesBufferB.IsCreated) forcesBufferB.Dispose();

        if (velocitiesBufferA.IsCreated) velocitiesBufferA.Dispose();
        if (velocitiesBufferB.IsCreated) velocitiesBufferB.Dispose();

        if (positionsBufferA.IsCreated) positionsBufferA.Dispose();
        if (positionsBufferB.IsCreated) positionsBufferB.Dispose();
    }
}
