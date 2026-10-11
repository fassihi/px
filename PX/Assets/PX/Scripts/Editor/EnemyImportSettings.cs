using UnityEditor;
using UnityEngine;

namespace PX.EditorTools
{
    /// <summary>
    /// Import settings for enemy art. Each enemy is a Tripo model auto-rigged as a humanoid, like Rana, so the
    /// Universal Animation Library's clips retarget onto it. Materials are built by the graybox builder.
    /// </summary>
    public sealed class EnemyImportSettings : AssetPostprocessor
    {
        public const string EnemyAFolder = "Assets/PX/Art/EnemyA";
        public const string EnemyABodyPath = EnemyAFolder + "/EnemyA.fbx";
        public const string EnemyATexturePath = EnemyAFolder + "/Textures/EnemyA_BaseColor.jpg";

        // Enemy A's clips, downloaded from Mixamo as FBX without skin at 30 fps and named for their use.
        public const string EnemyAMixamoFolder = EnemyAFolder + "/Animations/Mixamo";

        // Clips that loop. The rest play once.
        private static readonly string[] LoopingClips = { "Idle", "Walk" };

        // Bump when the settings below change, so Unity re-imports the enemies' files.
        public override uint GetVersion() => 7;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(EnemyAFolder))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = assetPath.StartsWith(EnemyAMixamoFolder);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(EnemyAMixamoFolder))
                return;
            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips.Length != 1)
                return;

            // As for Rana's Mixamo clips: named after the file, played in place (the game moves it), with the height
            // baked in so a fall reaches the floor. A swing keeps the facing it was made with. A death keeps its
            // travel too, so the body falls back rather than in place.
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool death = name == "Death";
            ModelImporterClipAnimation clip = clips[0];
            clip.name = name;
            clip.loopTime = System.Array.IndexOf(LoopingClips, name) >= 0;
            clip.lockRootPositionXZ = death;
            clip.keepOriginalPositionXZ = death;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootRotation = name == "Attack" || death;
            clip.keepOriginalOrientation = name == "Attack" || death;
            importer.clipAnimations = clips;
        }

        private void OnPostprocessModel(GameObject model)
        {
            if (assetPath != EnemyABodyPath)
                return;

            // The knight's coat, straps and tassels are skinned to its arms by the auto-rig; hang them from the torso instead.
            int moved = CoatWeights.Fix(model);
            Debug.Log($"{System.IO.Path.GetFileName(assetPath)}: moved {moved} cloth vertices from the arms to the torso.");

            // Its sword hangs from a relaxed hand in the model; turn it into the two-handed grip of the Great Sword clips.
            int seated = HeldWeapon.Fix(model, "Right");
            Debug.Log($"{System.IO.Path.GetFileName(assetPath)}: seated a {seated}-vertex sword in the right hand.");
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(EnemyAFolder))
                return;

            // The source is 8K. A side-scroller enemy never fills enough of the screen to need more than 2K.
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 2048;
        }
    }
}
