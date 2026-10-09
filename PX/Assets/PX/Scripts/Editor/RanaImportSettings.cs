using UnityEditor;

namespace PX.EditorTools
{
    /// <summary>
    /// Import settings for Rana's art. The model is a plain static mesh for now: no rig, no animation,
    /// and the materials are built by <see cref="RanaViewerBuilder"/> rather than imported.
    /// </summary>
    public sealed class RanaImportSettings : AssetPostprocessor
    {
        public const string Folder = "Assets/PX/Art/Rana";

        // Bump when the settings below change, so Unity re-imports Rana's files.
        public override uint GetVersion() => 1;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }
    }
}
