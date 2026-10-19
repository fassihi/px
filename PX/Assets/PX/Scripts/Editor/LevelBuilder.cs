using System.Collections.Generic;
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
    /// Generates Level 01, "The Gate Halls", from code: one rail that winds through two acts, the halls over the
    /// water, the guardhouse, the gate chamber, the scaffold ascent, the bazaar walk and the face shrine. Shrines,
    /// guards on patrol, sentinels, the tether and its lamps, the levers and doors, the encounters and the Captain.
    /// The environment is the simple art of <see cref="EnvironmentKit"/>. Like the graybox, the scene is
    /// overwritten on every rebuild; to change the level, change this.
    ///
    /// Menu: PX > Level 01 > Rebuild Scene.
    /// Command line: -executeMethod PX.EditorTools.LevelBuilder.Build
    /// </summary>
    public static class LevelBuilder
    {
        public const string ScenePath = "Assets/PX/Scenes/Level01.unity";

        private const string MaterialFolder = EnvironmentKit.MaterialFolder;

        private const float StartDistance = 3f;
        private const float WaterLevel = -2.5f;
        private const float KillLevel = -2f;
        private const float Deep = -10f;

        // The zones, as rail distances. The rail is 592 m: see CreateRail.
        private const float ThresholdEnd = 60f;
        private const float CourtStart = 79f, CourtEnd = 140f;
        private const float GuardStart = 140f, GuardEnd = 230f;
        private const float GateStart = 230f, GateEnd = 300f;
        private const float AscentStart = 319f, AscentEnd = 430f;
        private const float BazaarStart = 430f, BazaarEnd = 520f;
        private const float FaceStart = 538f, ArenaStart = 546f, ArenaEnd = 584f;
        private const float GalleryHeight = 9f;
        private const float WalkHeight = 12f;

        private sealed class Level
        {
            public Rail Rail;
            public EnvironmentKit.Palette Hall;
            public EnvironmentKit.Palette Shrine;
            public Transform Root;
            public Transform Ground;
            public Transform Backdrop;
            public Transform Props;
            public CharacterMotor Player;
            public PlayerController Controller;
            public readonly List<Shrine> Shrines = new List<Shrine>();
            public readonly List<EnemyController> Enemies = new List<EnemyController>();
            public readonly List<Sentinel> Sentinels = new List<Sentinel>();
            public readonly List<Pickup> Pickups = new List<Pickup>();
            public readonly List<Lever> Levers = new List<Lever>();
            public readonly List<EncounterGroup> Encounters = new List<EncounterGroup>();
            public readonly List<CameraZone> Zones = new List<CameraZone>();
            public readonly List<CameraDirector.Reveal> Reveals = new List<CameraDirector.Reveal>();
            public EnemyController Captain;
            public EncounterGroup Arena;
            public GameObject PlayerPrefab, EnemyAPrefab, EnemyBPrefab, CaptainPrefab, SentinelPrefab;
        }

        [MenuItem("PX/Level 01/Rebuild Scene")]
        public static void Build()
        {
            AvatarPose.StraightenAll();
            EnsureFolder(System.IO.Path.GetDirectoryName(ScenePath));
            EnsureFolder(MaterialFolder);

            var level = new Level { Hall = EnvironmentKit.GateHalls(), Shrine = EnvironmentKit.FaceShrine() };
            int repaired = EnvironmentKit.RepairEmission(MaterialFolder) + EnvironmentKit.RepairEmission(GrayboxMaterialFolder);
            if (repaired > 0)
                Debug.Log($"Turned emission on for {repaired} materials that had lost it.");
            level.PlayerPrefab = CharacterBuilder.LoadPlayerPrefab();
            CharacterBuilder.UpgradeConfigs();
            level.EnemyAPrefab = CharacterBuilder.LoadEnemyAPrefab();
            level.EnemyBPrefab = CharacterBuilder.LoadEnemyBPrefab();
            level.CaptainPrefab = CharacterBuilder.LoadCaptainPrefab();
            GameObject boltPrefab = CharacterBuilder.LoadBoltPrefab(level.Hall);
            level.SentinelPrefab = CharacterBuilder.LoadSentinelPrefab(level.Hall, boltPrefab);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            level.Rail = CreateRail();
            level.Root = new GameObject("Level").transform;
            level.Ground = CreateGroup("Ground", level.Root);
            level.Backdrop = CreateGroup("Backdrop", level.Root);
            level.Props = CreateGroup("Props", level.Root);

            level.Player = PlacePlayer(level.Rail, level.PlayerPrefab, StartDistance);
            level.Controller = level.Player.GetComponent<PlayerController>();

            BuildThreshold(level);
            BuildCourt(level);
            BuildGuardhouse(level);
            BuildGate(level);
            BuildAscent(level);
            BuildBazaar(level);
            BuildFaceShrine(level);

            RailCamera railCamera = CreateLevelCamera(level);
            CreateLighting(level);
            CreateSystems(level, railCamera);

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Level 01 rebuilt at {ScenePath}. Rail length {level.Rail.Length:0.0} m, {level.Enemies.Count} guards, {level.Sentinels.Count} sentinels.");
        }

        // ---------------------------------------------------------------- rail

        /// <summary>
        /// The threshold runs east, turns north into the long court, the guardhouse and the gate chamber, turns west
        /// over the chasm for the ascent and the bazaar, and turns south to the face. Rail distances: 60 at the first
        /// turn, 79 into the court, 301 at the end of the gate chamber, 320 into the ascent, 520 at the last turn,
        /// 539 into the shrine, 592 at the end.
        /// </summary>
        private static Rail CreateRail()
        {
            var go = new GameObject("Rail");
            var rail = go.AddComponent<Rail>();
            Spline spline = go.GetComponent<SplineContainer>().Spline;
            spline.Clear();

            const float radius = 12f;
            const float handle = 12f;
            float arc = radius * 0.5523f;
            Vector3 p = Vector3.zero;
            Vector3 h = Vector3.right;
            var knots = new List<(Vector3 pos, Vector3 tin, Vector3 tout)> { (p, -h * handle, h * handle) };

            void Straight(float length)
            {
                p += h * length;
                knots.Add((p, -h * handle, h * handle));
            }

            void Turn(bool left)
            {
                Vector3 side = left ? Vector3.Cross(h, Vector3.up) : Vector3.Cross(Vector3.up, h);
                // The knot that starts the turn is the last one: its out tangent becomes the arc.
                (Vector3 pos, Vector3 tin, Vector3 tout) start = knots[knots.Count - 1];
                knots[knots.Count - 1] = (start.pos, start.tin, h * arc);
                Vector3 centre = p + side * radius;
                p = centre + h * radius;
                h = side;
                knots.Add((p, -h * arc, h * handle));
            }

            Straight(60f);
            Turn(left: true);
            Straight(222f);
            Turn(left: true);
            Straight(200f);
            Turn(left: true);
            Straight(54f);

            foreach ((Vector3 pos, Vector3 tin, Vector3 tout) in knots)
                spline.Add(new BezierKnot(new float3(pos.x, 0f, pos.z), new float3(tin.x, 0f, tin.z), new float3(tout.x, 0f, tout.z)), TangentMode.Broken);
            rail.Rebuild();
            return rail;
        }

        // ----------------------------------------------------------- threshold

        /// <summary>
        /// Where she wakes. A shrine, a gap in the floor over the water, a step and a ledge, a guard with its back to
        /// her for the first takedown, and an alcove high on the wall she cannot reach yet.
        /// </summary>
        private static void BuildThreshold(Level level)
        {
            Rail rail = level.Rail;
            EnvironmentKit.Palette p = level.Hall;
            Transform g = level.Ground;

            EnvironmentKit.AddPlatform(g, rail, "Floor", 0f, 24f, Deep, 0f, p, depth: 9f, glowEdges: false);
            EnvironmentKit.AddPlatform(g, rail, "Floor", 28.5f, 32f, Deep, 0f, p, depth: 9f, glowEdges: false);
            EnvironmentKit.AddPlatform(g, rail, "Step", 32f, 36f, Deep, 1.5f, p, depth: 9f);
            EnvironmentKit.AddPlatform(g, rail, "Ledge", 36f, 41f, Deep, 3.2f, p, depth: 9f);
            EnvironmentKit.AddPlatform(g, rail, "Floor", 41f, 72f, Deep, 0f, p, depth: 9f, glowEdges: false);
            EnvironmentKit.AddWater(g, rail, 0f, 80f, WaterLevel, p, width: 70f);
            // A sliver of light along the first floor's edge, so the floor reads against the water.
            AddBlocks(g, rail, "Floor edge", 0.2f, 23.8f, -0.02f, 0.03f, p.Edge, -4.4f, 0.12f, collide: false);
            AddBlocks(g, rail, "Floor edge", 41.2f, 71.8f, -0.02f, 0.03f, p.Edge, -4.4f, 0.12f, collide: false);

            // The alcove, high on the far wall: a flask for whoever comes back with the tether.
            EnvironmentKit.AddPlatform(g, rail, "Alcove", 13f, 19f, 8.6f, 9.1f, p, depth: 4f, lateral: -1f);
            AddPickup(level, PickupKind.FlaskSlot, 0f, 16f, 9.1f, p);
            AddAnchor(level, 9f, 7.2f, p);
            AddAnchor(level, 14f, 12f, p);

            AddShrine(level, 4.5f, 0f, "The Threshold", p);
            AddGuard(level, level.EnemyBPrefab, 52f, 0f, facing: 1, patrolFrom: 0f, patrolTo: 0f);

            // The hall: arches and pillars along the far wall, banners, lamps on chains, shards from the dark above.
            HallBackdrop(level, 0f, 72f, p);
            level.Zones.Add(Zone("Threshold", 0f, 60f, yaw: 0f, pitch: 5f, distance: 14f, fov: 35f, focus: 1.6f, lead: 2f));
            level.Zones.Add(Zone("First turn", 60f, CourtStart, yaw: 26f, pitch: 14f, distance: 17f, fov: 36f, focus: 1.8f, lead: 2f));
            level.Reveals.Add(Reveal("The Gate Halls", 9f, yaw: 34f, pitch: 20f, distance: 30f, fov: 42f, focus: 4f, duration: 3.6f));
        }

        // --------------------------------------------------------------- court

        /// <summary>
        /// Broken platforms over dark water, two sentinels drifting above them, a ledge high over the middle with a
        /// cache of light for the tether, a shrine at the far end.
        /// </summary>
        private static void BuildCourt(Level level)
        {
            Rail rail = level.Rail;
            EnvironmentKit.Palette p = level.Hall;
            Transform g = level.Ground;

            EnvironmentKit.AddPlatform(g, rail, "Platform", 72f, 88f, Deep, 0f, p, depth: 6f);
            EnvironmentKit.AddPlatform(g, rail, "Platform", 91.5f, 97.5f, Deep, 0.5f, p, depth: 6f);
            EnvironmentKit.AddPlatform(g, rail, "Platform", 101f, 106.5f, Deep, 0f, p, depth: 6f);
            EnvironmentKit.AddPlatform(g, rail, "Platform", 111f, 117f, Deep, 1.2f, p, depth: 6f);
            EnvironmentKit.AddPlatform(g, rail, "Platform", 120.5f, 127f, Deep, 0f, p, depth: 6f);
            EnvironmentKit.AddPlatform(g, rail, "Platform", 130f, 143f, Deep, 0f, p, depth: 7f);
            EnvironmentKit.AddWater(g, rail, 80f, 150f, WaterLevel, p, width: 70f);

            // The ledge over the water: light for whoever can reach it.
            EnvironmentKit.AddPlatform(g, rail, "High ledge", 112f, 118.5f, 7.1f, 7.6f, p, depth: 4f, lateral: -1f);
            AddPickup(level, PickupKind.Light, 220f, 115f, 7.6f, p);
            AddAnchor(level, 105f, 6.4f, p);
            AddAnchor(level, 112.5f, 9.8f, p);

            AddSentinel(level, 93f, 104f, 3.6f);
            AddSentinel(level, 119f, 131f, 4.2f);
            AddShrine(level, 138f, 0f, "The Flooded Court", p);

            // Pillars rise from the water on the far side; arches and shards beyond them.
            for (float d = 84f; d < 140f; d += 11f)
                EnvironmentKit.AddPillar(level.Backdrop, rail, d, -7f, 13f, p, radius: 0.8f);
            HallBackdrop(level, 79f, 140f, p, wallLateral: -16f, arches: true);
            level.Zones.Add(Zone("Court", CourtStart, CourtEnd, yaw: 0f, pitch: 7f, distance: 17f, fov: 35f, focus: 2.2f, lead: 2f));
            level.Reveals.Add(Reveal("The Flooded Court", 81f, yaw: -28f, pitch: 22f, distance: 26f, fov: 42f, focus: 3f, duration: 3f));
        }

        // ---------------------------------------------------------- guardhouse

        /// <summary>
        /// Low walls to crouch behind and guards on their rounds between them; a sealed door at the far end that a
        /// lever beside the last guard opens. Above it all runs the gallery, reached later from the gate chamber.
        /// </summary>
        private static void BuildGuardhouse(Level level)
        {
            Rail rail = level.Rail;
            EnvironmentKit.Palette p = level.Hall;
            Transform g = level.Ground;

            EnvironmentKit.AddPlatform(g, rail, "Floor", 143f, 232f, Deep, 0f, p, depth: 9f, glowEdges: false);
            AddBlocks(g, rail, "Floor edge", 143.2f, 231.8f, -0.02f, 0.03f, p.Edge, -4.4f, 0.12f, collide: false);
            foreach (float d in new[] { 150f, 164f, 178f, 192f, 206f, 220f })
                AddBlocks(g, rail, "Cover", d, d + 1.6f, 0f, 1.05f, p.DeepStone, 0f, 2.4f);

            AddShrine(level, 145.5f, 0f, "The Guardhouse", p);
            EnemyController g1 = AddGuard(level, level.EnemyBPrefab, 156f, 0f, facing: 1, patrolFrom: 153f, patrolTo: 162f);
            EnemyController g2 = AddGuard(level, level.EnemyAPrefab, 184f, 0f, facing: 1, patrolFrom: 180f, patrolTo: 190f);
            EnemyController g3 = AddGuard(level, level.EnemyBPrefab, 210f, 0f, facing: -1, patrolFrom: 208f, patrolTo: 218f);
            EnemyController g4 = AddGuard(level, level.EnemyBPrefab, 226f, 0f, facing: 1, patrolFrom: 0f, patrolTo: 0f);
            AddEncounter(level, "The Guardhouse", 147f, 229f, new[] { g1, g2, g3, g4 }, null, null, rouse: false, lockGates: false);

            // The sealed door and its lever, under the last guard's eye.
            Door door = AddDoor(level, "Sealed door", 229.2f, 230.2f, 0f, 4.2f, p, openOffset: new Vector3(0f, 4.4f, 0f), startsOpen: false);
            AddLever(level, 227.5f, 0f, p, door);

            // The gallery: a high floor over the guardhouse with gaps to jump, running back from the gate chamber.
            EnvironmentKit.AddPlatform(g, rail, "Gallery", 150f, 172f, GalleryHeight - 0.5f, GalleryHeight, p, depth: 5f);
            EnvironmentKit.AddPlatform(g, rail, "Gallery", 175.5f, 200f, GalleryHeight - 0.5f, GalleryHeight, p, depth: 5f);
            EnvironmentKit.AddPlatform(g, rail, "Gallery", 204f, 236f, GalleryHeight - 0.5f, GalleryHeight, p, depth: 5f);
            EnvironmentKit.AddPlatform(g, rail, "Gallery", 240.5f, 262f, GalleryHeight - 0.5f, GalleryHeight, p, depth: 5f);
            EnvironmentKit.AddPlatform(g, rail, "Gallery", 265.5f, 296f, GalleryHeight - 0.5f, GalleryHeight, p, depth: 5f);
            AddPickup(level, PickupKind.Light, 160f, 158f, GalleryHeight, p);
            EnemyController g5 = AddGuard(level, level.EnemyAPrefab, 250f, GalleryHeight, facing: 1, patrolFrom: 243f, patrolTo: 258f);
            AddEncounter(level, "The Gallery", 238f, 262f, new[] { g5 }, null, null, rouse: false, lockGates: false);

            HallBackdrop(level, 140f, 230f, p, wallLateral: -11f, arches: false, lattice: true);
            level.Zones.Add(Zone("Guardhouse", GuardStart, GuardEnd, yaw: 10f, pitch: 7f, distance: 15f, fov: 35f, focus: 1.6f, lead: 2.5f));
            level.Zones.Add(Zone("Gallery", 148f, 298f, yaw: -12f, pitch: 9f, distance: 15f, fov: 35f, focus: 1.6f, lead: 2.5f, heightFrom: GalleryHeight - 2f, heightTo: 30f));
            level.Reveals.Add(Reveal("The Guardhouse", 146f, yaw: -22f, pitch: 12f, distance: 22f, fov: 40f, focus: 2.5f, duration: 2.8f));
        }

        // ---------------------------------------------------------------- gate

        /// <summary>
        /// The chamber of the great mandala gate. A shrine, the tether on its plinth, a knight guarding it, and the
        /// lamps that lead up to the gallery and on across the chasm.
        /// </summary>
        private static void BuildGate(Level level)
        {
            Rail rail = level.Rail;
            EnvironmentKit.Palette p = level.Hall;
            Transform g = level.Ground;

            EnvironmentKit.AddPlatform(g, rail, "Floor", 232f, 301f, Deep, 0f, p, depth: 10f, glowEdges: false);
            AddBlocks(g, rail, "Floor edge", 232.2f, 300.8f, -0.02f, 0.03f, p.Edge, -4.9f, 0.12f, collide: false);
            EnvironmentKit.AddWater(g, rail, 296f, 330f, WaterLevel, p, width: 70f);
            AddShrine(level, 238f, 0f, "The Gate", p);

            // The tether on its plinth, under the gate.
            AddBlocks(g, rail, "Plinth", 251f, 253f, 0f, 0.8f, p.Trim, 0f, 1.6f);
            AddPickup(level, PickupKind.Tether, 0f, 252f, 1.7f, p);

            EnemyController guardian = AddGuard(level, level.EnemyAPrefab, 266f, 0f, facing: -1, patrolFrom: 0f, patrolTo: 0f);
            AddEncounter(level, "The Gate", 255f, 285f, new[] { guardian }, null, null, rouse: false, lockGates: false);

            // Lamps: up to the gallery, and on across the chasm to the ascent.
            AddAnchor(level, 262f, 6f, p);
            AddAnchor(level, 266f, 11.5f, p);
            AddAnchor(level, 306f, 5.6f, p);
            AddAnchor(level, 313f, 6.6f, p);

            EnvironmentKit.AddGate(level.Backdrop, rail, 268f, -17f, 7f, p);
            HallBackdrop(level, 230f, 300f, p, wallLateral: -19f, arches: true, skip: (258f, 280f));
            level.Zones.Add(Zone("Gate", GateStart, GateEnd, yaw: 0f, pitch: 9f, distance: 21f, fov: 36f, focus: 3f, lead: 2f));
            level.Zones.Add(Zone("Chasm", GateEnd, AscentStart, yaw: -18f, pitch: 14f, distance: 20f, fov: 38f, focus: 3f, lead: 3f));
            level.Reveals.Add(Reveal("The Gate", 233f, yaw: 30f, pitch: 24f, distance: 36f, fov: 44f, focus: 5f, duration: 3.8f));
        }

        // -------------------------------------------------------------- ascent

        /// <summary>
        /// Act 2 begins across the chasm: scaffolds climbing through the bazaar's underside, platforms, a wall to
        /// catch, lamps to swing from, sentinels drifting between the poles, a knight on a walkway.
        /// </summary>
        private static void BuildAscent(Level level)
        {
            Rail rail = level.Rail;
            EnvironmentKit.Palette p = level.Shrine;
            Transform g = level.Ground;

            // The dark ground far below, and the planks that climb.
            EnvironmentKit.AddPlatform(g, rail, "Floor", 310f, 600f, Deep, -6f, p, depth: 40f, glowEdges: false);
            EnvironmentKit.AddScaffold(g, rail, 316.5f, 331f, 2.5f, p, depth: 5f);
            EnvironmentKit.AddScaffold(g, rail, 334f, 342f, 4.5f, p, depth: 5f);
            AddBlocks(g, rail, "Climb wall", 345f, 347.5f, -6f, 8.6f, p.DeepStone, 0f, 5f);
            EnvironmentKit.AddScaffold(g, rail, 347.5f, 357f, 8.6f, p, depth: 5f);
            AddAnchor(level, 361f, 12.2f, p);
            EnvironmentKit.AddScaffold(g, rail, 363f, 373f, 9.5f, p, depth: 5f);
            EnvironmentKit.AddScaffold(g, rail, 376f, 385f, 10.5f, p, depth: 5f);
            EnvironmentKit.AddScaffold(g, rail, 389.5f, 399f, 10.5f, p, depth: 5f);
            AddAnchor(level, 403f, 14.2f, p);
            EnvironmentKit.AddScaffold(g, rail, 406f, 419f, WalkHeight, p, depth: 5f);
            EnvironmentKit.AddScaffold(g, rail, 421.5f, 433f, WalkHeight, p, depth: 6f);
            AddShrine(level, 427f, WalkHeight, "The Scaffolds", p);

            EnemyController knight = AddGuard(level, level.EnemyAPrefab, 352f, 8.6f, facing: -1, patrolFrom: 349f, patrolTo: 355.5f);
            AddEncounter(level, "The Walkway", 346f, 358f, new[] { knight }, null, null, rouse: false, lockGates: false);
            AddSentinel(level, 336f, 352f, 7.2f);
            AddSentinel(level, 394f, 412f, 13.6f);

            ScaffoldBackdrop(level, 319f, 430f, p);
            level.Zones.Add(Zone("Ascent", AscentStart, AscentEnd, yaw: 0f, pitch: 12f, distance: 19f, fov: 36f, focus: 3f, lead: 2f));
            level.Reveals.Add(Reveal("The Scaffolds", 321f, yaw: -32f, pitch: 26f, distance: 28f, fov: 44f, focus: 6f, duration: 3.2f));
        }

        // -------------------------------------------------------------- bazaar

        /// <summary>
        /// Hanging walkways under the neon of the bazaar: a patrol of two among low cover, a sentinel above, and the
        /// last shrine before the face.
        /// </summary>
        private static void BuildBazaar(Level level)
        {
            Rail rail = level.Rail;
            EnvironmentKit.Palette p = level.Shrine;
            Transform g = level.Ground;

            EnvironmentKit.AddScaffold(g, rail, 433f, 451f, WalkHeight, p, depth: 6f);
            EnvironmentKit.AddScaffold(g, rail, 454.5f, 471f, WalkHeight, p, depth: 6f);
            EnvironmentKit.AddScaffold(g, rail, 475f, 496f, WalkHeight + 0.6f, p, depth: 6f);
            EnvironmentKit.AddScaffold(g, rail, 499f, 541f, WalkHeight, p, depth: 7f);
            foreach (float d in new[] { 441f, 460f, 484f })
                AddBlocks(g, rail, "Cover", d, d + 1.6f, WalkHeight + (d > 474f ? 0.6f : 0f), WalkHeight + (d > 474f ? 0.6f : 0f) + 1.05f, p.DeepStone, 0f, 2.4f);

            EnemyController b1 = AddGuard(level, level.EnemyBPrefab, 462f, WalkHeight, facing: -1, patrolFrom: 456f, patrolTo: 469f);
            EnemyController b2 = AddGuard(level, level.EnemyAPrefab, 486f, WalkHeight + 0.6f, facing: -1, patrolFrom: 478f, patrolTo: 493f);
            AddEncounter(level, "The Bazaar", 452f, 497f, new[] { b1, b2 }, null, null, rouse: false, lockGates: false);
            AddSentinel(level, 470f, 492f, WalkHeight + 4.5f);
            AddShrine(level, 516f, WalkHeight, "The Bazaar", p);

            ScaffoldBackdrop(level, 430f, 520f, p, dense: true);
            level.Zones.Add(Zone("Bazaar", BazaarStart, BazaarEnd, yaw: 16f, pitch: 8f, distance: 16f, fov: 35f, focus: 1.8f, lead: 2.5f));
            level.Zones.Add(Zone("Last turn", BazaarEnd, FaceStart, yaw: 30f, pitch: 12f, distance: 18f, fov: 36f, focus: 2f, lead: 2f));
            level.Reveals.Add(Reveal("The Bazaar", 435f, yaw: 36f, pitch: 16f, distance: 26f, fov: 42f, focus: 3f, duration: 3f));
        }

        // --------------------------------------------------------- face shrine

        /// <summary>The arena before the face: gates either side that fall when the Captain wakes, and the Captain.</summary>
        private static void BuildFaceShrine(Level level)
        {
            Rail rail = level.Rail;
            EnvironmentKit.Palette p = level.Shrine;
            Transform g = level.Ground;

            EnvironmentKit.AddPlatform(g, rail, "Arena", 541f, 592f, Deep, WalkHeight, p, depth: 12f);
            Door west = AddDoor(level, "Arena gate", ArenaStart - 1f, ArenaStart, WalkHeight, WalkHeight + 5f, p, openOffset: new Vector3(0f, 5.5f, 0f), startsOpen: true);
            Door east = AddDoor(level, "Arena gate", ArenaEnd, ArenaEnd + 1f, WalkHeight, WalkHeight + 5f, p, openOffset: new Vector3(0f, 5.5f, 0f), startsOpen: true);

            level.Captain = PlaceEnemy(rail, level.CaptainPrefab, 572f, level.Player, facing: -1, parent: level.Props);
            level.Captain.name = "Captain";
            PlaceAtHeight(level, level.Captain.GetComponent<CharacterMotor>(), 572f, WalkHeight);
            WireBool(level.Captain.GetComponent<EnemyBrain>(), "stealth", false);
            level.Enemies.Add(level.Captain);
            level.Arena = AddEncounter(level, "The Captain", ArenaStart + 1f, ArenaEnd - 1f, new[] { level.Captain }, null, new[] { west, east }, rouse: true, lockGates: true);

            EnvironmentKit.AddFace(level.Backdrop, rail, 565f, -13f, WalkHeight - 2f, p);
            for (float d = 544f; d < 592f; d += 12f)
            {
                EnvironmentKit.AddPillar(level.Backdrop, rail, d, -9f, 16f, p, radius: 0.9f);
                EnvironmentKit.AddBanner(level.Backdrop, rail, d + 6f, -8f, WalkHeight + 13f, 9f, p);
            }
            for (float d = 540f; d < 592f; d += 9f)
                EnvironmentKit.AddShard(level.Backdrop, rail, d, -4f - (d % 5f), WalkHeight + 24f, 7f + (d % 4f), 1.4f, 8f, p);
            level.Zones.Add(Zone("Face shrine", FaceStart, 600f, yaw: 0f, pitch: 7f, distance: 19f, fov: 35f, focus: 2.6f, lead: 1.5f));
            level.Reveals.Add(Reveal("The Face Shrine", 540f, yaw: 24f, pitch: 18f, distance: 30f, fov: 44f, focus: 7f, duration: 3.6f));
        }

        // ------------------------------------------------------------ backdrop

        /// <summary>Arches, pillars, banners, lamps and shards along a stretch of the gate halls' far wall.</summary>
        private static void HallBackdrop(Level level, float from, float to, EnvironmentKit.Palette p, float wallLateral = -13f, bool arches = true, bool lattice = false, (float, float)? skip = null)
        {
            Rail rail = level.Rail;
            Transform b = level.Backdrop;
            EnvironmentKit.AddWall(b, rail, from, to, Deep, 18f, wallLateral, p, latticeBottom: lattice ? 1.5f : 4f, latticeHeight: lattice ? 5f : 3f);
            for (float d = from + 6f; d < to - 4f; d += 12f)
            {
                if (skip.HasValue && d > skip.Value.Item1 && d < skip.Value.Item2)
                    continue;
                if (arches)
                    EnvironmentKit.AddArch(b, rail, d, wallLateral + 2.5f, 7f, 10f, p);
                else
                    EnvironmentKit.AddPillar(b, rail, d, wallLateral + 2f, 11f, p);
                EnvironmentKit.AddBanner(b, rail, d + 6f, wallLateral + 4f, 12f, 6f, p);
            }
            for (float d = from + 3f; d < to; d += 9f)
                EnvironmentKit.AddLamp(b, rail, d, 6.5f + (d % 3f), wallLateral + 6f, 15f, p);
            // Low lamps by the wall's foot light the floor she walks on.
            for (float d = from + 7f; d < to; d += 13f)
                EnvironmentKit.AddLamp(b, rail, d, 2.6f, wallLateral + 4.2f, 15f, p);
            for (float d = from + 8f; d < to; d += 15f)
                EnvironmentKit.AddShard(b, rail, d, wallLateral * 0.5f - (d % 4f), 22f, 6f + (d % 5f), 1.2f + (d % 2f) * 0.5f, 6f, p);
            // The ceiling the chains hang from, lost in the fog.
            AddBlocks(b, rail, "Ceiling", from, to, 15f, 16f, p.DeepStone, wallLateral * 0.5f, 30f, collide: false);
        }

        /// <summary>Scaffold towers, neon signs, lamps and banners of the bazaar, on the far side.</summary>
        private static void ScaffoldBackdrop(Level level, float from, float to, EnvironmentKit.Palette p, bool dense = false)
        {
            Rail rail = level.Rail;
            Transform b = level.Backdrop;
            EnvironmentKit.AddWall(b, rail, from, to, Deep, 30f, -18f, p, latticeBottom: 10f, latticeHeight: 4f);
            float step = dense ? 8f : 11f;
            for (float d = from + 4f; d < to - 3f; d += step)
            {
                float h = 6f + (d % 7f) * 1.5f;
                EnvironmentKit.AddScaffold(b, rail, d, d + 5f, h, p, lateral: -9f, depth: 3f);
                EnvironmentKit.AddScaffold(b, rail, d + 1f, d + 4f, h + 5f, p, lateral: -9f, depth: 3f);
                EnvironmentKit.AddSign(b, rail, d + 2.5f, -10.5f, h + 8f, 2.6f + (d % 3f), 1.2f, p, secondary: (d % 2f) < 1f);
                if (dense)
                    EnvironmentKit.AddSign(b, rail, d + 6f, -8.5f, h + 3f, 1.6f, 2.4f, p, secondary: (d % 3f) < 1.5f);
                EnvironmentKit.AddBanner(b, rail, d + 4f, -7f, h + 12f, 5f, p, width: 1.2f);
            }
            for (float d = from + 2f; d < to; d += 10f)
                EnvironmentKit.AddLamp(b, rail, d, 9f + (d % 5f), -6f, 30f, p);
            for (float d = from + 5f; d < to; d += 14f)
                EnvironmentKit.AddShard(b, rail, d, -5f - (d % 3f), 32f, 8f + (d % 4f), 1.6f, 5f, p);
        }

        // --------------------------------------------------------------- props

        private static void AddShrine(Level level, float distance, float height, string areaName, EnvironmentKit.Palette p)
        {
            Transform shrine = PlaceAlongRail(level.Props, "Shrine " + areaName, level.Rail, distance, height, 0f);
            // The ring of light in the floor, the lantern post, and the flame that lights when she comes.
            Material ringMaterial = EnvironmentKit.LoadOrCreateRingMaterial(MaterialFolder, $"{p.Name} Shrine Ring", p.GlowColor * 0.8f);
            GameObject ring = AddPart(shrine, PrimitiveType.Quad, new Vector3(0f, 0.04f, 0f), new Vector3(3.2f, 3.2f, 1f), ringMaterial);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            AddPart(shrine, PrimitiveType.Cylinder, new Vector3(-1.1f, 0.9f, 0f), new Vector3(0.18f, 0.9f, 0.18f), p.Trim);
            AddPart(shrine, PrimitiveType.Cube, new Vector3(-1.1f, 1.9f, 0f), new Vector3(0.5f, 0.5f, 0.5f), p.DeepStone);
            var flame = CreateGroup("Flame", shrine);
            flame.localPosition = new Vector3(-1.1f, 1.9f, 0f);
            AddPart(flame, PrimitiveType.Cube, Vector3.zero, new Vector3(0.3f, 0.3f, 0.3f), p.Glow).transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            EnvironmentKit.AddPointLight(flame, Vector3.zero, p.GlowColor, 9f, 4f);
            flame.gameObject.AddComponent<Pulse>();

            var component = shrine.gameObject.AddComponent<Shrine>();
            WireFloat(component, "distance", distance);
            WireFloat(component, "height", height);
            WireString(component, "areaName", areaName);
            Wire(component, "flame", flame.gameObject);
            level.Shrines.Add(component);
        }

        private static EnemyController AddGuard(Level level, GameObject prefab, float distance, float height, int facing, float patrolFrom, float patrolTo)
        {
            EnemyController enemy = PlaceEnemy(level.Rail, prefab, distance, level.Player, facing, level.Props);
            PlaceAtHeight(level, enemy.GetComponent<CharacterMotor>(), distance, height);
            var brain = enemy.GetComponent<EnemyBrain>();
            WireBool(brain, "stealth", true);
            WireFloat(brain, "patrolFrom", patrolFrom);
            WireFloat(brain, "patrolTo", patrolTo);
            enemy.name = $"{prefab.name} {distance:0}";
            level.Enemies.Add(enemy);
            return enemy;
        }

        private static void PlaceAtHeight(Level level, CharacterMotor motor, float distance, float height)
        {
            motor.Teleport(level.Rail, distance, height + 0.05f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(motor.transform);
        }

        private static Sentinel AddSentinel(Level level, float from, float to, float height)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(level.SentinelPrefab, level.Props);
            var sentinel = go.GetComponent<Sentinel>();
            RailPoint point = level.Rail.Evaluate((from + to) * 0.5f);
            go.transform.position = new Vector3(point.Position.x, height, point.Position.z);
            Wire(sentinel, "rail", level.Rail);
            Wire(sentinel, "target", level.Player);
            WireFloat(sentinel, "patrolFrom", from);
            WireFloat(sentinel, "patrolTo", to);
            WireFloat(sentinel, "height", height);
            go.name = $"Sentinel {from:0}";
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            level.Sentinels.Add(sentinel);
            return sentinel;
        }

        private static void AddAnchor(Level level, float distance, float height, EnvironmentKit.Palette p)
        {
            Transform lamp = EnvironmentKit.AddLamp(level.Props, level.Rail, distance, height, 0f, height + 8f, p);
            lamp.name = $"Lamp {distance:0}";
            var anchor = lamp.gameObject.AddComponent<TetherAnchor>();
            WireFloat(anchor, "distance", distance);
            WireFloat(anchor, "height", height);
            Wire(anchor, "player", level.Controller);
        }

        private static void AddPickup(Level level, PickupKind kind, float amount, float distance, float height, EnvironmentKit.Palette p)
        {
            Transform pickup = PlaceAlongRail(level.Props, $"Pickup {kind}", level.Rail, distance, height, 0f);
            Transform visual = CreateGroup("Visual", pickup);
            visual.localPosition = Vector3.up * 0.6f;
            switch (kind)
            {
                case PickupKind.Tether:
                    // A ribbon of light standing on end, turning slowly.
                    AddPart(visual, PrimitiveType.Cube, Vector3.zero, new Vector3(0.08f, 1.1f, 0.3f), p.Glow2);
                    AddPart(visual, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.2f, 0.6f, 0.08f), p.Glow);
                    break;
                case PickupKind.FlaskSlot:
                    AddPart(visual, PrimitiveType.Capsule, Vector3.zero, new Vector3(0.25f, 0.3f, 0.25f), p.Glow);
                    AddPart(visual, PrimitiveType.Cylinder, new Vector3(0f, 0.32f, 0f), new Vector3(0.1f, 0.08f, 0.1f), p.Trim);
                    break;
                default:
                    AddPart(visual, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.4f, p.Glow);
                    break;
            }
            EnvironmentKit.AddPointLight(visual, Vector3.zero, kind == PickupKind.Tether ? p.Glow2Color : p.GlowColor, 5f, 2.5f);
            var bob = visual.gameObject.AddComponent<Bob>();
            WireFloat(bob, "amplitude", 0.15f);
            WireFloat(bob, "period", 2.4f);
            var pulse = visual.gameObject.AddComponent<Pulse>();
            WireFloat(pulse, "period", 1.8f);
            var component = pickup.gameObject.AddComponent<Pickup>();
            WireInt(component, "kind", (int)kind);
            WireFloat(component, "amount", amount);
            WireFloat(component, "distance", distance);
            WireFloat(component, "height", height);
            Wire(component, "target", level.Player);
            level.Pickups.Add(component);
        }

        private static Door AddDoor(Level level, string name, float from, float to, float bottom, float top, EnvironmentKit.Palette p, Vector3 openOffset, bool startsOpen)
        {
            Transform door = CreateGroup(name, level.Props);
            AddBlocks(door, level.Rail, name, from, to, bottom, top, p.Trim, 0f, 7f);
            // A band of lattice across its face, so it reads as a gate and not a wall.
            AddBlocks(door, level.Rail, name + " lattice", from - 0.05f, to + 0.05f, bottom + 0.4f, top - 0.4f, p.Lattice, 0f, 7.2f, collide: false);
            var component = door.gameObject.AddComponent<Door>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("openOffset").vector3Value = openOffset;
            serialized.FindProperty("startsOpen").boolValue = startsOpen;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static void AddLever(Level level, float distance, float height, EnvironmentKit.Palette p, params Door[] doors)
        {
            Transform lever = PlaceAlongRail(level.Props, "Lever", level.Rail, distance, height, -1.2f);
            AddPart(lever, PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 1f, 0.5f), p.DeepStone);
            var handle = CreateGroup("Handle", lever);
            handle.localPosition = new Vector3(0f, 1f, 0f);
            AddPart(handle, PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(0.06f, 0.4f, 0.06f), p.Trim);
            AddPart(handle, PrimitiveType.Sphere, new Vector3(0f, 0.85f, 0f), Vector3.one * 0.18f, p.Glow);
            EnvironmentKit.AddPointLight(lever, new Vector3(0f, 1.8f, 0f), p.GlowColor, 4f, 1.5f);
            var component = lever.gameObject.AddComponent<Lever>();
            WireFloat(component, "distance", distance);
            WireFloat(component, "height", height);
            WireArray(component, "doors", doors.Cast<Object>().ToArray());
            Wire(component, "handle", handle);
            level.Levers.Add(component);
        }

        private static EncounterGroup AddEncounter(Level level, string name, float from, float to, EnemyController[] enemies, Sentinel[] sentinels, Door[] gates, bool rouse, bool lockGates)
        {
            var go = new GameObject("Encounter " + name);
            go.transform.SetParent(level.Props, false);
            var group = go.AddComponent<EncounterGroup>();
            WireString(group, "encounterName", name);
            Wire(group, "target", level.Player);
            WireArray(group, "enemies", (enemies ?? new EnemyController[0]).Cast<Object>().ToArray());
            WireArray(group, "sentinels", (sentinels ?? new Sentinel[0]).Cast<Object>().ToArray());
            WireArray(group, "gates", (gates ?? new Door[0]).Cast<Object>().ToArray());
            WireFloat(group, "from", from);
            WireFloat(group, "to", to);
            WireBool(group, "rouseOnEntry", rouse);
            WireBool(group, "lockGates", lockGates);
            level.Encounters.Add(group);
            return group;
        }

        // ------------------------------------------------------- camera, light

        private static CameraZone Zone(string name, float from, float to, float yaw, float pitch, float distance, float fov, float focus, float lead,
            float heightFrom = 0f, float heightTo = 0f)
        {
            return new CameraZone
            {
                name = name,
                from = from,
                to = to,
                view = Flatter(new CameraView { yaw = yaw, pitch = pitch, distance = distance, fieldOfView = fov, focusHeight = focus, lookAhead = lead }),
                useHeight = heightTo > heightFrom,
                heightFrom = heightFrom,
                heightTo = heightTo,
            };
        }

        private static CameraDirector.Reveal Reveal(string name, float at, float yaw, float pitch, float distance, float fov, float focus, float duration)
        {
            return new CameraDirector.Reveal
            {
                name = name,
                distance = at,
                view = new CameraView { yaw = yaw, pitch = pitch, distance = distance, fieldOfView = fov, focusHeight = focus, lookAhead = 0f },
                duration = duration,
            };
        }

        private static RailCamera CreateLevelCamera(Level level)
        {
            RailCamera railCamera = CreateCamera(level.Rail, level.Player);
            // The halls have no sky: the camera clears to the fog, so the dark closes in everywhere.
            var camera = railCamera.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = level.Hall.FogColor;
            railCamera.DefaultView = Flatter(new CameraView { yaw = 0f, pitch = 6f, distance = 15f, fieldOfView = 35f, focusHeight = 1.7f, lookAhead = 2f });
            foreach (CameraZone zone in level.Zones)
                railCamera.Zones.Add(zone);
            railCamera.Snap();
            return railCamera;
        }

        private static void CreateLighting(Level level)
        {
            // A dim, cool key from above and behind, for the shapes; the colour comes from the lamps.
            var moon = new GameObject("Moon");
            var light = moon.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.72f, 0.82f);
            light.intensity = 0.45f;
            light.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            EnvironmentKit.Palette hall = level.Hall;
            EnvironmentKit.Palette shrine = level.Shrine;
            var atmosphere = new GameObject("Atmosphere").AddComponent<Atmosphere>();
            Wire(atmosphere, "target", level.Player);
            Wire(atmosphere, "background", Object.FindFirstObjectByType<Camera>());
            var serialized = new SerializedObject(atmosphere);
            SerializedProperty keys = serialized.FindProperty("keys");
            var list = new[]
            {
                (0f, hall), (290f, hall), (335f, shrine), (600f, shrine),
            };
            keys.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++)
            {
                SerializedProperty key = keys.GetArrayElementAtIndex(i);
                key.FindPropertyRelative("distance").floatValue = list[i].Item1;
                key.FindPropertyRelative("fog").colorValue = list[i].Item2.FogColor;
                key.FindPropertyRelative("fogDensity").floatValue = list[i].Item2.FogDensity;
                key.FindPropertyRelative("sky").colorValue = list[i].Item2.AmbientSky;
                key.FindPropertyRelative("equator").colorValue = list[i].Item2.AmbientEquator;
                key.FindPropertyRelative("ground").colorValue = list[i].Item2.AmbientGround;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = hall.AmbientSky;
            RenderSettings.ambientEquatorColor = hall.AmbientEquator;
            RenderSettings.ambientGroundColor = hall.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = hall.FogDensity;
            RenderSettings.fogColor = hall.FogColor;

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = LoadOrCreateVolumeProfile();
        }

        private const string LevelVolumeProfilePath = "Assets/Settings/Level01VolumeProfile.asset";

        /// <summary>
        /// The level's post-processing: a heavier bloom than the graybox, so the lamps and the lattice halo as they do
        /// in the paintings, a vignette that closes the dark in, ACES tonemapping, and a little more saturation.
        /// </summary>
        private static VolumeProfile LoadOrCreateVolumeProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LevelVolumeProfilePath);
            if (existing != null)
                return existing;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, LevelVolumeProfilePath);

            var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.68f);
            var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);
            var tonemapping = profile.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);
            tonemapping.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);
            var colour = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
            colour.saturation.Override(12f);
            colour.contrast.Override(8f);
            colour.postExposure.Override(0.15f);
            foreach (VolumeComponent component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<VolumeProfile>(LevelVolumeProfilePath);
        }

        /// <summary>The effects, the camera director, the respawn, the water, the level's conductor and the HUD.</summary>
        private static void CreateSystems(Level level, RailCamera railCamera)
        {
            PlayerController player = level.Controller;
            CreateHitEffects(GrayboxMaterialFolder);
            CreateMovementEffects(player, GrayboxMaterialFolder);

            var director = railCamera.gameObject.AddComponent<CameraDirector>();
            Wire(director, "player", player);
            Wire(director, "railCamera", railCamera);
            var serialized = new SerializedObject(director);
            SerializedProperty reveals = serialized.FindProperty("reveals");
            reveals.arraySize = level.Reveals.Count;
            for (int i = 0; i < level.Reveals.Count; i++)
            {
                SerializedProperty reveal = reveals.GetArrayElementAtIndex(i);
                reveal.FindPropertyRelative("name").stringValue = level.Reveals[i].name;
                reveal.FindPropertyRelative("distance").floatValue = level.Reveals[i].distance;
                reveal.FindPropertyRelative("duration").floatValue = level.Reveals[i].duration;
                SerializedProperty view = reveal.FindPropertyRelative("view");
                view.FindPropertyRelative("yaw").floatValue = level.Reveals[i].view.yaw;
                view.FindPropertyRelative("pitch").floatValue = level.Reveals[i].view.pitch;
                view.FindPropertyRelative("distance").floatValue = level.Reveals[i].view.distance;
                view.FindPropertyRelative("fieldOfView").floatValue = level.Reveals[i].view.fieldOfView;
                view.FindPropertyRelative("focusHeight").floatValue = level.Reveals[i].view.focusHeight;
                view.FindPropertyRelative("lookAhead").floatValue = level.Reveals[i].view.lookAhead;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var respawn = new GameObject("PlayerRespawn").AddComponent<PlayerRespawn>();
            Wire(respawn, "player", player);
            WireArray(respawn, "enemies", level.Enemies.Cast<Object>().ToArray());
            WireFloats(respawn, "checkpoints", new[] { 4.5f });
            WireBool(respawn, "reviveOnDeath", true);

            var water = new GameObject("Water").AddComponent<KillPlane>();
            Wire(water, "player", player);
            WireFloat(water, "level", KillLevel);

            var flow = new GameObject("LevelFlow").AddComponent<LevelFlow>();
            Wire(flow, "player", player);
            Wire(flow, "respawn", respawn);
            Wire(flow, "director", director);
            Wire(flow, "killPlane", water);
            WireArray(flow, "shrines", level.Shrines.Cast<Object>().ToArray());
            WireArray(flow, "enemies", level.Enemies.Cast<Object>().ToArray());
            WireArray(flow, "sentinels", level.Sentinels.Cast<Object>().ToArray());
            WireArray(flow, "pickups", level.Pickups.Cast<Object>().ToArray());
            WireArray(flow, "levers", level.Levers.Cast<Object>().ToArray());
            WireArray(flow, "encounters", level.Encounters.Cast<Object>().ToArray());
            Wire(flow, "boss", level.Captain);
            Wire(flow, "bossArena", level.Arena);
            Wire(flow, "droppedLightMaterial", level.Hall.Glow);
            WireColor(flow, "lightColor", level.Hall.GlowColor);

            var hud = new GameObject("LevelHud").AddComponent<LevelHud>();
            Wire(hud, "player", player);
            Wire(hud, "flow", flow);
            Wire(hud, "director", director);
            Wire(hud, "worldCamera", railCamera.GetComponent<Camera>());

            var overlay = new GameObject("Overlay").AddComponent<GrayboxOverlay>();
            Wire(overlay, "player", player);
            Wire(overlay, "motor", level.Player);
            Wire(overlay, "railCamera", railCamera);
            WireArray(overlay, "enemies", level.Enemies.Cast<Object>().ToArray());
            WireBool(overlay, "showControls", false);
        }
    }
}
