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

        // Enemy A, the armoured knight, stands a head taller than her. Enemy B, the swordsman, a little less.
        private const float EnemyAHeight = 2.0f;
        private const float EnemyBHeight = 1.9f;

        // Where the enemies wait on the last straight: Enemy A in an arena with crates to climb, then a stretch of
        // ledges to climb, then Enemy B at the end.
        private const float EnemyADistance = 112f;
        private const float ArenaAEnd = 126f;
        private const float LedgesEnd = 158f;
        private const float EnemyBDistance = 178f;

        // Where she gets up after a fall: the start, before each fight, and before the ledges.
        private static readonly float[] Checkpoints = { StartDistance, 98f, ArenaAEnd + 1f, LedgesEnd + 1f };
        // Enemy clip windows, in seconds into each Mixamo clip: where the part that plays starts, where the blade is
        // furthest out, and where it ends. Measured by sampling the clips on the enemies.
        private const float AttackA2From = 0.7f, AttackA2Impact = 1.4f, AttackA2To = 1.8f;
        private const float AttackB1From = 0.1f, AttackB1Impact = 0.6f, AttackB1To = 1.2f;
        private const float AttackB2From = 0.1f, AttackB2Impact = 0.7f, AttackB2To = 1.5f;
        // The climb is Mixamo's running jump, all of it.
        private const float ClimbAFrom = 0f, ClimbATo = 0f;
        private const float ClimbBFrom = 0f, ClimbBTo = 0f;

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
            PlayerCombatConfig combat = LoadOrCreateAsset<PlayerCombatConfig>($"{ConfigFolder}/PlayerCombat.asset", null);
            HeroineAnimationConfig heroineAnimation = LoadOrCreateAsset<HeroineAnimationConfig>($"{ConfigFolder}/HeroineAnimation.asset", FillHeroineAnimation);
            GameObject playerPrefab = LoadOrCreatePrefab($"{PrefabFolder}/Player.prefab", () => CreatePlayer(move, combo, combat, heroineAnimation, palette));
            GameObject dummyPrefab = LoadOrCreatePrefab($"{PrefabFolder}/TrainingDummy.prefab", () => CreateDummy(palette));
            EnemyConfig enemyA = LoadOrCreateAsset<EnemyConfig>($"{ConfigFolder}/EnemyA.asset", FillEnemyA);
            GameObject enemyAPrefab = LoadOrCreatePrefab($"{PrefabFolder}/EnemyA.prefab", () => CreateEnemy(
                "EnemyA", enemyA, EnemyImportSettings.EnemyABodyPath, EnemyImportSettings.EnemyATexturePath, EnemyAHeight, 120f, 0.45f));
            EnemyConfig enemyB = LoadOrCreateAsset<EnemyConfig>($"{ConfigFolder}/EnemyB.asset", FillEnemyB);
            GameObject enemyBPrefab = LoadOrCreatePrefab($"{PrefabFolder}/EnemyB.prefab", () => CreateEnemy(
                "EnemyB", enemyB, EnemyImportSettings.EnemyBBodyPath, EnemyImportSettings.EnemyBTexturePath, EnemyBHeight, 80f, 0.4f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Rail rail = CreateRail();
            BuildLevel(rail, palette);
            PlaceDummies(rail, dummyPrefab);
            CharacterMotor motor = PlacePlayer(rail, playerPrefab);
            EnemyController[] enemies =
            {
                PlaceEnemy(rail, enemyAPrefab, EnemyADistance, motor),
                PlaceEnemy(rail, enemyBPrefab, EnemyBDistance, motor),
            };
            RailCamera railCamera = CreateCamera(rail, motor);
            CreateLighting();
            CreateOverlay(motor, railCamera, enemies);
            CreateCombatLayer(motor.GetComponent<PlayerController>(), enemies);

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
            // Long enough for Enemy A's arena, the ledges and Enemy B's ground at the end.
            const float secondStraight = 140f;
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

            // Enemy A's arena: crates she can jump onto and the knight climbs after her, one onto the other.
            AddBlocks(platforms, rail, "Crate low", 117f, 119f, 0f, 1.4f, palette.Platform);
            AddBlocks(platforms, rail, "Crate high", 119f, 122f, 0f, 2.6f, palette.Platform);

            // The ledges: walls too tall to jump onto, whose tops she catches and climbs. A jump reaches the first
            // wall's top with her hands; the second, from the first, the same. Falling between them drops her to the floor.
            AddBlocks(platforms, rail, "Climb wall", 130f, 135f, 0f, 4.5f, palette.Platform);
            AddBlocks(platforms, rail, "Climb wall high", 138f, 143f, 0f, 9.1f, palette.Platform);
            AddBlocks(platforms, rail, "Ledge step down", 146f, 150f, 0f, 4.5f, palette.Platform);
            // A floating ledge to catch from below and pull up onto.
            AddBlocks(platforms, rail, "Hanging ledge", 152.5f, 156f, 3.9f, 4.4f, palette.Platform);

            // Enemy B's ground: a low wall it climbs over to reach her.
            AddBlocks(platforms, rail, "Low wall", 169f, 170.5f, 0f, 1.1f, palette.Platform);

            // A terrace and a colonnade on the far side of the rail give the eye something to measure depth with.
            // On the last stretch the camera looks down the rail, so the near side gets columns too: a corridor to run through.
            const float corridorFrom = 96f;
            AddBlocks(backdrop, rail, "Terrace", 0f, end, FloorBottom, 0f, palette.Ground, lateral: -10f, depth: 12f);
            AddBlocks(backdrop, rail, "Terrace near", 80f, end, FloorBottom, 0f, palette.Ground, lateral: 10f, depth: 12f);
            for (float d = 2f; d < end; d += 7f)
            {
                AddColumn(backdrop, rail, d, -7f, 11f, palette.Backdrop);
                // Only where the camera looks down the rail: in the side views past it they would stand in front of the camera.
                if (d > corridorFrom && d < EnemyADistance - 10f)
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

        private static GameObject CreatePlayer(MoveConfig move, ComboConfig combo, PlayerCombatConfig combat, HeroineAnimationConfig animation, Palette palette)
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
            var stamina = root.AddComponent<Stamina>();
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
            Wire(player, "combat", combat);
            Wire(stamina, "config", combat);
            Wire(debugView, "player", player);
            Wire(debugView, "hitboxVisual", slash.transform);
            Wire(heroineAnimator, "player", player);
            Wire(heroineAnimator, "motor", root.GetComponent<CharacterMotor>());
            Wire(heroineAnimator, "animator", animator);
            Wire(heroineAnimator, "config", animation);

            root.AddComponent<HitFlash>();
            return root;
        }

        /// <summary>
        /// What sits beside the fighters in the scene: the particles where blows land, the health and stamina bars, and
        /// getting back up at a checkpoint after a fall.
        /// </summary>
        private static void CreateCombatLayer(PlayerController player, EnemyController[] enemies)
        {
            Material glow = LoadOrCreateParticleMaterial("HitGlow", ring: false, additive: true);
            Material ring = LoadOrCreateParticleMaterial("HitRing", ring: true, additive: true);
            Material dust = LoadOrCreateParticleMaterial("HitDust", ring: false, additive: false);
            var effects = new GameObject("HitEffects").AddComponent<HitEffects>();
            Wire(effects, "sparks", CreateSparks(effects.transform, glow));
            Wire(effects, "flash", CreateBurst(effects.transform, "Flash", glow, lifetime: 0.08f, size: 0.45f, speed: 0f, grow: 1.5f));
            Wire(effects, "ring", CreateBurst(effects.transform, "Ring", ring, lifetime: 0.2f, size: 0.5f, speed: 0f, grow: 3f));
            Wire(effects, "dust", CreateBurst(effects.transform, "Dust", dust, lifetime: 0.6f, size: 0.35f, speed: 2f, grow: 2.5f));

            CreateMovementEffects(player, glow, ring);

            var hud = new GameObject("CombatHud").AddComponent<CombatHud>();
            Wire(hud, "player", player);
            WireArray(hud, "enemies", enemies.Select(e => (Object)e.GetComponent<EnemyBrain>()).ToArray());

            var respawn = new GameObject("PlayerRespawn").AddComponent<PlayerRespawn>();
            Wire(respawn, "player", player);
            WireArray(respawn, "enemies", enemies.Cast<Object>().ToArray());
            var serialized = new SerializedObject(respawn);
            SerializedProperty checkpoints = serialized.FindProperty("checkpoints");
            checkpoints.arraySize = Checkpoints.Length;
            for (int i = 0; i < Checkpoints.Length; i++)
                checkpoints.GetArrayElementAtIndex(i).floatValue = Checkpoints[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The ribbons and sprays of her dash and double jump, see <see cref="MovementEffects"/>.</summary>
        private static void CreateMovementEffects(PlayerController player, Material glow, Material ringMaterial)
        {
            var effects = new GameObject("MovementEffects").AddComponent<MovementEffects>();
            Wire(effects, "player", player);
            // Alpha-blended rather than additive: a ribbon of light would vanish against the bright sky.
            Material ribbon = LoadOrCreateParticleMaterial("MoveTrail", ring: false, additive: false, streak: true);
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
        }

        /// <summary>A ribbon that follows its object and fades out behind it, tapering to nothing.</summary>
        private static TrailRenderer NewTrail(Transform parent, string name, Material material, float time, float width, Color color)
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
        private static ParticleSystem CreateSparks(Transform parent, Material material)
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
        private static ParticleSystem CreateBurst(Transform parent, string name, Material material, float lifetime, float size, float speed, float grow)
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

        /// <summary>
        /// A URP particle material on a generated soft texture: a dot fading from the centre, or a thin ring.
        /// Additive for light (sparks, flashes), alpha-blended for dust.
        /// </summary>
        private static Material LoadOrCreateParticleMaterial(string name, bool ring, bool additive, bool streak = false)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetTexture("_BaseMap", LoadOrCreateSoftTexture(streak ? "SoftStreak" : ring ? "SoftRing" : "SoftDot", ring, streak));
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

        private static Texture2D LoadOrCreateSoftTexture(string name, bool ring, bool streak = false)
        {
            string path = $"{MaterialFolder}/{name}.png";
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
            var image = new Texture2D(size, size, TextureFormat.RGBA32, false);
            image.SetPixels32(pixels);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>A particle system that only emits when told to, in world space, on real time so it runs through hit-stop.</summary>
        private static ParticleSystem NewParticles(Transform parent, string name)
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

        // ------------------------------------------------------------ heroine

        private static void FillHeroineAnimation(HeroineAnimationConfig config)
        {
            // Most of her clips come from Mixamo. The standing jump comes from the Universal Animation Library (CC0);
            // it is a humanoid rig too, so its clips retarget onto her.
            AnimationClip[] library = RanaViewerBuilder.LoadClips($"{HeroineFolder}/Animations/UAL1_Standard.fbx");

            // Her idle is the library's plain standing idle.
            config.idle = RanaViewerBuilder.FindClip(library, "Idle_Loop");
            // Her run (from Mixamo's Female Locomotion Pack: a plain, natural run), turn and running jump, retargeted
            // onto her humanoid rig.
            config.run = MixamoClip("Running");
            // How fast that run's planted foot sweeps back at her size, in m/s (see Run Clip Speed).
            config.runClipSpeed = RunClipSpeed;
            config.runTurn = MixamoClip("Running Turn 180");
            config.runJump = MixamoClip("Unarmed Jump Running");
            // The combo is a jab, a cross and a side kick from Mixamo, each trimmed to its strike. Times are seconds
            // into the clip: where the part that plays starts, where the blow lands, and where it ends.
            config.comboAttacks = new[]
            {
                MixamoWindow("Combo 1 Jab", 0.1f, 0.4f, 0.85f),
                MixamoWindow("Combo 2 Cross", 0.75f, 1.13f, 1.5f),
                MixamoWindow("Combo 3 Side Kick", 0.15f, 0.63f, 1.2f),
                MixamoWindow("Combo 4 Roundhouse", Roundhouse.x, Roundhouse.y, Roundhouse.z),
                MixamoWindow("Combo 5 Spinning Back Kick", SpinningKick.x, SpinningKick.y, SpinningKick.z),
            };
            // The counter after a parry: Mixamo's flying hurricane kick.
            config.counter = MixamoWindow("Counter Hurricane Kick", HurricaneKick.x, HurricaneKick.y, HurricaneKick.z);
            config.attack = config.comboAttacks[0].clip;
            // No crouch down or stand up clip: Mixamo's are upright squats that end in a different pose from the held
            // crouch below, which made two movements (squat, then a snap to the pose). She blends straight into the pose.
            config.crouchDown = default;
            // Down on one knee with a hand to the ground: Mixamo's male crouch pose, more ninja than a plain squat.
            config.crouchIdle = MixamoClip("Ninja Crouch");
            config.crouchUp = default;
            config.crouchAttack = MixamoWindow("Crouch Sweep", 0.4f, 0.87f, 1.4f);
            // The landings start at touchdown. Landing at a run plays the end of the running jump: it follows on from the
            // jump's pose, and its arms end where the run's begin. (Mixamo's Land Run holds one arm out in front.)
            config.landRun = MixamoWindow("Unarmed Jump Running", 1.04f, 0f, 1.4f);
            config.landIdle = MixamoWindow("Land Idle", 0.3f, 0f, 1.1f);
            // Hit: Mixamo's large hit reaction from the front, trimmed to the recoil, so it snaps back on the blow.
            config.hurt = MixamoWindow("Hurt", HurtWindow.x, 0f, HurtWindow.y);
            config.death = MixamoWindow("Death", 0f, 0f, DeathEnd);
            config.dodge = MixamoWindow("Standing Dodge Backward", DodgeWindow.x, 0f, DodgeWindow.y);
            // The flying kick (Mixamo's Flying Kick): knee up, then the leg thrown out at 0.5 s.
            config.airAttack = MixamoWindow("Air Attack", AirAttackWindow.x, AirAttackWindow.y, AirAttackWindow.z);
            config.guard = MixamoClip("Guard");
            config.parry = MixamoWindow("Parry", ParryWindow.x, 0f, ParryWindow.y);
            config.guardHit = MixamoWindow("Guard Hit", GuardHitWindow.x, 0f, GuardHitWindow.y);
            config.ledgeGrab = MixamoWindow("Ledge Grab", LedgeGrabWindow.x, 0f, LedgeGrabWindow.y);
            config.ledgeHang = MixamoClip("Ledge Hang Braced");
            config.ledgeClimb = MixamoWindow("Ledge Climb", LedgeClimbWindow.x, 0f, LedgeClimbWindow.y);
            config.jumpStart = RanaViewerBuilder.FindClip(library, "Jump_Start");
            config.jumpLoop = RanaViewerBuilder.FindClip(library, "Jump_Loop");
            // The dash is too short for a whole move to read, so it holds a single Mixamo pose: a sprinting lunge on the
            // ground, a forward leap with the arms swept back in the air.
            config.dash = MixamoClip("Dash Ground Pose");
            config.airDash = MixamoClip("Dash Air Pose");
            config.stretchDashToDuration = false;
        }

        // Rana's new clip windows, in seconds into each Mixamo clip (from, impact, to for strikes; from, to otherwise).
        // Measured by sampling the clips on her: a strike's impact is where the striking hand or foot is furthest out.
        // The hurricane kick spins in the air with a foot out every 0.45 s; one turn of it is the counter.
        private static readonly Vector3 Roundhouse = new Vector3(0.25f, 0.73f, 1.3f);
        private static readonly Vector3 SpinningKick = new Vector3(0.35f, 0.93f, 1.6f);
        private static readonly Vector3 HurricaneKick = new Vector3(0.15f, 0.45f, 1.0f);
        private static readonly Vector2 HurtWindow = new Vector2(0f, 0.9f);
        // Her body reaches the floor 0.93 s into the fall and settles by 1.5 s.
        private const float DeathEnd = 1.5f;
        private const float RunClipSpeed = 3.1f;
        // A quick backstep with her guard kept up, played in place: the clip's own lead-in and settle are cut.
        private static readonly Vector2 DodgeWindow = new Vector2(0.05f, 0.95f);
        private static readonly Vector3 AirAttackWindow = new Vector3(0.1f, 0.5f, 0.95f);
        // The sweep of her forearm, from guard to furthest out at 0.4 s and back.
        private static readonly Vector2 ParryWindow = new Vector2(0.2f, 0.9f);
        private static readonly Vector2 GuardHitWindow = new Vector2(0f, 0.8f);
        private static readonly Vector2 LedgeGrabWindow = new Vector2(0.3f, 1.2f);
        // The pull-up is done by 0.93 s, her feet on the top.
        private static readonly Vector2 LedgeClimbWindow = new Vector2(0f, 1.0f);

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
        /// walk and backward walk, downward slash and power slash, unblocked and blocked impact, a jump to climb with,
        /// and the two-handed sword death falling back.
        /// </summary>
        private static void FillEnemyA(EnemyConfig config)
        {
            AnimationClip Clip(string name) => EnemyClip(EnemyImportSettings.EnemyAMixamoFolder, name);
            config.idle = Clip("Idle");
            config.walk = Clip("Walk");
            config.walkBack = Clip("Walk Back");
            config.run = Clip("Run");
            config.runSpeed = 3.6f;
            config.runClipSpeed = 3.6f;
            // The chop is furthest out 0.64 s into the clip; that is lined up with the live hitbox (EnemyTests checks it).
            config.attackClip = new ClipWindow { clip = Clip("Attack"), impact = 0.64f };
            config.attack2Clip = new ClipWindow { clip = Clip("Attack 2"), from = AttackA2From, impact = AttackA2Impact, to = AttackA2To };
            config.hurt = new ClipWindow { clip = Clip("Hit") };
            config.parried = new ClipWindow { clip = Clip("Parried") };
            config.climb = new ClipWindow { clip = Clip("Jump"), from = ClimbAFrom, to = ClimbATo };
            config.death = Clip("Death");
        }

        /// <summary>
        /// Enemy B is a Tripo swordsman with a broad sabre: quicker and frailer than the knight, swinging sooner and
        /// following up more often. Its clips are Mixamo's Sword And Shield set (it has no shield; the free arm is empty).
        /// </summary>
        private static void FillEnemyB(EnemyConfig config)
        {
            AnimationClip Clip(string name) => EnemyClip(EnemyImportSettings.EnemyBMixamoFolder, name);
            config.aggroRange = 8f;
            config.attackRange = 1.7f;
            config.tooClose = 1f;
            config.walkSpeed = 2.2f;
            config.walkBackSpeed = 1.5f;
            config.attack = new AttackStep
            {
                startup = 0.38f, active = 0.12f, recovery = 0.5f, cancelDelay = 0.15f,
                damage = 11f, lunge = 2f, hitStop = 0.1f, shake = 0.1f, knockback = 6f,
                hitboxCenter = new Vector2(0.95f, 1.1f), hitboxSize = new Vector2(1.4f, 1.4f),
            };
            config.attack2 = new AttackStep
            {
                startup = 0.32f, active = 0.14f, recovery = 0.65f, cancelDelay = 0f,
                damage = 14f, lunge = 2.5f, hitStop = 0.12f, shake = 0.14f, knockback = 8f,
                hitboxCenter = new Vector2(1f, 1.1f), hitboxSize = new Vector2(1.5f, 1.3f),
            };
            config.comboChance = 0.65f;
            config.attackCooldown = 0.7f;
            config.attackPoise = 12f;
            config.hitStun = 0.5f;
            config.walkClipSpeed = 1.3f;
            config.runSpeed = 4.4f;
            config.runClipSpeed = 4.4f;
            config.runDistance = 6.5f;
            config.idle = Clip("Idle");
            config.walk = Clip("Walk");
            config.walkBack = Clip("Walk Back");
            config.run = Clip("Run Weighted");
            config.attackClip = new ClipWindow { clip = Clip("Attack"), from = AttackB1From, impact = AttackB1Impact, to = AttackB1To };
            config.attack2Clip = new ClipWindow { clip = Clip("Attack 2"), from = AttackB2From, impact = AttackB2Impact, to = AttackB2To };
            config.hurt = new ClipWindow { clip = Clip("Hit") };
            config.parried = new ClipWindow { clip = Clip("Parried") };
            config.climb = new ClipWindow { clip = Clip("Jump"), from = ClimbBFrom, to = ClimbBTo };
            config.death = Clip("Death");
        }

        private static AnimationClip EnemyClip(string folder, string name)
        {
            return RanaViewerBuilder.FindClip(RanaViewerBuilder.LoadClips($"{folder}/{name}.fbx"), name);
        }

        private static GameObject CreateEnemy(string name, EnemyConfig config, string bodyPath, string texturePath, float height, float health, float radius)
        {
            var root = new GameObject(name);

            var controller = root.AddComponent<CharacterController>();
            controller.height = height;
            controller.radius = radius;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f;

            root.AddComponent<CharacterMotor>();
            var healthComponent = root.AddComponent<Health>();
            var serializedHealth = new SerializedObject(healthComponent);
            serializedHealth.FindProperty("max").floatValue = health;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
            var enemy = root.AddComponent<EnemyController>();
            var brain = root.AddComponent<EnemyBrain>();
            var enemyAnimator = root.AddComponent<EnemyAnimator>();
            var ragdoll = root.AddComponent<EnemyRagdoll>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            GameObject model = InstantiateModel(bodyPath, visual);
            Material skin = LoadOrCreateTexturedMaterial(name, texturePath);
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                renderer.sharedMaterial = skin;
            float modelHeight = MeasureHeight(model);
            if (modelHeight > 0.01f)
                visual.localScale = Vector3.one * (height / modelHeight);

            Animator animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            root.AddComponent<HitFlash>();
            Wire(enemy, "config", config);
            Wire(brain, "body", enemy);
            Wire(enemyAnimator, "enemy", enemy);
            Wire(enemyAnimator, "animator", animator);
            Wire(ragdoll, "animator", animator);
            return root;
        }

        private static EnemyController PlaceEnemy(Rail rail, GameObject prefab, float distance, CharacterMotor target)
        {
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var motor = enemy.GetComponent<CharacterMotor>();
            Wire(motor, "rail", rail);
            motor.Teleport(rail, distance, 0.05f);
            motor.Facing = -1;
            Wire(enemy.GetComponent<EnemyBrain>(), "target", target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(enemy.transform);
            return enemy.GetComponent<EnemyController>();
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

        // Less perspective: a narrower lens from further back frames the same picture with less distortion.
        private const float LensScale = 0.55f;
        private const float DistanceScale = 1.5f;

        // Low angle: the camera sits near ground level and looks up, so the floor shows as a sliver under her feet.
        private const float LowAnglePitch = -11f;
        private const float LowAngleFocusLift = 1f;

        private static CameraView Flatter(CameraView view)
        {
            view.fieldOfView *= LensScale;
            view.distance *= DistanceScale;
            view.pitch += LowAnglePitch;
            view.focusHeight += LowAngleFocusLift;
            return view;
        }

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
            railCamera.DefaultView = Flatter(new CameraView { yaw = 0f, pitch = 5f, distance = 12f, fieldOfView = 35f, focusHeight = 1.4f, lookAhead = 2f });

            // The same gameplay, seen four ways. Distances are metres along the rail.
            railCamera.Zones.Add(new CameraZone
            {
                name = "Corner: three-quarter view through the turn",
                from = 38f,
                to = 58f,
                view = Flatter(new CameraView { yaw = 28f, pitch = 14f, distance = 16f, fieldOfView = 35f, focusHeight = 1.6f, lookAhead = 2f }),
            });
            railCamera.Zones.Add(new CameraZone
            {
                name = "Arena: pulled back for the fight",
                from = 58f,
                to = 72f,
                view = Flatter(new CameraView { yaw = 0f, pitch = 5f, distance = 16f, fieldOfView = 35f, focusHeight = 1.6f, lookAhead = 1f }),
            });
            railCamera.Zones.Add(new CameraZone
            {
                name = "Into depth: behind her, looking down the rail",
                from = 82f,
                to = EnemyADistance - 10f,
                view = Flatter(new CameraView { yaw = 72f, pitch = 18f, distance = 11f, fieldOfView = 50f, focusHeight = 1.6f, lookAhead = 3f }),
            });
            railCamera.Zones.Add(new CameraZone
            {
                name = "Enemy A: back to the side view for the fight",
                from = EnemyADistance - 10f,
                to = ArenaAEnd,
                view = Flatter(new CameraView { yaw = 0f, pitch = 5f, distance = 14f, fieldOfView = 35f, focusHeight = 1.6f, lookAhead = 1.5f }),
            });
            railCamera.Zones.Add(new CameraZone
            {
                name = "Ledges: pulled back and raised to see the walls' tops",
                from = ArenaAEnd,
                to = LedgesEnd,
                view = Flatter(new CameraView { yaw = 0f, pitch = 8f, distance = 20f, fieldOfView = 35f, focusHeight = 3.5f, lookAhead = 2f }),
            });
            railCamera.Zones.Add(new CameraZone
            {
                name = "Enemy B: side view for the last fight",
                from = LedgesEnd,
                to = rail.Length,
                view = Flatter(new CameraView { yaw = 0f, pitch = 5f, distance = 14f, fieldOfView = 35f, focusHeight = 1.6f, lookAhead = 1.5f }),
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

        private static void CreateOverlay(CharacterMotor motor, RailCamera railCamera, EnemyController[] enemies)
        {
            var overlay = new GameObject("Overlay").AddComponent<GrayboxOverlay>();
            Wire(overlay, "player", motor.GetComponent<PlayerController>());
            Wire(overlay, "motor", motor);
            Wire(overlay, "railCamera", railCamera);
            WireArray(overlay, "enemies", enemies.Cast<Object>().ToArray());
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

        /// <summary>
        /// Jab, cross, side kick, roundhouse, spinning back kick: quick hands to open, kicks that hit harder and cost
        /// more, the spinning kick a heavy finisher. Hitboxes reach about as far as her fist or foot does, and no
        /// further: a hand's width past the limb, so a whiff looks like a whiff.
        /// </summary>
        private static void FillDefaultCombo(ComboConfig combo)
        {
            combo.steps = new[]
            {
                new AttackStep
                {
                    startup = 0.06f, active = 0.08f, recovery = 0.22f, cancelDelay = 0.04f,
                    damage = 10f, lunge = 3f, hitStop = 0.08f, shake = 0.06f, knockback = 2.5f, staminaCost = 6f,
                    hitboxCenter = new Vector2(0.75f, 1.35f), hitboxSize = new Vector2(1.0f, 0.6f),
                },
                new AttackStep
                {
                    startup = 0.07f, active = 0.08f, recovery = 0.24f, cancelDelay = 0.05f,
                    damage = 12f, lunge = 3.5f, hitStop = 0.09f, shake = 0.07f, knockback = 3f, staminaCost = 7f,
                    hitboxCenter = new Vector2(0.8f, 1.35f), hitboxSize = new Vector2(1.1f, 0.6f),
                },
                new AttackStep
                {
                    startup = 0.12f, active = 0.1f, recovery = 0.3f, cancelDelay = 0.1f,
                    damage = 16f, lunge = 2.5f, hitStop = 0.11f, shake = 0.1f, knockback = 4.5f, staminaCost = 10f,
                    hitboxCenter = new Vector2(0.85f, 1.0f), hitboxSize = new Vector2(1.3f, 0.8f),
                },
                new AttackStep
                {
                    startup = 0.14f, active = 0.1f, recovery = 0.32f, cancelDelay = 0.12f,
                    damage = 18f, lunge = 2.5f, hitStop = 0.12f, shake = 0.12f, knockback = 5f, staminaCost = 12f,
                    hitboxCenter = new Vector2(0.85f, 1.3f), hitboxSize = new Vector2(1.3f, 0.8f),
                },
                new AttackStep
                {
                    startup = 0.18f, active = 0.12f, recovery = 0.42f, cancelDelay = 0.16f,
                    damage = 26f, lunge = 3f, hitStop = 0.17f, shake = 0.18f, knockback = 8f, staminaCost = 16f,
                    hitboxCenter = new Vector2(0.9f, 1.25f), hitboxSize = new Vector2(1.4f, 0.9f),
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

        /// <summary>Assigns an array of object references to a serialized field by name.</summary>
        private static void WireArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException($"{target.GetType().Name} has no serialized field named '{field}'.");

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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
