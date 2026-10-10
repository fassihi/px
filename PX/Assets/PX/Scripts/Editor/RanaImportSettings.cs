using UnityEditor;

namespace PX.EditorTools
{
    /// <summary>
    /// Import settings for Rana's art. The animation file holds her body and her clips, imported as a humanoid so
    /// other clips retarget onto her. The materials are built by <see cref="RanaViewerBuilder"/>.
    /// </summary>
    public sealed class RanaImportSettings : AssetPostprocessor
    {
        public const string Folder = "Assets/PX/Art/Rana";

        // Clips downloaded from Mixamo, "without skin". Their avatars need straightening, see AvatarPose.
        public const string MixamoFolder = Folder + "/Animations/Mixamo";

        // Rana's body is the model inside her animation file. Tripo's separate "rigged" export has a different bind
        // pose, and the same clips retargeted onto it lean her backwards. Clips and body must come from one export.
        public const string BodyPath = Folder + "/Animations/RanaAnimations.fbx";

        // Mixamo clips that loop. The rest play once.
        private static readonly string[] LoopingClips = { "Running", "Crouch Idle", "Dash Ground Pose", "Dash Air Pose" };

        // Bump when the settings below change, so Unity re-imports Rana's files.
        public override uint GetVersion() => 10;

        private void OnPreprocessAnimation()
        {
            var importer = (ModelImporter)assetImporter;
            // Mixamo names every take "mixamo.com". Name the clip after its file, so it can be found.
            if (!assetPath.StartsWith(MixamoFolder))
                return;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips.Length != 1)
                return;

            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool jump = name.Contains("Jump");
            bool turn = name.Contains("Turn");
            // Strikes keep the orientation they were made with. Dropping their root rotation squares her body to the
            // front and so turns a punch thrown from a bladed stance off to her side.
            bool keepFacing = turn || name.StartsWith("Combo") || name.Contains("Sweep");
            ModelImporterClipAnimation clip = clips[0];
            clip.name = name;
            // Cycles and held poses loop; moves, landings and transitions play once.
            clip.loopTime = System.Array.IndexOf(LoopingClips, name) >= 0;
            // The game moves her, so clips play in place: travel along the ground is dropped, and so is the rise of a
            // jump. A turn keeps its rotation, so she visibly spins round.
            clip.lockRootPositionXZ = false;
            clip.lockRootHeightY = !jump;
            clip.keepOriginalPositionY = true;
            clip.lockRootRotation = keepFacing;
            clip.keepOriginalOrientation = keepFacing;
            importer.clipAnimations = clips;
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder))
                return;

            var importer = (ModelImporter)assetImporter;
            bool isHumanoid = assetPath.Contains("/Animations/");
            importer.animationType = isHumanoid ? ModelImporterAnimationType.Human : ModelImporterAnimationType.None;
            importer.avatarSetup = isHumanoid ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation = isHumanoid;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }
    }
}
