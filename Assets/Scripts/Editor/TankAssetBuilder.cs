using System.IO;
using Kepler.Parts;
using Kepler.Rendering;
using UnityEditor;
using UnityEngine;

namespace Kepler.Editor
{
    public static class TankAssetBuilder
    {
        const string Root = "Assets/KeplerGenerated/Tanks/";
        const string TextureRoot = "Assets/Textures/Tanks/Metal006/";
        static Texture2D colorMap, normalMap, metallicMap;
        static Texture2D LoadTexture(string filename, bool normal, bool srgb)
        {
            string path = TextureRoot + filename;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new System.IO.FileNotFoundException("Missing tank PBR map", path);
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type || importer.sRGBTexture != srgb || importer.wrapMode != TextureWrapMode.Repeat ||
                (!normal && filename.EndsWith(".png") && importer.alphaSource != TextureImporterAlphaSource.FromInput))
            {
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.wrapMode = TextureWrapMode.Repeat;
                if (!normal && filename.EndsWith(".png")) importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture) throw new System.IO.FileNotFoundException("Failed to import tank PBR map", path);
            return texture;
        }
        [MenuItem("Tools/Build Tank Prototypes")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            colorMap = LoadTexture("Metal006_Color.jpg", false, true);
            normalMap = LoadTexture("Metal006_NormalGL.jpg", true, false);
            metallicMap = LoadTexture("Metal006_MetallicSmoothness.png", false, false);
            Make("cylinder", "Цилиндрический бак", TankShape.Cylinder, 1.1f, 3.6f, 900, 8500, new Color(.68f, .71f, .73f), .8f);
            Make("sphere", "Сферический бак", TankShape.Sphere, .9f, 1.8f, 450, 2500, new Color(.72f, .74f, .76f), .7f);
            Make("domed", "Бак с куполами", TankShape.Domed, 1.2f, 4.8f, 1200, 12000, new Color(.65f, .34f, .12f), .05f);
            Make("rcs-bottle", "Баллон РСУ", TankShape.RcsBottle, .22f, .9f, 40, 90, new Color(.48f, .50f, .52f), .7f);
            AssetDatabase.SaveAssets();
            Debug.Log("Kepler: созданы прототипы баков с LOD, MeshCollider и узлами крепления.");
        }
        static Mesh SaveMesh(string path, Mesh generated)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(generated, existing);
                Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }
        static void Make(string id, string displayName, TankShape shape, float radius, float height,
                         double dryMass, double fuelMass, Color color, float metallic)
        {
            string path = Root + id;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path + ".mat");
            if (!material)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new System.InvalidOperationException("URP Lit shader not found");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path + ".mat");
            }
            bool foam = shape == TankShape.Domed;
            material.SetColor("_BaseColor", foam ? color : Color.white);
            material.SetFloat("_Metallic", foam ? metallic : 1f);
            material.SetFloat("_Smoothness", foam ? .48f : 1f);
            material.SetTexture("_BaseMap", foam ? null : colorMap);
            material.SetTexture("_BumpMap", foam ? null : normalMap);
            material.SetTexture("_MetallicGlossMap", foam ? null : metallicMap);
            if (foam) { material.DisableKeyword("_NORMALMAP"); material.DisableKeyword("_METALLICSPECGLOSSMAP"); }
            else { material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            EditorUtility.SetDirty(material);
            var meshes = new Mesh[3];
            meshes[0] = SaveMesh(path + "-lod0.asset", TankMesh.Create(shape, radius, height, 48, 12));
            meshes[1] = SaveMesh(path + "-lod1.asset", TankMesh.Create(shape, radius, height, 24, 6));
            meshes[2] = SaveMesh(path + "-lod2.asset", TankMesh.Create(shape, radius, height, 12, 3));
            var root = new GameObject(displayName);
            try
            {
                var lodGroup = root.AddComponent<LODGroup>();
                var lods = new LOD[3];
                float[] thresholds = { .5f, .2f, .02f };
                for (int i = 0; i < 3; i++)
                {
                    var child = new GameObject("LOD" + i);
                    child.transform.SetParent(root.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                    var renderer = child.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    lods[i] = new LOD(thresholds[i], new Renderer[] { renderer });
                }
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
                var collider = root.AddComponent<MeshCollider>();
                collider.sharedMesh = meshes[2];
                collider.convex = true;
                Node(root.transform, "top", new Vector3(0, height * .5f, 0), Vector3.up);
                Node(root.transform, "bottom", new Vector3(0, -height * .5f, 0), Vector3.down);
                Node(root.transform, "side-x", new Vector3(radius, 0, 0), Vector3.right);
                Node(root.transform, "side--x", new Vector3(-radius, 0, 0), Vector3.left);
                Node(root.transform, "side-z", new Vector3(0, 0, radius), Vector3.forward);
                Node(root.transform, "side--z", new Vector3(0, 0, -radius), Vector3.back);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path + ".prefab");
                if (!prefab) throw new System.InvalidOperationException("Failed to save tank prefab " + id);
                var data = AssetDatabase.LoadAssetAtPath<TankDefinition>(path + ".asset");
                if (!data)
                {
                    data = ScriptableObject.CreateInstance<TankDefinition>();
                    AssetDatabase.CreateAsset(data, path + ".asset");
                }
                data.id = id; data.displayName = displayName; data.shape = shape;
                data.radius = radius; data.height = height; data.volumeM3 = TankMesh.Volume(shape, radius, height);
                data.dryMassKg = dryMass; data.fuelMassKg = fuelMass; data.prefab = prefab;
                EditorUtility.SetDirty(data);
            }
            finally { Object.DestroyImmediate(root); }
        }
        static void Node(Transform parent, string id, Vector3 position, Vector3 outward)
        {
            var go = new GameObject("Attach-" + id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, outward);
            var node = go.AddComponent<AttachNode>();
            node.nodeId = id;
            node.size = id.StartsWith("side") ? "radial" : "standard";
        }
    }
}
