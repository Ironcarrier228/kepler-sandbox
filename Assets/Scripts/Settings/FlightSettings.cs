using UnityEngine;
namespace Kepler
{
    [CreateAssetMenu(menuName = "Kepler/Flight settings")]
    public sealed class FlightSettings : ScriptableObject
    {
        [Range(0.01f, 0.1f)]
        public float stepSeconds = 0.04f;
        [Range(1, 12)]
        public int maxStepsPerFrame = 6;
        [Min(1)]
        public double spawnAltitude = 420000;
        [Range(1, 9)]
        public int homeIndex = 3;
        public int skySeed = 228;
    }
}
