using UnityEngine;
using Unity.Mathematics;
namespace Kepler
{
    [CreateAssetMenu(menuName = "Kepler/Ship")]
    public sealed class ShipData : ScriptableObject
    {
        public EngineData engine;
        [Min(1)]
        public double mass = 14000;
        public double3 inertia = new double3(85000, 95000, 56000);
        [Min(0)]
        public double torque = 90000, damping = 1.8;
        void OnValidate()
        {
            inertia = math.max(inertia, new double3(1));
        }
    }
}
