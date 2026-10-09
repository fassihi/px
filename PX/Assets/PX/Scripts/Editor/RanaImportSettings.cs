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

        // Rana's body is the model inside her animation file. Tripo's separate "rigged" export has a different bind
        // pose, and the same clips retargeted onto it lean her backwards. Clips and body must come from one export.
        public const string BodyPath = Folder + "/Animations/RanaAnimations.fbx";

        // Bump when the settings below change, so Unity re-imports Rana's files.
        public override uint GetVersion() => 4;

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
