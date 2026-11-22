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
        VisualElement navigationMap, mapPlot;
        Label warpInfo;
        Button warpButton;
        VisualElement[] mapMarkers;
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
            BuildMap(root.Q("screen"));
            Refresh(flight);
            root.schedule.Execute(() => Refresh(flight)).Every(200);
        }
        void BuildMap(VisualElement screen)
        {
            var open = new Button(() => navigationMap.style.display = DisplayStyle.Flex) { text = "Карта" };
            screen.Q("next").parent.Add(open);
            navigationMap = new VisualElement();
            navigationMap.AddToClassList("map-window");
            navigationMap.style.display = DisplayStyle.None;
            screen.Add(navigationMap);
            var heading = new VisualElement();
            heading.AddToClassList("map-header");
            heading.Add(new Label("КАРТА СОЛНЕЧНОЙ СИСТЕМЫ"));
            heading.Add(new Button(() => navigationMap.style.display = DisplayStyle.None) { text = "Закрыть" });
            navigationMap.Add(heading);
            navigationMap.Add(new Label("Вид сверху · логарифмический масштаб · выберите цель из списка"));
            mapPlot = new VisualElement();
            mapPlot.AddToClassList("map-plot");
            navigationMap.Add(mapPlot);
            mapMarkers = new VisualElement[flight.Craft + 1];
            for (int i = 0; i < mapMarkers.Length; i++)
            {
                var marker = new Label(i == flight.Craft ? "▲" : "●");
                marker.AddToClassList("map-marker");
                marker.style.color = i == flight.Craft ? Color.white : flight.bodies[i].tint;
                mapPlot.Add(marker);
                mapMarkers[i] = marker;
            }
            var choices = new VisualElement();
            choices.AddToClassList("map-choices");
            for (int i = 0; i < flight.Craft; i++)
            {
                int index = i;
                choices.Add(new Button(() => flight.SelectTarget(index)) { text = flight.bodies[i].displayName });
            }
            navigationMap.Add(choices);
            warpInfo = new Label();
            warpInfo.AddToClassList("warp-info");
            navigationMap.Add(warpInfo);
            warpButton = new Button(() => flight.StartWarp()) { text = "Запустить тестовый варп" };
            navigationMap.Add(warpButton);
        }
        void RefreshMap(Flight f)
        {
            if (navigationMap.resolvedStyle.display == DisplayStyle.None) return;
            float width = mapPlot.resolvedStyle.width, height = mapPlot.resolvedStyle.height;
            if (width > 0 && height > 0)
            {
                // Logarithmic radial projection keeps inner planets visible beside Neptune.
                double max = 1;
                for (int i = 0; i <= f.Craft; i++)
                {
                    var p = f.World.Position[i] - f.World.Position[0];
                    max = math.max(max, math.length(new double2(p.x, p.z)));
                }
                double denom = math.log(1 + max / 1e9);
                for (int i = 0; i <= f.Craft; i++)
                {
                    var p = f.World.Position[i] - f.World.Position[0];
                    var d = new double2(p.x, p.z);
                    double r = math.length(d);
                    var v = r > 0 ? d / r * (math.log(1 + r / 1e9) / denom) : double2.zero;
                    mapMarkers[i].style.left = (float)(width * .5 + v.x * (width * .46)) - 6;
                    mapMarkers[i].style.top = (float)(height * .5 - v.y * (height * .46)) - 8;
                }
            }
            double range = math.distance(f.World.Position[f.Craft], f.World.Position[f.Target]);
            warpInfo.text = f.Warping
                ? $"ВАРП → {f.bodies[f.Target].displayName} · осталось {f.WarpRemaining:F1} с"
                : $"Цель: {f.bodies[f.Target].displayName} · до центра {range/1000:N0} км · примерное время {range/Flight.WarpSpeed:F1} с";
            warpButton.SetEnabled(!f.Warping && !f.Paused && !f.Impact);
        }
        void Refresh(Flight f)
        {
            RefreshMap(f);
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
