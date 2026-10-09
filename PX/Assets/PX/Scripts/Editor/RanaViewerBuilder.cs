using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PX.EditorTools
{
    /// <summary>
    /// Builds RanaViewer.unity: Rana on a plain floor, neutral light, and an orbit camera. Used to judge the model.
    /// Like the graybox, the scene is generated; change this builder and rebuild.
    /// </summary>
    public static class RanaViewerBuilder
    {
        private const string ScenePath = "Assets/PX/Scenes/RanaViewer.unity";
        private const string MaterialPath = RanaImportSettings.Folder + "/Rana.mat";
        private const float Height = 1.7f;

        [MenuItem("PX/Rana/Rebuild Viewer Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RanaImportSettings.Folder + "/Rana.fbx");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Rana";

            Material material = LoadOrCreateMaterial();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;

            Bounds bounds = MeasureBounds(model);
            float scale = bounds.size.y > 0.01f ? Height / bounds.size.y : 1f;
            model.transform.localScale = Vector3.one * scale;
            // Stand her on the floor, centred on the origin.
            model.transform.position = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
            Debug.Log($"Rana mesh is {bounds.size.y:0.00} m tall before scaling (scaled by {scale:0.###}).");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(3f, 0.02f, 3f);
            floor.transform.position = new Vector3(0f, -0.02f, 0f);
            floor.GetComponent<Renderer>().sharedMaterial = LoadOrCreateFloorMaterial();

            var sun = new GameObject("Key Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(40f, 150f, 0f);

            var fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.4f;
            fill.transform.rotation = Quaternion.Euler(20f, -30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.5f);

            var cameraObject = new GameObject("Orbit Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
            camera.fieldOfView = 35f;
            cameraObject.AddComponent<AudioListener>();

            var focus = new GameObject("Focus").transform;
            focus.position = new Vector3(0f, Height * 0.55f, 0f);
            var orbit = cameraObject.AddComponent<RanaOrbitView>();
            var serialized = new SerializedObject(orbit);
            serialized.FindProperty("target").objectReferenceValue = focus;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Built {ScenePath}.");
        }

        private static Bounds MeasureBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Material LoadOrCreateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RanaImportSettings.Folder + "/Textures/Rana_BaseColor.jpg"));
            material.SetFloat("_Smoothness", 0.3f);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static Material LoadOrCreateFloorMaterial()
        {
            const string path = RanaImportSettings.Folder + "/RanaViewerFloor.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.3f, 0.31f, 0.34f));
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
