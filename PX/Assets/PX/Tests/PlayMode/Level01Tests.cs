using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PX.Tests
{
    /// <summary>Level 01: its layout, the shrines and flasks, stealth and takedowns, the tether, the sentinels, the Captain.</summary>
    public sealed class Level01Tests : Level01Fixture
    {
        [UnityTest]
        public IEnumerator TheLevel_HasItsRailAndItsSystems()
        {
            Assert.That(Rail.Length, Is.EqualTo(592f).Within(4f), "the rail runs the length the plan says");
            Assert.That(Flow.Shrines.Count, Is.EqualTo(6), "six shrines: threshold, court, guardhouse, gate, scaffolds, bazaar");
            Assert.That(Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).Length, Is.GreaterThanOrEqualTo(10), "guards and the Captain");
            Assert.That(Object.FindObjectsByType<Sentinel>(FindObjectsSortMode.None).Length, Is.EqualTo(5), "five sentinels");
            Assert.That(Flow.Boss, Is.Not.Null);
            Assert.That(Flow.Boss.Config.isBoss, Is.True, "the Captain is a boss");
            Assert.That(Object.FindObjectsByType<TetherAnchor>(FindObjectsSortMode.None).Length, Is.GreaterThanOrEqualTo(8), "lamps to tether to");
            Assert.That(Director, Is.Not.Null, "a camera director for the reveals and finishers");
            Assert.That(Object.FindFirstObjectByType<KillPlane>(), Is.Not.Null, "the water");
            Assert.That(Player.HasTether, Is.False, "she starts without the tether");
            Assert.That(Player.Flasks, Is.EqualTo(Player.MaxFlasks), "flasks full at the start");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Shrines_LightAsShePasses_AndBecomeWhereSheGetsUp()
        {
            Shrine court = ShrineNamed("The Flooded Court");
            Assert.That(court, Is.Not.Null);
            Assume.That(court.IsLit, Is.False);
            yield return PlaceAt(court.Distance - 0.5f, court.Height + 0.1f);
            yield return Frames(5);
            Assert.That(court.IsLit, Is.True, "walking up to a shrine lights it");
            Assert.That(Respawn.Checkpoint, Is.EqualTo(court.Distance).Within(0.01f));

            Health health = Player.GetComponent<Health>();
            health.TakeHit(new Hit(health.Max * 2f, Vector3.back, null, 3f));
            yield return Frames(3);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Dead));
            yield return Seconds(Player.Combat.respawnDelay + 0.2f);
            Assert.That(Player.State, Is.Not.EqualTo(PlayerState.Dead));
            Assert.That(Motor.Distance, Is.EqualTo(court.Distance).Within(0.5f), "she got up at the shrine");
        }

        [UnityTest]
        public IEnumerator Flasks_Heal_AndAreRefilledOnlyByResting()
        {
            Shrine threshold = ShrineNamed("The Threshold");
            yield return PlaceAt(threshold.Distance + 4f);
            Health health = Player.GetComponent<Health>();
            health.TakeHit(new Hit(60f, Vector3.back, null));
            yield return Seconds(Player.Move.hurtStun + 0.2f);
            float hurt = health.Current;
            Assume.That(hurt, Is.LessThan(health.Max));

            Input.PressFlask();
            yield return Frames(3);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Drink), "she drinks");
            Assert.That(Player.Flasks, Is.EqualTo(Player.MaxFlasks - 1), "a flask is spent at once");
            yield return Seconds(Player.Combat.flaskDrinkTime + 0.1f);
            Assert.That(health.Current, Is.EqualTo(Mathf.Min(health.Max, hurt + Player.Combat.flaskHeal)).Within(0.01f), "the flask healed her");
            Assert.That(Player.State, Is.EqualTo(PlayerState.Locomotion));

            // Standing at the shrine, Interact rests: flasks full again, and the menu opens.
            yield return PlaceAt(threshold.Distance);
            Input.PressInteract();
            yield return Frames(3);
            Assert.That(Player.Flasks, Is.EqualTo(Player.MaxFlasks), "a rest refills the flasks");
            Assert.That(Flow.MenuOpen, Is.True, "the shrine's menu opens");
            Input.PressInteract();
            yield return Frames(2);
            Assert.That(Flow.MenuOpen, Is.False, "and closes again");
        }

        [UnityTest]
        public IEnumerator AGuardWithItsBackToHer_DoesNotNoticeHer_AndCanBeTakenDown()
        {
            EnemyController guard = GuardNear(52f);
            Assert.That(guard, Is.Not.Null);
            var brain = guard.GetComponent<EnemyBrain>();
            Assume.That(brain.Stealth, Is.True, "the level's guards see only in front of them");
            Assume.That(guard.Motor.Facing, Is.EqualTo(1), "the first guard faces away from where she comes");

            yield return PlaceAt(guard.PostDistance - 6f);
            yield return Seconds(2f);
            Assert.That(brain.Awareness, Is.EqualTo(Awareness.Unaware), "standing behind it in plain view, she is not seen");

            // Up behind it: the takedown is on offer, and Attack takes it.
            yield return PlaceAt(guard.PostDistance - 1.2f);
            Assert.That(Player.FindFinisherTarget(out bool execution), Is.EqualTo(guard), "a takedown is on offer");
            Assert.That(execution, Is.False);
            Input.PressAttack();
            yield return Frames(3);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Finisher));
            Assert.That(Director != null && RailCamera.IsShotActive, Is.True, "the camera cuts in for the finisher");
            yield return Seconds(Player.Combo.finisher.Total + 0.3f);
            Assert.That(guard.Health.IsDead, Is.True, "the takedown kills it");
            Assert.That(Flow.Light, Is.GreaterThan(0f), "and leaves light");
        }

        [UnityTest]
        public IEnumerator AGuardFacingHer_SeesHer_AndComes()
        {
            EnemyController guard = GuardNear(52f);
            var brain = guard.GetComponent<EnemyBrain>();
            yield return PlaceAt(guard.PostDistance + 5f);
            Motor.Facing = -1;
            for (int i = 0; i < 180 && brain.Awareness != Awareness.Alert; i++)
                yield return null;
            Assert.That(brain.Awareness, Is.EqualTo(Awareness.Alert), "in front of it, she is seen");
            Assert.That(Player.FindFinisherTarget(out _), Is.Null, "no takedown on an alert guard");
        }

        [UnityTest]
        public IEnumerator CrouchedBehindCover_SheIsHidden_StandingSheIsSeen()
        {
            // The guardhouse: cover at 164, the first guard walking its round toward it.
            EnemyController guard = GuardNear(156f);
            var brain = guard.GetComponent<EnemyBrain>();
            yield return PlaceAt(166.3f, crouched: true);
            Motor.Facing = -1;
            yield return Frames(5);
            Assume.That(Player.State, Is.EqualTo(PlayerState.Crouch));
            Assume.That(brain.Awareness, Is.Not.EqualTo(Awareness.Alert), "it had not seen her standing");
            float waited = 0f;
            // Wait until it faces her, walking toward the cover.
            while (guard.Motor.Facing != 1 && waited < 20f)
            {
                yield return null;
                waited += FrameTime;
            }
            yield return Seconds(2.5f);
            Assert.That(brain.Awareness, Is.Not.EqualTo(Awareness.Alert), "crouched behind the cover she is not seen");

            Input.CrouchHeld = false;
            for (int i = 0; i < 150 && brain.Awareness != Awareness.Alert; i++)
                yield return null;
            Assert.That(brain.Awareness, Is.EqualTo(Awareness.Alert), "standing up, she is");
        }

        [UnityTest]
        public IEnumerator ANoise_MakesAGuardSuspicious_AndItComesToLook()
        {
            // Behind the last guard, crouched behind the cover at 220, so that it hears her but cannot see her.
            EnemyController guard = GuardNear(226f);
            var brain = guard.GetComponent<EnemyBrain>();
            yield return PlaceAt(219f);
            Motor.Facing = 1;
            Input.CrouchHeld = true;
            yield return Seconds(1f);
            Assume.That(brain.Awareness, Is.EqualTo(Awareness.Unaware));

            Noise.Emit(Player.transform.position, 9f, Player.gameObject);
            yield return Frames(3);
            Assert.That(brain.Awareness, Is.EqualTo(Awareness.Suspicious), "it heard something");
            float start = guard.Motor.Distance;
            yield return Seconds(1.5f);
            Assert.That(guard.Motor.Distance, Is.LessThan(start - 0.5f), "and walks toward it");
            Assert.That(guard.Mood, Is.EqualTo(Mood.Suspicious));
        }

        [UnityTest]
        public IEnumerator TheTether_PullsHerUpToALamp_AndOnToTheGallery()
        {
            // Below the gate chamber's lamps, with the tether in hand.
            Player.HasTether = true;
            yield return PlaceAt(258f);
            Motor.Facing = 1;
            Assert.That(Player.FindAnchor(), Is.Not.Null, "a lamp is in reach");
            Input.PressInteract();
            yield return Frames(3);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Tether), "she throws the tether");
            for (int i = 0; i < 120 && Player.State == PlayerState.Tether; i++)
                yield return null;
            Assert.That(Motor.transform.position.y, Is.GreaterThan(3.5f), "the pull carried her up");

            // From the top of the first pull, the second lamp is in reach; it lands her on the gallery.
            Input.PressInteract();
            var trace = new System.Text.StringBuilder();
            for (int i = 0; i < 240 && !(Motor.IsGrounded && Player.State == PlayerState.Locomotion && Motor.transform.position.y > 5f); i++)
            {
                if (Player.State != PlayerState.Tether && !Motor.IsGrounded && i % 10 == 0)
                    Input.PressInteract();
                if (i % 15 == 0)
                    trace.Append($"[{i}: {Player.State} d={Motor.Distance:0.0} y={Motor.transform.position.y:0.0} g={Motor.IsGrounded}] ");
                yield return null;
            }
            Assert.That(Motor.IsGrounded, Is.True, "she landed: " + trace);
            Assert.That(Motor.transform.position.y, Is.EqualTo(9f).Within(0.6f), "on the gallery");
        }

        [UnityTest]
        public IEnumerator WithoutTheTether_Interact_DoesNothingAtALamp()
        {
            yield return PlaceAt(258f);
            Assume.That(Player.HasTether, Is.False);
            Input.PressInteract();
            yield return Frames(3);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Locomotion));
        }

        [UnityTest]
        public IEnumerator ASentinel_SeesHer_AndFires_AndSheCanParryTheBoltBack()
        {
            Sentinel sentinel = SentinelNear(98f);
            Assert.That(sentinel, Is.Not.Null);
            yield return PlaceAt(96f, 0.6f);
            // It drifts between its posts and sees only the way it faces, so it may take a few seconds to turn.
            for (int i = 0; i < 900 && !sentinel.Alert; i++)
                yield return null;
            Assert.That(sentinel.Alert, Is.True, "it sees her over the platforms");

            Bolt bolt = null;
            for (int i = 0; i < 400 && bolt == null; i++)
            {
                bolt = Object.FindFirstObjectByType<Bolt>();
                yield return null;
            }
            Assert.That(bolt, Is.Not.Null, "it fires");

            // Guard goes up just as the bolt arrives: parried, it flies back the way it came.
            Motor.Facing = bolt.Distance > Motor.Distance ? 1 : -1;
            for (int i = 0; i < 300 && bolt != null && Mathf.Abs(bolt.Distance - Motor.Distance) > 1.4f; i++)
                yield return null;
            Assert.That(bolt, Is.Not.Null, "the bolt still flies");
            Input.PressGuard();
            for (int i = 0; i < 40 && bolt != null && !bolt.Reflected; i++)
                yield return null;
            Assert.That(bolt != null && bolt.Reflected, Is.True, "parried, the bolt turns round");
            Assert.That(Player.GetComponent<Health>().Current, Is.EqualTo(Player.GetComponent<Health>().Max), "and did no harm");
        }

        [UnityTest]
        public IEnumerator TheDashAttack_IsALungeWithTheBlade()
        {
            yield return PlaceAt(246f);
            Input.Move = 1f;
            yield return Frames(10);
            Input.PressDash();
            yield return Frames(6);
            Assume.That(Player.State, Is.EqualTo(PlayerState.Dash));
            Input.PressAttack();
            yield return Frames(3);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Attack));
            Assert.That(Player.IsDashAttack, Is.True, "attack out of a dash is the lunge");
            Assert.That(Player.CurrentStep, Is.SameAs(Player.Combo.dashAttack));
        }

        [UnityTest]
        public IEnumerator EnoughBlows_StaggerAGuard_AndSheCanExecuteIt()
        {
            EnemyController guard = GuardNear(52f);
            yield return PlaceAt(guard.PostDistance - 1.3f);
            Motor.Facing = 1;
            // Rock it past its stagger limit with heavy blows from behind.
            Health health = guard.Health;
            for (int i = 0; i < 6 && !guard.IsStaggered; i++)
            {
                health.TakeHit(new Hit(12f, Vector3.right, Player.gameObject, 1f));
                yield return Frames(3);
            }
            Assert.That(guard.IsStaggered, Is.True, "its stagger ran over");
            Assert.That(Player.FindFinisherTarget(out bool execution), Is.EqualTo(guard));
            Assert.That(execution, Is.True, "an execution is on offer");
            Input.PressAttack();
            yield return Frames(3);
            Assert.That(Player.State, Is.EqualTo(PlayerState.Finisher));
            Assert.That(Player.IsExecution, Is.True);
            yield return Seconds(Player.Combo.finisher.Total + 0.3f);
            Assert.That(guard.Health.IsDead, Is.True, "the execution kills a regular guard");
        }

        [UnityTest]
        public IEnumerator FallingIntoTheWater_IsADeath_AndSheGetsUpAtHerShrine()
        {
            Shrine threshold = ShrineNamed("The Threshold");
            yield return PlaceAt(threshold.Distance);
            yield return Frames(3);
            yield return PlaceAt(26f, 0.5f);
            for (int i = 0; i < 240 && Player.State != PlayerState.Dead; i++)
                yield return null;
            Assert.That(Player.State, Is.EqualTo(PlayerState.Dead), "the water takes her");
            yield return Seconds(Player.Combat.respawnDelay + 0.3f);
            Assert.That(Player.State, Is.Not.EqualTo(PlayerState.Dead));
            Assert.That(Motor.Distance, Is.EqualTo(threshold.Distance).Within(0.5f), "she gets up at the shrine");
        }

        [UnityTest]
        public IEnumerator WhenSheFalls_HerLightWaitsWhereSheFell_AndSheCanTakeItBack()
        {
            EnemyController guard = GuardNear(52f);
            Shrine threshold = ShrineNamed("The Threshold");
            yield return PlaceAt(threshold.Distance);
            yield return Frames(3);
            yield return PlaceAt(40f, 3.3f);
            guard.Health.TakeHit(new Hit(1000f, Vector3.right, Player.gameObject, 2f));
            yield return Frames(3);
            float carried = Flow.Light;
            Assume.That(carried, Is.GreaterThan(0f));

            Health health = Player.GetComponent<Health>();
            health.TakeHit(new Hit(health.Max * 2f, Vector3.back, null, 3f));
            yield return Frames(3);
            Assert.That(Flow.Light, Is.EqualTo(0f), "it is dropped");
            Assert.That(Flow.Dropped, Is.Not.Null, "and waits where she fell");
            Assert.That(Flow.Dropped.Amount, Is.EqualTo(carried));
            yield return Seconds(Player.Combat.respawnDelay + 0.3f);
            Assume.That(Player.State, Is.Not.EqualTo(PlayerState.Dead));
            Assert.That(guard.Health.IsDead, Is.False, "the guards come back when she gets up");

            yield return PlaceAt(Flow.Dropped.Distance, 3.3f);
            yield return Frames(10);
            Assert.That(Flow.Light, Is.EqualTo(carried), "walking into it takes it back");
            Assert.That(Flow.Dropped, Is.Null);
        }

        [UnityTest]
        public IEnumerator TheCaptain_WakesWhenSheEnters_AndHasASecondPhase()
        {
            EnemyController captain = Flow.Boss;
            yield return PlaceAt(556f, 12.1f);
            yield return Frames(10);
            Assert.That(Flow.BossActive, Is.True, "the arena closes behind her");
            var brain = captain.GetComponent<EnemyBrain>();
            Assert.That(brain.Awareness, Is.EqualTo(Awareness.Alert), "the Captain wakes");

            float max = captain.Health.Max;
            captain.Health.TakeHit(new Hit(max * 0.55f, Vector3.left, Player.gameObject, 1f));
            yield return Frames(2);
            Assert.That(captain.Phase, Is.EqualTo(2), "below half health it enters its second phase");
            Assert.That(captain.State, Is.EqualTo(EnemyState.PowerUp), "with a roar");
            Assert.That(captain.SpeedScale, Is.GreaterThan(1f));

            captain.Health.TakeHit(new Hit(max, Vector3.left, Player.gameObject, 1f));
            yield return Frames(5);
            Assert.That(Flow.IsComplete, Is.True, "its fall ends the level");
            Assert.That(RailCamera.IsShotActive, Is.True, "with a long shot");
        }

        [UnityTest]
        public IEnumerator Reveals_CutToAWideShot_WithTheNameOfThePlace()
        {
            yield return PlaceAt(78f);
            Input.Move = 1f;
            for (int i = 0; i < 120 && !RailCamera.IsShotActive; i++)
                yield return null;
            Assert.That(RailCamera.IsShotActive, Is.True, "crossing into the court starts its reveal");
            Assert.That(Director.Card, Is.EqualTo("The Flooded Court"));
            Assert.That(Director.Letterbox, Is.GreaterThan(0f).Or.EqualTo(0f));
            Input.Move = 0f;
        }

        /// <summary>Renders the level from the camera at points along the rail. Only runs with PX_SCREENSHOT_DIR set.</summary>
        [UnityTest]
        public IEnumerator LevelScreenshots()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("PX_SCREENSHOT_DIR")))
                Assert.Ignore("Set PX_SCREENSHOT_DIR to capture screenshots.");
            var stops = new (string name, float distance, float height)[]
            {
                ("level-01-threshold", 10f, 0.1f), ("level-02-threshold-guard", 46f, 0.1f), ("level-03-court", 95f, 0.6f),
                ("level-04-court-ledge", 114f, 1.3f), ("level-05-guardhouse", 160f, 0.1f), ("level-06-gallery", 250f, 9.1f),
                ("level-07-gate", 262f, 0.1f), ("level-08-chasm", 300f, 0.1f), ("level-09-ascent", 350f, 8.7f),
                ("level-10-ascent-high", 410f, 12.1f), ("level-11-bazaar", 465f, 12.1f), ("level-12-face-shrine", 560f, 12.1f),
            };
            foreach ((string name, float distance, float height) in stops)
            {
                yield return PlaceAt(distance, height);
                yield return Frames(90);
                Capture(name);
            }

            // Close: her idle with the sabre, and a takedown as the blade goes in.
            yield return PlaceAt(8f);
            RailCamera.PlayShot(new CameraView { yaw = 35f, pitch = 8f, distance = 3.2f, fieldOfView = 30f, focusHeight = 1.0f, lookAhead = 0f }, 10f, 0.05f);
            yield return Frames(60);
            Capture("level-13-rana-sabre");
            RailCamera.EndShot();
            EnemyController guard = GuardNear(52f);
            yield return PlaceAt(guard.PostDistance - 1.2f);
            Motor.Facing = 1;
            yield return Frames(30);
            Input.PressAttack();
            yield return Seconds(Player.Combo.finisher.startup + 0.05f);
            Capture("level-14-takedown");
            yield return Seconds(1f);
        }
    }
}
