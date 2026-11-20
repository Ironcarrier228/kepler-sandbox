using Unity.Mathematics;
using UnityEngine;
namespace Kepler
{
    // Unit quaternion stored as (x,y,z,w) in double. Body axes are principal axes at COM.
    public sealed class Helm
    {
        public double4 q = new double4(0, 0, 0, 1);
        public double3 omega;
        public double throttle;
        public void Tick(ShipData spec, double3 command, double lever, double dt)
        {
            throttle = math.clamp(throttle + lever * dt * 0.6, 0, 1);
            double3 I = spec.inertia;
            double3 torque =
                math.clamp(command * spec.torque - omega * I * spec.damping, -spec.torque, spec.torque);
            omega += (torque - math.cross(omega, I * omega)) / I * dt;
            double angle = math.length(omega) * dt;
            double3 v = math.normalizesafe(omega) * math.sin(angle * 0.5);
            double w = math.cos(angle * 0.5);
            q = math.normalize(
                new double4(q.w * v + w * q.xyz + math.cross(q.xyz, v), q.w * w - math.dot(q.xyz, v)));
        }
        public double3 Forward
        {
            get {
                double3 z = new double3(0, 0, 1);
                return z + 2 * math.cross(q.xyz, math.cross(q.xyz, z) + q.w * z);
            }
        }
        public Quaternion Rotation => new Quaternion((float)q.x, (float)q.y, (float)q.z, (float)q.w);
    }
}
