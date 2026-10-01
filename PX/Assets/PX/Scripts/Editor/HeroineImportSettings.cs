using UnityEditor;
using UnityEngine;

namespace PX.EditorTools
{
    /// <summary>
    /// Import settings for the heroine's art, kept in code so a re-import or a fresh clone gets them right.
    /// The body and the animation library share one humanoid rig, so clips retarget onto the body.
    /// </summary>
    public sealed class HeroineImportSettings : AssetPostprocessor
    {
        public const string Folder = "Assets/PX/Art/Heroine";

        // Bump when the settings below change, so Unity re-imports the heroine's files.
        public override uint GetVersion() => 1;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder))
                return;

            var importer = (ModelImporter)assetImporter;
            bool isAnimationLibrary = assetPath.Contains("/Animations/");
            bool isBody = assetPath.EndsWith("Superhero_Female_FullBody.fbx");

            importer.animationType = isAnimationLibrary || isBody ? ModelImporterAnimationType.Human : ModelImporterAnimationType.None;
            importer.importAnimation = isAnimationLibrary;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.avatarSetup = importer.animationType == ModelImporterAnimationType.Human
                ? ModelImporterAvatarSetup.CreateFromThisModel
                : ModelImporterAvatarSetup.NoAvatar;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder))
                return;

            var importer = (TextureImporter)assetImporter;
            if (assetPath.Contains("_Normal"))
                importer.textureType = TextureImporterType.NormalMap;
        }
    }
}
