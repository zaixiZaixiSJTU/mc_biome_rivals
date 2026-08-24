using System;
using UnityEditor;
using UnityEngine;

namespace BiomeRivals.Demo.Editor
{
    public sealed class DemoWorldTexturePostprocessor : AssetPostprocessor
    {
        private const string TextureRoot = "Assets/Generated/MinecraftWorldTextures/Resources/DemoWorld/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TextureRoot, StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 256;
        }
    }
}
