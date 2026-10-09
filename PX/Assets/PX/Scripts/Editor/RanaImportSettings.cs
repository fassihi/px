using UnityEditor;

namespace PX.EditorTools
{
    /// <summary>
    /// Import settings for Rana's art. RanaRigged.fbx is Tripo's auto-rigged model, imported as a humanoid so the
    /// heroine's animation clips retarget onto it. The materials are built by <see cref="RanaViewerBuilder"/>.
    /// </summary>
    public sealed class RanaImportSettings : AssetPostprocessor
    {
        public const string Folder = "Assets/PX/Art/Rana";

        // Bump when the settings below change, so Unity re-imports Rana's files.
        public override uint GetVersion() => 3;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder))
                return;

            var importer = (ModelImporter)assetImporter;
            bool isAnimationFile = assetPath.Contains("/Animations/");
            bool isHumanoid = isAnimationFile || assetPath.EndsWith("RanaRigged.fbx");
            importer.animationType = isHumanoid ? ModelImporterAnimationType.Human : ModelImporterAnimationType.None;
            importer.avatarSetup = isHumanoid ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation = isAnimationFile;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }
    }
}
