using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kepler
{
    public sealed class Flight : MonoBehaviour
    {
        public FlightSettings settings;
        public ShipData vessel;
        public BodyData[] bodies;
        public Transform[] globes;
        public Transform ship, plume;
        public FlightRig rig;
        public FlightHud hud;
        public InputActionAsset controls;
        public GravityWorld World { get; private set; }
        public Helm Pilot { get; private set; }
        public int Craft => bodies.Length;
        public int Target { get; private set; }
        public int Nearest { get; private set; }
        public bool Paused { get; private set; }
        public bool Impact { get; private set; }
        public double Clock { get; private set; }
        public double Altitude =>
            math.distance(World.Position[Craft], World.Position[Nearest]) - bodies[Nearest].radius;
        Quaternion previousRotation;
        double accumulator;
        double3[] previous;
        InputAction pitch, yaw, roll, throttle, cut, pause, reset, target, view;
        InputActionMap map;
        void Awake()
        {
            World = new GravityWorld(Craft + 1);
            previous = new double3[Craft + 1];
            map = controls.FindActionMap("Flight", true);
            pitch = map["Pitch"];
            yaw = map["Yaw"];
            roll = map["Roll"];
            throttle = map["Throttle"];
            cut = map["Cut"];
            pause = map["Pause"];
            reset = map["Restart"];
            target = map["Target"];
            view = map["View"];
            Restart();
        }
        void OnEnable()
        {
            map?.Enable();
        }
        void OnDisable()
        {
            map?.Disable();
        }
        void OnDestroy()
        {
            World?.Dispose();
        }
        public void Restart()
        {
            int home = settings.homeIndex;
            Target = Nearest = home;
            Pilot = new Helm();
            Pilot.q = SolarStart.Load(World, bodies, settings);
            previousRotation = Pilot.Rotation;
            for (int i = 0; i < previous.Length; i++)
                previous[i] = World.Position[i];
            World.SampleGravity();
            Clock = accumulator = 0;
            Paused = Impact = false;
            rig.ResetView();
        }
        public void NextTarget()
        {
            Target = (Target + 1) % Craft;
        }
        public void TogglePause()
        {
            if (!Impact)
                Paused = !Paused;
        }
        void Update()
        {
            if (reset.WasPressedThisFrame())
                Restart();
            if (pause.WasPressedThisFrame())
                TogglePause();
            if (target.WasPressedThisFrame())
                NextTarget();
            if (view.WasPressedThisFrame())
                rig.SwitchView();
            if (cut.WasPressedThisFrame())
                Pilot.throttle = 0;
            double dt = settings.stepSeconds;
            if (!Paused)
                accumulator = math.min(accumulator + Time.unscaledDeltaTime, dt * settings.maxStepsPerFrame);
            double3 command =
                new double3(pitch.ReadValue<float>(), yaw.ReadValue<float>(), roll.ReadValue<float>());
            while (!Paused && accumulator >= dt)
            {
                previousRotation = Pilot.Rotation;
                Pilot.Tick(vessel, command, throttle.ReadValue<float>(), dt);
                for (int i = 0; i < previous.Length; i++)
                    previous[i] = World.Position[i];
                World.Step(dt, Pilot.Forward * (Pilot.throttle * vessel.engine.thrustNewtons / vessel.mass));
                accumulator -= dt;
                Clock += dt;
                double min = double.MaxValue;
                for (int i = 0; i < Craft; i++)
                {
                    double h = math.distance(World.Position[Craft], World.Position[i]) - bodies[i].radius;
                    if (h < min)
                    {
                        Nearest = i;
                        min = h;
                    }
                }
                if (min <= 0)
                {
                    Paused = Impact = true;
                }
            }
            double alpha = Impact ? 1 : accumulator / dt;
            ship.SetPositionAndRotation(Vector3.zero,
                                        Quaternion.Slerp(previousRotation, Pilot.Rotation, (float)alpha));
            bool burning = Pilot.throttle > 0;
            if (plume.gameObject.activeSelf != burning)
                plume.gameObject.SetActive(burning);
            if (burning)
                plume.localScale = new Vector3(1, 1, (float)Pilot.throttle);
            for (int i = 0; i < Craft; i++)
            {
                // Subtract the ship in double BEFORE scaling/casting; interpolate relative coordinates.
                var relative =
                    math.lerp(previous[i] - previous[Craft], World.Position[i] - World.Position[Craft], alpha);
                globes[i].position = (Vector3)(float3)(relative * FlightRig.Scale);
            }
            rig.RenderFrame(this);
        }
    }
}
