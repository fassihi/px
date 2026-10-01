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
| Dash | Left Shift or K | East button or right trigger |
| Attack (press again to chain three swings) | J or left mouse | West button |

Run to the right to see the same gameplay through four camera angles: side view, a three-quarter view
through the turn, a pulled-back view at the training dummies, and a view from behind near the end.

## Layout

```
PX/                         Unity project
  Assets/
    PX/
      Scripts/Runtime/      Game code (assembly PX.Runtime)
        Rails/              The path the game is played on
        Character/          Motor, player control, input, movement tuning
        Combat/             Health, combo data, hit-stop, training dummy
        Camera/             Rail-relative camera and camera zones
        Debugging/          Graybox stand-ins for animation and UI
        (Character/ also has HeroineAnimator, which plays the clips named in Config/HeroineAnimation)
      Scripts/Editor/       Editor tools (assembly PX.Editor)
      Tests/EditMode/       Fast tests that need no scene
      Tests/PlayMode/       Tests that play the graybox scene
      Config/               Tuning assets: PlayerMove, PlayerLightCombo
      Prefabs/              Player, TrainingDummy
      Scenes/               Graybox.unity (generated, see below)
      Art/                  Art assets. Graybox materials, and the stand-in heroine (Art/Heroine)
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

**Tuning is data.** Movement numbers live in `Config/PlayerMove.asset` and the combo's timings in
`Config/PlayerLightCombo.asset`. Change them in the Inspector, during play if you like.

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

Render screenshots of the four camera views while running the play mode tests:

```bash
PX_SCREENSHOT_DIR="$PWD/Screenshots" "$UNITY" -batchmode -projectPath PX -runTests -testPlatform PlayMode -testResults TestResults-PlayMode.xml -logFile -
```

In the editor, the same tests are under Window > General > Test Runner.
