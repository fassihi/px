using UnityEditor;
using UnityEngine;
using static PX.EditorTools.BuilderKit;

namespace PX.EditorTools
{
    /// <summary>
    /// The characters as prefabs, and their tuning as config assets, shared by every scene builder: Rana with her
    /// clips, Enemy A (the knight), Enemy B (the swordsman) and the training dummy. Each is only created when missing,
    /// so tuning done in the editor survives a rebuild. To regenerate one, delete it (and the prefabs that use it).
    /// </summary>
    internal static class CharacterBuilder
    {
        public const float HeroineHeight = 1.75f;

        // Enemy A, the armoured knight, stands a head taller than her. Enemy B, the swordsman, a little less.
        public const float EnemyAHeight = 2.0f;
        public const float EnemyBHeight = 1.9f;

        private const string HeroineFolder = HeroineImportSettings.Folder;

        // Enemy clip windows, in seconds into each Mixamo clip: where the part that plays starts, where the blade is
        // furthest out, and where it ends. Measured by sampling the clips on the enemies.
        private const float AttackA2From = 0.7f, AttackA2Impact = 1.4f, AttackA2To = 1.8f;
        private const float AttackB1From = 0.1f, AttackB1Impact = 0.6f, AttackB1To = 1.2f;
        private const float AttackB2From = 0.1f, AttackB2Impact = 0.7f, AttackB2To = 1.5f;
        // The climb is Mixamo's running jump, all of it.
        private const float ClimbAFrom = 0f, ClimbATo = 0f;
        private const float ClimbBFrom = 0f, ClimbBTo = 0f;

        // Rana's clip windows, in seconds into each Mixamo clip (from, impact, to for strikes; from, to otherwise).
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

        // ------------------------------------------------------------- loading

        public static MoveConfig LoadMoveConfig() => LoadOrCreateAsset<MoveConfig>($"{ConfigFolder}/PlayerMove.asset", null);

        public static ComboConfig LoadComboConfig() => LoadOrCreateAsset<ComboConfig>($"{ConfigFolder}/PlayerLightCombo.asset", FillDefaultCombo);

        public static PlayerCombatConfig LoadCombatConfig() => LoadOrCreateAsset<PlayerCombatConfig>($"{ConfigFolder}/PlayerCombat.asset", null);

        public static HeroineAnimationConfig LoadHeroineAnimation() =>
            LoadOrCreateAsset<HeroineAnimationConfig>($"{ConfigFolder}/HeroineAnimation.asset", FillHeroineAnimation);

        public static GameObject LoadPlayerPrefab()
        {
            MoveConfig move = LoadMoveConfig();
            ComboConfig combo = LoadComboConfig();
            PlayerCombatConfig combat = LoadCombatConfig();
            HeroineAnimationConfig animation = LoadHeroineAnimation();
            return LoadOrCreatePrefab($"{PrefabFolder}/Player.prefab", () => CreatePlayer(move, combo, combat, animation));
        }

        public static GameObject LoadDummyPrefab() => LoadOrCreatePrefab($"{PrefabFolder}/TrainingDummy.prefab", CreateDummy);

        public static EnemyConfig LoadEnemyAConfig() => LoadOrCreateAsset<EnemyConfig>($"{ConfigFolder}/EnemyA.asset", FillEnemyA);

        public static EnemyConfig LoadEnemyBConfig() => LoadOrCreateAsset<EnemyConfig>($"{ConfigFolder}/EnemyB.asset", FillEnemyB);

        public static GameObject LoadEnemyAPrefab()
        {
            EnemyConfig config = LoadEnemyAConfig();
            return LoadOrCreatePrefab($"{PrefabFolder}/EnemyA.prefab", () => CreateEnemy(
                "EnemyA", config, EnemyImportSettings.EnemyABodyPath, EnemyImportSettings.EnemyATexturePath, EnemyAHeight, 120f, 0.45f));
        }

        public static GameObject LoadEnemyBPrefab()
        {
            EnemyConfig config = LoadEnemyBConfig();
            return LoadOrCreatePrefab($"{PrefabFolder}/EnemyB.prefab", () => CreateEnemy(
                "EnemyB", config, EnemyImportSettings.EnemyBBodyPath, EnemyImportSettings.EnemyBTexturePath, EnemyBHeight, 80f, 0.4f));
        }

        /// <summary>The Captain stands a third taller than the knight whose body it borrows.</summary>
        public const float CaptainHeight = 2.7f;

        public static EnemyConfig LoadCaptainConfig() => LoadOrCreateAsset<EnemyConfig>($"{ConfigFolder}/Captain.asset", FillCaptain);

        public static GameObject LoadCaptainPrefab()
        {
            EnemyConfig config = LoadCaptainConfig();
            return LoadOrCreatePrefab($"{PrefabFolder}/Captain.prefab", () => CreateEnemy(
                "Captain", config, EnemyImportSettings.EnemyABodyPath, EnemyImportSettings.EnemyATexturePath, CaptainHeight, 620f, 0.6f));
        }

        public static GameObject LoadBoltPrefab(EnvironmentKit.Palette palette) =>
            LoadOrCreatePrefab($"{PrefabFolder}/Bolt.prefab", () => CreateBolt(palette));

        public static GameObject LoadSentinelPrefab(EnvironmentKit.Palette palette, GameObject boltPrefab) =>
            LoadOrCreatePrefab($"{PrefabFolder}/Sentinel.prefab", () => CreateSentinel(palette, boltPrefab));

        /// <summary>
        /// Fills the fields added to the configs since they were made, where they are still empty: the guards' look
        /// around, the light they leave, the sabre's reach. Existing tuning is left alone.
        /// </summary>
        public static void UpgradeConfigs()
        {
            EnemyConfig a = LoadEnemyAConfig();
            if (a.lookAround.clip == null && HasEnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Look Around"))
            {
                a.lookAround = new ClipWindow { clip = EnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Look Around") };
                a.lightValue = 45f;
                EditorUtility.SetDirty(a);
            }
            EnemyConfig b = LoadEnemyBConfig();
            if (b.lookAround.clip == null && HasEnemyClip(EnemyImportSettings.EnemyBMixamoFolder, "Look Around"))
            {
                b.lookAround = new ClipWindow { clip = EnemyClip(EnemyImportSettings.EnemyBMixamoFolder, "Look Around") };
                b.lightValue = 30f;
                b.staggerMax = 45f;
                EditorUtility.SetDirty(b);
            }
            ComboConfig combo = LoadComboConfig();
            if (combo.steps.Length == 5 && combo.steps[0].hitboxSize.x < 1.3f && HasMixamoClip("Sabre 1 Horizontal"))
            {
                FillSabreCombo(combo);
                EditorUtility.SetDirty(combo);
            }
            // The clip windows are measured numbers (see ClipMeasure), so they are re-applied on every build.
            HeroineAnimationConfig animation = LoadHeroineAnimation();
            if (HasMixamoClip("Takedown Stab"))
            {
                FillSabreAnimation(animation);
                EditorUtility.SetDirty(animation);
            }
            EnemyConfig captain = LoadCaptainConfig();
            ApplyCaptainClips(captain);
            EditorUtility.SetDirty(captain);
        }

        // ------------------------------------------------------------- heroine

        private static GameObject CreatePlayer(MoveConfig move, ComboConfig combo, PlayerCombatConfig combat, HeroineAnimationConfig animation)
        {
            var root = new GameObject("Player") { tag = "Player" };
            UpgradeConfigs();

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
            Material slashMaterial = LoadOrCreateMaterial(GrayboxMaterialFolder, "Slash", new Color(1f, 0.87f, 0.45f, 0.4f), lit: false);
            GameObject slash = AddPart(root.transform, PrimitiveType.Cube, Vector3.zero, Vector3.one, slashMaterial);
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

            // Her sabre, seated in her right hand once the rig is posed, and the ribbon of the tether.
            Transform sabre = CreateSabreVisual(root.transform);
            var held = root.AddComponent<HeldSabre>();
            Wire(held, "animator", animator);
            Wire(held, "blade", sabre);
            var tether = root.AddComponent<TetherEffects>();
            Wire(tether, "player", player);
            Wire(tether, "animator", animator);
            var ribbon = new GameObject("TetherRibbon").AddComponent<LineRenderer>();
            ribbon.transform.SetParent(root.transform, false);
            ribbon.positionCount = 2;
            ribbon.widthMultiplier = 0.06f;
            ribbon.numCapVertices = 4;
            ribbon.alignment = LineAlignment.View;
            ribbon.sharedMaterial = LoadOrCreateParticleMaterial(GrayboxMaterialFolder, "MoveTrail", ring: false, additive: false, streak: true);
            ribbon.enabled = false;
            Wire(tether, "ribbon", ribbon);

            root.AddComponent<HitFlash>();
            return root;
        }

        /// <summary>
        /// A sabre from primitives: a wrapped grip, a small guard, and a blade of segments that curve toward the edge,
        /// with a thread of light along the edge. Local +Z runs from pommel to tip, +Y toward the edge.
        /// </summary>
        private static Transform CreateSabreVisual(Transform root)
        {
            Material steel = EnvironmentKit.LoadOrCreateMetal(GrayboxMaterialFolder, "Sabre Steel", new Color(0.62f, 0.66f, 0.7f));
            Material grip = LoadOrCreateMaterial(GrayboxMaterialFolder, "Sabre Grip", new Color(0.18f, 0.12f, 0.08f), lit: true, smoothness: 0.35f);
            Material edge = EnvironmentKit.LoadOrCreateEmissive(GrayboxMaterialFolder, "Sabre Edge", new Color(1f, 0.5f, 0.15f) * 0.5f, new Color(1f, 0.45f, 0.1f), 2.6f);
            Transform sabre = CreateGroup("Sabre", root);

            AddPart(sabre, PrimitiveType.Sphere, new Vector3(0f, 0f, -0.01f), Vector3.one * 0.05f, steel);
            GameObject handle = AddPart(sabre, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.08f), new Vector3(0.036f, 0.08f, 0.036f), grip);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            AddPart(sabre, PrimitiveType.Cube, new Vector3(0f, 0f, 0.17f), new Vector3(0.11f, 0.03f, 0.025f), steel);
            AddPart(sabre, PrimitiveType.Cube, new Vector3(0f, -0.035f, 0.1f), new Vector3(0.012f, 0.1f, 0.012f), steel);

            // The blade: segments turned a little more toward the edge each time, so the tip sweeps up.
            Vector3 position = new Vector3(0f, 0f, 0.185f);
            Quaternion turn = Quaternion.identity;
            const int segments = 6;
            const float length = 0.14f;
            for (int i = 0; i < segments; i++)
            {
                float taper = i == segments - 1 ? 0.45f : 1f - i * 0.06f;
                Vector3 centre = position + turn * new Vector3(0f, 0f, length * 0.5f);
                GameObject segment = AddPart(sabre, PrimitiveType.Cube, centre, new Vector3(0.006f, 0.034f * taper, length + 0.004f), steel);
                segment.transform.localRotation = turn;
                GameObject thread = AddPart(sabre, PrimitiveType.Cube, centre + turn * new Vector3(0f, 0.017f * taper, 0f), new Vector3(0.0035f, 0.004f, length), edge);
                thread.transform.localRotation = turn;
                position += turn * new Vector3(0f, 0f, length);
                turn *= Quaternion.Euler(-4.5f, 0f, 0f);
            }
            return sabre;
        }

        /// <summary>
        /// The sabre combo: a horizontal cut, a backhand, her side kick, a heavy downward cut, and the spinning back
        /// kick to finish. The blade reaches a little further than her fists did.
        /// </summary>
        private static void FillSabreCombo(ComboConfig combo)
        {
            combo.steps[0].hitboxCenter = new Vector2(0.95f, 1.2f);
            combo.steps[0].hitboxSize = new Vector2(1.5f, 0.9f);
            combo.steps[0].damage = 12f;
            combo.steps[0].startup = 0.08f;
            combo.steps[1].hitboxCenter = new Vector2(0.95f, 1.2f);
            combo.steps[1].hitboxSize = new Vector2(1.5f, 0.9f);
            combo.steps[1].damage = 14f;
            combo.steps[1].startup = 0.09f;
            combo.steps[3].hitboxCenter = new Vector2(1.0f, 1.0f);
            combo.steps[3].hitboxSize = new Vector2(1.6f, 1.6f);
            combo.steps[3].damage = 22f;
            combo.steps[3].startup = 0.16f;
            combo.steps[3].recovery = 0.36f;
            combo.steps[3].knockback = 6f;
            combo.steps[3].staminaCost = 13f;
            combo.hitboxDepth = 0.7f;
        }

        // Sabre clip windows, in seconds into each Mixamo clip: from, impact (blade furthest out), to.
        // Measured with ClipMeasure (PX > Rana > Measure Clips): the impact is where the right hand is furthest in
        // front of the hips. The backhand has a second, forehand swing after 1.6 s that is left out.
        private static readonly Vector3 SabreHorizontal = new Vector3(0.5f, 0.9f, 1.7f);
        private static readonly Vector3 SabreBackhand = new Vector3(0.6f, 1.05f, 1.6f);
        private static readonly Vector3 SabreDownward = new Vector3(0.4f, 0.85f, 1.5f);
        private static readonly Vector3 SabreSpin = new Vector3(0.5f, 1.0f, 1.7f);
        // The stab goes in at 1.0 s and stays in until 1.5 s.
        private static readonly Vector3 TakedownStab = new Vector3(0.4f, 1.0f, 2.0f);
        private static readonly Vector2 DrinkWindow = new Vector2(2.0f, 4.6f);

        /// <summary>Her sabre clips: the cuts in the combo, the lunge out of a dash, the stab for finishers, the drink.</summary>
        private static void FillSabreAnimation(HeroineAnimationConfig config)
        {
            if (HasMixamoClip("Sabre 1 Horizontal") && HasMixamoClip("Sabre 2 Backhand") && HasMixamoClip("Sabre 3 Downward"))
            {
                config.comboAttacks[0] = MixamoWindow("Sabre 1 Horizontal", SabreHorizontal.x, SabreHorizontal.y, SabreHorizontal.z);
                config.comboAttacks[1] = MixamoWindow("Sabre 2 Backhand", SabreBackhand.x, SabreBackhand.y, SabreBackhand.z);
                config.comboAttacks[3] = MixamoWindow("Sabre 3 Downward", SabreDownward.x, SabreDownward.y, SabreDownward.z);
                config.attack = config.comboAttacks[0].clip;
            }
            if (HasMixamoClip("Sabre Spin"))
                config.dashAttack = MixamoWindow("Sabre Spin", SabreSpin.x, SabreSpin.y, SabreSpin.z);
            if (HasMixamoClip("Takedown Stab"))
                config.finisher = MixamoWindow("Takedown Stab", TakedownStab.x, TakedownStab.y, TakedownStab.z);
            if (HasMixamoClip("Drink"))
                config.drink = MixamoWindow("Drink", DrinkWindow.x, 0f, DrinkWindow.y);
        }

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
            FillSabreAnimation(config);
        }

        public static ClipWindow MixamoWindow(string name, float from, float impact, float to)
        {
            return new ClipWindow { clip = MixamoClip(name), from = from, impact = impact, to = to };
        }

        public static AnimationClip MixamoClip(string name)
        {
            return RanaViewerBuilder.FindClip(RanaViewerBuilder.LoadClips($"{RanaImportSettings.MixamoFolder}/{name}.fbx"), name);
        }

        /// <summary>True when a Mixamo clip of that name has been downloaded for Rana.</summary>
        public static bool HasMixamoClip(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{RanaImportSettings.MixamoFolder}/{name}.fbx") != null;

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

        public static AnimationClip EnemyClip(string folder, string name)
        {
            return RanaViewerBuilder.FindClip(RanaViewerBuilder.LoadClips($"{folder}/{name}.fbx"), name);
        }

        // The Captain's clip windows, in seconds into Mixamo's Great Sword clips (from, impact, to), measured with
        // ClipMeasure: the slam's chop lands at 1.15 s after the leap, the charge's cut at 1.15 s, the roar peaks at 2.5 to 3 s.
        private static readonly Vector3 SlamWindow = new Vector3(0.2f, 1.15f, 1.9f);
        private static readonly Vector3 SpinWindow = new Vector3(0.3f, 1.15f, 1.8f);
        private static readonly Vector2 PowerUpWindow = new Vector2(0.3f, 3.4f);

        /// <summary>
        /// The Captain: the knight's body and clips, half again as much reach, slow heavy swings that hit hard, poise
        /// that shrugs off her light cuts, a leaping slam, a spinning charge, and a second phase that comes faster.
        /// </summary>
        private static void FillCaptain(EnemyConfig config)
        {
            FillEnemyA(config);
            config.isBoss = true;
            config.aggroRange = 30f;
            config.chaseRange = 60f;
            config.aggroHeight = 4f;
            config.attackRange = 2.6f;
            config.tooClose = 1.4f;
            config.walkSpeed = 1.8f;
            config.runSpeed = 5.2f;
            config.runClipSpeed = 3.6f;
            config.runDistance = 5f;
            config.walkBackSpeed = 1.3f;
            config.climbHeight = 2.4f;
            config.attack = new AttackStep
            {
                startup = 0.7f, active = 0.16f, recovery = 0.8f, cancelDelay = 0.3f,
                damage = 26f, lunge = 1.8f, hitStop = 0.14f, shake = 0.2f, knockback = 9f,
                hitboxCenter = new Vector2(1.5f, 1.3f), hitboxSize = new Vector2(2.4f, 2.2f),
            };
            config.attack2 = new AttackStep
            {
                startup = 0.55f, active = 0.2f, recovery = 0.95f, cancelDelay = 0f,
                damage = 34f, lunge = 3f, hitStop = 0.16f, shake = 0.26f, knockback = 12f,
                hitboxCenter = new Vector2(1.5f, 1.2f), hitboxSize = new Vector2(2.6f, 2.0f),
            };
            config.secondOpenerChance = 0.3f;
            config.comboChance = 0.55f;
            config.retreatChance = 0.25f;
            config.attackCooldown = 1.1f;
            config.hitboxDepth = 1f;
            config.attackPoise = 60f;
            config.hitStun = 0.3f;
            config.parriedStun = 1.6f;
            config.staggerMax = 220f;
            config.staggerDecay = 10f;
            config.staggeredTime = 3f;
            config.parryStagger = 70f;
            config.executeDamageShare = 0.22f;
            config.lightValue = 400f;
            config.idleHeadLift = 12f;
            config.runLean = 4f;
            config.slamLeap = 5f;
            config.slamArc = 2.2f;
            config.chargeDistance = 6.5f;
            config.phase2Health = 0.5f;
            config.phase2Speed = 1.25f;
            config.phase2Cooldown = 0.6f;
            if (HasEnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Slam"))
                config.slamClip = new ClipWindow { clip = EnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Slam"), from = SlamWindow.x, impact = SlamWindow.y, to = SlamWindow.z };
            if (HasEnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Spin Attack"))
                config.spinClip = new ClipWindow { clip = EnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Spin Attack"), from = SpinWindow.x, impact = SpinWindow.y, to = SpinWindow.z };
            if (HasEnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Power Up"))
                config.powerUp = new ClipWindow { clip = EnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Power Up"), from = PowerUpWindow.x, to = PowerUpWindow.y };
            if (HasEnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Look Around"))
                config.lookAround = new ClipWindow { clip = EnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Look Around") };
        }

        /// <summary>The Captain's clip windows, re-applied on every build: they are measured numbers, not tuning.</summary>
        private static void ApplyCaptainClips(EnemyConfig config)
        {
            config.powerUpTime = 2.2f;
            if (HasEnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Slam"))
                config.slamClip = new ClipWindow { clip = EnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Slam"), from = SlamWindow.x, impact = SlamWindow.y, to = SlamWindow.z };
            if (HasEnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Spin Attack"))
                config.spinClip = new ClipWindow { clip = EnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Spin Attack"), from = SpinWindow.x, impact = SpinWindow.y, to = SpinWindow.z };
            if (HasEnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Power Up"))
                config.powerUp = new ClipWindow { clip = EnemyClip(EnemyImportSettings.EnemyAMixamoFolder, "Power Up"), from = PowerUpWindow.x, to = PowerUpWindow.y };
        }

        // ------------------------------------------------------------ craft

        /// <summary>A sentinel's bolt: a small glowing sphere with a light and a short trail.</summary>
        private static GameObject CreateBolt(EnvironmentKit.Palette palette)
        {
            var root = new GameObject("Bolt");
            var bolt = root.AddComponent<Bolt>();
            AddPart(root.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.32f, palette.Neon);
            Light light = EnvironmentKit.AddPointLight(root.transform, Vector3.zero, palette.Glow2Color, 4f, 2f);
            TrailRenderer trail = NewTrail(root.transform, "Trail", LoadOrCreateParticleMaterial(GrayboxMaterialFolder, "MoveTrail", ring: false, additive: false, streak: true), 0.25f, 0.25f, palette.Glow2Color);
            trail.emitting = true;
            Wire(bolt, "trail", trail);
            Wire(bolt, "glow", light);
            return root;
        }

        /// <summary>A sentinel: the craft's visual, a sphere to be hit, health, and the brain that is the component itself.</summary>
        private static GameObject CreateSentinel(EnvironmentKit.Palette palette, GameObject boltPrefab)
        {
            var root = new GameObject("Sentinel");
            var collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.8f;
            var health = root.AddComponent<Health>();
            WireFloat(health, "max", 60f);
            var sentinel = root.AddComponent<Sentinel>();
            var visual = CreateGroup("Visual", root.transform);
            EnvironmentKit.AddCraftVisual(visual, palette);
            Renderer eye = null;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
            {
                if (renderer.name == "Eye")
                    eye = renderer;
            }
            Wire(sentinel, "visual", visual);
            Wire(sentinel, "eye", eye);
            Wire(sentinel, "boltPrefab", boltPrefab.GetComponent<Bolt>());
            root.AddComponent<HitFlash>();
            return root;
        }

        public static bool HasEnemyClip(string folder, string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name}.fbx") != null;

        public static GameObject CreateEnemy(string name, EnemyConfig config, string bodyPath, string texturePath, float height, float health, float radius)
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
            WireFloat(healthComponent, "max", health);
            var enemy = root.AddComponent<EnemyController>();
            var brain = root.AddComponent<EnemyBrain>();
            var enemyAnimator = root.AddComponent<EnemyAnimator>();
            var ragdoll = root.AddComponent<EnemyRagdoll>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            GameObject model = InstantiateModel(bodyPath, visual);
            string materialPath = $"{System.IO.Path.GetDirectoryName(texturePath).Replace("\\", "/").Replace("/Textures", "")}/{name}.mat";
            Material skin = LoadOrCreateTexturedMaterial(materialPath, texturePath);
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

        // -------------------------------------------------------------- dummy

        private static GameObject CreateDummy()
        {
            var root = new GameObject("TrainingDummy");

            var collider = root.AddComponent<CapsuleCollider>();
            collider.height = 1.8f;
            collider.radius = 0.4f;
            collider.center = new Vector3(0f, 0.9f, 0f);

            root.AddComponent<Health>();
            var dummy = root.AddComponent<TrainingDummy>();

            Material material = LoadOrCreateMaterial(GrayboxMaterialFolder, "Dummy", new Color(0.72f, 0.33f, 0.2f), lit: true);
            GameObject body = AddPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), material);
            body.name = "Body";
            AddPart(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.35f, 0f), new Vector3(0.16f, 0.16f, 1.3f), material).name = "Arms";

            Wire(dummy, "body", body.GetComponent<Renderer>());
            return root;
        }
    }
}
