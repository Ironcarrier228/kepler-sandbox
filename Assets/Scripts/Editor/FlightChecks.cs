using System;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Kepler.Editor
{
    public static class FlightChecks
    {
        static void Check(bool ok, string message)
        {
            if (!ok)
                throw new InvalidOperationException("Kepler check: " + message);
        }
        [MenuItem("Tools/Verify Space Game")]
        public static void Verify()
        {
            var f = UnityEngine.Object.FindFirstObjectByType<Flight>();
            Check(f != null, "Сначала откройте сцену SolarFlight (Tools → Build Space Game)");
            Check(f.bodies.Length == 10 && f.globes.Length == 10, "Body catalogue");
            Check(f.rig.distant.GetUniversalAdditionalCameraData().cameraStack.Contains(f.rig.local),
                  "Camera stack");
            Check((f.rig.local.cullingMask & f.rig.distant.cullingMask) == 0, "Camera layers overlap");
            foreach (string id in new[] { "Throttle", "Pitch", "Yaw", "Roll", "Cut", "View", "Target",
                                          "Restart", "Pause" })
                Check(f.controls.FindAction("Flight/" + id, true).bindings.Count >= 2, id);
            var doc = f.hud.GetComponent<UIDocument>();
            Check(doc.panelSettings && doc.panelSettings.themeStyleSheet && doc.visualTreeAsset,
                  "HUD resources");
            var ui = doc.visualTreeAsset.CloneTree();
            Check(ui.Q<Button>("next") != null && ui.Q<Label>("speed") != null, "HUD layout");
            using (var w = new GravityWorld(f.Craft + 1))
            {
                var q = SolarStart.Load(w, f.bodies, f.settings);
                Check(math.abs(math.length(q) - 1) < 1e-12, "Initial attitude");
                int home = f.settings.homeIndex;
                double r = f.bodies[home].radius + f.settings.spawnAltitude;
                Check(math.abs(math.distance(w.Position[f.Craft], w.Position[home]) - r) < .001,
                      "Spawn altitude");
                Check(math.abs(math.distance(w.Velocity[f.Craft], w.Velocity[home]) -
                               math.sqrt(InitialOrbit.G * w.Mass[home] / r)) < 1e-7,
                      "Spawn velocity");
                double3 momentum = 0;
                double m = 0;
                for (int i = 0; i < f.Craft; i++)
                {
                    momentum += w.Mass[i] * w.Velocity[i];
                    m += w.Mass[i];
                    Check(f.bodies[i].parent < i, "Catalogue is not topologically sorted");
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        Check(f.globes[i].GetComponent<Renderer>().sharedMaterial.shader.isSupported,
                              "Unsupported globe shader");
                }
                Check(math.length(momentum / m) < 1e-9, "Barycentric velocity");
                w.SampleGravity();
                for (int i = 0; i < 100; i++)
                    w.Step(.04, 0);
                Check(math.all(math.isfinite(w.Position[f.Craft])), "Non-finite flight state");
            }
            using (var w = new GravityWorld(2))
            {
                const double radius = 6791000;
                w.Mass[0] = 5.9722e24;
                double mu = InitialOrbit.G * w.Mass[0];
                double v = math.sqrt(mu / radius);
                w.Position[1] = new double3(radius, 0, 0);
                w.Velocity[1] = new double3(0, 0, v);
                w.SampleGravity();
                const int steps = 4096;
                double dt = 2 * math.PI_D * radius / v / steps, maxDrift = 0;
                for (int i = 0; i < steps; i++)
                {
                    w.Step(dt, 0);
                    maxDrift = math.max(maxDrift, math.abs(math.length(w.Position[1]) - radius));
                }
                Check(maxDrift / radius < 2e-6, "Circular orbit radius drift");
                Check(math.distance(w.Position[1], new double3(radius, 0, 0)) < radius * 1e-5,
                      "One-period phase error");
                double energy = .5 * math.lengthsq(w.Velocity[1]) - mu / math.length(w.Position[1]);
                Check(math.abs(energy + .5 * v * v) / (v * v) < 1e-9, "Specific energy drift");
            }
            using (var w = new GravityWorld(3))
            {
                // Both primaries must move: a fixed central attractor would fail this check.
                w.Mass[0] = 3e20;
                w.Mass[1] = 7e20;
                w.Position[0] = new double3(2e11, 0, 0);
                w.Position[1] = w.Position[0] + new double3(1e7, 0, 0);
                w.Position[2] = w.Position[0] + new double3(0, 1e8, 0);
                w.SampleGravity();
                w.Step(1, 0);
                Check(w.Velocity[0].x > 0 && w.Velocity[1].x < 0, "Mutual attraction");
                Check(math.length(w.Mass[0] * w.Velocity[0] + w.Mass[1] * w.Velocity[1]) /
                              (w.Mass[0] + w.Mass[1]) <
                          1e-15,
                      "Linear momentum conservation");
            }
            using (var w = new GravityWorld(1))
            {
                w.SampleGravity();
                w.Step(2, new double3(0, 0, 3));
                Check(math.abs(w.Position[0].z - 6) < 1e-12 && math.abs(w.Velocity[0].z - 6) < 1e-12,
                      "Powered flight");
            }
            Debug.Log("Kepler: проверены сцена, управление, стартовое состояние, круговая орбита и тяга.");
        }
        public static void BuildAndVerify()
        {
            SpaceBuilder.Build();
            Verify();
        }
    }
}
