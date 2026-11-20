using UnityEngine;
namespace Kepler
{
    public sealed class FlightRig : MonoBehaviour
    {
        static readonly int SolPosition = Shader.PropertyToID("_KeplerSolPosition");
        public const double Scale = 1e-6; // one background unit = 1000 km
        public Camera distant, local;
        public Light sun;
        public bool Survey { get; private set; }
        public void ResetView()
        {
            Survey = false;
        }
        public void SwitchView()
        {
            Survey = !Survey;
        }
        public void RenderFrame(Flight f)
        {
            Quaternion rot = f.ship.rotation;
            Vector3 pos = rot * new Vector3(0, 2.6f, -10);
            if (Survey)
            {
                var dir = f.globes[f.Target].position;
                if (dir.sqrMagnitude > 0)
                    rot = Quaternion.LookRotation(dir, rot * Vector3.up);
                pos = rot * new Vector3(0, 0, 6);
            }
            else
                rot *= Quaternion.Euler(8, 0, 0);
            local.transform.SetPositionAndRotation(pos, rot);
            distant.transform.SetPositionAndRotation(pos * (float)Scale, rot);
            // Tighten the depth range, especially on GPUs without reversed Z.
            float surface = float.MaxValue;
            for (int i = 0; i < f.Craft; i++)
                surface =
                    Mathf.Min(surface, Vector3.Distance(distant.transform.position, f.globes[i].position) -
                                           (float)(f.bodies[i].radius * Scale));
            distant.nearClipPlane = Mathf.Clamp(surface * .4f, .00001f, 10000f);
            Shader.SetGlobalVector(SolPosition, f.globes[0].position);
            sun.transform.rotation = Quaternion.LookRotation(-f.globes[0].position.normalized);
        }
    }
}
