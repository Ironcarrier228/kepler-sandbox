using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Kepler
{
    [Serializable]
    public sealed class ShipLayout
    {
        public List<string> modules = new List<string>();
    }

    // A single axial stack with a common propellant pool and continuously firing engines.
    public sealed class ShipAssembly
    {
        public const double StandardGravity = 9.80665;
        public const int Limit = 12;
        public readonly ModuleData[] catalog;
        public ShipLayout Layout { get; private set; }
        public double DryMass { get; private set; }
        public double Capacity { get; private set; }
        public double Fuel { get; private set; }
        public double Thrust { get; private set; }
        public double Flow { get; private set; }
        public double RcsTorque { get; private set; }
        public bool HasWarp { get; private set; }
        public double Mass => DryMass + Fuel;
        public double DeltaV => Flow > 0 && DryMass > 0 ? Thrust / Flow * math.log(Mass / DryMass) : 0;
        public ModuleData Find(string id)
        {
            foreach (var part in catalog) if (part != null && part.id == id) return part;
            return null;
        }
        public ShipAssembly(ModuleData[] catalog)
        {
            this.catalog = catalog;
            ShipLayout saved = null;
            try { saved = JsonUtility.FromJson<ShipLayout>(PlayerPrefs.GetString("shipLayout", "")); }
            catch (ArgumentException) { }
            if (!Valid(saved)) saved = DefaultLayout();
            Apply(saved, false);
        }
        public ShipLayout DefaultLayout() => new ShipLayout { modules = new List<string> { "cockpit", "tank-large", "engine-chemical", "rcs", "warp" } };
        public bool Valid(ShipLayout layout)
        {
            if (layout == null || layout.modules == null || layout.modules.Count < 1 || layout.modules.Count > Limit) return false;
            int cockpits = 0;
            foreach (string id in layout.modules)
            {
                var p = Find(id);
                if (p == null) return false;
                if (p.kind == ModuleKind.Cockpit) cockpits++;
            }
            return cockpits == 1 && Find(layout.modules[0]).kind == ModuleKind.Cockpit;
        }
        public void Apply(ShipLayout layout, bool save)
        {
            if (!Valid(layout)) throw new ArgumentException("Invalid ship layout");
            Layout = new ShipLayout { modules = new List<string>(layout.modules) };
            DryMass = Capacity = Thrust = Flow = RcsTorque = 0;
            HasWarp = false;
            foreach (string id in Layout.modules)
            {
                var p = Find(id);
                DryMass += p.dryMass;
                Capacity += p.fuelCapacity;
                if (p.kind == ModuleKind.Engine && p.specificImpulse > 0)
                {
                    Thrust += p.thrust;
                    Flow += p.thrust / (p.specificImpulse * StandardGravity);
                }
                if (p.kind == ModuleKind.Rcs) RcsTorque += p.torque;
                if (p.kind == ModuleKind.Warp) HasWarp = true;
            }
            Fuel = Capacity;
            if (save) { PlayerPrefs.SetString("shipLayout", JsonUtility.ToJson(Layout)); PlayerPrefs.Save(); }
        }
        public void Refuel() { Fuel = Capacity; }
        public double TakeFuel(double requested)
        {
            double used = math.min(Fuel, math.max(0, requested));
            Fuel = math.max(0, Fuel - used);
            return used;
        }
        public double Burn(double throttle, double dt)
        {
            double before = Mass;
            double fuel = TakeFuel(Flow * math.clamp(throttle, 0, 1) * dt);
            if (fuel <= 0 || Flow <= 0 || dt <= 0) return 0;
            // Equivalent exhaust velocity and rocket equation integrated over this step.
            return (Thrust / Flow) * math.log(before / Mass) / dt;
        }
        public double BurnRcs(double requestedTorque, double dt)
        {
            // Approximate effective 1 m lever arm and 220 s RCS specific impulse.
            double need = math.max(0, requestedTorque) * dt / (220 * StandardGravity);
            if (need <= 0) return 0;
            return TakeFuel(need) / need;
        }
    }
}
