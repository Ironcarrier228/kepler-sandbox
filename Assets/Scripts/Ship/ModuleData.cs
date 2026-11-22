using UnityEngine;

namespace Kepler
{
    public enum ModuleKind { Cockpit, Tank, Engine, Rcs, Warp }

    [CreateAssetMenu(menuName = "Kepler/Ship module")]
    public sealed class ModuleData : ScriptableObject
    {
        public string id, displayName;
        public ModuleKind kind;
        public double dryMass, fuelCapacity, thrust, specificImpulse, torque;
        public Color color = Color.white;
    }
}
