# PX

Codename for a short action side-scroller set in a sci-fi world inspired by ancient Persian art.
A Dead Mage project by Amir (design, code direction) and Soheil (character and environment art).

Design documents live in Notion: [PX](https://app.notion.com/p/deadmage/PX-3ecc022a513e80afa668e90b61bf2c1b).
This repository holds the game project and its technical conventions.

## Requirements

- **Unity 6000.3.11f1** (Unity 6.3 LTS), installed through Unity Hub.
- **Git LFS.** Run `git lfs install` once per machine before cloning. Textures, models, audio and other binary files are stored through it.

## Open and play

1. In Unity Hub, add the `PX/` folder as a project and open it with 6000.3.11f1.
2. Open `Assets/PX/Scenes/Graybox.unity` and press Play.

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Move | A / D or arrow keys | Left stick or d-pad |
| Jump (press again in the air for a second jump) | Space | South button |
| Dash (with a direction held) | Left Shift or K | East button or right trigger |
| Dodge back (Dash with no direction, on the ground; blows pass through her early in it) | Left Shift or K | East button or right trigger |
| Attack (press again to chain jab, cross, side kick, roundhouse, spinning back kick) | J or left mouse | West button |
| Air attack (Attack while airborne: a flying kick that carries her forward; landing ends it) | J or left mouse | West button |
| Guard (hold to block; press just as a blow lands to parry) | L or right mouse | Left bumper |
| Counter (Attack just after a parry) | J or left mouse | West button |
| Crouch (hold; she fits behind low cover, can turn but not walk) | S or down arrow | Stick or d-pad down |
| Sweep (attack while crouched) | J or left mouse | West button |
| Ledges: she catches one in the air; Jump or toward the wall climbs, Down lets go, Jump away springs off | | |
| Revive enemies (debug: stands the dead back up where they lie, at full health) | R, or the button at the top right | |

Run to the right to see the same gameplay through several camera angles: side view, a three-quarter view
through the turn, a pulled-back view at the training dummies, a view from behind, then the side again for
Enemy A's arena, a raised view for the climbing walls and the side view for Enemy B at the end.

## Layout

```
PX/                         Unity project
  Assets/
    PX/
      Scripts/Runtime/      Game code (assembly PX.Runtime)
        Rails/              The path the game is played on
        Character/          Motor, player control, input, movement tuning
        Combat/             Health (with block and parry), stamina, combo and player combat config, hit-stop,
                            hit effects and hit flash, training dummy
        Enemies/            Enemy config, controller (idle, walk, run, walk back, two swings that it opens with either of and
                            sometimes chains, hurt, parried, climb, dead), AI brain (an IInputSource), animator and
                            ragdoll (a dead enemy goes limp as a ragdoll, its joints made around the pose it dies in; Revive stands it up again), animator also lays a procedural layer on its clips (run lean on a spring, stride sway, swivel when it turns, a crouch-leap-land jump over obstacles)
        UI/                 Combat HUD: her health and stamina, the health of the enemy she fights
        (Character/ also has MovementEffects: the ribbons and sprays of her air dash and double jump)
        Camera/             Rail-relative camera and camera zones
        Debugging/          Graybox stand-ins for animation and UI
        (Character/ also has HeroineAnimator, which plays the clips named in Config/HeroineAnimation, one attack clip per combo step)
      Scripts/Editor/       Editor tools (assembly PX.Editor)
      Tests/EditMode/       Fast tests that need no scene
      Tests/PlayMode/       Tests that play the graybox scene
      Config/               Tuning assets: PlayerMove, PlayerLightCombo, PlayerCombat, HeroineAnimation, EnemyA, EnemyB
      Prefabs/              Player, TrainingDummy, EnemyA, EnemyB
      Scenes/               Graybox.unity (generated, see below)
      Art/                  Art assets. Graybox materials, Rana (Art/Rana, the player; open Scenes/RanaViewer to preview her animations; Mixamo clips go in Art/Rana/Animations/Mixamo, downloaded as FBX without skin at 30 fps and named for their use (Combo 1 Jab is Mixamo's Lead Jab, Combo 2 Cross its Cross Punch, Combo 3 Side Kick its Mma Kick (side kick), Crouch Sweep its Front Leg Sweep (held down to her crouch by BodyHeightJob, since Mixamo plays it from a higher stance), Running the running from its Female Locomotion Pack (her run: an ordinary upright run; the ninja run was dropped), Crouch Idle its Low Crouching Idle, and Land Idle comes from its unarmed jump and crouch pack (there is no crouch down or stand up clip: those squats ended in a different pose from the held crouch, which read as two movements, so she blends straight into the pose); landing at a run plays the end of Unarmed Jump Running, since Land Run holds an arm out; Dash Ground Pose is Mixamo's Female Locomotion Pose "Sprinting", Dash Air Pose its Male Dynamic Pose "Leaping Forward, Arms Extended Backwards"); the graybox builder straightens their avatars and hers, see AvatarPose) and the old stand-in heroine (Art/Heroine, now only the Universal Animation Library: her idle, standing jump and hit reaction). Enemy A (Art/EnemyA) is Soheil's Tripo knight, auto-rigged as a humanoid (its coat is re-weighted off the arms on import, see CoatWeights, and its sword is made rigid in the right hand and turned into the two-handed grip of the Great Sword clips, see HeldWeapon); it waits in an arena on the last straight. Its clips are in Art/EnemyA/Animations/Mixamo, from Mixamo's Great Sword set: Idle (Great Sword Idle), Walk (Great Sword Walk), Walk Back (Great Sword Backward Walk), Run (Great Sword Run), Attack (Great Sword Downward Slash), Attack 2 (Great Sword Power Slash), Hit (Great Sword Unblocked Impact), Parried (Great Sword Blocked Impact), Climb (Great Sword Jump) and Death (Two Handed Sword Death, falling back). Enemy B (Art/EnemyB) is Soheil's Tripo swordsman with a sabre and a musket slung on its back (strapped to its torso on import), at the end of the level; its clips are Mixamo's Sword And Shield set under the same names (Idle, Walk, Walk Back, Run (Sword And Shield Run), High Attack, Cross Slash, Unblocked and Blocked Impact, Jump From Idle, Falling Back Death). Rana's newer clips: Ninja Crouch (Male Crouch Pose "Leaning Forward, Right Hand Supporting"), Combo 4 Roundhouse and Combo 5 Spinning Back Kick (Mma Kick), Counter Hurricane Kick (Flying Hurricane Kick), Hurt (Standing React Large From Front), Death (Flying Back Death), Standing Dodge Backward (a controlled backstep; the old Jumping Backwards Dodge flailed), Air Attack (Flying Kick: her air attack), Guard (Block), Parry (Outward Block), Guard Hit (Standing Block React Large), Ledge Grab (Ledge Hit To Hanging Braced), Ledge Hang Braced (Braced Hang: her hands reach over the lip; the old free hang held her 0.5 m out from the wall) and Ledge Climb (Braced Hang To Crouch, its rise kept)
    Settings/               Render pipeline assets and volume profiles
  Packages/                 Package manifest
  ProjectSettings/
```

## How the game is put together

**The rail.** She moves along a `Rail`: a spline that can curve freely through the 3D world.
Gameplay code only knows two numbers, distance along the rail and height. `CharacterMotor`
turns those into 3D movement with collisions.

**The camera.** `RailCamera` describes its view relative to the rail's direction, so a side view stays
a side view around corners. A `CameraZone` gives any stretch of rail its own angle. Changing the
camera never touches gameplay code.

**Tuning is data.** Movement numbers live in `Config/PlayerMove.asset` (including the dodge and ledges), the
combo's timings in `Config/PlayerLightCombo.asset`, stamina, guard, parry, counter and death in
`Config/PlayerCombat.asset`, and each enemy's in `Config/EnemyA.asset` and `Config/EnemyB.asset`. Change them in
the Inspector, during play if you like.

**The fight is soulslike.** Strikes, dashes, dodges and blocks cost stamina, which comes back after a pause; an
empty bar means no attack. The dodge back has invulnerable frames. Guard blocks frontal blows for stamina and a
little chip damage, and breaks when stamina runs out; pressed just before a blow lands it parries, throwing the
enemy off balance and opening a counter. Enemies have poise: light strikes do not interrupt their swings. When she
falls she gets up at the last checkpoint and the enemies still standing return to their posts.

**The graybox scene is generated.** `Graybox.unity` is written by
`Scripts/Editor/GrayboxBuilder.cs` and overwritten on every rebuild (menu: PX > Graybox > Rebuild Scene).
To change the test level, change the builder. Config assets, materials and prefabs are only created
when missing, so edits made to them in the editor are kept. Hand-made scenes go in their own files.

## Command line

Close the project in the Unity editor first. Unity allows one process per project.

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.3.11f1/Unity.app/Contents/MacOS/Unity"
```

Rebuild the graybox scene:

```bash
"$UNITY" -batchmode -quit -projectPath PX -executeMethod PX.EditorTools.GrayboxBuilder.Build -logFile -
```

Run the tests (leave out `-quit`, the test runner exits by itself):

```bash
"$UNITY" -batchmode -projectPath PX -runTests -testPlatform EditMode -testResults TestResults-EditMode.xml -logFile -
"$UNITY" -batchmode -projectPath PX -runTests -testPlatform PlayMode -testResults TestResults-PlayMode.xml -logFile -
```

Render screenshots of the camera views, close-ups of her moves and the fights while running the play mode tests:

```bash
PX_SCREENSHOT_DIR="$PWD/Screenshots" "$UNITY" -batchmode -projectPath PX -runTests -testPlatform PlayMode -testResults TestResults-PlayMode.xml -logFile -
```

In the editor, the same tests are under Window > General > Test Runner.
