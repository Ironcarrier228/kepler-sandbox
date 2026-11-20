using UnityEngine;
namespace Kepler.Editor
{
    internal static class SolarCatalogue
    {
        // a [AU], e, i, mean longitude, longitude of perihelion and node [degrees].
        // JPL approximate planetary elements for 1800-2050, evaluated at J2000.
        static readonly(string id, string name, double mass, double radius, double a, double e, double i,
                        double L, double peri, double node, string color)[] rows = {
            ("Sun", "Солнце", 1.98847e30, 695700000, 0, 0, 0, 0, 0, 0, "FFD492"),
            ("Mercury", "Меркурий", 3.3011e23, 2439700, .38709927, .20563593, 7.00497902, 252.25032350,
             77.45779628, 48.33076593, "B5A79C"),
            ("Venus", "Венера", 4.8675e24, 6051800, .72333566, .00677672, 3.39467605, 181.97909950,
             131.60246718, 76.67984255, "E5BE86"),
            ("Earth", "Земля", 5.9722e24, 6371000, 1.00000261, .01671123, -.00001531, 100.46457166,
             102.93768193, 0, "538DB1"),
            ("Mars", "Марс", 6.4171e23, 3389500, 1.52371034, .09339410, 1.84969142, -4.55343205, -23.94362959,
             49.55953891, "CC7852"),
            ("Jupiter", "Юпитер", 1.8982e27, 69911000, 5.20288700, .04838624, 1.30439695, 34.39644051,
             14.72847983, 100.47390909, "DDBB98"),
            ("Saturn", "Сатурн", 5.6834e26, 58232000, 9.53667594, .05386179, 2.48599187, 49.95424423,
             92.59887831, 113.66242448, "DBC999"),
            ("Uranus", "Уран", 8.681e25, 25362000, 19.18916464, .04725744, .77263783, 313.23810451,
             170.95427630, 74.01692503, "9BDAD9"),
            ("Neptune", "Нептун", 1.02413e26, 24622000, 30.06992276, .00859048, 1.77004347, -55.12002969,
             44.96476227, 131.78422574, "688FD7"),
            ("Moon", "Луна", 7.342e22, 1737400, 384400000 / InitialOrbit.AU, .0549, 5.145, 218.316, 83.353,
             125.045, "B1ACA5")
        };
        public static BodyData[] Create()
        {
            var data = new BodyData[rows.Length];
            for (int i = 0; i < data.Length; i++)
            {
                var r = rows[i];
                var b = SpaceBuilder.Asset<BodyData>("Bodies/" + r.id);
                data[i] = b;
                b.displayName = r.name;
                b.parent = i == 0 ? -1 : i == 9 ? 3 : 0;
                b.mass = r.mass;
                b.radius = r.radius;
                b.semiMajorAxis = r.a * InitialOrbit.AU;
                b.eccentricity = r.e;
                b.inclination = r.i;
                b.meanLongitude = r.L;
                b.periLongitude = r.peri;
                b.node = r.node;
                ColorUtility.TryParseHtmlString("#" + r.color, out b.tint);
            }
            return data;
        }
    }
}
