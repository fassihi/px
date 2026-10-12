using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PX.EditorTools
{
    /// <summary>
    /// Import settings for enemy art. Each enemy is a Tripo model auto-rigged as a humanoid, like Rana, in its own
    /// folder: Art/EnemyX/EnemyX.fbx, its texture in Textures/, and its Mixamo clips in Animations/Mixamo.
    /// Materials are built by the graybox builder.
    /// </summary>
    public sealed class EnemyImportSettings : AssetPostprocessor
    {
        public const string EnemyAFolder = "Assets/PX/Art/EnemyA";
        public const string EnemyABodyPath = EnemyAFolder + "/EnemyA.fbx";
        public const string EnemyATexturePath = EnemyAFolder + "/Textures/EnemyA_BaseColor.jpg";

        // Enemy A's clips, downloaded from Mixamo as FBX without skin at 30 fps and named for their use.
        public const string EnemyAMixamoFolder = EnemyAFolder + "/Animations/Mixamo";

        public const string EnemyBFolder = "Assets/PX/Art/EnemyB";
        public const string EnemyBBodyPath = EnemyBFolder + "/EnemyB.fbx";
        public const string EnemyBTexturePath = EnemyBFolder + "/Textures/EnemyB_BaseColor.jpg";
        public const string EnemyBMixamoFolder = EnemyBFolder + "/Animations/Mixamo";

        public static readonly string[] BodyPaths = { EnemyABodyPath, EnemyBBodyPath };
        public static readonly string[] MixamoFolders = { EnemyAMixamoFolder, EnemyBMixamoFolder };
        private static readonly string[] Folders = { EnemyAFolder, EnemyBFolder };

        // Clips that loop. The rest play once.
        private static readonly string[] LoopingClips = { "Idle", "Walk", "Walk Back", "Run", "Run Weighted" };

        // Bump when the settings below change, so Unity re-imports the enemies' files.
        public override uint GetVersion() => 20;

        private bool IsEnemy => Folders.Any(folder => assetPath.StartsWith(folder + "/"));

        private bool IsClip => MixamoFolders.Any(folder => assetPath.StartsWith(folder + "/"));

        private void OnPreprocessModel()
        {
            if (!IsEnemy)
                return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = IsClip;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsClip)
                return;
            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips.Length != 1)
                return;

            // As for Rana's Mixamo clips: named after the file, played in place (the game moves it), with the height
            // baked in so a fall reaches the floor. A swing keeps the facing it was made with. A death keeps its
            // travel too, so the body falls back rather than in place. A jump keeps its rise, so it reads as a climb.
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool death = name == "Death";
            bool swing = name.StartsWith("Attack");
            ModelImporterClipAnimation clip = clips[0];
            clip.name = name;
            clip.loopTime = System.Array.IndexOf(LoopingClips, name) >= 0;
            clip.lockRootPositionXZ = death;
            clip.keepOriginalPositionXZ = death;
            clip.lockRootHeightY = name != "Climb";
            clip.keepOriginalPositionY = true;
            clip.lockRootRotation = swing || death;
            clip.keepOriginalOrientation = swing || death;
            importer.clipAnimations = clips;
        }

        private void OnPostprocessModel(GameObject model)
        {
            if (assetPath == EnemyBBodyPath)
            {
                // The swordsman's sabre is skinned to its fingers, so it is seated first: the coat fix below would
                // otherwise take it for cloth near the hand. Its coat is skinned to its arms like the knight's, and its
                // musket, slung on its back, is tied to its left arm and leg.
                int sabre = HeldWeapon.Fix(model, "Right", HeldWeapon.OneHandedGripDirection);
                int cloth = CoatWeights.Fix(model);
                int strapped = HeldWeapon.StrapToTorso(model);
                Debug.Log($"{System.IO.Path.GetFileName(assetPath)}: seated a {sabre}-vertex sabre, moved {cloth} cloth vertices " +
                          $"from the arms to the torso, strapped {strapped} vertices to the back.");
                return;
            }
            if (assetPath != EnemyABodyPath)
                return;

            // The knight's coat, straps and tassels are skinned to its arms by the auto-rig; hang them from the torso instead.
            int moved = CoatWeights.Fix(model);
            Debug.Log($"{System.IO.Path.GetFileName(assetPath)}: moved {moved} cloth vertices from the arms to the torso.");

            // Its sword hangs from a relaxed hand in the model; turn it into the two-handed grip of the Great Sword clips.
            int seated = HeldWeapon.Fix(model, "Right", HeldWeapon.GripDirection);
            Debug.Log($"{System.IO.Path.GetFileName(assetPath)}: seated a {seated}-vertex sword in the right hand.");
        }

        private void OnPreprocessTexture()
        {
            if (!IsEnemy)
                return;

            // The sources are 8K. A side-scroller enemy never fills enough of the screen to need more than 2K.
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 2048;
        }
    }
}
