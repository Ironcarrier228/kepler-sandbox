using UnityEngine;

namespace Kepler.Parts
{
    public enum TankShape { Cylinder, Sphere, Domed, RcsBottle }

    // Dimensions are in metres, fuel volume in cubic metres and masses in kilograms.
    [CreateAssetMenu(menuName = "Kepler/Parts/Tank")]
    public sealed class TankDefinition : ScriptableObject
    {
        public string id, displayName;
        public TankShape shape;
        public float radius, height;
        public double volumeM3, dryMassKg, fuelMassKg;
        public GameObject prefab;
    }
}
