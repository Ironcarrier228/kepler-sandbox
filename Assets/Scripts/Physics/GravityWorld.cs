using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Kepler
{
    [BurstCompile(FloatMode = FloatMode.Strict)]
    public struct GravityJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<double3> pos;
        [ReadOnly]
        public NativeArray<double> mass;
        [WriteOnly]
        public NativeArray<double3> acc;
        public void Execute(int i)
        {
            double3 a = 0;
            for (int j = 0; j < pos.Length; j++)
            {
                if (i == j || mass[j] == 0)
                    continue;
                double3 d = pos[j] - pos[i];
                double inv = math.rsqrt(math.max(1.0, math.lengthsq(d)));
                a += d * (InitialOrbit.G * mass[j] * inv * inv * inv);
            }
            acc[i] = a;
        }
    }
    public sealed class GravityWorld : IDisposable
    {
        public NativeArray<double3> Position, Velocity;
        public NativeArray<double> Mass;
        NativeArray<double3> acc;
        public GravityWorld(int count)
        {
            Position = new(count, Allocator.Persistent);
            Velocity = new(count, Allocator.Persistent);
            Mass = new(count, Allocator.Persistent);
            acc = new(count, Allocator.Persistent);
        }
        public void SampleGravity()
        {
            var job = new GravityJob { pos = Position, mass = Mass, acc = acc };
            // Eleven states in the starter scene: dispatching to workers costs more than the loop.
            if (Position.Length < 64)
                job.Run(Position.Length);
            else
                job.Schedule(Position.Length, 8).Complete();
        }
        public void Step(double dt, double3 thrustAcceleration)
        {
            int ship = Position.Length - 1;
            for (int i = 0; i < Position.Length; i++)
            {
                Velocity[i] += (acc[i] + (i == ship ? thrustAcceleration : double3.zero)) * (0.5 * dt);
                Position[i] += Velocity[i] * dt;
            }
            SampleGravity();
            for (int i = 0; i < Position.Length; i++)
                Velocity[i] += (acc[i] + (i == ship ? thrustAcceleration : double3.zero)) * (0.5 * dt);
        }
        public void Dispose()
        {
            acc.Dispose();
            Mass.Dispose();
            Velocity.Dispose();
            Position.Dispose();
        }
    }
}
