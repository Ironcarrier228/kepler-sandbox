using UnityEngine;
using UnityEngine.UIElements;
using Unity.Mathematics;
namespace Kepler
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class FlightHud : MonoBehaviour
    {
        public Flight flight;
        public StyleSheet sheet;
        Label clock, place, height, speed, power, target, range, guide;
        VisualElement bar;
        Button pause, view;
        bool help;
        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.styleSheets.Add(sheet);
            clock = root.Q<Label>("clock");
            place = root.Q<Label>("place");
            height = root.Q<Label>("height");
            speed = root.Q<Label>("speed");
            power = root.Q<Label>("power");
            target = root.Q<Label>("target");
            range = root.Q<Label>("range");
            guide = root.Q<Label>("guide");
            bar = root.Q("bar");
            root.Q<Button>("next").clicked += flight.NextTarget;
            view = root.Q<Button>("view");
            view.clicked += flight.rig.SwitchView;
            pause = root.Q<Button>("pause");
            pause.clicked += flight.TogglePause;
            root.Q<Button>("reset").clicked += flight.Restart;
            root.Q<Button>("help").clicked += () =>
            { help = !help; };
            Refresh(flight);
            root.schedule.Execute(() => Refresh(flight)).Every(200);
        }
        void Refresh(Flight f)
        {
            // Text formatting is throttled to 5 Hz, independent of render/physics rates.
            int i = f.Nearest, t = f.Target, k = f.Craft;
            var w = f.World;
            clock.text = $"SOL   /   T + {f.Clock:000000.0} s";
            place.text = f.bodies[i].displayName;
            height.text = $"Высота     {f.Altitude/1000:N1} км";
            speed.text = $"Скорость     {math.distance(w.Velocity[i],w.Velocity[k]):N1} м/с";
            power.text =
                $"Тяга     {f.Pilot.throttle*f.vessel.engine.thrustNewtons/1000:F1} кН   /   {100*f.Pilot.throttle:F0}%";
            bar.style.width = Length.Percent((float)f.Pilot.throttle * 100);
            target.text = f.bodies[t].displayName;
            range.text = $"До центра   {math.distance(w.Position[k],w.Position[t])/1000:N0} км";
            guide.text = f.Impact ? "Контакт с поверхностью. Нажмите R, чтобы вернуться на орбиту."
                         : help
                             ? "W/S тангаж · A/D рыскание · Q/E крен · ↑/↓ тяга · X отсечка\nC камера · Tab " +
                                   "цель · P пауза · R сброс"
                             : "РУЧНОЙ ПОЛЁТ     /     ↑ ↓  тяга     ·     WASD  ориентация";
            pause.text = f.Paused ? "Продолжить" : "Пауза";
            view.text = f.rig.Survey ? "К кораблю" : "Обзор";
        }
    }
}
