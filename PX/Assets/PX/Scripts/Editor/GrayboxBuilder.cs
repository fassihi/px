using System;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Splines;
using Object = UnityEngine.Object;

namespace PX.EditorTools
{
    /// <summary>
    /// Generates the graybox test scene from code, so the whole scene is reviewable as a script.
    /// The scene file is overwritten on every rebuild: change the level here, not in the scene.
    /// Config assets, materials and prefabs are only created when missing, so tuning done in the editor survives.
    ///
    /// Menu: PX > Graybox > Rebuild Scene.
    /// Command line: -executeMethod PX.EditorTools.GrayboxBuilder.Build
    /// </summary>
    public static class GrayboxBuilder
    {
        public const string ScenePath = "Assets/PX/Scenes/Graybox.unity";

        private const string ConfigFolder = "Assets/PX/Config";
        private const string MaterialFolder = "Assets/PX/Art/Graybox/Materials";
        private const string PrefabFolder = "Assets/PX/Prefabs";
        private const string HeroineFolder = HeroineImportSettings.Folder;
        private const float HeroineHeight = 1.75f;

        // Enemy A, the armoured knight, stands a head taller than her.
        private const float EnemyAHeight = 2.0f;

        // Where Enemy A waits: near the end of the level, on the last straight.
        private const float EnemyADistance = 110f;
        private const string VolumeProfilePath = "Assets/Settings/SceneVolumeProfile.asset";

        private const float FloorDepth = 8f;
        private const float FloorBottom = -8f;
        private const float StartDistance = 3f;

        private sealed class Palette
        {
            public Material Ground;
            public Material Platform;
            public Material Backdrop;
            public Material PlayerBody;
            public Material PlayerAccent;
            public Material Dummy;
            public Material Slash;
            public Material HitSpark;
        }

        [MenuItem("PX/Graybox/Rebuild Scene")]
        public static void Build()
        {
            // Retargeting needs every avatar in the same reference pose. New Mixamo clips are fixed up here.
            AvatarPose.StraightenAll();

            EnsureFolder(Path.GetDirectoryName(ScenePath));
            EnsureFolder(ConfigFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);

            Palette palette = LoadPalette();
            MoveConfig move = LoadOrCreateAsset<MoveConfig>($"{ConfigFolder}/PlayerMove.asset", null);
            ComboConfig combo = LoadOrCreateAsset<ComboConfig>($"{ConfigFolder}/PlayerLightCombo.asset", FillDefaultCombo);
            HeroineAnimationConfig heroineAnimation = LoadOrCreateAsset<HeroineAnimationConfig>($"{ConfigFolder}/HeroineAnimation.asset", FillHeroineAnimation);
            GameObject playerPrefab = LoadOrCreatePrefab($"{PrefabFolder}/Player.prefab", () => CreatePlayer(move, combo, heroineAnimation, palette));
            GameObject dummyPrefab = LoadOrCreatePrefab($"{PrefabFolder}/TrainingDummy.prefab", () => CreateDummy(palette));
            EnemyConfig enemyA = LoadOrCreateAsset<EnemyConfig>($"{ConfigFolder}/EnemyA.asset", FillEnemyA);
            GameObject enemyAPrefab = LoadOrCreatePrefab($"{PrefabFolder}/EnemyA.prefab", () => CreateEnemyA(enemyA));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Rail rail = CreateRail();
            BuildLevel(rail, palette);
            PlaceDummies(rail, dummyPrefab);
            CharacterMotor motor = PlacePlayer(rail, playerPrefab);
            PlaceEnemy(rail, enemyAPrefab, EnemyADistance, motor);
            RailCamera railCamera = CreateCamera(rail, motor);
            CreateLighting();
            CreateOverlay(motor, railCamera);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"Graybox scene rebuilt at {ScenePath}. Rail length {rail.Length:0.0} m.");
        }

        // ---------------------------------------------------------------- rail

        /// <summary>
        /// A straight run, a quarter turn, another straight run.
        /// The turn is there to prove the game is not tied to a flat plane.
        /// </summary>
        private static Rail CreateRail()
        {
            var go = new GameObject("Rail");
            var rail = go.AddComponent<Rail>();
            Spline spline = go.GetComponent<SplineContainer>().Spline;
            spline.Clear();

            const float firstStraight = 40f;
            const float radius = 10f;
            const float secondStraight = 62f;
            const float handle = 12f;
            // Handle length that makes a cubic Bezier follow a quarter circle.
            float arc = radius * 0.5523f;

            float cornerX = firstStraight + radius;
            spline.Add(new BezierKnot(new float3(0f, 0f, 0f), new float3(-handle, 0f, 0f), new float3(handle, 0f, 0f)), TangentMode.Broken);
            spline.Add(new BezierKnot(new float3(firstStraight, 0f, 0f), new float3(-handle, 0f, 0f), new float3(arc, 0f, 0f)), TangentMode.Broken);
            spline.Add(new BezierKnot(new float3(cornerX, 0f, radius), new float3(0f, 0f, -arc), new float3(0f, 0f, handle)), TangentMode.Broken);
            spline.Add(new BezierKnot(new float3(cornerX, 0f, radius + secondStraight), new float3(0f, 0f, -handle), new float3(0f, 0f, handle)), TangentMode.Broken);

            rail.Rebuild();
            return rail;
        }

        // --------------------------------------------------------------- level

        private static void BuildLevel(Rail rail, Palette palette)
        {
            Transform level = new GameObject("Level").transform;
            Transform ground = CreateGroup("Ground", level);
            Transform platforms = CreateGroup("Platforms", level);
            Transform backdrop = CreateGroup("Backdrop", level);
            float end = rail.Length;

            // Floor, with one trench to jump over or climb out of.
            AddBlocks(ground, rail, "Floor", 0f, 22f, FloorBottom, 0f, palette.Ground);
            AddBlocks(ground, rail, "Trench", 22f, 26f, FloorBottom, -2.5f, palette.Ground);
            AddBlocks(ground, rail, "Floor", 26f, end, FloorBottom, 0f, palette.Ground);

            // Jump tests on the first straight.
            AddBlocks(platforms, rail, "Step low", 8f, 11f, 0f, 1.5f, palette.Platform);
            AddBlocks(platforms, rail, "Step high", 11f, 15f, 0f, 3f, palette.Platform);
            AddBlocks(platforms, rail, "Floating platform", 28f, 32f, 2.3f, 2.6f, palette.Platform);
            AddBlocks(platforms, rail, "Wall", 35.5f, 36.5f, 0f, 2f, palette.Platform);

            // A ledge after the dummies, to fight around.
            AddBlocks(platforms, rail, "Ledge", 74f, 79f, 0f, 1.5f, palette.Platform);

            // A terrace and a colonnade on the far side of the rail give the eye something to measure depth with.
            // On the last stretch the camera looks down the rail, so the near side gets columns too: a corridor to run through.
            const float corridorFrom = 96f;
            AddBlocks(backdrop, rail, "Terrace", 0f, end, FloorBottom, 0f, palette.Ground, lateral: -10f, depth: 12f);
            AddBlocks(backdrop, rail, "Terrace near", 80f, end, FloorBottom, 0f, palette.Ground, lateral: 10f, depth: 12f);
            for (float d = 2f; d < end; d += 7f)
            {
                AddColumn(backdrop, rail, d, -7f, 11f, palette.Backdrop);
                if (d > corridorFrom)
                    AddColumn(backdrop, rail, d, 7f, 11f, palette.Backdrop);
            }
        }

        /// <summary>
        /// Lays floor or platform along the rail between two distances.
        /// Straight stretches become one block; curved stretches are cut into short pieces that follow the bend.
        /// </summary>
        private static void AddBlocks(
            Transform parent, Rail rail, string name, float from, float to, float bottom, float top, Material material,
            float lateral = 0f, float depth = FloorDepth)
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
                block.isStatic = true;

                start = pieceEnd;
            }
        }

        private static void AddColumn(Transform parent, Rail rail, float distance, float lateral, float height, Material material)
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

        // ---------------------------------------------------------- characters

        private static CharacterMotor PlacePlayer(Rail rail, GameObject prefab)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var motor = player.GetComponent<CharacterMotor>();
            Wire(motor, "rail", rail);
            motor.Teleport(rail, StartDistance, 0.05f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            return motor;
        }

        private static void PlaceDummies(Rail rail, GameObject prefab)
        {
            Transform group = new GameObject("Dummies").transform;
            foreach (float distance in new[] { 62f, 65.5f, 69f })
            {
                RailPoint point = rail.Evaluate(distance);
                var dummy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                dummy.transform.SetPositionAndRotation(
                    new Vector3(point.Position.x, 0f, point.Position.z),
                    Quaternion.LookRotation(point.Tangent, Vector3.up));
                PrefabUtility.RecordPrefabInstancePropertyModifications(dummy.transform);
            }
        }

        private static GameObject CreatePlayer(MoveConfig move, ComboConfig combo, HeroineAnimationConfig animation, Palette palette)
        {
            var root = new GameObject("Player") { tag = "Player" };

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.7f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.85f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 50f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f;

            root.AddComponent<CharacterMotor>();
            root.AddComponent<Health>();
            root.AddComponent<PlayerInputSource>();
            var player = root.AddComponent<PlayerController>();
            var debugView = root.AddComponent<PlayerDebugView>();
            var heroineAnimator = root.AddComponent<HeroineAnimator>();

            Animator animator = AddHeroineVisual(root.transform);
            GameObject slash = AddPart(root.transform, PrimitiveType.Cube, Vector3.zero, Vector3.one, palette.Slash);
            slash.name = "Hitbox";
            slash.SetActive(false);

            Wire(player, "move", move);
            Wire(player, "combo", combo);
            Wire(debugView, "player", player);
            Wire(debugView, "hitboxVisual", slash.transform);
            Wire(heroineAnimator, "player", player);
            Wire(heroineAnimator, "motor", root.GetComponent<CharacterMotor>());
            Wire(heroineAnimator, "animator", animator);
            Wire(heroineAnimator, "config", animation);

            var sparks = root.AddComponent<HitSparks>();
            Wire(sparks, "player", player);
            Wire(sparks, "sparks", CreateHitSparks(root.transform, palette));
            return root;
        }

        /// <summary>
        /// The burst of sparks where a blow lands. Short streaks thrown forward in a cone, on real time so they still
        /// fly during hit-stop. Emitted by <see cref="HitSparks"/>, never played on its own.
        /// </summary>
        private static ParticleSystem CreateHitSparks(Transform parent, Palette palette)
        {
            var go = new GameObject("HitSparks");
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.gravityModifier = 1.5f;
            main.maxParticles = 200;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.05f;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 2f;
            renderer.sharedMaterial = palette.HitSpark;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return system;
        }

        // ------------------------------------------------------------ heroine

        private static void FillHeroineAnimation(HeroineAnimationConfig config)
        {
            // Most of her clips come from Mixamo. The standing jump comes from the Universal Animation Library (CC0);
            // it is a humanoid rig too, so its clips retarget onto her.
            AnimationClip[] library = RanaViewerBuilder.LoadClips($"{HeroineFolder}/Animations/UAL1_Standard.fbx");

            // Her idle is the library's plain standing idle.
            config.idle = RanaViewerBuilder.FindClip(library, "Idle_Loop");
            // Her run (from Mixamo's Female Locomotion Pack), turn and running jump, retargeted onto her humanoid rig.
            config.run = MixamoClip("Running");
            config.runTurn = MixamoClip("Running Turn 180");
            config.runJump = MixamoClip("Unarmed Jump Running");
            // The combo is a jab, a cross and a side kick from Mixamo, each trimmed to its strike. Times are seconds
            // into the clip: where the part that plays starts, where the blow lands, and where it ends.
            config.comboAttacks = new[]
            {
                MixamoWindow("Combo 1 Jab", 0.1f, 0.4f, 0.85f),
                MixamoWindow("Combo 2 Cross", 0.75f, 1.13f, 1.5f),
                MixamoWindow("Combo 3 Side Kick", 0.15f, 0.63f, 1.2f),
            };
            config.attack = config.comboAttacks[0].clip;
            config.crouchDown = MixamoWindow("Crouch Down", 0.25f, 0f, 1f);
            config.crouchIdle = MixamoClip("Crouch Idle");
            config.crouchUp = MixamoWindow("Crouch Up", 0f, 0f, 0.75f);
            config.crouchAttack = MixamoWindow("Crouch Sweep", 0.4f, 0.87f, 1.4f);
            // The landings start at touchdown. Landing at a run plays the end of the running jump: it follows on from the
            // jump's pose, and its arms end where the run's begin. (Mixamo's Land Run holds one arm out in front.)
            config.landRun = MixamoWindow("Unarmed Jump Running", 1.04f, 0f, 1.4f);
            config.landIdle = MixamoWindow("Land Idle", 0.3f, 0f, 1.1f);
            config.hurt = new ClipWindow { clip = RanaViewerBuilder.FindClip(library, "Hit_Chest") };
            config.jumpStart = RanaViewerBuilder.FindClip(library, "Jump_Start");
            config.jumpLoop = RanaViewerBuilder.FindClip(library, "Jump_Loop");
            // The dash is too short for a whole move to read, so it holds a single Mixamo pose: a sprinting lunge on the
            // ground, a forward leap with the arms swept back in the air.
            config.dash = MixamoClip("Dash Ground Pose");
            config.airDash = MixamoClip("Dash Air Pose");
            config.stretchDashToDuration = false;
        }

        private static ClipWindow MixamoWindow(string name, float from, float impact, float to)
        {
            return new ClipWindow { clip = MixamoClip(name), from = from, impact = impact, to = to };
        }

        private static AnimationClip MixamoClip(string name)
        {
            return RanaViewerBuilder.FindClip(RanaViewerBuilder.LoadClips($"{RanaImportSettings.MixamoFolder}/{name}.fbx"), name);
        }

        /// <summary>Rana's rigged model, scaled to the character controller's height.</summary>
        private static Animator AddHeroineVisual(Transform root)
        {
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);

            GameObject model = InstantiateModel(RanaImportSettings.BodyPath, visual);
            Material skin = RanaViewerBuilder.LoadOrCreateMaterial();
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                renderer.sharedMaterial = skin;

            float height = MeasureHeight(model);
            if (height > 0.01f)
                visual.localScale = Vector3.one * (HeroineHeight / height);
            Debug.Log($"Rana's model is {height:0.00} m tall before scaling.");

            Animator animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return animator;
        }

        private static GameObject InstantiateModel(string path, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                throw new InvalidOperationException($"Model not found at {path}. Has Unity imported Rana's files?");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            instance.name = Path.GetFileNameWithoutExtension(path);
            return instance;
        }

        private static float MeasureHeight(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length == 0)
                return 0f;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds.size.y;
        }

        // ------------------------------------------------------------ enemies

        /// <summary>
        /// Enemy A is a Tripo knight, rigged as a humanoid like Rana. Its clips are Mixamo's Great Sword set: idle,
        /// walk, downward slash, unblocked impact, and the two-handed sword death falling back.
        /// </summary>
        private static void FillEnemyA(EnemyConfig config)
        {
            AnimationClip Clip(string name) => RanaViewerBuilder.FindClip(
                RanaViewerBuilder.LoadClips($"{EnemyImportSettings.EnemyAMixamoFolder}/{name}.fbx"), name);
            config.idle = Clip("Idle");
            config.walk = Clip("Walk");
            // The chop is furthest out 0.64 s into the clip; that is lined up with the live hitbox (EnemyTests checks it).
            config.attackClip = new ClipWindow { clip = Clip("Attack"), impact = 0.64f };
            config.hurt = new ClipWindow { clip = Clip("Hit") };
            config.death = Clip("Death");
        }

        private static GameObject CreateEnemyA(EnemyConfig config)
        {
            var root = new GameObject("EnemyA");

            var controller = root.AddComponent<CharacterController>();
            controller.height = EnemyAHeight;
            controller.radius = 0.45f;
            controller.center = new Vector3(0f, EnemyAHeight * 0.5f, 0f);
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f;

            root.AddComponent<CharacterMotor>();
            var health = root.AddComponent<Health>();
            var serializedHealth = new SerializedObject(health);
            serializedHealth.FindProperty("max").floatValue = 80f;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
            var enemy = root.AddComponent<EnemyController>();
            var brain = root.AddComponent<EnemyBrain>();
            var enemyAnimator = root.AddComponent<EnemyAnimator>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            GameObject model = InstantiateModel(EnemyImportSettings.EnemyABodyPath, visual);
            Material skin = LoadOrCreateTexturedMaterial("EnemyA", EnemyImportSettings.EnemyATexturePath);
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                renderer.sharedMaterial = skin;
            float height = MeasureHeight(model);
            if (height > 0.01f)
                visual.localScale = Vector3.one * (EnemyAHeight / height);

            Animator animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Wire(enemy, "config", config);
            Wire(brain, "body", enemy);
            Wire(enemyAnimator, "enemy", enemy);
            Wire(enemyAnimator, "animator", animator);
            return root;
        }

        private static void PlaceEnemy(Rail rail, GameObject prefab, float distance, CharacterMotor target)
        {
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var motor = enemy.GetComponent<CharacterMotor>();
            Wire(motor, "rail", rail);
            motor.Teleport(rail, distance, 0.05f);
            motor.Facing = -1;
            Wire(enemy.GetComponent<EnemyBrain>(), "target", target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(enemy.transform);
        }

        private static Material LoadOrCreateTexturedMaterial(string name, string texturePath)
        {
            string path = $"{Path.GetDirectoryName(texturePath).Replace("/Textures", "")}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetFloat("_Smoothness", 0.3f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject CreateDummy(Palette palette)
        {
            var root = new GameObject("TrainingDummy");

            var collider = root.AddComponent<CapsuleCollider>();
            collider.height = 1.8f;
            collider.radius = 0.4f;
            collider.center = new Vector3(0f, 0.9f, 0f);

            root.AddComponent<Health>();
            var dummy = root.AddComponent<TrainingDummy>();

            GameObject body = AddPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), palette.Dummy);
            body.name = "Body";
            AddPart(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.35f, 0f), new Vector3(0.16f, 0.16f, 1.3f), palette.Dummy).name = "Arms";

            Wire(dummy, "body", body.GetComponent<Renderer>());
            return root;
        }

        // ------------------------------------------------------- camera, light

        private static RailCamera CreateCamera(Rail rail, CharacterMotor motor)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 400f;
            go.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var railCamera = go.AddComponent<RailCamera>();
            railCamera.Target = motor;
            railCamera.DefaultView = CameraView.Side;

            // The same gameplay, seen four ways. Distances are metres along the rail.
            railCamera.Zones.Add(new CameraZone
            {
                name = "Corner: three-quarter view through the turn",
                from = 38f,
                to = 58f,
                view = new CameraView { yaw = 28f, pitch = 14f, distance = 16f, fieldOfView = 35f, focusHeight = 1.6f, lookAhead = 2f },
            });
            railCamera.Zones.Add(new CameraZone
            {
                name = "Arena: pulled back for the fight",
                from = 58f,
                to = 72f,
                view = new CameraView { yaw = 0f, pitch = 5f, distance = 16f, fieldOfView = 35f, focusHeight = 1.6f, lookAhead = 1f },
            });
            railCamera.Zones.Add(new CameraZone
            {
                name = "Into depth: behind her, looking down the rail",
                from = 82f,
                to = EnemyADistance - 10f,
                view = new CameraView { yaw = 72f, pitch = 18f, distance = 11f, fieldOfView = 50f, focusHeight = 1.6f, lookAhead = 3f },
            });
            railCamera.Zones.Add(new CameraZone
            {
                name = "Enemy A: back to the side view for the fight",
                from = EnemyADistance - 10f,
                to = rail.Length,
                view = new CameraView { yaw = 0f, pitch = 5f, distance = 14f, fieldOfView = 35f, focusHeight = 1.6f, lookAhead = 1.5f },
            });

            railCamera.Snap();
            return railCamera;
        }

        private static void CreateLighting()
        {
            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.82f);
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, 35f, 0f);

            // Flat ambient colours need no bake, so the scene looks the same on a fresh checkout.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.66f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.56f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.3f, 0.24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.009f;
            RenderSettings.fogColor = new Color(0.78f, 0.74f, 0.68f);

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile != null)
            {
                var volume = new GameObject("Global Volume").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
            }
        }

        private static void CreateOverlay(CharacterMotor motor, RailCamera railCamera)
        {
            var overlay = new GameObject("Overlay").AddComponent<GrayboxOverlay>();
            Wire(overlay, "player", motor.GetComponent<PlayerController>());
            Wire(overlay, "motor", motor);
            Wire(overlay, "railCamera", railCamera);
        }

        // -------------------------------------------------------------- assets

        private static Palette LoadPalette()
        {
            // Colours borrowed from the glazed brick friezes of Susa: lapis, turquoise, gold, ochre.
            return new Palette
            {
                Ground = LoadOrCreateMaterial("Ground", new Color(0.76f, 0.62f, 0.42f), lit: true),
                Platform = LoadOrCreateMaterial("Platform", new Color(0.16f, 0.6f, 0.6f), lit: true),
                Backdrop = LoadOrCreateMaterial("Backdrop", new Color(0.83f, 0.78f, 0.68f), lit: true),
                PlayerBody = LoadOrCreateMaterial("PlayerBody", new Color(0.12f, 0.22f, 0.62f), lit: true),
                PlayerAccent = LoadOrCreateMaterial("PlayerAccent", new Color(0.95f, 0.75f, 0.2f), lit: true),
                Dummy = LoadOrCreateMaterial("Dummy", new Color(0.72f, 0.33f, 0.2f), lit: true),
                Slash = LoadOrCreateMaterial("Slash", new Color(1f, 0.87f, 0.45f, 0.4f), lit: false),
                HitSpark = LoadOrCreateMaterial("HitSpark", new Color(1f, 0.85f, 0.4f), lit: false),
            };
        }

        private static Material LoadOrCreateMaterial(string name, Color color, bool lit)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            string shaderName = lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit";
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"Shader '{shaderName}' not found. Is the Universal Render Pipeline package installed?");

            material = new Material(shader);
            material.SetColor("_BaseColor", color);
            if (lit)
                material.SetFloat("_Smoothness", 0.15f);
            if (color.a < 1f)
                MakeTransparent(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Switches a URP material to alpha blending. This is what the Surface Type dropdown does in the Inspector.</summary>
        private static void MakeTransparent(Material material)
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

        private static void FillDefaultCombo(ComboConfig combo)
        {
            combo.steps = new[]
            {
                new AttackStep
                {
                    startup = 0.06f, active = 0.08f, recovery = 0.22f, cancelDelay = 0.04f,
                    damage = 10f, lunge = 3f, hitStop = 0.08f, shake = 0.06f, knockback = 2.5f,
                    hitboxCenter = new Vector2(1.1f, 1f), hitboxSize = new Vector2(1.8f, 1.4f),
                },
                new AttackStep
                {
                    startup = 0.07f, active = 0.08f, recovery = 0.24f, cancelDelay = 0.05f,
                    damage = 12f, lunge = 3.5f, hitStop = 0.09f, shake = 0.07f, knockback = 3f,
                    hitboxCenter = new Vector2(1.2f, 1.1f), hitboxSize = new Vector2(2f, 1.6f),
                },
                new AttackStep
                {
                    startup = 0.12f, active = 0.1f, recovery = 0.38f, cancelDelay = 0.14f,
                    damage = 22f, lunge = 2.5f, hitStop = 0.16f, shake = 0.16f, knockback = 7f,
                    hitboxCenter = new Vector2(1.4f, 1.1f), hitboxSize = new Vector2(2.6f, 2f),
                },
            };
        }

        private static T LoadOrCreateAsset<T>(string path, Action<T> initialize) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            initialize?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static GameObject LoadOrCreatePrefab(string path, Func<GameObject> create)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                return prefab;

            GameObject instance = create();
            prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        // ------------------------------------------------------------- helpers

        private static Transform CreateGroup(string name, Transform parent)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        /// <summary>A primitive used as a visual only: its collider is removed.</summary>
        private static GameObject AddPart(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        /// <summary>Assigns an object reference to a serialized field by name.</summary>
        private static void Wire(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException($"{target.GetType().Name} has no serialized field named '{field}'.");

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
