using System;
using UnityEditor;
using UnityEngine;

namespace BiomeRivals.Demo.Editor
{
    public sealed class DemoWorldTexturePostprocessor : AssetPostprocessor
    {
        private const string TextureRoot = "Assets/Generated/MinecraftWorldTextures/Resources/DemoWorld/";
        private const string CardFrameRoot = "Assets/Game/Demo/Art/Resources/DemoCardFrames/";

        private void OnPreprocessTexture()
        {
            var importer = (TextureImporter)assetImporter;
            if (assetPath.StartsWith(CardFrameRoot, StringComparison.Ordinal))
            {
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                return;
            }
            if (!assetPath.StartsWith(TextureRoot, StringComparison.Ordinal)) return;
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
