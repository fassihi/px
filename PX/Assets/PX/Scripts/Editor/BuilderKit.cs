using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace PX.EditorTools
{
    /// <summary>
    /// What the scene builders share: loading or creating assets, laying blocks along a rail, wiring serialized
    /// fields, the particle systems and trails of the fights, the camera and placing the characters. Each builder keeps
    /// only its own layout, camera zones and lighting.
    /// </summary>
    internal static class BuilderKit
    {
        public const string ConfigFolder = "Assets/PX/Config";
        public const string PrefabFolder = "Assets/PX/Prefabs";
        public const string GrayboxMaterialFolder = "Assets/PX/Art/Graybox/Materials";

        public const float FloorDepth = 8f;

        // Less perspective: a narrower lens from further back frames the same picture with less distortion.
        private const float LensScale = 0.55f;
        private const float DistanceScale = 1.5f;

        // ---------------------------------------------------------------- assets

        public static T LoadOrCreateAsset<T>(string path, Action<T> initialize) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            EnsureFolder(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            initialize?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static GameObject LoadOrCreatePrefab(string path, Func<GameObject> create)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                return prefab;

            EnsureFolder(Path.GetDirectoryName(path));
            GameObject instance = create();
            prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Adds a scene to the build settings, keeping the ones already there.</summary>
        public static void RegisterScene(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(scene => AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) != null && scene.path != scenePath)
                .ToList();
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>A URP Lit or Unlit material of one colour, created once. A colour with alpha below 1 is alpha-blended.</summary>
        public static Material LoadOrCreateMaterial(string folder, string name, Color color, bool lit, float smoothness = 0.15f)
        {
            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            EnsureFolder(folder);
            material = new Material(FindShader(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            if (lit)
                material.SetFloat("_Smoothness", smoothness);
            if (color.a < 1f)
                MakeTransparent(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static Shader FindShader(string name)
        {
            Shader shader = Shader.Find(name);
            if (shader == null)
                throw new InvalidOperationException($"Shader '{name}' not found. Is the Universal Render Pipeline package installed?");
            return shader;
        }

        /// <summary>Switches a URP material to alpha blending. This is what the Surface Type dropdown does in the Inspector.</summary>
        public static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        public static Material LoadOrCreateTexturedMaterial(string path, string texturePath)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(FindShader("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetFloat("_Smoothness", 0.3f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// A URP particle material on a generated soft texture: a dot fading from the centre, or a thin ring.
        /// Additive for light (sparks, flashes), alpha-blended for dust.
        /// </summary>
        public static Material LoadOrCreateParticleMaterial(string folder, string name, bool ring, bool additive, bool streak = false)
        {
            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            EnsureFolder(folder);
            material = new Material(FindShader("Universal Render Pipeline/Particles/Unlit"));
            material.SetTexture("_BaseMap", LoadOrCreateSoftTexture(folder, streak ? "SoftStreak" : ring ? "SoftRing" : "SoftDot", ring, streak));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static Texture2D LoadOrCreateSoftTexture(string folder, string name, bool ring, bool streak = false)
        {
            string path = $"{folder}/{name}.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
                return texture;

            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude / (size * 0.5f);
                    float a = ring ? Mathf.Clamp01(1f - Mathf.Abs(r - 0.8f) / 0.12f) : Mathf.Clamp01(1f - r);
                    a = ring ? a * a : a * a * a;
                    if (streak)
                    {
                        // Across the ribbon only: bright core, soft edges, the same all along its length.
                        float across = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                        a = Mathf.Pow(1f - across, 1.2f);
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            return SaveTexture(path, size, size, pixels, clamp: true);
        }

        /// <summary>Writes pixels out as a PNG asset and imports it as a plain, unfiltered-by-mipmaps texture.</summary>
        public static Texture2D SaveTexture(string path, int width, int height, Color32[] pixels, bool clamp, bool mipmaps = false)
        {
            EnsureFolder(Path.GetDirectoryName(path));
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.SetPixels32(pixels);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.mipmapEnabled = mipmaps;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------------------------------------------------------- scene objects

        public static Transform CreateGroup(string name, Transform parent)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        /// <summary>A primitive used as a visual only: its collider is removed.</summary>
        public static GameObject AddPart(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        /// <summary>
        /// Lays floor or platform along the rail between two distances.
        /// Straight stretches become one block; curved stretches are cut into short pieces that follow the bend.
        /// </summary>
        public static void AddBlocks(
            Transform parent, Rail rail, string name, float from, float to, float bottom, float top, Material material,
            float lateral = 0f, float depth = FloorDepth, bool collide = true)
        {
            const float step = 1f;
            const float straightDot = 0.9999f;
            // On a curve the outer edge is longer than the centre line. Pieces overlap to close the gaps.
            const float curveOverlap = 1.6f;

            float start = from;
            while (start < to - 1e-3f)
            {
                Vector3 startTangent = rail.Evaluate(start).Tangent;
                float pieceEnd = Mathf.Min(start + step, to);
                while (pieceEnd < to &&
                       Vector3.Dot(startTangent, rail.Evaluate(Mathf.Min(pieceEnd + step, to)).Tangent) > straightDot)
                {
                    pieceEnd = Mathf.Min(pieceEnd + step, to);
                }

                bool straight = Vector3.Dot(startTangent, rail.Evaluate(pieceEnd).Tangent) > straightDot;
                float length = pieceEnd - start;
                RailPoint mid = rail.Evaluate((start + pieceEnd) * 0.5f);

                GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = name;
                block.transform.SetParent(parent, false);
                Vector3 centre = mid.Position + mid.Side * lateral;
                block.transform.SetPositionAndRotation(
                    new Vector3(centre.x, (bottom + top) * 0.5f, centre.z),
                    Quaternion.LookRotation(mid.Tangent, Vector3.up));
                block.transform.localScale = new Vector3(depth, top - bottom, straight ? length : length * curveOverlap);
                block.GetComponent<Renderer>().sharedMaterial = material;
                if (!collide)
                    Object.DestroyImmediate(block.GetComponent<Collider>());
                block.isStatic = true;

                start = pieceEnd;
            }
        }

        public static void AddColumn(Transform parent, Rail rail, float distance, float lateral, float height, Material material)
        {
            RailPoint point = rail.Evaluate(distance);
            Vector3 foot = point.Position + point.Side * lateral;
            foot.y = 0f;
            Quaternion rotation = Quaternion.LookRotation(point.Tangent, Vector3.up);

            Transform column = CreateGroup("Column", parent);
            column.SetPositionAndRotation(foot, rotation);

            AddPart(column, PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(2.2f, 0.6f, 2.2f), material);
            AddPart(column, PrimitiveType.Cylinder, new Vector3(0f, height * 0.5f, 0f), new Vector3(1.4f, height * 0.5f, 1.4f), material);
            AddPart(column, PrimitiveType.Cube, new Vector3(0f, height + 0.4f, 0f), new Vector3(2.6f, 0.8f, 2.6f), material);
        }

        /// <summary>A transform at a rail distance, height and lateral offset, turned to face along the rail.</summary>
        public static Transform PlaceAlongRail(Transform parent, string name, Rail rail, float distance, float height, float lateral = 0f)
        {
            RailPoint point = rail.Evaluate(distance);
            Transform t = CreateGroup(name, parent);
            Vector3 position = point.Position + point.Side * lateral;
            position.y = height;
            t.SetPositionAndRotation(position, Quaternion.LookRotation(point.Tangent, Vector3.up));
            return t;
        }

        /// <summary>Assigns an object reference to a serialized field by name.</summary>
        public static void Wire(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = FindField(serialized, target, field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Assigns an array of object references to a serialized field by name.</summary>
        public static void WireArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = FindField(serialized, target, field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireFloats(Object target, string field, float[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = FindField(serialized, target, field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).floatValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            FindField(serialized, target, field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireInt(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            FindField(serialized, target, field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireBool(Object target, string field, bool value)
        {
            var serialized = new SerializedObject(target);
            FindField(serialized, target, field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireString(Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            FindField(serialized, target, field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireColor(Object target, string field, Color value)
        {
            var serialized = new SerializedObject(target);
            FindField(serialized, target, field).colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty FindField(SerializedObject serialized, Object target, string field)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException($"{target.GetType().Name} has no serialized field named '{field}'.");
            return property;
        }

        public static GameObject InstantiateModel(string path, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                throw new InvalidOperationException($"Model not found at {path}. Has Unity imported the files?");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            instance.name = Path.GetFileNameWithoutExtension(path);
            return instance;
        }

        public static float MeasureHeight(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length == 0)
                return 0f;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds.size.y;
        }

        // ------------------------------------------------------------ characters

        public static CharacterMotor PlacePlayer(Rail rail, GameObject prefab, float distance)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var motor = player.GetComponent<CharacterMotor>();
            Wire(motor, "rail", rail);
            motor.Teleport(rail, distance, 0.05f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            return motor;
        }

        public static EnemyController PlaceEnemy(Rail rail, GameObject prefab, float distance, CharacterMotor target, int facing = -1, Transform parent = null)
        {
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            var motor = enemy.GetComponent<CharacterMotor>();
            Wire(motor, "rail", rail);
            motor.Teleport(rail, distance, 0.05f);
            motor.Facing = facing;
            Wire(enemy.GetComponent<EnemyBrain>(), "target", target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(enemy.transform);
            return enemy.GetComponent<EnemyController>();
        }

        // ------------------------------------------------------------ effects

        /// <summary>The particles where blows land, see <see cref="HitEffects"/>.</summary>
        public static HitEffects CreateHitEffects(string materialFolder)
        {
            Material glow = LoadOrCreateParticleMaterial(materialFolder, "HitGlow", ring: false, additive: true);
            Material ring = LoadOrCreateParticleMaterial(materialFolder, "HitRing", ring: true, additive: true);
            Material dust = LoadOrCreateParticleMaterial(materialFolder, "HitDust", ring: false, additive: false);
            var effects = new GameObject("HitEffects").AddComponent<HitEffects>();
            Wire(effects, "sparks", CreateSparks(effects.transform, glow));
            Wire(effects, "flash", CreateBurst(effects.transform, "Flash", glow, lifetime: 0.08f, size: 0.45f, speed: 0f, grow: 1.5f));
            Wire(effects, "ring", CreateBurst(effects.transform, "Ring", ring, lifetime: 0.2f, size: 0.5f, speed: 0f, grow: 3f));
            Wire(effects, "dust", CreateBurst(effects.transform, "Dust", dust, lifetime: 0.6f, size: 0.35f, speed: 2f, grow: 2.5f));
            return effects;
        }

        /// <summary>The ribbons and sprays of her dash and double jump, see <see cref="MovementEffects"/>.</summary>
        public static MovementEffects CreateMovementEffects(PlayerController player, string materialFolder)
        {
            Material glow = LoadOrCreateParticleMaterial(materialFolder, "HitGlow", ring: false, additive: true);
            Material ringMaterial = LoadOrCreateParticleMaterial(materialFolder, "HitRing", ring: true, additive: true);
            var effects = new GameObject("MovementEffects").AddComponent<MovementEffects>();
            Wire(effects, "player", player);
            // Alpha-blended rather than additive: a ribbon of light would vanish against a bright sky.
            Material ribbon = LoadOrCreateParticleMaterial(materialFolder, "MoveTrail", ring: false, additive: false, streak: true);
            Wire(effects, "dashTrail", NewTrail(effects.transform, "DashTrail", ribbon, 0.3f, 0.2f, new Color(0.45f, 0.85f, 1f, 0.6f)));
            Wire(effects, "jumpTrail", NewTrail(effects.transform, "JumpTrail", ribbon, 0.7f, 0.18f, new Color(1f, 0.8f, 0.3f)));
            Wire(effects, "jumpTrailBack", NewTrail(effects.transform, "JumpTrailBack", ribbon, 0.7f, 0.18f, new Color(1f, 0.8f, 0.3f)));

            ParticleSystem streaks = CreateSparks(effects.transform, glow);
            streaks.name = "Streaks";
            ParticleSystem.MainModule main = streaks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.gravityModifier = 0.2f;
            ParticleSystem.ShapeModule streakShape = streaks.shape;
            streakShape.angle = 25f;
            streakShape.radius = 0.25f;
            Wire(effects, "streaks", streaks);

            // A ring flat on the air at her feet, widening as it fades.
            ParticleSystem ring = CreateBurst(effects.transform, "JumpRing", ringMaterial, lifetime: 0.35f, size: 0.5f, speed: 0f, grow: 4f);
            ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            Wire(effects, "ring", ring);
            return effects;
        }

        /// <summary>A ribbon that follows its object and fades out behind it, tapering to nothing.</summary>
        public static TrailRenderer NewTrail(Transform parent, string name, Material material, float time, float width, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = time;
            trail.emitting = false;
            trail.minVertexDistance = 0.04f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.12f, 1f), new Keyframe(1f, 0f));
            trail.widthMultiplier = width;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.sharedMaterial = material;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0.7f * color.a, 0.4f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = fade;
            return trail;
        }

        /// <summary>
        /// Short streaks thrown forward in a cone, falling under gravity. Emitted by <see cref="HitEffects"/>, which
        /// colours and sizes each burst.
        /// </summary>
        public static ParticleSystem CreateSparks(Transform parent, Material material)
        {
            ParticleSystem system = NewParticles(parent, "Sparks");
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.gravityModifier = 1.5f;
            main.maxParticles = 400;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.05f;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 1.5f;
            renderer.sharedMaterial = material;
            return system;
        }

        /// <summary>Soft round particles that swell and fade: the flash of an impact, a shock ring, a puff of dust.</summary>
        public static ParticleSystem CreateBurst(Transform parent, string name, Material material, float lifetime, float size, float speed, float grow)
        {
            ParticleSystem system = NewParticles(parent, name);
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = lifetime;
            main.startSpeed = speed > 0f ? new ParticleSystem.MinMaxCurve(speed * 0.4f, speed) : new ParticleSystem.MinMaxCurve(0f);
            main.startSize = size;
            main.gravityModifier = speed > 0f ? -0.05f : 0f;
            main.maxParticles = 100;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = speed > 0f ? ParticleSystemShapeType.Circle : ParticleSystemShapeType.Sphere;
            shape.radius = speed > 0f ? 0.3f : 0.01f;

            ParticleSystem.SizeOverLifetimeModule sizeOverLife = system.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(grow, AnimationCurve.EaseInOut(0f, 1f / grow, 1f, 1f));

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            return system;
        }

        /// <summary>A particle system that only emits when told to, in world space, on real time so it runs through hit-stop.</summary>
        public static ParticleSystem NewParticles(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            go.GetComponent<ParticleSystemRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return system;
        }

        // ------------------------------------------------------------- camera

        public static CameraView Flatter(CameraView view)
        {
            view.fieldOfView *= LensScale;
            view.distance *= DistanceScale;
            return view;
        }

        /// <summary>The main camera following her on the rail, with post-processing on and a flattened side view by default.</summary>
        public static RailCamera CreateCamera(Rail rail, CharacterMotor motor)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 400f;
            go.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var railCamera = go.AddComponent<RailCamera>();
            railCamera.Target = motor;
            railCamera.DefaultView = Flatter(new CameraView { yaw = 0f, pitch = 5f, distance = 12f, fieldOfView = 35f, focusHeight = 1.4f, lookAhead = 2f });
            return railCamera;
        }
    }
}
