using Unity.Mathematics;
using UnityEngine;
namespace Kepler
{
    public static class SolarStart
    {
        public static double4 Load(GravityWorld world, BodyData[] bodies, FlightSettings settings)
        {
            int craft = bodies.Length, home = settings.homeIndex;
            for (int i = 0; i < craft; i++)
            {
                var b = bodies[i];
                int p = b.parent;
                double3 r = 0, v = 0;
                if (p >= 0)
                {
                    InitialOrbit.State(b, bodies[p].mass, out r, out v);
                    r += world.Position[p];
                    v += world.Velocity[p];
                }
                world.Position[i] = r;
                world.Velocity[i] = v;
                world.Mass[i] = b.mass;
            }
            // Put the origin at the system barycentre, not a pinned Sun.
            double3 com = 0, drift = 0;
            double total = 0;
            for (int i = 0; i < craft; i++)
            {
                com += world.Mass[i] * world.Position[i];
                drift += world.Mass[i] * world.Velocity[i];
                total += world.Mass[i];
            }
            for (int i = 0; i < craft; i++)
            {
                world.Position[i] -= com / total;
                world.Velocity[i] -= drift / total;
            }
            var up = math.normalize(world.Position[0] - world.Position[home]);
            var pro = math.normalize(math.cross(new double3(0, 1, 0), up));
            double radius = bodies[home].radius + settings.spawnAltitude;
            world.Position[craft] = world.Position[home] + radius * up;
            world.Velocity[craft] =
                world.Velocity[home] + pro * math.sqrt(InitialOrbit.G * bodies[home].mass / radius);
            world.Mass[craft] = 0; // test particle; inertial mass still controls thrust
            Quaternion q = Quaternion.LookRotation((Vector3)(float3)(pro - 0.22 * up), (Vector3)(float3)up);
            return math.normalize(new double4(q.x, q.y, q.z, q.w));
        }
    }
}
