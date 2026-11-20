using UnityEngine;
namespace Kepler
{
    [CreateAssetMenu(menuName = "Kepler/Engine")]
    public sealed class EngineData : ScriptableObject
    {
        [Min(1)]
        public float thrustNewtons = 180000;
    }
}
