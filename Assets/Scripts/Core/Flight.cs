using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kepler
{
    public sealed class Flight : MonoBehaviour
    {
        public FlightSettings settings;
        public ShipData vessel;
        public ModuleData[] modules;
        public ShipAssembly Assembly { get; private set; }
        Transform assembledVisual;
        public double CurrentThrust { get; private set; }
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
        // Gameplay-only warp speed, not an implementation of relativity.
        public const double WarpSpeed = 10000000000.0;
        public bool Warping { get; private set; }
        public double WarpDuration { get; private set; }
        public double WarpElapsed { get; private set; }
        public double WarpRemaining => math.max(0, WarpDuration - WarpElapsed);
        int warpTarget;
        double3 warpStart, warpDirection;
        public void SelectTarget(int index)
        {
            if (index >= 0 && index < Craft && !Warping) Target = index;
        }
        public double WarpFuelCost(int index)
        {
            double distance = math.distance(World.Position[Craft], World.Position[index]);
            return 50 + distance / 1e10 * .1;
        }
        public bool CanWarp => Assembly.HasWarp && Assembly.Fuel >= WarpFuelCost(Target) && !Warping && !Paused && !Impact;
        public bool StartWarp()
        {
            if (!CanWarp) return false;
            Assembly.TakeFuel(WarpFuelCost(Target));
            warpTarget = Target;
            warpStart = World.Position[Craft];
            warpDirection = math.normalizesafe(warpStart - World.Position[warpTarget], new double3(1, 0, 0));
            double3 destination = WarpDestination();
            WarpDuration = math.max(settings.stepSeconds, math.distance(warpStart, destination) / WarpSpeed);
            WarpElapsed = 0;
            Pilot.throttle = 0;
            Warping = true;
            return true;
        }
        double3 WarpDestination() => World.Position[warpTarget] + warpDirection *
            (bodies[warpTarget].radius + math.max(420000.0, bodies[warpTarget].radius * 0.1));
        void AdvanceWarp(double dt)
        {
            WarpElapsed = math.min(WarpDuration, WarpElapsed + dt);
            World.Position[Craft] = math.lerp(warpStart, WarpDestination(), WarpElapsed / WarpDuration);
            if (WarpElapsed >= WarpDuration)
            {
                // Place the craft on a circular orbit in a plane perpendicular to its arrival radius.
                double3 tangent = math.normalizesafe(math.cross(new double3(0, 1, 0), warpDirection));
                if (math.lengthsq(tangent) < 0.5) tangent = math.normalize(math.cross(new double3(1, 0, 0), warpDirection));
                double radius = math.distance(World.Position[Craft], World.Position[warpTarget]);
                World.Velocity[Craft] = World.Velocity[warpTarget] + tangent *
                    math.sqrt(InitialOrbit.G * bodies[warpTarget].mass / radius);
                Warping = false;
            }
            World.SampleGravity();
        }
        public double Altitude =>
            math.distance(World.Position[Craft], World.Position[Nearest]) - bodies[Nearest].radius;
        Quaternion previousRotation;
        double accumulator;
        double3[] previous;
        InputAction pitch, yaw, roll, throttle, cut, pause, reset, target, view;
        InputActionMap map;
        void Awake()
        {
            Assembly = new ShipAssembly(modules);
            BuildShipVisual();
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
        public void ApplyLayout(ShipLayout layout)
        {
            Assembly.Apply(layout, true);
            BuildShipVisual();
            Restart();
        }
        void BuildShipVisual()
        {
            if (assembledVisual) Destroy(assembledVisual.gameObject);
            foreach (Transform child in ship)
                if (child != plume) child.gameObject.SetActive(false);
            var material = ship.GetComponentInChildren<Renderer>(true).sharedMaterial;
            assembledVisual = new GameObject("Modular ship").transform;
            assembledVisual.SetParent(ship, false);
            int count = Assembly.Layout.modules.Count;
            for (int i = 0; i < count; i++)
            {
                ModuleData part = Assembly.Find(Assembly.Layout.modules[i]);
                var piece = GameObject.CreatePrimitive(part.kind == ModuleKind.Cockpit ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
                piece.name = part.displayName;
                piece.layer = ship.gameObject.layer;
                piece.transform.SetParent(assembledVisual, false);
                piece.transform.localPosition = new Vector3(0, 0, (count - 1) * .6f - i * 1.2f);
                if (part.kind == ModuleKind.Cockpit) piece.transform.localScale = new Vector3(1.4f, 1.4f, 1.7f);
                else
                {
                    piece.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    float width = part.kind == ModuleKind.Engine ? .8f : 1.25f;
                    piece.transform.localScale = new Vector3(width, .55f, width);
                }
                Destroy(piece.GetComponent<Collider>());
                var renderer = piece.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", part.color);
                renderer.SetPropertyBlock(properties);
            }
            plume.localPosition = new Vector3(0, 0, -count * .6f);
            int n = 0;
            foreach (Transform jet in plume) { jet.localPosition = new Vector3((n++ == 0 ? -.25f : .25f), 0, -.55f); }
        }
        public void Restart()
        {
            Assembly.Refuel();
            CurrentThrust = 0;
            int home = settings.homeIndex;
            Target = Nearest = home;
            Pilot = new Helm();
            Pilot.q = SolarStart.Load(World, bodies, settings);
            previousRotation = Pilot.Rotation;
            for (int i = 0; i < previous.Length; i++)
                previous[i] = World.Position[i];
            World.SampleGravity();
            Clock = accumulator = 0;
            Paused = Impact = Warping = false;
            rig.ResetView();
        }
        public void NextTarget()
        {
            if (!Warping) Target = (Target + 1) % Craft;
        }
        public void TogglePause()
        {
            if (!Impact)
                Paused = !Paused;
        }
        void Update()
        {
            if (!hud.IsOverlayOpen && reset.WasPressedThisFrame())
                Restart();
            if (!hud.IsOverlayOpen && pause.WasPressedThisFrame())
                TogglePause();
            if (!hud.IsOverlayOpen && target.WasPressedThisFrame())
                NextTarget();
            if (!hud.IsOverlayOpen && view.WasPressedThisFrame())
                rig.SwitchView();
            if (!hud.IsOverlayOpen && cut.WasPressedThisFrame())
                Pilot.throttle = 0;
            double dt = settings.stepSeconds;
            if (!Paused && !hud.IsMenuOpen)
                accumulator = math.min(accumulator + Time.unscaledDeltaTime, dt * settings.maxStepsPerFrame);
            double3 command =
                hud.IsOverlayOpen ? double3.zero :
                new double3(pitch.ReadValue<float>(), yaw.ReadValue<float>(), roll.ReadValue<float>());
            while (!Paused && !hud.IsMenuOpen && accumulator >= dt)
            {
                previousRotation = Pilot.Rotation;
                if (!Warping)
                {
                    double inertiaScale = math.max(.05, Assembly.Mass / 14000.0);
                    double3 demand = Pilot.RequestedTorque(vessel, command, Assembly.RcsTorque, inertiaScale);
                    double authority = Assembly.BurnRcs(math.csum(math.abs(demand)), dt);
                    Pilot.Tick(vessel, command, hud.IsOverlayOpen ? 0 : throttle.ReadValue<float>(), dt,
                               Assembly.RcsTorque, inertiaScale, authority);
                }
                for (int i = 0; i < previous.Length; i++)
                    previous[i] = World.Position[i];
                double fuelBefore = Assembly.Fuel;
                double acceleration = Warping ? 0 : Assembly.Burn(Pilot.throttle, dt);
                CurrentThrust = dt > 0 && Assembly.Flow > 0 ? (fuelBefore - Assembly.Fuel) / dt * Assembly.Thrust / Assembly.Flow : 0;
                World.Step(dt, Pilot.Forward * acceleration);
                bool wasWarping = Warping;
                if (wasWarping) AdvanceWarp(dt);
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
                if (min <= 0 && !wasWarping)
                {
                    Paused = Impact = true;
                }
            }
            double alpha = Impact ? 1 : accumulator / dt;
            ship.SetPositionAndRotation(Vector3.zero,
                                        Quaternion.Slerp(previousRotation, Pilot.Rotation, (float)alpha));
            bool burning = CurrentThrust > 0 && !Warping;
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
