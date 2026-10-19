using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;
using static PX.EditorTools.BuilderKit;
using Object = UnityEngine.Object;

namespace PX.EditorTools
{
    /// <summary>
    /// Generates the graybox test scene from code, so the whole scene is reviewable as a script.
    /// The scene file is overwritten on every rebuild: change the level here, not in the scene.
    /// Config assets, materials and prefabs are only created when missing (see <see cref="CharacterBuilder"/>), so
    /// tuning done in the editor survives.
    ///
    /// Menu: PX > Graybox > Rebuild Scene.
    /// Command line: -executeMethod PX.EditorTools.GrayboxBuilder.Build
    /// </summary>
    public static class GrayboxBuilder
    {
        public const string ScenePath = "Assets/PX/Scenes/Graybox.unity";

        private const string MaterialFolder = GrayboxMaterialFolder;

        // Where the enemies wait on the last straight: Enemy A in an arena with crates to climb, then a stretch of
        // ledges to climb, then Enemy B at the end.
        private const float EnemyADistance = 112f;
        private const float ArenaAEnd = 126f;
        private const float LedgesEnd = 158f;
        private const float EnemyBDistance = 178f;

        // Where she gets up after a fall: the start, before each fight, and before the ledges.
        private static readonly float[] Checkpoints = { StartDistance, 98f, ArenaAEnd + 1f, LedgesEnd + 1f };

        private const string VolumeProfilePath = "Assets/Settings/SceneVolumeProfile.asset";

        private const float FloorBottom = -8f;
        private const float StartDistance = 3f;

        private sealed class Palette
        {
            public Material Ground;
            public Material Platform;
            public Material Backdrop;
        }

        [MenuItem("PX/Graybox/Rebuild Scene")]
        public static void Build()
        {
            // Retargeting needs every avatar in the same reference pose. New Mixamo clips are fixed up here.
            AvatarPose.StraightenAll();

            EnsureFolder(System.IO.Path.GetDirectoryName(ScenePath));
            EnsureFolder(MaterialFolder);

            Palette palette = LoadPalette();
            GameObject playerPrefab = CharacterBuilder.LoadPlayerPrefab();
            GameObject dummyPrefab = CharacterBuilder.LoadDummyPrefab();
            GameObject enemyAPrefab = CharacterBuilder.LoadEnemyAPrefab();
            GameObject enemyBPrefab = CharacterBuilder.LoadEnemyBPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Rail rail = CreateRail();
            BuildLevel(rail, palette);
            PlaceDummies(rail, dummyPrefab);
            CharacterMotor motor = PlacePlayer(rail, playerPrefab, StartDistance);
            EnemyController[] enemies =
            {
                PlaceEnemy(rail, enemyAPrefab, EnemyADistance, motor),
                PlaceEnemy(rail, enemyBPrefab, EnemyBDistance, motor),
            };
            RailCamera railCamera = CreateGrayboxCamera(rail, motor);
            CreateLighting();
            CreateOverlay(motor, railCamera, enemies);
            CreateCombatLayer(motor.GetComponent<PlayerController>(), enemies);

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene(ScenePath);
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

        // ---------------------------------------------------------- characters

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

        /// <summary>
        /// What sits beside the fighters in the scene: the particles where blows land, the health and stamina bars, and
        /// getting back up at a checkpoint after a fall.
        /// </summary>
        private static void CreateCombatLayer(PlayerController player, EnemyController[] enemies)
        {
            CreateHitEffects(MaterialFolder);
            CreateMovementEffects(player, MaterialFolder);

            var hud = new GameObject("CombatHud").AddComponent<CombatHud>();
            Wire(hud, "player", player);
            WireArray(hud, "enemies", enemies.Select(e => (Object)e.GetComponent<EnemyBrain>()).ToArray());

            var respawn = new GameObject("PlayerRespawn").AddComponent<PlayerRespawn>();
            Wire(respawn, "player", player);
            WireArray(respawn, "enemies", enemies.Cast<Object>().ToArray());
            WireFloats(respawn, "checkpoints", Checkpoints);
        }

        // ------------------------------------------------------- camera, light

        private static RailCamera CreateGrayboxCamera(Rail rail, CharacterMotor motor)
        {
            RailCamera railCamera = CreateCamera(rail, motor);

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
                Ground = LoadOrCreateMaterial(MaterialFolder, "Ground", new Color(0.76f, 0.62f, 0.42f), lit: true),
                Platform = LoadOrCreateMaterial(MaterialFolder, "Platform", new Color(0.16f, 0.6f, 0.6f), lit: true),
                Backdrop = LoadOrCreateMaterial(MaterialFolder, "Backdrop", new Color(0.83f, 0.78f, 0.68f), lit: true),
            };
        }
    }
}
