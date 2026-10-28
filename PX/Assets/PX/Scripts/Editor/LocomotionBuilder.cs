using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Splines;
using static PX.EditorTools.BuilderKit;

namespace PX.EditorTools
{
    /// <summary>
    /// Generates the locomotion scene: a plain, evenly lit stretch of ground where Rana only walks, runs, crouches and
    /// jumps, with a heavier movement tuning and a closer camera than the levels. It is where her weight is worked out.
    /// The scene file is overwritten on every rebuild. Its two configs (PlayerMoveLocomotion, HeroineAnimationLocomotion)
    /// are only created when missing, so tuning done in the editor survives; delete one to get the defaults below back.
    ///
    /// Menu: PX > Locomotion > Rebuild Scene.
    /// Command line: -executeMethod PX.EditorTools.LocomotionBuilder.Build
    /// </summary>
    public static class LocomotionBuilder
    {
        public const string ScenePath = "Assets/PX/Scenes/Locomotion.unity";
        public const string MoveConfigPath = ConfigFolder + "/PlayerMoveLocomotion.asset";
        public const string AnimationConfigPath = ConfigFolder + "/HeroineAnimationLocomotion.asset";

        private const float StartDistance = 6f;
        private const float RailLength = 120f;
        private const float FloorBottom = -4f;

        [MenuItem("PX/Locomotion/Rebuild Scene")]
        public static void Build()
        {
            AvatarPose.StraightenAll();
            EnsureFolder(System.IO.Path.GetDirectoryName(ScenePath));
            EnsureFolder(GrayboxMaterialFolder);

            GameObject playerPrefab = CharacterBuilder.LoadPlayerPrefab();
            LoadOrCreateAsset<MoveConfig>(MoveConfigPath, FillMove);
            LoadOrCreateAsset<HeroineAnimationConfig>(AnimationConfigPath, FillAnimation);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Loaded again after the new scene, which unloads assets nothing in it refers to yet.
            var move = AssetDatabase.LoadAssetAtPath<MoveConfig>(MoveConfigPath);
            var animation = AssetDatabase.LoadAssetAtPath<HeroineAnimationConfig>(AnimationConfigPath);

            Rail rail = CreateRail();
            BuildGround(rail);
            CharacterMotor motor = PlacePlayer(rail, playerPrefab, StartDistance);
            Wire(motor.GetComponent<PlayerController>(), "move", move);
            Wire(motor.GetComponent<HeroineAnimator>(), "config", animation);
            WireBool(motor.GetComponent<PlayerInputSource>(), "locomotionOnly", true);
            CreateLocomotionCamera(rail, motor);
            CreateLighting();

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Locomotion scene rebuilt at {ScenePath}. Walk clip average speed " +
                      $"{(animation.walk != null ? animation.walk.averageSpeed.magnitude : 0f):0.00} m/s, run clip " +
                      $"{(animation.run != null ? animation.run.averageSpeed.magnitude : 0f):0.00} m/s (Mixamo's skeleton).");
        }

        // ------------------------------------------------------------- tuning

        /// <summary>
        /// Heavy: she walks unless Run is held, gathers before she jumps, has one jump and little say over it in the air,
        /// and every full jump lands hard enough to cost her some speed.
        /// </summary>
        private static void FillMove(MoveConfig move)
        {
            EditorUtility.CopySerialized(CharacterBuilder.LoadMoveConfig(), move);
            move.walkSpeed = 1.2f;
            move.runSpeed = 3.8f;
            move.groundAcceleration = 5f;
            move.groundDeceleration = 7f;
            move.airAcceleration = 4f;
            move.airDeceleration = 2f;

            move.jumpHeight = 1.15f;
            move.timeToApex = 0.4f;
            move.fallGravityMultiplier = 1.35f;
            move.releasedGravityMultiplier = 1.6f;
            move.airJumps = 0;
            move.airDashes = 0;
            move.jumpWindup = 0.3f;
            move.runJumpWindup = 0.07f;

            move.hardLandingSpeed = 6f;
            move.landingShake = 0.015f;
            move.hardLandingSpeedKeep = 0.7f;
            move.hardLandingRecovery = 0.2f;

            move.turnMinSpeed = 2.6f;
            move.turnDuration = 0.45f;
        }

        private static void FillAnimation(HeroineAnimationConfig animation)
        {
            EditorUtility.CopySerialized(CharacterBuilder.LoadHeroineAnimation(), animation);
            if (CharacterBuilder.HasMixamoClip("Walk"))
                animation.walk = CharacterBuilder.MixamoClip("Walk");
            FillHeavyClips(animation);
            // Measured from the clip's average speed, scaled to her size as the run's (3.62 m/s on Mixamo's skeleton,
            // 3.1 measured on hers).
            animation.walkClipSpeed = 0.8f;
            animation.walkRunSpeed = 1.6f;
            animation.walkRunFadeTime = 0.35f;
            animation.facingTurnTime = 0.32f;
            animation.jumpWindupDip = 0.04f;
            animation.jumpPushTime = 0.1f;

            // Softer blends, deeper landings and a slower crouch.
            animation.fadeTime = 0.2f;
            animation.crouchFadeTime = 0.4f;
            animation.runJumpMinSpeed = 2.6f;
            // The landing clips already sink into the knees; the dip only adds the jolt of a hard landing.
            animation.landDip = 0.08f;
            animation.landDipSpeedRange = new Vector2(2f, 8f);
            animation.landDipDown = 0.08f;
            animation.landDipUp = 0.45f;
            animation.landIdleDuration = 0.85f;
            animation.landRunDuration = 0.42f;
        }

        /// <summary>
        /// Mixamo clips picked for weight (2026-10-28). Times are seconds into each clip, read off pose sheets
        /// (PoseSnapshot.RenderSheet): where the gather starts, where her feet leave the ground, where they touch down.
        /// </summary>
        private static void FillHeavyClips(HeroineAnimationConfig animation)
        {
            // Idle: Mixamo's Weight Shift Idle, a calm stance that rocks from foot to foot. It looks ahead, not down.
            if (CharacterBuilder.HasMixamoClip("Idle Weight Shift"))
            {
                animation.idle = CharacterBuilder.MixamoClip("Idle Weight Shift");
                animation.idleHeadLift = 0f;
            }
            // Standing jump: Mixamo's Jumping And Landing In Place. A deep squat, a push up, knees drawn in, legs
            // reaching down for the ground.
            if (CharacterBuilder.HasMixamoClip("Jump Standing"))
            {
                animation.standJump = CharacterBuilder.MixamoClip("Jump Standing");
                animation.standJumpWindupFrom = 0.1f;
                animation.standJumpTakeoff = 0.78f;
                animation.standJumpLanding = 1.2f;
            }
            // Running jump: Mixamo's Female Forward Running Jump. A long striding leap; the old one tucked like a ninja.
            if (CharacterBuilder.HasMixamoClip("Jump Running Stride"))
            {
                animation.runJump = CharacterBuilder.MixamoClip("Jump Running Stride");
                animation.runJumpWindupFrom = 0f;
                animation.runJumpTakeoff = 0.08f;
                animation.runJumpLanding = 0.52f;
            }
            animation.fitJumpToAirTime = true;
            // Landings: Mixamo's Landing From Jump, a deep squat with the hands to the knees, and Landing From Running
            // Jump Into Run Forward, which takes the weight and runs on.
            if (CharacterBuilder.HasMixamoClip("Land Heavy"))
                animation.landIdle = CharacterBuilder.MixamoWindow("Land Heavy", 0.42f, 0f, 1.35f);
            if (CharacterBuilder.HasMixamoClip("Land Running Heavy"))
                animation.landRun = CharacterBuilder.MixamoWindow("Land Running Heavy", 0.33f, 0f, 0.75f);
        }

        // --------------------------------------------------------------- level

        private static Rail CreateRail()
        {
            var go = new GameObject("Rail");
            var rail = go.AddComponent<Rail>();
            Spline spline = go.GetComponent<SplineContainer>().Spline;
            spline.Clear();
            const float handle = 20f;
            spline.Add(new BezierKnot(new float3(0f, 0f, 0f), new float3(-handle, 0f, 0f), new float3(handle, 0f, 0f)), TangentMode.Broken);
            spline.Add(new BezierKnot(new float3(RailLength, 0f, 0f), new float3(-handle, 0f, 0f), new float3(handle, 0f, 0f)), TangentMode.Broken);
            rail.Rebuild();
            return rail;
        }

        /// <summary>A long flat walk, then steps to jump onto, a ditch to jump over and a ledge to drop from.</summary>
        private static void BuildGround(Rail rail)
        {
            Material ground = LoadOrCreateMaterial(GrayboxMaterialFolder, "Locomotion Ground", new Color(0.62f, 0.6f, 0.57f), lit: true);
            Material block = LoadOrCreateMaterial(GrayboxMaterialFolder, "Locomotion Block", new Color(0.78f, 0.76f, 0.72f), lit: true);
            Material backdrop = LoadOrCreateMaterial(GrayboxMaterialFolder, "Locomotion Backdrop", new Color(0.7f, 0.69f, 0.66f), lit: true);

            Transform level = new GameObject("Level").transform;
            Transform floor = CreateGroup("Ground", level);
            Transform blocks = CreateGroup("Blocks", level);
            Transform back = CreateGroup("Backdrop", level);

            AddBlocks(floor, rail, "Floor", 0f, 58f, FloorBottom, 0f, ground);
            AddBlocks(floor, rail, "Ditch", 58f, 60f, FloorBottom, -1f, ground);
            AddBlocks(floor, rail, "Floor", 60f, RailLength, FloorBottom, 0f, ground);

            AddBlocks(blocks, rail, "Step low", 40f, 43f, 0f, 0.45f, block);
            AddBlocks(blocks, rail, "Step high", 43f, 47f, 0f, 0.9f, block);
            AddBlocks(blocks, rail, "Platform", 70f, 76f, 0f, 0.8f, block);
            AddBlocks(blocks, rail, "Ledge", 88f, 94f, 0f, 1.6f, block);

            // Something to measure depth and speed by: a wall behind her and a row of columns in front of it.
            AddBlocks(back, rail, "Back wall", 0f, RailLength, 0f, 6f, backdrop, lateral: -9f, depth: 1f, collide: false);
            for (float d = 2f; d < RailLength; d += 6f)
                AddColumn(back, rail, d, -6f, 5f, backdrop);
        }

        // ------------------------------------------------------- camera, light

        private static void CreateLocomotionCamera(Rail rail, CharacterMotor motor)
        {
            RailCamera railCamera = CreateCamera(rail, motor);
            railCamera.GetComponent<Camera>().GetUniversalAdditionalCameraData().renderPostProcessing = false;
            // Close and low, with a camera that trails her a little, so her weight carries into the frame.
            railCamera.DefaultView = new CameraView
            {
                yaw = 0f, pitch = 4f, distance = 6.5f, fieldOfView = 30f, focusHeight = 1.05f, lookAhead = 0.8f,
            };
            WireFloat(railCamera, "followSmoothTime", 0.22f);
            WireFloat(railCamera, "lookAheadSmoothTime", 0.9f);
            railCamera.Snap();
        }

        /// <summary>Plain daylight: one white sun, flat neutral ambient, no fog and no post-processing.</summary>
        private static void CreateLighting()
        {
            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45f, 30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.34f, 0.32f);
            RenderSettings.fog = false;
        }
    }
}
