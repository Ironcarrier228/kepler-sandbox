using UnityEngine;
using UnityEngine.InputSystem;
namespace Kepler
{
    public sealed class FlightRig : MonoBehaviour
    {
        static readonly int SolPosition = Shader.PropertyToID("_KeplerSolPosition");
        public const double Scale = 1e-6; // one background unit = 1000 km
        public Camera distant, local;
        public Light sun;
        public bool Survey { get; private set; }
        float orbitYaw, orbitPitch, distance = 10f;
        public float MouseSensitivity { get; set; } = 0.18f;
        public void ResetView()
        {
            Survey = false;
            orbitYaw = orbitPitch = 0;
            distance = 10f;
        }
        public void SwitchView()
        {
            Survey = !Survey;
        }
        public void RenderFrame(Flight f)
        {
            var mouse = Mouse.current;
            if (mouse != null && !f.hud.IsOverlayOpen)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    orbitYaw += delta.x * MouseSensitivity;
                    orbitPitch = Mathf.Clamp(orbitPitch - delta.y * MouseSensitivity, -80f, 80f);
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0) distance = Mathf.Clamp(distance * Mathf.Exp(-scroll * .001f), 4f, 80f);
            }
            Quaternion rot = f.ship.rotation * Quaternion.Euler(orbitPitch + 8, orbitYaw, 0);
            Vector3 pos = rot * new Vector3(0, 2.6f, -distance);
            if (Survey)
            {
                var dir = f.globes[f.Target].position;
                if (dir.sqrMagnitude > 0)
                    rot = Quaternion.LookRotation(dir, f.ship.up) * Quaternion.Euler(orbitPitch, orbitYaw, 0);
                pos = rot * new Vector3(0, 0, 6);
            }

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
