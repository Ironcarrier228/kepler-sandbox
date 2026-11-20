using UnityEngine;

namespace Kepler
{
    [CreateAssetMenu(menuName = "Kepler/Celestial body")]
    public sealed class BodyData : ScriptableObject
    {
        public string displayName;
        public double mass, radius, semiMajorAxis, eccentricity;
        // J2000 mean elements, degrees, relative to parent. Index -1 = system root.
        public int parent;
        public double inclination, node, periLongitude, meanLongitude;
        public Color tint;
    }
}
