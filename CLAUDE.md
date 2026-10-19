# PX: notes for Claude

PX is the codename for a short 3D action side-scroller: a girl, sci-fi inspired by ancient Persian art,
semi-stylized. Early concept stage. Amir directs design and code; Soheil is the art director (character and
environment art) and also helps steer the overall game direction; Claude does the coding and engine work.
Design decisions are made by Amir and Soheil together. The weapon is not decided yet: keep it swappable.

- Design documents are in Notion, not in this repo: https://app.notion.com/p/deadmage/PX-3ecc022a513e80afa668e90b61bf2c1b
  Update the relevant Notion page when a design or technical decision changes, and add settled decisions to the Decision Log page.
- `README.md` describes the layout, architecture and command lines. Read it first.
- Soheil's art is dropped in `../art/` (next to this repo, outside git). Check it for new files when starting work
  and look at them: the art drives design and camera decisions. Do not copy it into the repo unless asked.
  Amir adds project artwork to this Google Drive folder: https://drive.google.com/drive/u/0/folders/1u4XMGdSKXZHW3xqpR5eb8qC4W9FnaLJH
- Persistent notes and memory for this project go in the md files in this repo (this file, `README.md`), not in
  machine-local memory, so that all of Amir's machines share them.

## Engine

Unity 6000.3.11f1 (6.3 LTS), Universal Render Pipeline, Input System, Splines. The Unity project is in `PX/`.

## Working without the editor UI

Almost everything is text, and the editor runs headless, so prefer scripts to manual editor steps.

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.3.11f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -quit -projectPath PX -executeMethod PX.EditorTools.GrayboxBuilder.Build -logFile <log>
"$UNITY" -batchmode -projectPath PX -runTests -testPlatform EditMode -testResults <xml> -logFile <log>
"$UNITY" -batchmode -projectPath PX -runTests -testPlatform PlayMode -testResults <xml> -logFile <log>
```

- Headless runs fail if the PX project is open in the editor. Check for a running Unity on this project first.
  A Unity editor open on a different project is fine.
- `Tools/typecheck.sh [runtime|editor|tests|all]` compiles the PX assemblies with Unity's own bundled Roslyn, using the
  references and defines from the generated `.csproj` files, and prints `error CS` lines. It needs no editor, so it
  works while the editor is busy or frozen. It is a type check only: it does not import assets, build scenes or run tests.
- The open editor's main loop stops while the Mac's screen is locked (the MCP stops answering and a requested compile
  never starts). It resumes when the screen is unlocked. Nothing can be done about it from here without closing the
  editor, which needs Amir; use the type check above in the meantime and write the code that does not need the editor.

### Work process: editor open or closed

- **Editor open, Unity MCP connected (preferred):** the project includes MCP for Unity (`com.coplaydev.unity-mcp`) and
  `UnityMCP` is configured for this repo as `http://127.0.0.1:8080/mcp`. Amir starts the server in the editor
  (Window > MCP for Unity) and reconnects it in Claude Code (or starts a new session). Check that the Unity tools are
  listed before relying on them. Then edit code, and use the MCP to compile, run PX > Graybox > Rebuild Scene, run the
  tests, read the console and take screenshots, without closing the editor.
- **Editor closed:** use the headless commands above. This is the reference run and the fallback if the MCP is down.
- Use the MCP only for build, test and inspection tasks. Do not start the server or change MCP or accessibility
  settings without being asked, and do not script the editor around its permissions.
- Compile errors can also be read from `~/Library/Logs/Unity/Editor.log` (`error CS`) while the editor is open.
- Do not commit a change that has not been compiled and tested by one of the two routes. Say so when it cannot be.
- Do not pass `-nographics` to play mode tests that render screenshots.
- Set `PX_SCREENSHOT_DIR` on a play mode run to get PNGs of the camera views; look at them to check visual changes.
- Compile errors appear in the log as `error CS`. Test results are NUnit XML.
- Unity writes `.meta` files on import. After adding files, run Unity once and commit the generated `.meta` files.

## Code conventions

- Namespace `PX` for runtime, `PX.EditorTools` for editor code, `PX.Tests` for tests. One assembly definition per area.
- Gameplay is in rail space: x = distance along the `Rail`, y = height. Only `CharacterMotor` and `RailCamera`
  convert to world space. New gameplay code should not assume the rail runs along a world axis.
- Tuning values go in ScriptableObject configs under `Assets/PX/Config`, not in code constants.
- Characters take input through `IInputSource`, so tests and later AI can drive them.
- Scene objects reference each other through serialized fields wired by the builder, not through `Find` calls.
- `Graybox.unity` is generated by `GrayboxBuilder` and `Level01.unity` by `LevelBuilder` (menu PX > Level 01 > Rebuild
  Scene). Change the builder and rebuild; do not hand-edit those scenes. The two builders share `BuilderKit` (assets,
  blocks along the rail, wiring, particles, camera) and `CharacterBuilder` (the prefabs and configs of Rana, the
  enemies, the Captain, the sentinel and its bolt); the level's art comes from `EnvironmentKit`.
  The builder only creates config assets, materials and prefabs when they are missing. To regenerate one, delete it first.
  A regenerated asset gets a new GUID, and prefabs that reference it are not rebuilt: also delete the prefabs that use it
  (the configs are wired into `Player.prefab`), or they keep a missing reference. Through the MCP, delete with
  `manage_asset` (action `delete`); `execute_code` blocks `AssetDatabase.DeleteAsset` in its safety checks.
- Every gameplay behaviour gets a play mode test in `Assets/PX/Tests/PlayMode` using `GrayboxFixture`.
  Tests run at a fixed 60 steps per second.
- Components under `Scripts/Runtime/Debugging` are graybox stand-ins and are expected to be deleted.
- To replace a Mixamo FBX with a new download, delete its `.meta` too (or give the new file a new name). The old
  `.meta` keeps the old take's avatar, and the new clip retargets badly. A reset `HumanDescription` from script also
  loses `skeletonHasParents` and the twist and stretch defaults.
- To check a pose by eye in edit mode, play the clip through a `PlayableGraph` onto the Player prefab and render it.
  `AnimationClip.SampleAnimation` does not retarget humanoid clips and shows wrong poses. Render at 512 px or more:
  side views at small sizes make a side-on stance look like a lean. `PX.EditorTools.PoseSnapshot` does this
  (`Render` for one frame, `RenderSheet` for many frames on one image); call it through the MCP's `execute_code`.
  In edit mode a skinned mesh is skinned once per editor update, so each frame needs a fresh instance (it does that).
- A weapon modelled into a Tripo character is skinned to the hand at whatever angle Tripo left it, and its hilt often
  to the arm and hips. `HeldWeapon` (run on import) makes it rigid in the hand and turns it into a Mixamo grip.
- Mixamo downloads: the browser pane's Mixamo sign-in expires after a day, and Amir cannot sign in there while away.
  Amir's Chrome stays signed in: use Claude in Chrome (`mcp__claude-in-chrome__*`), search, click the card (by its
  description text in `ul.product-metadata li`, through `javascript_tool`, which is reliable), Download, set Skin to
  Without Skin on the dialog's `<select>` through `javascript_tool` (the native dropdown ignores keys), Download. Chrome
  holds each download for a save prompt, but the finished FBX is already in `~/Downloads/.com.google.Chrome.*`: copy it
  out and name it. Do not read the page's stored tokens. Ask before downloading unless Amir has said to download freely.
- With the MCP, screenshot tests run in the open editor if `PX_SCREENSHOT_DIR` is set in the editor process
  (`System.Environment.SetEnvironmentVariable` through `execute_code`); unset it afterwards. Play mode entered
  through the MCP does not advance while the editor window is unfocused, so live screenshots are static.

## Git

- Binary assets go through Git LFS (see `.gitattributes`). Add new binary extensions there before committing such files.
- After every change, commit and push to `origin` (private GitHub repo `fassihi/px`). Leave unrelated Unity-generated changes out of the commit.
