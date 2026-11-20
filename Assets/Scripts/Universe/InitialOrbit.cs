using Unity.Mathematics;
namespace Kepler
{
    public static class InitialOrbit
    {
        public const double G = 6.67430e-11, AU = 149597870700.0;
        // Rotate from perifocal to ecliptic, then map ecliptic XY into Unity XZ.
        static double3 Frame(double x, double y, double node, double inc, double arg)
        {
            double u = x * math.cos(arg) - y * math.sin(arg), v = x * math.sin(arg) + y * math.cos(arg);
            return new double3(u * math.cos(node) - v * math.cos(inc) * math.sin(node), v * math.sin(inc),
                               u * math.sin(node) + v * math.cos(inc) * math.cos(node));
        }
        public static void State(BodyData b, double parentMass, out double3 r, out double3 v)
        {
            double e = b.eccentricity, a = b.semiMajorAxis, m = math.radians(b.meanLongitude - b.periLongitude),
                   E = m;
            for (int i = 0; i < 12; i++)
                E -= (E - e * math.sin(E) - m) / (1 - e * math.cos(E));
            double n = math.radians(b.node), inc = math.radians(b.inclination),
                   w = math.radians(b.periLongitude - b.node);
            double s = math.sqrt(1 - e * e),
                   speed = math.sqrt(G * (parentMass + b.mass) / a) / (1 - e * math.cos(E));
            r = Frame(a * (math.cos(E) - e), a * s * math.sin(E), n, inc, w);
            v = Frame(-speed * math.sin(E), speed * s * math.cos(E), n, inc, w);
        }
    }
}
