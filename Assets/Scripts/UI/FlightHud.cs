using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
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
        VisualElement navigationMap, mapPlot, menu, settingsPanel, menuButtons;
        bool menuOpen = true, mapOpen;
        float mapYaw, mapPitch = 55, mapZoom = 1;
        Vector2 lastMapPointer;
        bool draggingMap;
        public bool IsMenuOpen => menuOpen;
        public bool IsOverlayOpen => menuOpen || mapOpen;
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
            BuildMenu(root.Q("screen"));
            Refresh(flight);
            root.schedule.Execute(() => Refresh(flight)).Every(200);
        }
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (mapOpen) CloseMap();
                else SetMenu(!menuOpen);
            }
        }
        void SetMenu(bool open)
        {
            menuOpen = open;
            menu.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open) { menuButtons.style.display = DisplayStyle.Flex; settingsPanel.style.display = DisplayStyle.None; }
        }
        void CloseMap()
        {
            mapOpen = false;
            navigationMap.style.display = DisplayStyle.None;
        }
        void BuildMenu(VisualElement screen)
        {
            screen.Q("reset").parent.Add(new Button(() => SetMenu(true)) { text = "Меню" });
            menu = new VisualElement();
            menu.AddToClassList("main-menu");
            screen.Add(menu);
            var panel = new VisualElement();
            panel.AddToClassList("main-menu-panel");
            menu.Add(panel);
            var title = new Label("КОСМИЧЕСКАЯ ПЕСОЧНИЦА");
            title.AddToClassList("main-menu-title");
            panel.Add(title);
            menuButtons = new VisualElement();
            panel.Add(menuButtons);
            menuButtons.Add(new Button(() => SetMenu(false)) { text = "В полёт" });
            menuButtons.Add(new Button(() => { menuButtons.style.display = DisplayStyle.None; settingsPanel.style.display = DisplayStyle.Flex; }) { text = "Настройки" });
            menuButtons.Add(new Button(Application.Quit) { text = "Выход" });
            settingsPanel = new VisualElement();
            settingsPanel.style.display = DisplayStyle.None;
            panel.Add(settingsPanel);
            var sensitivity = new Slider("Чувствительность мыши", .03f, .6f);
            sensitivity.value = Mathf.Clamp(PlayerPrefs.GetFloat("cameraSensitivity", .18f), .03f, .6f);
            flight.rig.MouseSensitivity = sensitivity.value;
            sensitivity.RegisterValueChangedCallback(e => { flight.rig.MouseSensitivity = e.newValue; PlayerPrefs.SetFloat("cameraSensitivity", e.newValue); PlayerPrefs.Save(); });
            settingsPanel.Add(sensitivity);
            var vsync = new Toggle("Вертикальная синхронизация") { value = PlayerPrefs.GetInt("vsync", 1) != 0 };
            QualitySettings.vSyncCount = vsync.value ? 1 : 0;
            vsync.RegisterValueChangedCallback(e => { QualitySettings.vSyncCount = e.newValue ? 1 : 0; PlayerPrefs.SetInt("vsync", e.newValue ? 1 : 0); PlayerPrefs.Save(); });
            settingsPanel.Add(vsync);
            var fullscreen = new Toggle("Полный экран") { value = PlayerPrefs.GetInt("fullscreen", Screen.fullScreen ? 1 : 0) != 0 };
            Screen.fullScreen = fullscreen.value;
            fullscreen.RegisterValueChangedCallback(e => { Screen.fullScreen = e.newValue; PlayerPrefs.SetInt("fullscreen", e.newValue ? 1 : 0); PlayerPrefs.Save(); });
            settingsPanel.Add(fullscreen);
            settingsPanel.Add(new Button(() => { settingsPanel.style.display = DisplayStyle.None; menuButtons.style.display = DisplayStyle.Flex; }) { text = "Назад" });
            SetMenu(true);
        }
        void BuildMap(VisualElement screen)
        {
            var open = new Button(() => { mapOpen = true; navigationMap.style.display = DisplayStyle.Flex; }) { text = "Карта" };
            screen.Q("next").parent.Add(open);
            navigationMap = new VisualElement();
            navigationMap.AddToClassList("map-window");
            navigationMap.style.display = DisplayStyle.None;
            screen.Add(navigationMap);
            var heading = new VisualElement();
            heading.AddToClassList("map-header");
            heading.Add(new Label("КАРТА СОЛНЕЧНОЙ СИСТЕМЫ"));
            heading.Add(new Button(CloseMap) { text = "Закрыть" });
            navigationMap.Add(heading);
            navigationMap.Add(new Label("Солнце в центре · ПКМ: вращение · колёсико: масштаб · цель выберите ниже"));
            mapPlot = new VisualElement();
            mapPlot.AddToClassList("map-plot");
            navigationMap.Add(mapPlot);
            mapPlot.generateVisualContent += DrawMapOrbits;
            mapPlot.RegisterCallback<PointerDownEvent>(e => {
                if (e.button != 1) return;
                draggingMap = true; lastMapPointer = e.position; mapPlot.CapturePointer(e.pointerId); e.StopPropagation();
            });
            mapPlot.RegisterCallback<PointerMoveEvent>(e => {
                if (!draggingMap) return;
                Vector2 point = e.position, delta = point - lastMapPointer;
                lastMapPointer = point; mapYaw += delta.x * .3f; mapPitch = Mathf.Clamp(mapPitch - delta.y * .3f, 1, 89);
                RefreshMap(flight); e.StopPropagation();
            });
            mapPlot.RegisterCallback<PointerUpEvent>(e => {
                if (e.button != 1 || !draggingMap) return;
                draggingMap = false; mapPlot.ReleasePointer(e.pointerId); e.StopPropagation();
            });
            mapPlot.RegisterCallback<PointerCaptureOutEvent>(e => draggingMap = false);
            mapPlot.RegisterCallback<WheelEvent>(e => { mapZoom = Mathf.Clamp(mapZoom * Mathf.Exp(-e.delta.y * .05f), .4f, 5f); RefreshMap(flight); e.StopPropagation(); });

            mapMarkers = new VisualElement[flight.Craft + 1];
            for (int i = 0; i < mapMarkers.Length; i++)
            {
                var marker = new Label(i == flight.Craft ? "▲" : i == 0 ? "☀" : "●");
                marker.pickingMode = PickingMode.Ignore;
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
        void DrawMapOrbits(MeshGenerationContext context)
        {
            if (flight.World == null) return;
            float width = mapPlot.contentRect.width, height = mapPlot.contentRect.height;
            double max = 1;
            for (int i = 0; i <= flight.Craft; i++)
            {
                var pos = flight.World.Position[i] - flight.World.Position[0];
                max = math.max(max, math.length(new double2(pos.x, pos.z)));
            }
            double denom = math.log(1 + max / 1e9);
            float scale = Mathf.Min(width, height) * .46f * mapZoom;
            var painter = context.painter2D;
            painter.lineWidth = 1;
            painter.strokeColor = new Color(.22f, .42f, .46f, .55f);
            Quaternion projection = Quaternion.Euler(mapPitch, mapYaw, 0);
            for (int i = 1; i < flight.Craft; i++)
            {
                if (flight.bodies[i].parent != 0) continue;
                var p = flight.World.Position[i] - flight.World.Position[0];
                double r = math.length(new double2(p.x, p.z));
                float radius = (float)(math.log(1 + r / 1e9) / denom);
                painter.BeginPath();
                for (int j = 0; j <= 96; j++)
                {
                    float angle = j * Mathf.PI * 2 / 96;
                    Vector3 v = projection * new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    var point = new Vector2(width * .5f + v.x * scale, height * .5f - v.y * scale);
                    if (j == 0) painter.MoveTo(point); else painter.LineTo(point);
                }
                painter.Stroke();
            }
        }
        void RefreshMap(Flight f)
        {
            if (!mapOpen) return;
            mapPlot.MarkDirtyRepaint();
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
                    Vector3 projected = Quaternion.Euler(mapPitch, mapYaw, 0) * new Vector3((float)v.x, 0, (float)v.y);
                    float scale = Mathf.Min(width, height) * .46f * mapZoom;
                    mapMarkers[i].style.left = width * .5f + projected.x * scale - 6;
                    mapMarkers[i].style.top = height * .5f - projected.y * scale - 8;
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
                             ? "W/S тангаж · A/D рыскание · Q/E крен · ↑/↓ тяга · X отсечка\nПКМ камера · C камера · Tab " +
                                   "цель · P пауза · R сброс"
                             : "РУЧНОЙ ПОЛЁТ     /     ↑ ↓  тяга     ·     WASD  ориентация";
            pause.text = f.Paused ? "Продолжить" : "Пауза";
            view.text = f.rig.Survey ? "К кораблю" : "Обзор";
        }
    }
}
