using UnityEngine;

namespace Kepler.Parts
{
    // Local +Y points outwards; compatible nodes must face each other.
    public sealed class AttachNode : MonoBehaviour
    {
        public string nodeId;
        public string size = "standard";
    }
}
