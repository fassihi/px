using UnityEditor;

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

        // Bump when the settings below change, so Unity re-imports the enemies' files.
        public override uint GetVersion() => 1;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(EnemyAFolder))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
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
