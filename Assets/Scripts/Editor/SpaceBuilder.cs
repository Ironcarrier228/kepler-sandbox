using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Kepler.Editor
{
    public static class SpaceBuilder
    {
        const string Root = "Assets/KeplerGenerated/";
        internal const string ScenePath = Root + "SolarFlight.unity";
        static T Get<T>(string file)
            where T : Object => AssetDatabase.LoadAssetAtPath<T>(Root + file);
        internal static T Asset<T>(string name)
            where T : ScriptableObject
        {
            string path = name + ".asset";
            var a = Get<T>(path);
            if (!a)
            {
                a = ScriptableObject.CreateInstance<T>();
                a.name = Path.GetFileName(name);
                AssetDatabase.CreateAsset(a, Root + path);
            }
            EditorUtility.SetDirty(a);
            return a;
        }
        static Material Mat(string name, string shader, Color tint)
        {
            string path = "Materials/" + name + ".mat";
            var m = Get<Material>(path);
            var s = Shader.Find(shader);
            if (!s)
                throw new InvalidOperationException("Shader not imported: " + shader);
            if (!m)
            {
                m = new Material(s);
                AssetDatabase.CreateAsset(m, Root + path);
            }
            m.shader = s;
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Tint"))
                m.SetColor("_Tint", tint);
            EditorUtility.SetDirty(m);
            return m;
        }
        static GameObject Node(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent)
                go.transform.SetParent(parent, false);
            return go;
        }
        static int Layer(SerializedProperty layers, string name)
        {
            for (int i = 8; i < 32; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == name)
                    return i;
            for (int i = 8; i < 32; i++)
            {
                var p = layers.GetArrayElementAtIndex(i);
                if (p.stringValue == "")
                {
                    p.stringValue = name;
                    return i;
                }
            }
            throw new InvalidOperationException("Свободных пользовательских слоёв нет");
        }
        static void Layers(out int distant, out int local)
        {
            var obj =
                new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = obj.FindProperty("layers");
            distant = Layer(layers, "KeplerScaled");
            local = Layer(layers, "KeplerLocal");
            var tags = obj.FindProperty("tags");
            foreach (var tag in new[] { "Spacecraft", "CelestialBody" })
            {
                bool exists = false;
                for (int i = 0; i < tags.arraySize; i++)
                    exists |= tags.GetArrayElementAtIndex(i).stringValue == tag;
                if (!exists)
                {
                    int n = tags.arraySize;
                    tags.InsertArrayElementAtIndex(n);
                    tags.GetArrayElementAtIndex(n).stringValue = tag;
                }
            }
            obj.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Pipeline()
        {
            var renderer = Asset<UniversalRendererData>("ForwardRenderer");
            var pipeline = Get<UniversalRenderPipelineAsset>("KeplerURP.asset");
            if (!pipeline)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, Root + "KeplerURP.asset");
            }
            pipeline.msaaSampleCount = 2;
            pipeline.supportsHDR = true;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }
        static InputActionAsset Bindings()
        {
            var a = Asset<InputActionAsset>("PilotControls");
            var old = a.FindActionMap("Flight");
            if (old != null)
                a.RemoveActionMap(old);
            var m = a.AddActionMap("Flight");
            void Axis(string name, string neg, string pos, string pad)
            {
                var action = m.AddAction(name, InputActionType.Value, expectedControlLayout: "Axis");
                action.AddCompositeBinding("1DAxis")
                    .With("Negative", "<Keyboard>/" + neg)
                    .With("Positive", "<Keyboard>/" + pos);
                action.AddBinding("<Gamepad>/" + pad);
            }
            void Button(string name, string key, string pad)
            {
                var a1 = m.AddAction(name, InputActionType.Button, "<Keyboard>/" + key);
                a1.AddBinding("<Gamepad>/" + pad);
            }
            Axis("Pitch", "s", "w", "rightStick/y");
            Axis("Yaw", "a", "d", "rightStick/x");
            Axis("Roll", "e", "q", "leftStick/x");
            Axis("Throttle", "downArrow", "upArrow", "leftStick/y");
            Button("Cut", "x", "buttonSouth");
            Button("Pause", "p", "start");
            Button("Restart", "r", "select");
            Button("Target", "tab", "rightShoulder");
            Button("View", "c", "leftShoulder");
            return a;
        }
        static GameObject SavePrefab(GameObject temp, string name)
        {
            var p = PrefabUtility.SaveAsPrefabAsset(temp, Root + "Prefabs/" + name + ".prefab");
            Object.DestroyImmediate(temp);
            return p;
        }
        static GameObject Instantiate(GameObject prefab, Transform parent = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent)
                go.transform.SetParent(parent, false);
            return go;
        }
        static GameObject BuildShip(int layer)
        {
            var g = Node("Peregrine");
            g.layer = layer;
            g.tag = "Spacecraft";
            var hull = Mat("Ceramic", "Universal Render Pipeline/Lit", new Color(.74f, .79f, .8f));
            var dark = Mat("Graphite", "Universal Render Pipeline/Lit", new Color(.11f, .15f, .19f));
            var glass = Mat("Canopy", "Universal Render Pipeline/Lit", new Color(.09f, .36f, .45f));
            glass.SetFloat("_Smoothness", .92f);
            glass.SetFloat("_Metallic", .65f);
            var accent = Mat("Markings", "Universal Render Pipeline/Lit", new Color(.96f, .41f, .10f));
            var jet = Mat("Exhaust", "Universal Render Pipeline/Unlit", new Color(.23f, .84f, 1f));
            void Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale,
                      Material mat, float yaw = 0)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name;
                go.layer = layer;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }
            Part(g.transform, "Pressure hull", PrimitiveType.Sphere, Vector3.zero, new Vector3(1.65f, 1, 5.5f),
                 hull);
            Part(g.transform, "Flight deck", PrimitiveType.Sphere, new Vector3(0, .39f, .72f),
                 new Vector3(1.05f, .6f, 2.2f), glass);
            var fx = Node("Exhaust", g.transform);
            fx.layer = layer;
            fx.transform.localPosition = new Vector3(0, 0, -2.3f);
            foreach (int side in new[] { -1, 1 })
            {
                Part(g.transform, "Pylon " + side, PrimitiveType.Cube, new Vector3(side * 1.05f, 0, -.45f),
                     new Vector3(2, .16f, .8f), hull, side * 18);
                Part(g.transform, "Nacelle " + side, PrimitiveType.Sphere, new Vector3(side * 1.7f, 0, -1),
                     new Vector3(.68f, .72f, 3.0f), dark);
                Part(g.transform, "Service stripe " + side, PrimitiveType.Cube,
                     new Vector3(side * 1.7f, .32f, -1), new Vector3(.21f, .08f, .92f), accent);
                Part(fx.transform, "Ionized gas " + side, PrimitiveType.Sphere,
                     new Vector3(side * 1.7f, 0, -.55f), new Vector3(.41f, .41f, 1.7f), jet);
            }
            return SavePrefab(g, "Peregrine");
        }
        static GameObject BuildBody(int layer)
        {
            var mesh = Get<Mesh>("Globe.asset");
            if (!mesh)
            {
                mesh = GlobeMesh.Create(48);
                AssetDatabase.CreateAsset(mesh, Root + "Globe.asset");
            }
            var g = Node("Globe");
            g.layer = layer;
            g.tag = "CelestialBody";
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>();
            return SavePrefab(g, "Globe");
        }
        static FlightRig Cameras(int distant, int local)
        {
            var rig = Node("Floating origin cameras").AddComponent<FlightRig>();
            Camera Make(string name, int layer, float near, float far)
            {
                var c = Node(name, rig.transform).AddComponent<Camera>();
                c.cullingMask = 1 << layer;
                c.nearClipPlane = near;
                c.farClipPlane = far;
                c.fieldOfView = 62;
                c.useOcclusionCulling = false;
                return c;
            }
            rig.distant = Make("Scaled space", distant, .00001f, 12000000);
            rig.distant.tag = "MainCamera";
            rig.distant.clearFlags = CameraClearFlags.Skybox;
            rig.local = Make("Ship space", local, .05f, 5000);
            rig.local.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
            // New URP overlay cameras clear depth by default (clearDepth is read-only).
            rig.distant.GetUniversalAdditionalCameraData().cameraStack.Add(rig.local);
            rig.local.gameObject.AddComponent<AudioListener>();
            rig.sun = Node("Sol light").AddComponent<Light>();
            rig.sun.type = LightType.Directional;
            rig.sun.intensity = 2;
            RenderSettings.sun = rig.sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.34f, .38f, .42f);
            return rig;
        }
        static ModuleData[] ModuleCatalog()
        {
            ModuleData Part(string id, string label, ModuleKind kind, double mass, double fuel,
                            double thrust, double isp, double torque, Color color)
            {
                var part = Asset<ModuleData>("Modules/" + id);
                part.id = id; part.displayName = label; part.kind = kind; part.dryMass = mass;
                part.fuelCapacity = fuel; part.thrust = thrust; part.specificImpulse = isp;
                part.torque = torque; part.color = color;
                EditorUtility.SetDirty(part);
                return part;
            }
            return new[] {
                Part("cockpit", "Кабина", ModuleKind.Cockpit, 3000, 0, 0, 0, 0, new Color(.65f,.8f,.85f)),
                Part("tank-small", "Бак малый", ModuleKind.Tank, 500, 2500, 0, 0, 0, new Color(.74f,.76f,.78f)),
                Part("tank-large", "Бак большой", ModuleKind.Tank, 1200, 9000, 0, 0, 0, new Color(.82f,.84f,.85f)),
                Part("engine-chemical", "Химический двигатель", ModuleKind.Engine, 1800, 0, 180000, 330, 0, new Color(.8f,.4f,.18f)),
                Part("engine-ion", "Ионный двигатель", ModuleKind.Engine, 400, 0, 3000, 2400, 0, new Color(.15f,.65f,.85f)),
                Part("rcs", "Блок РСУ", ModuleKind.Rcs, 350, 0, 0, 0, 45000, new Color(.85f,.7f,.3f)),
                Part("warp", "Варп-модуль", ModuleKind.Warp, 2000, 0, 0, 0, 0, new Color(.45f,.3f,.8f))
            };
        }
        [MenuItem("Tools/Build Space Game")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Выйдите из Play Mode перед сборкой.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            foreach (var p in new[] { "", "Bodies", "Materials", "Prefabs", "Modules" })
                Directory.CreateDirectory(Root + p);
            AssetDatabase.Refresh();
            Layers(out int distant, out int local);
            Pipeline();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var f = Node("Flight computer").AddComponent<Flight>();
            f.settings = Asset<FlightSettings>("FlightSettings");
            f.vessel = Asset<ShipData>("PeregrineSpec");
            f.modules = ModuleCatalog();
            f.vessel.engine = Asset<EngineData>("MainEngine");
            f.controls = Bindings();
            f.bodies = SolarCatalogue.Create();
            var universe = Node("Universe");
            var galaxy = Node("Milky Way", universe.transform);
            var system = Node("Sol • J2000", galaxy.transform);
            f.globes = new Transform[f.bodies.Length];
            var prefab = BuildBody(distant);
            for (int i = 0; i < f.bodies.Length; i++)
            {
                var b = f.bodies[i];
                var g = Instantiate(prefab, system.transform);
                g.name = b.name;
                var m = Mat(b.name, "Kepler/Orbital body", b.tint);
                m.SetFloat("_Kind", i == 0 ? 1 : i == 3 ? 2 : i >= 5 && i <= 8 ? 3 : 0);
                m.SetFloat("_Seed", i * 11);
                g.GetComponent<Renderer>().sharedMaterial = m;
                g.transform.localScale = Vector3.one * (float)(2 * b.radius * FlightRig.Scale);
                f.globes[i] = g.transform;
                PrefabUtility.RecordPrefabInstancePropertyModifications(g.GetComponent<Renderer>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
            }
            f.ship = Instantiate(BuildShip(local)).transform;
            f.plume = f.ship.Find("Exhaust");
            f.rig = Cameras(distant, local);
            var sky = Mat("Star atlas", "Kepler/Star atlas", Color.white);
            sky.SetFloat("_Seed", f.settings.skySeed);
            RenderSettings.skybox = sky;
            var panel = Asset<PanelSettings>("FlightPanel");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.match = .5f;
            File.WriteAllText(Root + "FlightTheme.tss", "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset(Root + "FlightTheme.tss");
            panel.themeStyleSheet = Get<ThemeStyleSheet>("FlightTheme.tss");
            var ui = Node("Flight instruments");
            var doc = ui.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Interface/Flight.uxml");
            f.hud = ui.AddComponent<FlightHud>();
            f.hud.flight = f;
            f.hud.sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Interface/Flight.uss");
            // Show the real initial layout in Edit Mode as well as at runtime.
            using (var preview = new GravityWorld(f.bodies.Length + 1))
            {
                var helm = new Helm { q = SolarStart.Load(preview, f.bodies, f.settings) };
                f.ship.rotation = helm.Rotation;
                f.plume.gameObject.SetActive(false);
                for (int i = 0; i < f.Craft; i++)
                    f.globes[i].position = (Vector3)(Unity.Mathematics.float3)(
                        (preview.Position[i] - preview.Position[f.Craft]) * FlightRig.Scale);
                f.rig.RenderFrame(f);
                foreach (var tr in f.globes)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(tr);
                PrefabUtility.RecordPrefabInstancePropertyModifications(f.ship);
                PrefabUtility.RecordPrefabInstancePropertyModifications(f.plume.gameObject);
            }
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Cannot save " + ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                                             .Concat(EditorBuildSettings.scenes.Where(x => x.path != ScenePath))
                                             .ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeObject = f.gameObject;
            Debug.Log("Kepler: SolarFlight готова. Нажмите Play.");
        }
    }
}
