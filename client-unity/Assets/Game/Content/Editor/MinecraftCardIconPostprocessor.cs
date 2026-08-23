using UnityEditor;
using UnityEngine;

namespace BiomeRivals.Content.Editor
{
    public sealed class MinecraftCardIconPostprocessor : AssetPostprocessor
    {
        private const string IconRoot = "Assets/Generated/MinecraftCardIcons/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(IconRoot, System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
